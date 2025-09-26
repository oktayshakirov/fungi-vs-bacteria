using UnityEngine;

public class GameManager : MonoBehaviour
{
  public static GameManager Instance { get; private set; }

  [SerializeField] private int startingGold = 500;
  [SerializeField] private int startingHealth = 100;

  public int currentGold;
  public int currentHealth;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
    }
    else
    {
      Destroy(gameObject);
    }
  }

  private void Start()
  {
    currentGold = startingGold;
    currentHealth = startingHealth;

    // Initial UI update
    UpdateUI();
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
    Debug.Log("Game Over!");
    AudioManager.Instance.PlaySound(AudioManager.SoundType.GameOver);
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