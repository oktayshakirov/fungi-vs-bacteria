using UnityEngine;

public class GameManager : MonoBehaviour
{
  public static GameManager Instance { get; private set; }

  [SerializeField] private int startingGold = 500;
  [SerializeField] private int startingHealth = 100;

  public int currentGold;
  public int currentHealth;

  private EnemySpawner spawner;
  private int aliveEnemies;
  private bool gameEnded;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      Application.targetFrameRate = 60;
    }
    else
    {
      Destroy(gameObject);
    }
  }

  private void Start()
  {
    LevelConfig level = GameSession.SelectedLevel;
    currentGold = level != null ? level.startingGold : startingGold;
    currentHealth = level != null ? level.startingHealth : startingHealth;

    spawner = FindFirstObjectByType<EnemySpawner>();

    // Initial UI update
    UpdateUI();
  }

  public void OnEnemySpawned()
  {
    aliveEnemies++;
  }

  public void OnEnemyRemoved()
  {
    aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
    CheckVictory();
  }

  public void CheckVictory()
  {
    if (gameEnded || currentHealth <= 0) return;
    if (spawner == null || !spawner.AreWavesComplete() || aliveEnemies > 0) return;

    Victory();
  }

  private void Victory()
  {
    gameEnded = true;

    LevelConfig level = GameSession.SelectedLevel;
    if (level != null)
    {
      LevelProgress.MarkLevelCompleted(level.environmentName, level.levelNumber);
    }

    Debug.Log("Victory! All waves cleared.");
    AudioManager.Instance.PlaySound(AudioManager.SoundType.Victory);
    CameraRig.Instance?.PlayEndOfLevelView();
    HUDManager.Instance.ShowVictoryScreen();
    PauseGame();
  }

  private void UpdateUI()
  {
    HUDManager.Instance.UpdateStats(currentHealth, currentGold);
  }

  public bool CanAfford(int cost)
  {
    return currentGold >= cost;
  }

  public bool TryPurchase(int cost)
  {
    if (CanAfford(cost))
    {
      currentGold -= cost;
      UpdateUI();
      return true;
    }
    return false;
  }

  public void AddGold(int amount)
  {
    currentGold += amount;
    UpdateUI();
  }

  public void TakeDamage(int damage)
  {
    currentHealth = Mathf.Max(0, currentHealth - damage);
    UpdateUI();

    if (currentHealth <= 0)
    {
      GameOver();
    }
  }

  private void GameOver()
  {
    if (gameEnded) return;
    gameEnded = true;

    Debug.Log("Game Over!");
    AudioManager.Instance.PlaySound(AudioManager.SoundType.GameOver);
    CameraRig.Instance?.PlayEndOfLevelView();
    HUDManager.Instance.ShowGameOverScreen();
    PauseGame();
  }

  public void PauseGame()
  {
    Time.timeScale = 0f;
  }

  public void ResumeGame()
  {
    Time.timeScale = 1f;
  }

  public void ReturnToMainMenu()
  {
    Time.timeScale = 1f;
    SceneController.Instance.LoadScene(SceneController.GameScene.MainMenu);
  }

  public void RestartGame()
  {
    Time.timeScale = 1f;
    SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
  }
}