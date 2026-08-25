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

  private VictoryScreen victoryScreen;

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

  // The balance is shared with the menu now, and towers/enemies/waves all move
  // it through Wallet rather than through this class, so the readout follows
  // the wallet directly instead of relying on every mutator to call UpdateStats.
  private void OnEnable()
  {
    Wallet.OnCoinsChanged += OnCoinsChanged;
  }

  private void OnDisable()
  {
    Wallet.OnCoinsChanged -= OnCoinsChanged;
  }

  private void OnCoinsChanged(int coins)
  {
    if (goldText != null) goldText.text = coins.ToString();
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

    // Built first, and before anything that can throw: these used to be created
    // at the end of Start(), so a single missing screen prefab silently took the
    // speed and view buttons down with it.
    Transform uiRoot = HudUiRoot();
    // TowersPanel is a direct child of the SafeArea HudUiRoot resolves to, and
    // has no serialized reference here; its cards are built at runtime.
    // Captured BEFORE theming: HudTheme reparents goldText into a chip, so
    // reading its parent afterwards returns the 62px chip instead of the stats
    // panel — which is what put the speed button back on top of the chips.
    RectTransform statsRect = goldText != null ? (RectTransform)goldText.transform.parent : null;

    HudTheme.Apply(
      statsRect, goldText, healthText, waveText, timerText,
      startWaveButton, pauseGameButton,
      uiRoot.Find("TowersPanel") as RectTransform);

    // Stacked under the stats panel, so they can never overlap it
    GameSpeedButton.Create(uiRoot, statsRect, 0);
    CameraViewButton.Create(uiRoot, statsRect, 1);


    // Initialize pause screen
    if (pauseGameScreenPrefab != null)
    {
      pauseGameScreen = Instantiate(pauseGameScreenPrefab, transform);
      pauseGameScreen.Initialize(OnPauseScreenClosed);
      pauseGameScreen.gameObject.SetActive(false);
    }
    else Debug.LogError("HUDManager: pauseGameScreenPrefab is not assigned.");

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

    if (TutorialOverlay.ShouldShow())
    {
      TutorialOverlay.Show(uiRoot);
    }
  }

  // Runtime-built UI must live under a Canvas to render. HUDManager itself is on
  // a plain (non-UI) transform, so find the HUD canvas (and its SafeArea).
  private Transform hudUiRoot;
  private Transform HudUiRoot()
  {
    if (hudUiRoot != null) return hudUiRoot;

    // Anchor to a HUD element we already know renders. Picking a canvas by
    // sorting order is a guess, and guessing wrong parents the buttons to
    // something invisible.
    Canvas best = null;
    if (goldText != null) best = goldText.canvas;
    if (best == null && waveText != null) best = waveText.canvas;
    if (best == null && startWaveButton != null) best = startWaveButton.GetComponentInParent<Canvas>();

    if (best == null)
    {
      foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
      {
        if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
        if (best == null || c.sortingOrder < best.sortingOrder) best = c;
      }
    }
    if (best == null) { hudUiRoot = transform; return hudUiRoot; }

    // Nested canvases render into their root, so always resolve up to it
    Canvas root = best.rootCanvas != null ? best.rootCanvas : best;

    // Prefer the SafeArea so buttons align with the other HUD content
    SafeArea safe = root.GetComponentInChildren<SafeArea>(true);
    hudUiRoot = safe != null ? safe.transform : root.transform;
    return hudUiRoot;
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
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.StartWave);
  }

  public void UpdateStats(int health, int gold)
  {
    // The chips carry a heart and a coin icon, so the values need no label
    if (healthText != null)
    {
      healthText.text = health.ToString();

      if (health <= 25)
        healthText.color = UiSkin.Danger;
      else if (health <= 50)
        healthText.color = UiSkin.Gold;
      else
        healthText.color = UiSkin.TextPrimary;
    }
    if (goldText != null)
    {
      goldText.text = gold.ToString();
    }
  }

  public void UpdateWaveText(int currentWave, int totalWaves)
  {
    if (waveText != null)
    {
      waveText.text = $"Wave {currentWave}/{totalWaves}";
    }
  }

  public void ShowWaveBanner(int currentWave, int totalWaves)
  {
    string message = currentWave >= totalWaves ? "FINAL WAVE" : $"WAVE {currentWave}";
    WaveBanner.Show(HudUiRoot(), message);
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
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
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

  public void ShowVictoryScreen(int stars, int coinsEarned = 0)
  {
    if (victoryScreen == null)
    {
      VictoryScreen prefab = Resources.Load<VictoryScreen>("Screens/VictoryScreen");
      if (prefab == null)
      {
        Debug.LogError("VictoryScreen prefab not found at Resources/Screens/VictoryScreen!");
        return;
      }
      victoryScreen = Instantiate(prefab, transform);
    }

    victoryScreen.Initialize(stars, coinsEarned);
    victoryScreen.gameObject.SetActive(true);
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