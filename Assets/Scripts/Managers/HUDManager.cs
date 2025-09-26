using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class HUDManager : MonoBehaviour
{
  public static HUDManager Instance { get; private set; }

  [Header("Wave Information")]
  [SerializeField] private TextMeshProUGUI waveText;
  [SerializeField] private TextMeshProUGUI timerText;

  [Header("Start Wave Button")]
  [SerializeField] private Button startWaveButton;
  [SerializeField] private TextMeshProUGUI startWaveButtonText;

  [Header("Player Stats")]
  [SerializeField] private TextMeshProUGUI healthText;
  [SerializeField] private TextMeshProUGUI goldText;

  [Header("Pause Game Button")]
  [SerializeField] private Button pauseGameButton;
  [SerializeField] private TextMeshProUGUI pauseGameButtonText;

  [Header("Pause Game Screen")]
  [SerializeField] private PauseGameScreen pauseGameScreenPrefab;
  private PauseGameScreen pauseGameScreen;

  [Header("Game Over Screen")]
  [SerializeField] private GameOverScreen gameOverPrefab;
  private GameOverScreen gameOverScreen;

  [Header("Tower Actions")]
  [SerializeField] private GameObject towerActionsPanel;
  private TowerDefense.UI.TowerActions towerActions;
  private Tower selectedTower;
  private Camera mainCamera;

  [Header("Tower Selection")]
  [SerializeField] private LayerMask selectableLayerMask;
  [SerializeField] private LayerMask deselectLayerMask;

  private EnemySpawner spawner;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      mainCamera = Camera.main;
      Debug.Log("HUDManager initialized");
    }
    else
    {
      Destroy(gameObject);
    }
  }

  private void Start()
  {
    spawner = FindFirstObjectByType<EnemySpawner>();
    if (spawner == null)
    {
      Debug.LogError("No EnemySpawner found!");
    }

    // Initial UI update
    if (GameManager.Instance != null)
    {
      UpdateStats(GameManager.Instance.currentHealth, GameManager.Instance.currentGold);
    }

    // Initialize pause screen
    pauseGameScreen = Instantiate(pauseGameScreenPrefab, transform);
    pauseGameScreen.Initialize(OnPauseScreenClosed);
    pauseGameScreen.gameObject.SetActive(false);

    if (towerActionsPanel != null)
    {
      towerActions = towerActionsPanel.GetComponent<TowerDefense.UI.TowerActions>();
      towerActionsPanel.SetActive(false);
    }

    if (gameOverPrefab != null)
    {
      gameOverScreen = Instantiate(gameOverPrefab, transform);
      gameOverScreen.Initialize();
      gameOverScreen.gameObject.SetActive(false);
    }
  }

  private void Update()
  {
    HandleTowerSelection();
  }

  private void HandleTowerSelection()
  {
    if (!Input.GetMouseButtonDown(0)) return;

    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

    // Try to select a tower first
    if (TrySelectTower(ray)) return;

    // If we didn't hit a tower, check if we should deselect
    TryDeselect(ray);
  }

  private bool TrySelectTower(Ray ray)
  {
    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayerMask))
    {
      if (hit.collider.TryGetComponent<Tower>(out Tower tower))
      {
        SelectTower(tower);
        return true;
      }
    }
    return false;
  }

  private void TryDeselect(Ray ray)
  {
    // Only deselect if we hit deselectable layers (ground/path)
    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, deselectLayerMask))
    {
      Debug.Log($"Deselecting tower {deselectLayerMask}");
      DeselectCurrentTower();
    }
  }

  private void SelectTower(Tower tower)
  {
    if (selectedTower == tower) return;

    DeselectCurrentTower();
    selectedTower = tower;
    tower.Select();
  }

  public void DeselectCurrentTower()
  {
    if (selectedTower != null)
    {
      selectedTower.Deselect();
      selectedTower = null;
    }
  }

  public void StartWave()
  {
    Debug.Log("Start Wave Button clicked");
    spawner.StartGame();
    Debug.Log("Starting game");
    AudioManager.Instance.PlaySound(AudioManager.SoundType.StartWave);
  }

  public void UpdateStats(int health, int gold)
  {
    Debug.Log($"Updating stats - Health: {health}, Gold: {gold}");
    if (healthText != null)
    {
      healthText.text = $"Health: {health}";

      if (health <= 25)
        healthText.color = Color.red;
      else if (health <= 50)
        healthText.color = Color.yellow;
      else
        healthText.color = Color.white;
    }
    if (goldText != null)
    {
      goldText.text = $"Gold: {gold}";
    }
  }

  public void UpdateWaveText(int currentWave, int totalWaves)
  {
    if (waveText != null)
    {
      waveText.text = $"Wave {currentWave}/{totalWaves}";
    }
  }

  public void UpdateWaveTimer(float timeRemaining)
  {
    if (timerText != null)
    {
      timerText.text = timeRemaining > 0 ? $"Next Wave in: {timeRemaining:0}" : "";
    }
  }

  public void UpdateStartWaveButton()
  {
    if (startWaveButton != null)
    {
      startWaveButton.interactable = false;
      startWaveButtonText.text = "WAVE STARTED";
    }
  }

  public void ShowPauseScreen()
  {
    pauseGameButtonText.text = "PAUSED";
    pauseGameScreen.Show();
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
  }

  private void OnPauseScreenClosed()
  {
    pauseGameButton.interactable = true;
    pauseGameButtonText.text = "PAUSE";
  }

  public void ShowTowerActions(Tower tower)
  {
    if (towerActions != null && towerActionsPanel != null)
    {
      towerActionsPanel.SetActive(true);
      towerActions.ShowForTower(tower);
    }
  }

  public void HideTowerActions()
  {
    if (towerActionsPanel != null)
    {
      towerActionsPanel.SetActive(false);
      towerActions?.Hide();
    }
  }

  public void ShowGameOverScreen()
  {
    if (gameOverScreen == null)
    {
      gameOverScreen = Instantiate(gameOverPrefab, transform);
      gameOverScreen.Initialize();
    }

    gameOverScreen.gameObject.SetActive(true);
    towerActionsPanel?.SetActive(false);
    DeselectCurrentTower();
  }

  public void HideGameOverScreen()
  {
    if (gameOverScreen != null)
    {
      gameOverScreen.gameObject.SetActive(false);
    }
  }
}