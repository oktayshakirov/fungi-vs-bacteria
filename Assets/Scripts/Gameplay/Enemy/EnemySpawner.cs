using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
  [SerializeField] private WaveConfig waveConfig;

  private int currentWave = 0;
  private bool isSpawning = false;
  private bool gameStarted = false;
  private float waveTimer;
  private bool isWaitingForNextWave = false;

  public bool IsLastWave => currentWave >= waveConfig.waves.Length;
  public bool IsWaveInProgress => isSpawning;

  private void Start()
  {
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

      GameObject enemyObj = Instantiate(
        enemyGroup.enemyConfig.prefab,
        spawnPoint,
        Quaternion.identity
      );

      Enemy enemy = enemyObj.GetComponent<Enemy>();
      if (enemy != null)
      {
        // Create path points at correct height for this enemy
        Vector3[] adjustedPathPoints = new Vector3[pathPoints.Length];
        for (int i = 0; i < pathPoints.Length; i++)
        {
          adjustedPathPoints[i] = new Vector3(pathPoints[i].x, heightOffset, pathPoints[i].z);
        }

        enemy.Initialize(
          adjustedPathPoints,
          enemyGroup.enemyConfig
        );
      }
    }
    else
    {
      Debug.LogError("Missing enemy config, prefab or PathManager!");
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