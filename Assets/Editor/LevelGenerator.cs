using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LevelGenerator
{
  // Grid bounds must match the GridManager in the MainGame scene (see DisplaySetup).
  private const int GridWidth = 10;
  private const int GridHeight = 5;

  private const int EnvironmentCount = 7;
  private const int LevelsPerEnvironment = 10;

  // Path length was the single biggest difficulty variable in the game and it
  // was pure luck: BalanceSim showed every unwinnable level had a path of
  // exactly 12 (the old minimum) while 15+ was trivial at difficulty 67, and
  // the verdict ladder LOSS/HARD/FAIR/EASY/TRIVIAL mapped monotonically onto
  // mean path length. Narrowing the band takes that variable out of the way so
  // difficulty comes from the enemy curve below instead of the dice.
  // The old MaxPathLength of 26 was dead config - the self-avoiding walk on a
  // 10x5 grid never produced more than 19.
  private const int MinPathLength = 15;
  private const int MaxPathLength = 20;
  private const int BaseSeed = 4242;

  // --- difficulty curve -----------------------------------------------------
  // Enemy assets are shared by every level, so before per-wave scaling existed
  // the generator could only add MORE enemies - which made levels longer, never
  // harder. BalanceSim measured the consequence: tower utilization FELL from
  // 27% at difficulty 1 to 11% at difficulty 70, i.e. the endgame was the
  // easiest part of the game, and kill depth sat flat at ~40% throughout.
  //
  // So: enemies get tougher rather than more numerous, and counts come down to
  // pull level length back from ~7.5 minutes to ~3.
  // The ramp is CONCAVE, not linear, and that shape is load-bearing. Player
  // power is capped by buildable cells (~33 once the path is carved out) and
  // saturates by the mid game, so the usable difficulty window is narrow. The
  // sim measured both walls: a linear 0.032/level left the first fifty levels
  // at 100% health, while 0.042/level made every level past difficulty 60 a
  // loss WITH A FULL BOARD - 31-34 towers built and gold still unspent, i.e.
  // nowhere left to build. Peak multiplier therefore has to land near 3.0.
  // Rising fast through the early environments and flattening at the top is
  // the only way to get both a real mid game and a winnable endgame.
  //   d10 ~1.6x   d30 ~2.2x   d50 ~2.6x   d70 ~3.0x
  //
  // Lowered from 0.152 (peak ~3.0x) when the four behaviour types landed. That
  // is the whole point of enemy variety: the difficulty it adds has to be PAID
  // FOR out of raw numbers, not stacked on top of them. Stacked, the sim lost
  // every level from d54 up with 31-35 towers built and up to 4,944 gold
  // unspent -- a full board with nowhere left to build, i.e. straight through
  // the structural ceiling. Peak is now ~2.5x.
  private const float HealthRampScale = 0.1133f;
  private const float HealthRampExponent = 0.61f;

  // Difficulty also has to ramp WITHIN a level, not just between levels. A flat
  // per-level multiplier put full-strength enemies in wave 1, when the player
  // owns only what starting gold could buy (~10 towers) - so the run died at
  // wave 3 and never earned the income that pays for the other twenty. The sim
  // showed exactly that: every level past difficulty 41 lost with ~12 towers
  // built and under 100 gold unspent. Opening at 60% of the level's peak gives
  // the economy room to get going.
  private const float FirstWaveHealthShare = 0.6f;

  // Reward grows in step with health: enemy counts are roughly halved, so
  // income per kill has to rise or the player cannot fill the board. The sim
  // is explicit that the endgame needs this - the levels it still loses end
  // with ~30 towers and under 200 gold, i.e. capped by income, not by space.
  private const float RewardShareOfHealthRamp = 1f;

  // --- enemy variety --------------------------------------------------------
  // Raw numbers are capped: player power is bounded by buildable cells (~33), so
  // peak health cannot pass ~3x before a full board loses. New BEHAVIOURS are
  // the only lever left, so one arrives per environment through the middle of
  // the game and the last two environments combine them.
  //   env2 swarm      - chaff that saturates single-target towers
  //   env3 shielded   - regenerating absorb; punishes chip damage
  //   env4 splitter   - children spawn where it died; punishes no AoE
  //   env5 healer     - heals the pack; punishes towers spread thin
  private const int SwarmFromDifficulty = 11;
  private const int ShieldedFromDifficulty = 21;
  private const int SplitterFromDifficulty = 31;
  private const int HealerFromDifficulty = 41;

  // Was 8s. With up to nine waves that alone was over a minute of standing
  // around per level.
  private const float TimeToNextWave = 5f;

  private const string LevelsFolder = "Assets/Resources/Levels";
  private const string PathsFolder = "Assets/Settings/Generated/Paths";
  private const string WavesFolder = "Assets/Settings/Generated/Waves";

  [MenuItem("Tools/Level Generator/Generate All Levels")]
  public static void Generate()
  {
    EnemyConfig basic = LoadEnemy("BasicEnemy");
    EnemyConfig fast = LoadEnemy("FastEnemy");
    EnemyConfig armored = LoadEnemy("ArmoredEnemy");
    EnemyConfig boss = LoadEnemy("BossEnemy");
    EnemyConfig swarm = LoadEnemy("SwarmEnemy");
    EnemyConfig shielded = LoadEnemy("ShieldedEnemy");
    EnemyConfig splitter = LoadEnemy("SplitterEnemy");
    EnemyConfig healer = LoadEnemy("HealerEnemy");
    if (basic == null || fast == null || armored == null || boss == null ||
        swarm == null || shielded == null || splitter == null || healer == null)
    {
      Debug.LogError("LevelGenerator: missing EnemyConfig assets in Assets/Settings/Enemies.");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    // Regenerate from scratch so the tool stays idempotent
    AssetDatabase.DeleteAsset(LevelsFolder);
    AssetDatabase.DeleteAsset("Assets/Settings/Generated");
    Directory.CreateDirectory(LevelsFolder);
    Directory.CreateDirectory(PathsFolder);
    Directory.CreateDirectory(WavesFolder);
    AssetDatabase.Refresh();

    int generated = 0;
    for (int env = 1; env <= EnvironmentCount; env++)
    {
      string envFolder = $"{LevelsFolder}/Environment{env}";
      Directory.CreateDirectory(envFolder);
      AssetDatabase.Refresh();

      for (int levelNumber = 1; levelNumber <= LevelsPerEnvironment; levelNumber++)
      {
        int difficulty = (env - 1) * LevelsPerEnvironment + levelNumber;

        PathConfig path = ScriptableObject.CreateInstance<PathConfig>();
        path.pathName = $"Env{env} Level{levelNumber} Path";
        path.pathGridCoordinates = GeneratePath(BaseSeed + difficulty * 97);
        AssetDatabase.CreateAsset(path, $"{PathsFolder}/Env{env}-Level{levelNumber:00}-Path.asset");

        WaveConfig waves = ScriptableObject.CreateInstance<WaveConfig>();
        waves.waves = GenerateWaves(difficulty, basic, fast, armored, boss,
          swarm, shielded, splitter, healer);
        AssetDatabase.CreateAsset(waves, $"{WavesFolder}/Env{env}-Level{levelNumber:00}-Waves.asset");

        LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
        level.levelNumber = levelNumber;
        level.environmentName = $"Environment {env}";
        level.pathConfig = path;
        level.waveConfig = waves;
        // Raised from 15/level: the opening board is bought entirely out of
        // starting gold, so it has to keep some pace with the health ramp or
        // wave 1 arrives against too few towers.
        level.startingGold = 500 + (difficulty - 1) * 24;
        level.startingHealth = 100;
        AssetDatabase.CreateAsset(level, $"{envFolder}/Level{levelNumber:00}.asset");

        generated++;
      }
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
    Debug.Log($"LevelGenerator: generated {generated} levels across {EnvironmentCount} environments.");
  }

  // Batch-mode entry point: generate, then exit with a status code
  public static void GenerateBatch()
  {
    Generate();
    EditorApplication.Exit(0);
  }

  private static EnemyConfig LoadEnemy(string name)
  {
    return AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
  }

  // Self-avoiding walk from the left edge to the right edge. The path never
  // touches itself (no two non-consecutive cells are 4-adjacent) so the
  // corridor reads unambiguously on the grid.
  private static List<Vector2Int> GeneratePath(int seed)
  {
    // More attempts than before: the acceptable length band is now much
    // narrower, so a larger share of walks get rejected.
    for (int attempt = 0; attempt < 600; attempt++)
    {
      List<Vector2Int> path = TryGeneratePath(new System.Random(seed + attempt));
      if (path != null) return path;
    }

    Debug.LogError($"LevelGenerator: failed to generate a valid path for seed {seed}.");
    return new List<Vector2Int>();
  }

  private static List<Vector2Int> TryGeneratePath(System.Random rng)
  {
    var start = new Vector2Int(0, rng.Next(1, GridHeight - 1));
    var path = new List<Vector2Int> { start };
    var occupied = new HashSet<Vector2Int> { start };

    Vector2Int east = Vector2Int.right;
    Vector2Int north = Vector2Int.up;
    Vector2Int south = Vector2Int.down;

    Vector2Int current = start;
    while (current.x < GridWidth - 1 && path.Count < MaxPathLength)
    {
      // Weighted direction preference: mostly east, wander north/south.
      // East used to be weighted 4 against 2+2, which raced to the far edge and
      // made a 15+ cell path rare - the reason the old minimum had to be 12.
      var candidates = new List<Vector2Int>();
      void AddWeighted(Vector2Int dir, int weight)
      {
        for (int i = 0; i < weight; i++) candidates.Add(dir);
      }
      AddWeighted(east, 3);
      AddWeighted(north, 2);
      AddWeighted(south, 2);

      Vector2Int? next = null;
      while (candidates.Count > 0 && next == null)
      {
        Vector2Int dir = candidates[rng.Next(candidates.Count)];
        candidates.RemoveAll(c => c == dir);
        Vector2Int cell = current + dir;
        if (IsUsable(cell, current, occupied)) next = cell;
      }

      if (next == null) return null; // dead end, retry with a new seed

      current = next.Value;
      path.Add(current);
      occupied.Add(current);
    }

    if (current.x != GridWidth - 1 || path.Count < MinPathLength) return null;
    return path;
  }

  private static bool IsUsable(Vector2Int cell, Vector2Int head, HashSet<Vector2Int> occupied)
  {
    if (cell.x < 0 || cell.x >= GridWidth || cell.y < 0 || cell.y >= GridHeight) return false;
    if (occupied.Contains(cell)) return false;

    // The new cell may only touch the current head, never earlier path cells
    foreach (Vector2Int neighbor in new[]
    {
      cell + Vector2Int.right, cell + Vector2Int.left,
      cell + Vector2Int.up, cell + Vector2Int.down
    })
    {
      if (neighbor != head && occupied.Contains(neighbor)) return false;
    }
    return true;
  }

  private static WaveConfig.Wave[] GenerateWaves(
    int difficulty, EnemyConfig basic, EnemyConfig fast, EnemyConfig armored, EnemyConfig boss,
    EnemyConfig swarm, EnemyConfig shielded, EnemyConfig splitter, EnemyConfig healer)
  {
    // The actual difficulty lever. At difficulty 70 a Basic enemy is ~4.1x its
    // authored 100 HP and the boss clears 3,500 effective HP behind its armor.
    float peakHealth = 1f + HealthRampScale * Mathf.Pow(difficulty - 1, HealthRampExponent);

    int waveCount = Mathf.Clamp(4 + difficulty / 12, 4, 9);
    var waves = new WaveConfig.Wave[waveCount];

    for (int w = 1; w <= waveCount; w++)
    {
      bool lastWave = w == waveCount;

      // Ramp from FirstWaveHealthShare of peak up to the full value on the
      // final wave, so the level opens at something the starting board can hold.
      float t = waveCount > 1 ? (float)(w - 1) / (waveCount - 1) : 1f;
      float health = peakHealth * Mathf.Lerp(FirstWaveHealthShare, 1f, t);
      float reward = 1f + (health - 1f) * RewardShareOfHealthRamp;

      WaveConfig.WaveEnemyGroup Group(EnemyConfig cfg, int count) =>
        new WaveConfig.WaveEnemyGroup
        {
          enemyConfig = cfg,
          count = count,
          healthMultiplier = health,
          rewardMultiplier = reward,
        };

      // Swarm takes over the chaff role once it appears, so Basic steps back
      // rather than stacking on top - otherwise every added type just makes the
      // level longer, which is the exact failure the health ramp was built to
      // fix in the first place.
      int basicCount = 4 + difficulty / 12 + w / 2;
      if (difficulty >= SwarmFromDifficulty) basicCount -= 3;

      var groups = new List<WaveConfig.WaveEnemyGroup>
      {
        Group(basic, Mathf.Max(2, basicCount))
      };

      if (difficulty >= 2 && w >= 2)
      {
        // Trimmed when Swarm arrived: the two overlap heavily, and leaving both
        // at full count pushed level length from 3.9 to 4.6 minutes.
        groups.Add(Group(fast, 1 + difficulty / 26 + w / 4));
      }
      if (difficulty >= 5 && w >= 3)
      {
        // Likewise against Shielded, which now covers the "needs burst" role.
        groups.Add(Group(armored, 1 + difficulty / 30 + w / 5));
      }

      // Numerous by design - it is the answer to a board of single-target towers.
      if (difficulty >= SwarmFromDifficulty && w >= 2)
      {
        groups.Add(Group(swarm, 3 + difficulty / 24 + w / 3));
      }
      // The rest are force multipliers, not bodies, so counts stay low.
      if (difficulty >= ShieldedFromDifficulty && w >= 2)
      {
        groups.Add(Group(shielded, 1 + difficulty / 30 + w / 5));
      }
      if (difficulty >= SplitterFromDifficulty && w >= 3)
      {
        groups.Add(Group(splitter, 1 + difficulty / 35 + w / 5));
      }
      // One healer is already a real problem; two is usually a wipe.
      if (difficulty >= HealerFromDifficulty && w >= 3)
      {
        groups.Add(Group(healer, 1 + difficulty / 60));
      }

      if (lastWave && difficulty >= 3)
      {
        // 1 + d/30 put THREE bosses on the final levels, each at ~2.5x health
        // behind 30% armor -- concentrated in exactly the levels the sim could
        // not win. Two is still a finale; three was the wall.
        groups.Add(Group(boss, 1 + difficulty / 45));
      }

      waves[w - 1] = new WaveConfig.Wave
      {
        enemyGroups = groups.ToArray(),
        timeBetweenSpawns = Mathf.Max(0.55f, 1.6f - difficulty * 0.012f - w * 0.04f),
        timeToNextWave = TimeToNextWave,
        // Modest completion bonus: most income should come from kills, so the
        // player can't coast on flat per-wave gold in the late game
        waveGoldReward = 20 + 4 * w + difficulty
      };
    }

    return waves;
  }
}
