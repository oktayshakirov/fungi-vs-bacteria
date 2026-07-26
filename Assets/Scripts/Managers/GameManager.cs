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
  private int levelStartingHealth = 100;

  // Chosen play speed (1x or 2x); pausing sets timeScale to 0 without losing it
  public float PlaySpeed { get; private set; } = 1f;
  public bool IsPaused => Time.timeScale == 0f;

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
    levelStartingHealth = currentHealth;

    PlaySpeed = 1f;
    Time.timeScale = 1f;

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

    int stars = LevelProgress.StarsForHealth(currentHealth, levelStartingHealth);

    LevelConfig level = GameSession.SelectedLevel;
    if (level != null)
    {
      LevelProgress.MarkLevelCompleted(level.environmentName, level.levelNumber);
      LevelProgress.SetStars(level.environmentName, level.levelNumber, stars);
    }

    Debug.Log($"Victory! All waves cleared. Stars: {stars}");
    AudioManager.Instance.PlaySound(AudioManager.SoundType.Victory);
    CameraRig.Instance?.PlayEndOfLevelView();
    HUDManager.Instance.ShowVictoryScreen(stars);
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

    // Feedback: the base was hit
    CameraRig.Instance?.Shake(0.35f);
    AudioManager.Instance?.Vibrate();

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
    Time.timeScale = PlaySpeed;
  }

  // Cycles the play speed between 1x and 2x. Ignored while paused or ended.
  public float ToggleSpeed()
  {
    PlaySpeed = Mathf.Approximately(PlaySpeed, 1f) ? 2f : 1f;
    if (!gameEnded && Time.timeScale != 0f)
    {
      Time.timeScale = PlaySpeed;
    }
    return PlaySpeed;
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