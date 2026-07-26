using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LevelGenerator
{
  // Grid bounds must match the GridManager in the MainGame scene (see DisplaySetup).
  private const int GridWidth = 16;
  private const int GridHeight = 9;

  private const int EnvironmentCount = 3;
  private const int LevelsPerEnvironment = 10;

  private const int MinPathLength = 18;
  private const int MaxPathLength = 46;
  private const int BaseSeed = 4242;

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
    if (basic == null || fast == null || armored == null || boss == null)
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
        waves.waves = GenerateWaves(difficulty, basic, fast, armored, boss);
        AssetDatabase.CreateAsset(waves, $"{WavesFolder}/Env{env}-Level{levelNumber:00}-Waves.asset");

        LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
        level.levelNumber = levelNumber;
        level.environmentName = $"Environment {env}";
        level.pathConfig = path;
        level.waveConfig = waves;
        level.startingGold = 500 + (difficulty - 1) * 15;
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
    for (int attempt = 0; attempt < 200; attempt++)
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
      // Weighted direction preference: mostly east, wander north/south
      var candidates = new List<Vector2Int>();
      void AddWeighted(Vector2Int dir, int weight)
      {
        for (int i = 0; i < weight; i++) candidates.Add(dir);
      }
      AddWeighted(east, 4);
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
    int difficulty, EnemyConfig basic, EnemyConfig fast, EnemyConfig armored, EnemyConfig boss)
  {
    int waveCount = Mathf.Clamp(3 + (difficulty + 2) / 3, 4, 10);
    var waves = new WaveConfig.Wave[waveCount];

    for (int w = 1; w <= waveCount; w++)
    {
      bool lastWave = w == waveCount;
      var groups = new List<WaveConfig.WaveEnemyGroup>
      {
        new WaveConfig.WaveEnemyGroup { enemyConfig = basic, count = 3 + difficulty / 3 + w / 2 }
      };

      if (difficulty >= 2 && w >= 2)
      {
        groups.Add(new WaveConfig.WaveEnemyGroup { enemyConfig = fast, count = 1 + difficulty / 4 + w / 3 });
      }
      if (difficulty >= 5 && w >= 3)
      {
        groups.Add(new WaveConfig.WaveEnemyGroup { enemyConfig = armored, count = difficulty / 5 + w / 4 });
      }
      if (lastWave && difficulty >= 3)
      {
        groups.Add(new WaveConfig.WaveEnemyGroup { enemyConfig = boss, count = 1 + difficulty / 15 });
      }

      waves[w - 1] = new WaveConfig.Wave
      {
        enemyGroups = groups.ToArray(),
        timeBetweenSpawns = Mathf.Max(0.6f, 2f - difficulty * 0.04f - w * 0.05f),
        timeToNextWave = 8f,
        // Modest completion bonus: most income should come from kills, so the
        // player can't coast on flat per-wave gold in the late game
        waveGoldReward = 20 + 4 * w + difficulty
      };
    }

    return waves;
  }
}
