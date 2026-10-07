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
  private TowerPlacement placement;
  private WavePreview wavePreview;
  private int displayedWaveSeconds = -1;
  public bool HasTowerSelection => selectedTower != null || (towerActionsPanel != null && towerActionsPanel.activeInHierarchy);

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
    placement = FindFirstObjectByType<TowerPlacement>();
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
    // No store inside a level: it lives on the main menu only. Boosters can
    // still be bought one at a time from their own panel in the bar.
    // The booster bar positions itself independently from the bottom of the
    // screen (see BoosterBar.Build) - it does not consume a slot in this
    // stack, and `reference`/`slot` are only passed for symmetry.
    BoosterBar.Create(uiRoot, statsRect, 3);


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

    if (timerText != null)
    {
      wavePreview = WavePreview.Create(uiRoot, timerText, this);
      if (spawner != null) RefreshWavePlanning(spawner);
    }

    if (TutorialOverlay.ShouldShow())
    {
      TutorialOverlay.Show(uiRoot, uiRoot.Find("TowersPanel") as RectTransform, startWaveButton!=null ? (RectTransform)startWaveButton.transform : null);
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
    if (!Input.GetMouseButtonDown(0) || mainCamera == null) return;
    // A direct UI raycast, not IsPointerOverGameObject - see UiHit for why the
    // latter let taps on the tower panel's buttons fall through to the board.
    Vector2 pointer = UiHit.PointerPosition;
    if (UiHit.Over(pointer)) return;

    Ray ray = mainCamera.ScreenPointToRay(pointer);

    // Try to select a tower first
    if (TrySelectTower(ray)) return;

    // If we didn't hit a tower, check if we should deselect
    TryDeselect(ray);
  }

  private bool TrySelectTower(Ray ray)
  {
    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayerMask))
    {
      // GetComponentInParent, not TryGetComponent: a tower's collider is not
      // always on the same object as its Tower component - several are on a
      // nested model prefab - and requiring both on one object would silently
      // make those towers unselectable, which is the same class of bug as the
      // layer one Tower.MakeSelectable fixes.
      Tower tower = hit.collider.GetComponentInParent<Tower>();
      if (tower != null)
      {
        SelectTower(tower);
        return true;
      }

      // The base shares the Tower layer for exactly this raycast.
      if (hit.collider.GetComponentInParent<BaseHouse>() != null)
      {
        ShowBasePanel();
        return true;
      }
    }
    return false;
  }

  // The base's heal/reinforce panel. It shares the bottom-left slot with the
  // tower, placement and booster panels, so opening it closes those.
  private void ShowBasePanel()
  {
    if (GameManager.Instance == null || GameManager.Instance.HasEnded) return;
    DeselectCurrentTower();
    placement?.CancelPlacement();
    BoosterPanel.Hide();
    BasePanel.Show(HudUiRoot());
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
  }

  private void TryDeselect(Ray ray)
  {
    // Only deselect if we hit deselectable layers (ground/path)
    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, deselectLayerMask))
    {
      DeselectCurrentTower();
      BasePanel.Hide();
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
    if (spawner == null || !spawner.CanStartWave) return;
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

  // Last values shown, for the pause and game-over screens' context line.
  public int CurrentWave { get; private set; }
  public int TotalWaves { get; private set; }

  public void UpdateWaveText(int currentWave, int totalWaves)
  {
    CurrentWave = currentWave;
    TotalWaves = totalWaves;
    if (waveText != null)
    {
      waveText.text = $"Wave {currentWave}/{totalWaves}";
    }
  }

  public void ShowWaveBanner(int currentWave, int totalWaves)
  {
    string message = currentWave >= totalWaves ? "FINAL WAVE" : $"WAVE {currentWave}";
    var waves=GameSession.SelectedLevel?.waveConfig?.waves;
    string detail=waves!=null && currentWave>0 && currentWave<=waves.Length ? WaveBanner.Describe(waves[currentWave-1]) : "";
    WaveBanner.Show(HudUiRoot(), message,detail);
  }

  public void ShowPathClear() => WaveBanner.Show(HudUiRoot(),"PATH CLEAR","A moment to strengthen your defenses");

  public void RefreshWavePlanning(EnemySpawner source)
  {
    spawner = source;
    bool ready = source.CanStartWave;
    if (startWaveButton != null)
    {
      startWaveButton.interactable = ready;
      // One short label whenever a wave can go, first or early: "SEND NEXT
      // WAVE" was the long one, and the rail-width plate shrank it to read it.
      startWaveButtonText.text = ready ? "START WAVE"
        : source.IsWaveInProgress ? "DEFEAT THIS WAVE" : "ALL WAVES SENT";
    }
    wavePreview?.Bind(source.NextWave, source.WavesStarted + 1, ready);
    displayedWaveSeconds = -1;
    UpdateWaveTimer(source.PreparationRemaining);
  }

  public void UpdateWaveTimer(float timeRemaining)
  {
    if (wavePreview == null) return;
    int seconds = Mathf.CeilToInt(timeRemaining);
    if (seconds == displayedWaveSeconds) return;
    displayedWaveSeconds = seconds;
    string status = timeRemaining > 0 ? $"NEXT IN {seconds}s  /  SCOUT"
      : spawner != null && spawner.NextWave != null ? "SCOUT NEXT WAVE" : "FINAL WAVE";
    wavePreview.SetStatus(status);
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
    // The other half of the rule in TowerPlacement.StartPlacement: selecting a
    // placed tower cancels an armed one and an armed booster, so the three
    // panels that share the bottom-left slot can never be up together.
    placement?.CancelPlacement();
    BoosterPanel.Hide();
    BasePanel.Hide();

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

  // The win's few seconds on the board (GameManager.Victory): every panel
  // closed and the HUD faded out of the shot, so the camera move and the
  // fireworks have the screen. The level always unloads after a win, so
  // nothing here ever has to be undone.
  public void BeginVictoryCelebration()
  {
    DeselectCurrentTower();
    placement?.CancelPlacement();
    BoosterPanel.Hide();
    BasePanel.Hide();
    towerActionsPanel?.SetActive(false);

    Canvas hud = goldText != null ? goldText.canvas.rootCanvas : null;
    if (hud == null) return;
    var group = hud.GetComponent<CanvasGroup>();
    if (group == null) group = hud.gameObject.AddComponent<CanvasGroup>();
    group.interactable = false;
    group.blocksRaycasts = false;
    StartCoroutine(FadeOut(group, 0.4f));
  }

  private static System.Collections.IEnumerator FadeOut(CanvasGroup group, float seconds)
  {
    for (float t = 0f; t < seconds && group != null; t += Time.unscaledDeltaTime)
    {
      group.alpha = 1f - t / seconds;
      yield return null;
    }
    if (group != null) group.alpha = 0f;
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