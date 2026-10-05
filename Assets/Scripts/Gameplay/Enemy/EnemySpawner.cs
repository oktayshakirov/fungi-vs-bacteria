using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
  public static event System.Action<int> OnWaveStarted;
  [SerializeField] private WaveConfig waveConfig;

  // Splitter children are spawned from Enemy when a parent dies, which needs a
  // way back to the spawner. One spawner per MainGame scene.
  public static EnemySpawner Instance { get; private set; }

  private int currentWave;
  private bool isSpawning;
  private bool awaitingClear;
  private float waveTimer;
  private bool isWaitingForNextWave;

  public bool IsLastWave => waveConfig != null && currentWave >= waveConfig.waves.Length;
  public int WavesStarted => currentWave;
  public bool IsWaveInProgress => isSpawning || awaitingClear;
  public bool CanStartWave => waveConfig != null && !IsLastWave && !IsWaveInProgress
    && (GameManager.Instance == null || !GameManager.Instance.HasEnded);
  public WaveConfig.Wave NextWave => waveConfig != null && currentWave < waveConfig.waves.Length
    ? waveConfig.waves[currentWave] : null;
  public int TotalWaves => waveConfig != null ? waveConfig.waves.Length : 0;
  public float PreparationRemaining => isWaitingForNextWave ? waveTimer : 0;

  private void Awake() { Instance = this; }
  private void OnDestroy() { if (Instance == this) Instance = null; }

  private void Start()
  {
    if (GameSession.SelectedLevel != null && GameSession.SelectedLevel.waveConfig != null)
      waveConfig = GameSession.SelectedLevel.waveConfig;
    if (waveConfig == null) { Debug.LogError("WaveConfig is not assigned to EnemySpawner!"); return; }
    HUDManager.Instance?.UpdateWaveText(0, TotalWaves);
    RefreshPlanning();
  }

  private void Update()
  {
    if (!isWaitingForNextWave || (GameManager.Instance != null && GameManager.Instance.HasEnded)) return;
    waveTimer = Mathf.Max(0, waveTimer - Time.deltaTime);
    HUDManager.Instance?.UpdateWaveTimer(waveTimer);
    if (waveTimer <= 0) StartNextWave();
  }

  // The same button starts the first wave and sends a prepared wave early.
  // Only Update owns the timer; there is no stale delayed coroutine to send
  // an extra wave after a manual start.
  public void StartGame() { StartNextWave(); }

  public void StartNextWave()
  {
    if (!CanStartWave || Time.timeScale <= 0) return;
    WaveConfig.Wave wave = NextWave;
    isWaitingForNextWave = false;
    isSpawning = true;
    awaitingClear = true;
    currentWave++;
    OnWaveStarted?.Invoke(currentWave);
    HUDManager.Instance?.UpdateWaveText(currentWave, TotalWaves);
    HUDManager.Instance?.ShowWaveBanner(currentWave, TotalWaves);
    RefreshPlanning();
    StartCoroutine(SpawnWave(wave));
  }

  private IEnumerator SpawnWave(WaveConfig.Wave wave)
  {
    if (wave.enemyGroups != null)
      foreach (var enemyGroup in wave.enemyGroups)
      {
        if (enemyGroup == null || enemyGroup.enemyConfig == null || enemyGroup.count <= 0) continue;
        for (int i = 0; i < enemyGroup.count; i++)
        {
          SpawnEnemy(enemyGroup);
          yield return new WaitForSeconds(Mathf.Max(.05f, wave.timeBetweenSpawns));
        }
      }
    isSpawning = false;
    CheckWaveClear();
    RefreshPlanning();
  }

  // Count includes splitter children, which register before their parent is
  // removed. Clearing the parent therefore cannot award a premature reward.
  public void CheckWaveClear()
  {
    if (!awaitingClear || isSpawning || GameManager.Instance == null
      || GameManager.Instance.HasEnded || GameManager.Instance.AliveEnemies > 0) return;
    awaitingClear = false;
    WaveConfig.Wave cleared = waveConfig.waves[currentWave - 1];
    GameManager.Instance.AddGold(cleared.waveGoldReward);
    if (!IsLastWave)
    {
      isWaitingForNextWave = true;
      waveTimer = Mathf.Max(0, cleared.timeToNextWave);
      HUDManager.Instance?.ShowPathClear();
    }
    RefreshPlanning();
    GameManager.Instance.CheckVictory();
  }

  private void RefreshPlanning() { HUDManager.Instance?.RefreshWavePlanning(this); }

  private void SpawnEnemy(WaveConfig.WaveEnemyGroup enemyGroup)
  {
    if (enemyGroup.enemyConfig != null && enemyGroup.enemyConfig.prefab != null && PathManager.Instance != null)
    {
      // Get the first point of the path for spawning
      Vector3[] pathPoints = PathManager.Instance.GetPathPoints();
      if (pathPoints == null || pathPoints.Length == 0)
      {
        Debug.LogError("No path points available!");
        return;
      }

      // Calculate spawn position using enemy's actual model height
      float heightOffset = UnitScale.EnemyGroundOffset(enemyGroup.enemyConfig.prefab, enemyGroup.enemyConfig.scaleMultiplier);
      Vector3 spawnPoint = pathPoints[0];
      spawnPoint.y = heightOffset;

      GameObject enemyObj = EnemyPool.Get(
        enemyGroup.enemyConfig.prefab,
        spawnPoint,
        Quaternion.identity
      );

      Enemy enemy = enemyObj.GetComponent<Enemy>();
      if (enemy != null)
      {
        GameManager.Instance.OnEnemySpawned();

        // Create path points at correct height for this enemy
        Vector3[] adjustedPathPoints = new Vector3[pathPoints.Length];
        for (int i = 0; i < pathPoints.Length; i++)
        {
          adjustedPathPoints[i] = new Vector3(pathPoints[i].x, heightOffset, pathPoints[i].z);
        }

        enemy.Initialize(
          adjustedPathPoints,
          enemyGroup.enemyConfig,
          enemyGroup.healthMultiplier,
          enemyGroup.rewardMultiplier
        );

        // Climbing out of the nest rather than appearing on top of it: mist out
        // of the mouth, and the enemy swelling up inside it. Both are purely
        // visual - the enemy is on the path, at full health, and targetable
        // from the frame it spawns.
        enemy.PlayEmergence();
        SpawnEffect.Spawn(spawnPoint, heightOffset * 2f);
        NestPulse.Pulse();
      }
    }
    else
    {
      Debug.LogError("Missing enemy config, prefab or PathManager!");
    }
  }

  // Called by Enemy when a splitter dies. The children reuse the parent's
  // already height-adjusted waypoint array and enter at the parent's waypoint,
  // so a splitter killed at the end of the path drops its children right on the
  // base -- which is the whole point of the type.
  public void SpawnSplitChildren(EnemyConfig cfg, Vector3[] path,
    Enemy.SpawnOverride childOverride, int count, int parentMaxHealth, int parentReward)
  {
    if (cfg == null || cfg.prefab == null || path == null || count <= 0) return;

    float heightOffset = UnitScale.EnemyGroundOffset(cfg.prefab, cfg.scaleMultiplier, childOverride.sizeScale);
    Vector3[] childPath = (Vector3[])path.Clone();
    for (int i = 0; i < childPath.Length; i++) childPath[i].y = heightOffset;
    Vector3 origin = path[Mathf.Clamp(childOverride.startWaypoint, 0, path.Length - 1)];

    for (int i = 0; i < count; i++)
    {
      GameObject childObj = EnemyPool.Get(cfg.prefab, origin, Quaternion.identity);
      Enemy child = childObj.GetComponent<Enemy>();
      if (child == null) continue;

      GameManager.Instance.OnEnemySpawned();

      // Health and reward are expressed relative to the PARENT's already
      // wave-scaled values, not to the raw config, or children would spawn at
      // level-1 strength in level 70.
      float healthMult = parentMaxHealth / Mathf.Max(1f, cfg.maxHealth);
      float rewardMult = parentReward / Mathf.Max(1f, cfg.goldReward);

      child.Initialize(childPath, cfg, healthMult, rewardMult, childOverride);

      // Fan the children out slightly so they do not overlap into one blob.
      Vector3 jitter = new Vector3((i - (count - 1) * 0.5f) * 0.45f, 0f, 0f);
      childObj.transform.position = new Vector3(origin.x, heightOffset, origin.z) + jitter;
      child.PlaySplitBirth();
    }
  }

  public bool AreWavesComplete()
  {
    return waveConfig != null && IsLastWave && !isSpawning && !awaitingClear;
  }
}