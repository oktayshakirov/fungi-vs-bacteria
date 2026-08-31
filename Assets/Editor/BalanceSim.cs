using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Headless balance harness.
//
// 40 of the 70 generated levels have never been played by a human, and
// `LevelGenerator.GenerateWaves` scales enemy COUNT only — never strength — so
// nobody knows where the difficulty curve actually sits. Playing 70 levels by
// hand is not viable; this plays all of them in a couple of seconds.
//
// This is a MODEL of the combat loop, not the game itself. It re-implements the
// arithmetic in Enemy / Tower / TowerTargeting / Projectile / EnemySpawner
// against the real LevelConfig, WaveConfig, EnemyConfig and TowerConfig assets,
// stepped at a fixed 60 Hz. It deliberately does not touch the scene, physics
// or coroutines — a full playthrough of every level would otherwise take hours
// and could not run in batch mode.
//
// Known simplifications (all of them make the sim slightly OPTIMISTIC for the
// player, so a level the sim says is lost is definitely lost):
//   - Distances are 2D (xz). The real game measures in 3D and enemies sit at
//     half their model height, so real tower range is fractionally shorter.
//   - Splash damage is measured centre-to-centre; the real Physics.OverlapSphere
//     also catches colliders whose edge is inside the radius.
//   - The player proxy (see BuildPhase) plays greedily and never sells or
//     repositions, which a good human would.
//
// What is modelled faithfully, because each one changes the outcome:
//   - Towers only tick their fire countdown while they HAVE a target
//     (Tower.HandleShooting), and start one full period from ready.
//   - Projectiles have travel time and are DISCARDED if their target dies in
//     flight (Projectile.Update) — that wasted damage is a real DPS loss.
//   - Nearest-target selection with retarget-on-out-of-range
//     (TowerTargeting), so overkill on a single leader is reproduced.
//   - Slow expires 2s after the FIRST application in a chain, not the last:
//     Enemy.ApplySlow starts a fresh coroutine per hit and the earliest one to
//     land clears the effect for everyone.
//   - Wave pacing, including the trailing timeBetweenSpawns after the last
//     enemy of a group and the 8s gap before the next wave.
public static class BalanceSim
{
  // Must match GridManager.cellSize in MainGame.unity. DisplaySetup owns the
  // board dimensions but reads cellSize off the scene component, so there is no
  // constant to borrow — if the scene value changes, change this too.
  private const float CellSize = 5f;

  private const float Dt = 1f / 60f;
  private const float MaxSimSeconds = 1200f;

  private const float ProjectileSpeed = 20f;       // hardcoded in Tower.Attack
  private const float ProjectileHitRadius = 0.5f;  // Projectile.collisionRadius
  private const float ProjectileMaxLifetime = 5f;  // Projectile.maxLifetime
  private const float SlowDuration = 2f;           // Enemy.slowDuration
  private const float WaypointEpsilon = 0.1f;      // Enemy.Update arrival test

  // How often the player proxy re-evaluates what it can afford. Fine enough to
  // react within a wave, coarse enough to stay cheap.
  private const float BuyIntervalSeconds = 0.5f;

  private const string OutputFolder = "Builds/Balance";

  [MenuItem("Tools/Balance/Simulate All Levels")]
  public static void Run()
  {
    TowerConfig[] towers = LoadTowers();
    LevelConfig[] levels = LoadLevels();
    if (towers == null || levels == null) return;

    WarnAboutDeadTowers(towers);

    var results = new List<LevelResult>();
    foreach (LevelConfig level in levels)
    {
      results.Add(SimulateLevel(level, towers));
    }

    WriteCsv(results);
    LogSummary(results);
  }

  public static void RunBatch()
  {
    Run();
    EditorApplication.Exit(0);
  }

  // ---------------------------------------------------------------- loading

  private static TowerConfig[] LoadTowers()
  {
    var db = AssetDatabase.LoadAssetAtPath<TowerDatabase>(
      "Assets/Settings/Towers/TowerDatabase.asset");
    if (db == null || db.availableTowers == null || db.availableTowers.Length == 0)
    {
      Debug.LogError("BALANCE: TowerDatabase.asset missing or empty.");
      return null;
    }
    return db.availableTowers.Where(t => t != null).ToArray();
  }

  private static LevelConfig[] LoadLevels()
  {
    LevelConfig[] levels = Resources.LoadAll<LevelConfig>("Levels");
    if (levels.Length == 0)
    {
      Debug.LogError("BALANCE: no LevelConfig assets in Resources/Levels.");
      return null;
    }
    return levels
      .OrderBy(EnvironmentIndex)
      .ThenBy(l => l.levelNumber)
      .ToArray();
  }

  private static int EnvironmentIndex(LevelConfig level)
  {
    // "Environment 3" -> 3. Sorting on the raw string would break at 10+.
    string digits = new string(level.environmentName.Where(char.IsDigit).ToArray());
    return int.TryParse(digits, out int n) ? n : 0;
  }

  // A tower with no damage or no fire rate can still be bought in game: it just
  // never shoots. That is worth shouting about from the balance tool, because
  // it looks like a balance problem long before it looks like missing code.
  private static void WarnAboutDeadTowers(TowerConfig[] towers)
  {
    foreach (TowerConfig t in towers)
    {
      if (IsAttacker(t) || t.isSupport) continue;
      Debug.LogWarning(
        $"BALANCE: '{t.towerName}' costs {t.cost} but has damage={t.damage}, " +
        $"fireRate={t.fireRate} and is not flagged isSupport — it can be bought " +
        "and does nothing. Excluded from the simulated loadout.");
    }
  }

  private static bool IsAttacker(TowerConfig t) =>
    !t.isSupport && t.damage > 0 && t.fireRate > 0f;

  // ------------------------------------------------------------ sim structs

  private class SimEnemy
  {
    public Vector2 pos;
    public int waypoint;
    public float normalSpeed;
    public float speed;
    public float slowAmount;
    public readonly List<float> slowExpiry = new List<float>();
    public int health;
    public int maxHealth;
    public float armor;
    public int damage;
    public int gold;
    public bool alive = true;
    public bool leaked;
  }

  private class SimTower
  {
    public Vector2 pos;
    public TowerConfig cfg;
    public float cooldown;
    public SimEnemy target;

    // Contributed by nearby support towers; mirrors TowerBuffs.
    public float damageMult = 1f;
    public float fireRateMult = 1f;

    // World units of path this tower's spot covers. Kept so a support tower can
    // be scored against exactly the same yardstick as an attacker.
    public float coverage;

    public int Damage => Mathf.RoundToInt(cfg.damage * damageMult);
    public float FireRate => cfg.fireRate * fireRateMult;
  }

  private class SimProjectile
  {
    public Vector2 pos;
    public SimEnemy target;
    public int damage;
    public bool aoe;
    public float splash;
    public bool slows;
    public float slowAmount;
    public float life;
  }

  private struct SpawnEvent
  {
    public float time;
    public EnemyConfig enemy;
    public float healthMultiplier;
    public float rewardMultiplier;
  }

  public class LevelResult
  {
    public string environment;
    public int environmentIndex;
    public int levelNumber;
    public int difficulty;
    public int waves;
    public int totalEnemies;
    public bool won;
    public bool timedOut;
    public int healthLeft;
    public int startingHealth;
    public int stars;
    public int leaks;
    public int towersBuilt;
    public int supportTowers;
    // Mean fraction of the path an enemy covered before dying (0-1).
    public float killDepth;

    // Effective HP the player destroyed, and the seconds during which there was
    // anything to shoot at.
    public long damageDealt;
    public float combatSeconds;
    // Sum of damage x fireRate over the final board, i.e. what the towers could
    // output if they never idled.
    public float potentialDps;

    public float ActualDps => combatSeconds > 0f ? damageDealt / combatSeconds : 0f;
    // Below ~30% the towers are mostly idle and there is a lot of headroom left.
    public float Utilization => potentialDps > 0f ? ActualDps / potentialDps : 0f;
    public int goldSpent;
    public int goldLeft;
    public int peakAlive;
    public float simSeconds;

    public float HealthPct => startingHealth <= 0 ? 0f : (float)healthLeft / startingHealth;

    public string Verdict
    {
      get
      {
        if (timedOut) return "TIMEOUT";
        if (!won) return "LOSS";
        if (stars == 1) return "HARD";
        if (stars == 2) return "FAIR";
        return leaks == 0 ? "TRIVIAL" : "EASY";
      }
    }
  }

  // ------------------------------------------------------------------- core

  private static LevelResult SimulateLevel(LevelConfig level, TowerConfig[] towers)
  {
    var result = new LevelResult
    {
      environment = level.environmentName,
      environmentIndex = EnvironmentIndex(level),
      levelNumber = level.levelNumber,
      startingHealth = level.startingHealth,
      goldLeft = level.startingGold,
    };
    result.difficulty = (result.environmentIndex - 1) * 10 + level.levelNumber;

    Vector2[] path = BuildPath(level.pathConfig);
    if (path.Length < 2)
    {
      Debug.LogError($"BALANCE: {level.name} has no usable path.");
      return result;
    }

    List<Vector2> freeCells = BuildFreeCells(level.pathConfig);
    List<SpawnEvent> spawns = BuildSpawnSchedule(level.waveConfig, out List<KeyValuePair<float, int>> waveGold);
    result.waves = level.waveConfig != null && level.waveConfig.waves != null
      ? level.waveConfig.waves.Length : 0;
    result.totalEnemies = spawns.Count;

    TowerConfig[] attackers = towers.Where(IsAttacker).ToArray();
    TowerConfig[] supports = towers.Where(t => t.isSupport).ToArray();
    List<float[]> coverage = BuildCoverageTable(freeCells, attackers, path);

    int gold = level.startingGold;
    int health = level.startingHealth;
    int spent = 0;

    var enemies = new List<SimEnemy>();
    var activeTowers = new List<SimTower>();
    var projectiles = new List<SimProjectile>();

    int nextSpawn = 0;
    int nextWaveGold = 0;
    float nextBuy = 0f;
    float t = 0f;

    int leaked = 0;
    int killed = 0;
    float killDepthSum = 0f;
    long damageDealt = 0;
    float combatSeconds = 0f;

    while (t < MaxSimSeconds)
    {
      // --- spawns
      while (nextSpawn < spawns.Count && spawns[nextSpawn].time <= t)
      {
        enemies.Add(MakeEnemy(spawns[nextSpawn], path[0]));
        nextSpawn++;
      }

      // --- wave completion bonuses
      while (nextWaveGold < waveGold.Count && waveGold[nextWaveGold].Key <= t)
      {
        gold += waveGold[nextWaveGold].Value;
        nextWaveGold++;
      }

      // --- player proxy
      if (t >= nextBuy)
      {
        nextBuy = t + BuyIntervalSeconds;
        BuildPhase(ref gold, ref spent, activeTowers, freeCells, attackers, supports, coverage);
      }

      // Only count time when there is something to shoot at; the idle gap
      // between waves would otherwise drag the measured dps down.
      if (enemies.Count > 0) combatSeconds += Dt;

      // --- towers acquire and fire
      foreach (SimTower tower in activeTowers)
      {
        if (tower.cfg.isSupport) continue;
        UpdateTower(tower, enemies, projectiles);
      }

      // --- projectiles fly and land
      UpdateProjectiles(projectiles, enemies, t, ref gold, ref damageDealt);

      // --- enemies move, leak, expire slows
      health -= UpdateEnemies(enemies, path, t);

      // Retire the dead immediately. Leaving them in the list makes every
      // tower's target scan walk hundreds of corpses instead of the dozen or so
      // enemies actually on the board.
      for (int i = enemies.Count - 1; i >= 0; i--)
      {
        SimEnemy e = enemies[i];
        if (e.alive) continue;

        if (e.leaked) leaked++;
        else
        {
          // How deep the enemy got before dying. This is the difficulty signal
          // that still discriminates when a level is won without a scratch:
          // dying at 30% of the path is a rout, dying at 90% is a close call.
          killDepthSum += (float)e.waypoint / Mathf.Max(1, path.Length - 1);
          killed++;
        }
        enemies.RemoveAt(i);
      }

      if (enemies.Count > result.peakAlive) result.peakAlive = enemies.Count;

      if (health <= 0)
      {
        health = 0;
        break;
      }

      if (nextSpawn >= spawns.Count && enemies.Count == 0)
      {
        result.won = true;
        break;
      }

      t += Dt;
    }

    result.timedOut = t >= MaxSimSeconds;
    result.simSeconds = t;
    result.healthLeft = health;
    result.goldLeft = gold;
    result.goldSpent = spent;
    result.towersBuilt = activeTowers.Count;
    result.supportTowers = activeTowers.Count(t2 => t2.cfg.isSupport);
    result.leaks = leaked;
    result.killDepth = killed > 0 ? killDepthSum / killed : 0f;
    result.damageDealt = damageDealt;
    result.combatSeconds = combatSeconds;
    result.potentialDps = activeTowers
      .Where(t2 => !t2.cfg.isSupport)
      .Sum(t2 => t2.Damage * t2.FireRate);
    result.stars = result.won
      ? LevelProgress.StarsForHealth(health, level.startingHealth)
      : 0;

    return result;
  }

  private static SimEnemy MakeEnemy(SpawnEvent spawnEvent, Vector2 spawn)
  {
    EnemyConfig cfg = spawnEvent.enemy;
    float speed = cfg.moveSpeed * (cfg.isFast ? cfg.speedMultiplier : 1f);
    int hp = Mathf.Max(1, Mathf.RoundToInt(cfg.maxHealth * spawnEvent.healthMultiplier));
    return new SimEnemy
    {
      pos = spawn,
      waypoint = 0,
      normalSpeed = speed,
      speed = speed,
      health = hp,
      maxHealth = hp,
      armor = cfg.isArmored ? cfg.armorDamageReduction : 0f,
      damage = cfg.baseDamage,
      gold = Mathf.Max(1, Mathf.RoundToInt(cfg.goldReward * spawnEvent.rewardMultiplier)),
    };
  }

  // ------------------------------------------------------------- sim update

  private static void UpdateTower(SimTower tower, List<SimEnemy> enemies,
    List<SimProjectile> projectiles)
  {
    // TowerTargeting.UpdateTarget: drop a dead or out-of-range target, then
    // take the nearest one inside range.
    if (tower.target != null &&
        (!tower.target.alive ||
         Vector2.Distance(tower.pos, tower.target.pos) > tower.cfg.range))
    {
      tower.target = null;
    }

    if (tower.target == null)
    {
      float best = float.MaxValue;
      SimEnemy nearest = null;
      for (int i = 0; i < enemies.Count; i++)
      {
        SimEnemy e = enemies[i];
        if (!e.alive) continue;
        float d = Vector2.Distance(tower.pos, e.pos);
        if (d < best && d <= tower.cfg.range)
        {
          best = d;
          nearest = e;
        }
      }
      tower.target = nearest;
    }

    // Tower.Update only calls HandleShooting when a target exists, so an idle
    // tower does not bank its cooldown.
    if (tower.target == null) return;

    tower.cooldown -= Dt;
    if (tower.cooldown > 0f) return;

    projectiles.Add(new SimProjectile
    {
      pos = tower.pos,
      target = tower.target,
      damage = tower.Damage,
      aoe = tower.cfg.isAoE,
      splash = tower.cfg.splashRadius,
      slows = tower.cfg.slowsEnemies,
      slowAmount = tower.cfg.slowAmount,
    });
    tower.cooldown = 1f / tower.FireRate;
  }

  private static void UpdateProjectiles(List<SimProjectile> projectiles,
    List<SimEnemy> enemies, float now, ref int gold, ref long damageDealt)
  {
    for (int i = projectiles.Count - 1; i >= 0; i--)
    {
      SimProjectile p = projectiles[i];
      p.life += Dt;

      // The shot is thrown away if its target died in flight. This is the
      // single biggest source of "wasted" tower output in the real game.
      if (p.target == null || !p.target.alive || p.life > ProjectileMaxLifetime)
      {
        projectiles.RemoveAt(i);
        continue;
      }

      float dist = Vector2.Distance(p.pos, p.target.pos);
      if (dist <= ProjectileHitRadius)
      {
        if (p.aoe)
        {
          for (int e = 0; e < enemies.Count; e++)
          {
            SimEnemy victim = enemies[e];
            if (!victim.alive) continue;
            if (Vector2.Distance(p.pos, victim.pos) <= p.splash)
            {
              Damage(victim, p, now, ref gold, ref damageDealt);
            }
          }
        }
        else
        {
          Damage(p.target, p, now, ref gold, ref damageDealt);
        }
        projectiles.RemoveAt(i);
        continue;
      }

      Vector2 dir = (p.target.pos - p.pos).normalized;
      p.pos += dir * (ProjectileSpeed * Dt);
    }
  }

  private static void Damage(SimEnemy e, SimProjectile p, float now, ref int gold,
    ref long damageDealt)
  {
    int dealt = Mathf.RoundToInt(p.damage * (1f - e.armor));
    // Overkill does not count as work done.
    damageDealt += Mathf.Min(dealt, Mathf.Max(0, e.health));
    e.health -= dealt;

    if (p.slows)
    {
      e.slowAmount = Mathf.Max(e.slowAmount, p.slowAmount);
      e.speed = e.normalSpeed * (1f - e.slowAmount);
      e.slowExpiry.Add(now + SlowDuration);
    }

    if (e.health <= 0)
    {
      e.alive = false;
      gold += e.gold;
    }
  }

  // Returns the damage dealt to the base this step.
  private static int UpdateEnemies(List<SimEnemy> enemies, Vector2[] path, float now)
  {
    int baseDamage = 0;

    for (int i = 0; i < enemies.Count; i++)
    {
      SimEnemy e = enemies[i];
      if (!e.alive) continue;

      // Enemy.ApplySlow starts a new coroutine per hit and never cancels the
      // old ones, so the FIRST to elapse clears the slow for good.
      if (e.slowExpiry.Count > 0)
      {
        bool expired = false;
        for (int s = 0; s < e.slowExpiry.Count; s++)
        {
          if (e.slowExpiry[s] <= now) { expired = true; break; }
        }
        if (expired)
        {
          e.slowExpiry.Clear();
          e.slowAmount = 0f;
          e.speed = e.normalSpeed;
        }
      }

      Vector2 target = path[e.waypoint];
      e.pos = Vector2.MoveTowards(e.pos, target, e.speed * Dt);

      if (Vector2.Distance(e.pos, target) < WaypointEpsilon)
      {
        e.waypoint++;
        if (e.waypoint >= path.Length)
        {
          baseDamage += e.damage;
          e.alive = false;
          e.leaked = true;
        }
      }
    }

    return baseDamage;
  }

  // ----------------------------------------------------------- player proxy

  // A greedy "competent but not clairvoyant" player: whenever it can afford
  // something, it buys whatever scores best right now and never sells. Score is
  // damage output x how much of the path the spot actually covers, per gold —
  // which is roughly how a player reasons about a build site.
  private static void BuildPhase(ref int gold, ref int spent, List<SimTower> towers,
    List<Vector2> freeCells, TowerConfig[] attackers, TowerConfig[] supports,
    List<float[]> coverage)
  {
    while (true)
    {
      float bestScore = 0f;
      int bestCell = -1;
      TowerConfig chosen = null;
      float chosenCover = 0f;

      for (int c = 0; c < freeCells.Count; c++)
      {
        for (int w = 0; w < attackers.Length; w++)
        {
          TowerConfig cfg = attackers[w];
          if (cfg.cost > gold) continue;

          float cover = coverage[c][w];
          if (cover <= 0f) continue;

          float score = TowerValue(cfg) * cover / cfg.cost;
          if (score > bestScore)
          {
            bestScore = score;
            bestCell = c;
            chosen = cfg;
            chosenCover = cover;
          }
        }

        // A support tower is worth exactly what it adds to the attackers it can
        // reach, so it only becomes attractive once they are clustered — which
        // is the decision a player makes too.
        foreach (TowerConfig cfg in supports)
        {
          if (cfg.cost > gold) continue;

          // Both boosts scale output roughly linearly, so their sum is a fair
          // stand-in for the fraction of dps added. Multiplying by each buffed
          // tower's own coverage puts this on exactly the attacker scale:
          // (value x path covered) per gold.
          float gain = 0f;
          foreach (SimTower t in towers)
          {
            if (t.cfg.isSupport) continue;
            if (Vector2.Distance(freeCells[c], t.pos) > cfg.range) continue;
            gain += TowerValue(t.cfg) * t.coverage * (cfg.damageBoost + cfg.fireRateBoost);
          }
          if (gain <= 0f) continue;

          float score = gain / cfg.cost;
          if (score > bestScore)
          {
            bestScore = score;
            bestCell = c;
            chosen = cfg;
            chosenCover = 0f;
          }
        }
      }

      if (bestCell < 0 || chosen == null) return;

      towers.Add(new SimTower
      {
        pos = freeCells[bestCell],
        cfg = chosen,
        coverage = chosenCover,
        // Tower.Initialize starts a full period from ready.
        cooldown = chosen.isSupport ? 0f : 1f / chosen.fireRate,
      });
      gold -= chosen.cost;
      spent += chosen.cost;

      // The cell is taken: drop it from both parallel lists so it cannot be
      // chosen again and the indices stay aligned.
      freeCells.RemoveAt(bestCell);
      coverage.RemoveAt(bestCell);

      RecalculateBuffs(towers);
    }
  }

  // Mirrors TowerBuffs.Recalculate: additive stacking, support towers buff only
  // attackers, never each other.
  private static void RecalculateBuffs(List<SimTower> towers)
  {
    foreach (SimTower tower in towers)
    {
      if (tower.cfg.isSupport)
      {
        tower.damageMult = 1f;
        tower.fireRateMult = 1f;
        continue;
      }

      float damage = 1f;
      float fireRate = 1f;
      foreach (SimTower source in towers)
      {
        if (source == tower || !source.cfg.isSupport) continue;
        if (Vector2.Distance(source.pos, tower.pos) > source.cfg.range) continue;
        damage += source.cfg.damageBoost;
        fireRate += source.cfg.fireRateBoost;
      }
      tower.damageMult = damage;
      tower.fireRateMult = fireRate;
    }
  }

  private static float TowerValue(TowerConfig cfg)
  {
    float dps = cfg.damage * cfg.fireRate;
    if (cfg.isAoE) dps *= 1.6f;          // splash hits more than one enemy
    if (cfg.slowsEnemies) dps *= 1.15f;  // slow buys every other tower more time
    return dps;
  }

  // For each free cell and each tower type, how many world units of the path
  // fall inside that tower's range. Sampled rather than solved: the path is a
  // polyline and an exact chord solution would be far more code for no gain.
  private static List<float[]> BuildCoverageTable(List<Vector2> cells,
    TowerConfig[] towers, Vector2[] path)
  {
    const float step = 0.25f;
    var samples = new List<Vector2>();
    for (int i = 0; i < path.Length - 1; i++)
    {
      float segLen = Vector2.Distance(path[i], path[i + 1]);
      int count = Mathf.Max(1, Mathf.RoundToInt(segLen / step));
      for (int s = 0; s < count; s++)
      {
        samples.Add(Vector2.Lerp(path[i], path[i + 1], (float)s / count));
      }
    }

    var table = new List<float[]>(cells.Count);
    for (int c = 0; c < cells.Count; c++)
    {
      var row = new float[towers.Length];
      for (int w = 0; w < towers.Length; w++)
      {
        float range = towers[w].range;
        int inside = 0;
        for (int s = 0; s < samples.Count; s++)
        {
          if (Vector2.Distance(cells[c], samples[s]) <= range) inside++;
        }
        row[w] = inside * step;
      }
      table.Add(row);
    }
    return table;
  }

  // ------------------------------------------------------------ level setup

  private static Vector2 GridToWorld(Vector2Int cell)
  {
    // Mirrors GridManager.GridToWorld with the origin DisplaySetup writes:
    // the board is centred on the world origin.
    float originX = -(DisplaySetup.BoardWidth * CellSize) * 0.5f;
    float originZ = -(DisplaySetup.BoardHeight * CellSize) * 0.5f;
    return new Vector2(
      originX + cell.x * CellSize + CellSize * 0.5f,
      originZ + cell.y * CellSize + CellSize * 0.5f);
  }

  private static Vector2[] BuildPath(PathConfig cfg)
  {
    if (cfg == null || cfg.pathGridCoordinates == null) return new Vector2[0];
    return cfg.pathGridCoordinates.Select(GridToWorld).ToArray();
  }

  private static List<Vector2> BuildFreeCells(PathConfig cfg)
  {
    var blocked = new HashSet<Vector2Int>(cfg.pathGridCoordinates);
    var free = new List<Vector2>();
    for (int x = 0; x < DisplaySetup.BoardWidth; x++)
    {
      for (int y = 0; y < DisplaySetup.BoardHeight; y++)
      {
        var cell = new Vector2Int(x, y);
        if (!blocked.Contains(cell)) free.Add(GridToWorld(cell));
      }
    }
    return free;
  }

  // Wave pacing is independent of how the fight goes (waves auto-advance), so
  // the whole schedule can be laid out up front. Mirrors EnemySpawner.SpawnWave,
  // including the wait AFTER the last enemy of each group.
  private static List<SpawnEvent> BuildSpawnSchedule(WaveConfig cfg,
    out List<KeyValuePair<float, int>> waveGold)
  {
    var spawns = new List<SpawnEvent>();
    waveGold = new List<KeyValuePair<float, int>>();
    if (cfg == null || cfg.waves == null) return spawns;

    float t = 0f;
    for (int w = 0; w < cfg.waves.Length; w++)
    {
      WaveConfig.Wave wave = cfg.waves[w];
      foreach (WaveConfig.WaveEnemyGroup group in wave.enemyGroups)
      {
        for (int i = 0; i < group.count; i++)
        {
          spawns.Add(new SpawnEvent
          {
            time = t,
            enemy = group.enemyConfig,
            healthMultiplier = group.healthMultiplier,
            rewardMultiplier = group.rewardMultiplier,
          });
          t += wave.timeBetweenSpawns;
        }
      }

      waveGold.Add(new KeyValuePair<float, int>(t, wave.waveGoldReward));

      if (w < cfg.waves.Length - 1) t += wave.timeToNextWave;
    }
    return spawns;
  }

  // --------------------------------------------------------------- reporting

  private static void WriteCsv(List<LevelResult> results)
  {
    Directory.CreateDirectory(OutputFolder);
    var sb = new StringBuilder();
    sb.AppendLine("env,level,difficulty,verdict,stars,healthLeft,healthPct,leaks,killDepth," +
                  "waves,enemies,peakAlive,towers,support,goldSpent,goldLeft,simSeconds");

    foreach (LevelResult r in results)
    {
      sb.AppendLine(string.Join(",", new[]
      {
        r.environmentIndex.ToString(),
        r.levelNumber.ToString(),
        r.difficulty.ToString(),
        r.Verdict,
        r.stars.ToString(),
        r.healthLeft.ToString(),
        r.HealthPct.ToString("F2", CultureInfo.InvariantCulture),
        r.leaks.ToString(),
        r.killDepth.ToString("F3", CultureInfo.InvariantCulture),
        r.waves.ToString(),
        r.totalEnemies.ToString(),
        r.peakAlive.ToString(),
        r.towersBuilt.ToString(),
        r.supportTowers.ToString(),
        r.goldSpent.ToString(),
        r.goldLeft.ToString(),
        r.simSeconds.ToString("F1", CultureInfo.InvariantCulture),
      }));
    }

    string path = Path.Combine(OutputFolder, "balance.csv");
    File.WriteAllText(path, sb.ToString());
    Debug.Log($"BALANCE: wrote {path}");
  }

  private static void LogSummary(List<LevelResult> results)
  {
    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine("=== BALANCE SIMULATION ===");
    sb.AppendLine($"Levels simulated: {results.Count}");
    sb.AppendLine();

    foreach (string verdict in new[] { "LOSS", "HARD", "FAIR", "EASY", "TRIVIAL", "TIMEOUT" })
    {
      int n = results.Count(r => r.Verdict == verdict);
      if (n == 0) continue;
      sb.AppendLine($"  {verdict,-8} {n,3}  ({100f * n / results.Count:F0}%)");
    }

    sb.AppendLine();
    sb.AppendLine("Per environment (levels 1-10 left to right):");
    foreach (var group in results.GroupBy(r => r.environmentIndex).OrderBy(g => g.Key))
    {
      string row = string.Join(" ", group
        .OrderBy(r => r.levelNumber)
        .Select(r => Symbol(r.Verdict)));
      sb.AppendLine($"  Env {group.Key}: {row}");
    }
    sb.AppendLine("  legend: . trivial  o easy  = fair  ! hard  X loss  ? timeout");

    sb.AppendLine();
    sb.AppendLine("Kill depth by difficulty (% of path enemies reach before dying;");
    sb.AppendLine("higher = closer call, and it still discriminates at 100% health):");
    foreach (var chunk in results.OrderBy(r => r.difficulty).Select((r, i) => new { r, i })
               .GroupBy(x => x.i / 10))
    {
      string row = string.Join(" ", chunk.Select(x => $"{x.r.killDepth * 100,3:F0}"));
      sb.AppendLine($"  d{chunk.First().r.difficulty,2}-{chunk.Last().r.difficulty,2}: {row}");
    }

    sb.AppendLine();
    sb.AppendLine("Health remaining by difficulty (1-70):");
    foreach (var chunk in results.OrderBy(r => r.difficulty).Select((r, i) => new { r, i })
               .GroupBy(x => x.i / 10))
    {
      string row = string.Join(" ", chunk.Select(x => $"{x.r.HealthPct * 100,3:F0}"));
      sb.AppendLine($"  d{chunk.First().r.difficulty,2}-{chunk.Last().r.difficulty,2}: {row}");
    }

    var losses = results.Where(r => !r.won).ToList();
    if (losses.Count > 0)
    {
      sb.AppendLine();
      sb.AppendLine($"Unwinnable ({losses.Count}):");
      foreach (LevelResult r in losses)
      {
        sb.AppendLine($"  Env{r.environmentIndex} L{r.levelNumber:00} (d{r.difficulty}) " +
                      $"{r.totalEnemies} enemies, peak {r.peakAlive} alive, " +
                      $"{r.towersBuilt} towers, {r.goldLeft} gold unspent");
      }
    }

    sb.AppendLine();
    sb.AppendLine("Economy:");
    sb.AppendLine($"  Median towers built: {Median(results.Select(r => (float)r.towersBuilt))}");
    sb.AppendLine($"  Median support towers: {Median(results.Select(r => (float)r.supportTowers))}");
    sb.AppendLine($"  Median kill depth:   {Median(results.Select(r => r.killDepth)) * 100:F0}%");
    sb.AppendLine();
    sb.AppendLine("Tower output (how much headroom the player still has):");
    sb.AppendLine($"  Median potential dps: {Median(results.Select(r => r.potentialDps)):F0}");
    sb.AppendLine($"  Median actual dps:    {Median(results.Select(r => r.ActualDps)):F0}");
    sb.AppendLine($"  Median utilization:   {Median(results.Select(r => r.Utilization)) * 100:F0}%");
    sb.AppendLine("  (utilization is the share of theoretical output actually used;");
    sb.AppendLine("   low means towers idle and the level is far below the player's ceiling)");
    sb.AppendLine();
    sb.AppendLine("Utilization by difficulty (1-70):");
    foreach (var chunk in results.OrderBy(r => r.difficulty).Select((r, i) => new { r, i })
               .GroupBy(x => x.i / 10))
    {
      string row = string.Join(" ", chunk.Select(x => $"{x.r.Utilization * 100,3:F0}"));
      sb.AppendLine($"  d{chunk.First().r.difficulty,2}-{chunk.Last().r.difficulty,2}: {row}");
    }
    sb.AppendLine($"  Median gold unspent: {Median(results.Select(r => (float)r.goldLeft))}");
    sb.AppendLine($"  Max gold unspent:    {results.Max(r => r.goldLeft)}");
    sb.AppendLine($"  Median peak alive:   {Median(results.Select(r => (float)r.peakAlive))}");
    sb.AppendLine($"  Max peak alive:      {results.Max(r => r.peakAlive)}");
    sb.AppendLine($"  Longest level:       {results.Max(r => r.simSeconds):F0}s");

    Debug.Log(sb.ToString());
  }

  private static string Symbol(string verdict)
  {
    switch (verdict)
    {
      case "TRIVIAL": return ".";
      case "EASY": return "o";
      case "FAIR": return "=";
      case "HARD": return "!";
      case "LOSS": return "X";
      default: return "?";
    }
  }

  private static float Median(IEnumerable<float> values)
  {
    var sorted = values.OrderBy(v => v).ToList();
    if (sorted.Count == 0) return 0f;
    int mid = sorted.Count / 2;
    return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) * 0.5f : sorted[mid];
  }
}
