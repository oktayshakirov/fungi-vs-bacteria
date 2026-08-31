using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
  [SerializeField] private WaveConfig waveConfig;

  // Splitter children are spawned from Enemy when a parent dies, which needs a
  // way back to the spawner. One spawner per MainGame scene.
  public static EnemySpawner Instance { get; private set; }

  private int currentWave = 0;
  private bool isSpawning = false;
  private bool gameStarted = false;
  private float waveTimer;
  private bool isWaitingForNextWave = false;

  public bool IsLastWave => currentWave >= waveConfig.waves.Length;
  public bool IsWaveInProgress => isSpawning;

  private void Awake()
  {
    Instance = this;
  }

  private void OnDestroy()
  {
    if (Instance == this) Instance = null;
  }

  private void Start()
  {
    if (GameSession.SelectedLevel != null && GameSession.SelectedLevel.waveConfig != null)
    {
      waveConfig = GameSession.SelectedLevel.waveConfig;
    }

    if (waveConfig == null)
    {
      Debug.LogError("WaveConfig is not assigned to EnemySpawner!");
      return;
    }
    HUDManager.Instance.UpdateWaveText(0, waveConfig.waves.Length);
  }

  private void Update()
  {
    if (isWaitingForNextWave)
    {
      waveTimer -= Time.deltaTime;
      HUDManager.Instance?.UpdateWaveTimer(waveTimer);
    }
  }

  public void StartGame()
  {
    if (!gameStarted && waveConfig != null)
    {
      gameStarted = true;
      StartNextWave();
    }
  }

  public void StartNextWave()
  {
    if (currentWave < waveConfig.waves.Length && !isSpawning)
    {
      StartCoroutine(SpawnWave(waveConfig.waves[currentWave]));
      currentWave++;

      HUDManager.Instance.UpdateWaveText(currentWave, waveConfig.waves.Length);
      HUDManager.Instance.ShowWaveBanner(currentWave, waveConfig.waves.Length);
      HUDManager.Instance.UpdateStartWaveButton();
    }
  }

  private IEnumerator SpawnWave(WaveConfig.Wave wave)
  {
    isSpawning = true;
    isWaitingForNextWave = false;

    foreach (var enemyGroup in wave.enemyGroups)
    {
      for (int i = 0; i < enemyGroup.count; i++)
      {
        SpawnEnemy(enemyGroup);
        yield return new WaitForSeconds(wave.timeBetweenSpawns);
      }
    }

    isSpawning = false;

    // Award gold for completing the wave
    GameManager.Instance.AddGold(wave.waveGoldReward);

    // Last wave finished spawning: victory may already be decided if all enemies are dead
    if (currentWave >= waveConfig.waves.Length)
    {
      GameManager.Instance.CheckVictory();
    }

    if (currentWave < waveConfig.waves.Length)
    {
      isWaitingForNextWave = true;
      waveTimer = wave.timeToNextWave;
      yield return new WaitForSeconds(wave.timeToNextWave);
      isWaitingForNextWave = false;
      StartNextWave();
    }
  }

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
      float heightOffset = GetEnemyHeight(enemyGroup.enemyConfig.prefab) / 2f;
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

    float heightOffset = GetEnemyHeight(cfg.prefab) / 2f;
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

      child.Initialize(path, cfg, healthMult, rewardMult, childOverride);

      // Fan the children out slightly so they do not overlap into one blob.
      Vector3 jitter = new Vector3((i - (count - 1) * 0.5f) * 0.45f, 0f, 0f);
      childObj.transform.position = new Vector3(origin.x, heightOffset, origin.z) + jitter;
    }
  }

  private float GetEnemyHeight(GameObject prefab)
  {
    // Get the mesh renderer bounds to calculate actual model height
    MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
    if (renderer != null)
    {
      return renderer.bounds.size.y;
    }

    Debug.LogWarning($"No MeshRenderer found on enemy prefab {prefab.name}, using default height");
    return 1f;
  }

  public bool AreWavesComplete()
  {
    return currentWave >= waveConfig.waves.Length && !isSpawning;
  }
}