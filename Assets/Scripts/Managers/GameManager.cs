using UnityEngine;

public class GameManager : MonoBehaviour
{
  public static GameManager Instance { get; private set; }

  [SerializeField] private int startingGold = 500;
  [SerializeField] private int startingHealth = 100;

  // Reads straight through to the shared wallet. Kept as a property with the
  // old name so every existing call site (towers, enemies, waves) is unchanged
  // by the merge.
  public int currentGold => Wallet.Coins;
  public int currentHealth;

  // Fired on every hit to the base, for board-side feedback (BaseFlinch).
  public static event System.Action<int> OnBaseDamaged;
  // A hit the Shield booster swallowed, so the dome can flash (BaseShield).
  public static event System.Action OnShieldAbsorbed;
  // Health bought at the base - a heal or a reinforcement (BasePanel).
  public static event System.Action OnBaseRepaired;

  // Reinforcements bought at the base this level. Each one raises the health
  // ceiling by BaseUpgrades.ReinforceAmount and fills the new room, the way a
  // tower upgrade raises its stats - 100 becomes 150, then 200.
  public int BaseLevel { get; private set; }
  public int ReinforceStep => BaseUpgrades.ReinforceAmount(levelStartingHealth);
  public int MaxHealth => levelStartingHealth + BaseLevel * ReinforceStep;
  public int HealCost => Mathf.Max(0, MaxHealth - currentHealth) * BaseUpgrades.CoinsPerHealth;
  public bool CanReinforce => BaseLevel < BaseUpgrades.ReinforceSteps;
  public int ReinforceCost => BaseUpgrades.ReinforceCost(BaseLevel);

  private EnemySpawner spawner;
  private int aliveEnemies;
  public int AliveEnemies => aliveEnemies;
  public bool HasEnded => gameEnded;
  private bool gameEnded;
  private int levelStartingHealth = 100;
  public BattleReport Report { get; private set; }

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

    // The wallet carries over between levels now; this only tops it up when the
    // player arrives below what this level was balanced for, as a loan that is
    // repaid when the level ends.
    Wallet.RepayLoan();
    Wallet.EnsureMinimum(level != null ? level.startingGold : startingGold);

    currentHealth = level != null ? level.startingHealth : startingHealth;

    levelStartingHealth = currentHealth;
    BaseLevel = 0;
    Report = new BattleReport(currentHealth, level != null ? LevelProgress.GetStars(level.environmentName,level.levelNumber) : 0);
    Boosters.BeginRun();

    PlaySpeed = 1f;
    Time.timeScale = 1f;

    // Static registry: without this it starts the level holding every tower
    // from the previous one, all of them destroyed.
    TowerBuffs.Clear();
    // Same reason: both are static, so without this a level inherits the
    // previous one's per-level booster usage and any running Overclock.
    BoosterEffects.ResetForLevel();

    EnvironmentTheme.Apply(GameSession.SelectedEnvironment);

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
    spawner?.CheckWaveClear();
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

    // Before the star payout, so the payout is never eaten by the repayment.
    Wallet.RepayLoan();

    // Health lost over the whole run, continues included - not health left,
    // which heals and reinforcements can top up (see StarsForDamage).
    int healthLost = Report != null ? Report.HealthLost : Mathf.Max(0, levelStartingHealth - currentHealth);
    int stars = LevelProgress.StarsForDamage(healthLost, levelStartingHealth);

    LevelConfig level = GameSession.SelectedLevel;
    int coinsEarned = 0;
    if (level != null)
    {
      // Read before SetStars overwrites it: the payout is for the improvement,
      // not the total, so replaying a cleared level cannot farm coins.
      int previousStars = LevelProgress.GetStars(level.environmentName, level.levelNumber);

      LevelProgress.MarkLevelCompleted(level.environmentName, level.levelNumber);
      LevelProgress.SetStars(level.environmentName, level.levelNumber, stars);

      coinsEarned = Wallet.AwardForLevel(level.environmentName, level.levelNumber,
        stars, previousStars);
    }

    Debug.Log($"Victory! All waves cleared. Stars: {stars}, coins: {coinsEarned}");
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Victory);

    // A moment on the board before the result screen: the camera swings in
    // round the base, the HUD steps aside, and fireworks go up over the house.
    // Progress and coins are already saved above, so leaving mid-show loses
    // nothing.
    Vector3 focus = BaseFocus(out float scale);
    CameraRig.Instance?.PlayVictoryView(focus);
    VictoryCelebration.Play(focus, scale);
    HUDManager.Instance.BeginVictoryCelebration();
    StartCoroutine(ShowVictoryAfterCelebration(stars, coinsEarned));
  }

  // Long enough for the camera move and the first few bursts; the show keeps
  // going behind the screen after that.
  private const float CelebrationSeconds = 2.8f;

  private System.Collections.IEnumerator ShowVictoryAfterCelebration(int stars, int coinsEarned)
  {
    yield return new WaitForSecondsRealtime(CelebrationSeconds);
    HUDManager.Instance.ShowVictoryScreen(stars, coinsEarned);
    PauseGame();
  }

  // Where the base stands, and a size to scale the celebration by. Falls back
  // to the end of the path if the house was never built.
  private static Vector3 BaseFocus(out float scale)
  {
    scale = 2f;
    BaseHouse house = BaseHouse.Current;
    if (house != null && house.Bounds.size != Vector3.zero)
    {
      scale = Mathf.Clamp(Mathf.Max(house.Bounds.extents.x, house.Bounds.extents.z), 1f, 4f);
      return new Vector3(house.Bounds.center.x, house.Bounds.min.y, house.Bounds.center.z);
    }

    Vector3[] path = PathManager.Instance != null ? PathManager.Instance.GetPathPoints() : null;
    return path != null && path.Length > 0 ? path[path.Length - 1] : Vector3.zero;
  }

  private void UpdateUI()
  {
    HUDManager.Instance.UpdateStats(currentHealth, currentGold);
  }

  public bool CanAfford(int cost) => Wallet.CanAfford(cost);

  public bool TryPurchase(int cost)
  {
    if (!Wallet.TrySpend(cost)) return false;
    UpdateUI();
    return true;
  }

  public void AddGold(int amount)
  {
    Wallet.Add(amount);
    UpdateUI();
  }

  // The Mend booster. Capped at the base's current ceiling - the health the
  // level started with, plus any reinforcement bought - so it undoes damage
  // but never overfills. Like every heal it keeps a run alive without
  // changing the stars, which count health lost (LevelProgress.StarsForDamage).
  public void Repair(int amount)
  {
    if (amount <= 0 || gameEnded) return;

    currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
    UpdateUI();
  }

  // Back to the ceiling in one go, paid per point of health restored.
  public bool TryHealBase()
  {
    if (gameEnded || currentHealth <= 0 || currentHealth >= MaxHealth) return false;
    if (!TryPurchase(HealCost)) return false;

    currentHealth = MaxHealth;
    UpdateUI();
    OnBaseRepaired?.Invoke();
    return true;
  }

  // Raises the ceiling and fills the new room, so the base can soak a leak
  // it otherwise could not. Two steps per level; resets with the level.
  public bool TryReinforceBase()
  {
    if (gameEnded || currentHealth <= 0 || !CanReinforce) return false;
    if (!TryPurchase(ReinforceCost)) return false;

    BaseLevel++;
    currentHealth += ReinforceStep;
    UpdateUI();
    OnBaseRepaired?.Invoke();
    return true;
  }

  public void TakeDamage(int damage) => TakeDamage(damage,null,false);

  public void TakeDamage(int damage, EnemyConfig source, bool isChild)
  {
    if (gameEnded || damage <= 0) return;
    int lost = BoosterEffects.ShieldActive ? 0 : Mathf.Min(currentHealth,damage);
    // Record before showing the result: the fatal enemy is still on the board.
    Report?.RecordEscape(source,isChild,lost);
    if (BoosterEffects.ShieldActive)
    {
      OnShieldAbsorbed?.Invoke();
      return;
    }

    currentHealth = Mathf.Max(0, currentHealth - lost);
    UpdateUI();

    // Feedback: the base was hit
    OnBaseDamaged?.Invoke(damage);
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
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.GameOver);
    CameraRig.Instance?.PlayEndOfLevelView();
    HUDManager.Instance.ShowGameOverScreen();
    PauseGame();
  }

  // Resumes a lost run in place, paid for with coins or a rewarded ad. The
  // enemies still on the board are left alone: clearing them would make a
  // continue strictly better than a clean run, and surviving the wave you died
  // to is the whole point of buying one.
  public void ContinueRun(int extraHealth, bool viaAd)
  {
    if (!gameEnded || currentHealth > 0) return;

    gameEnded = false;
    currentHealth = extraHealth;
    Boosters.MarkContinueUsed(viaAd);

    UpdateUI();
    HUDManager.Instance.HideGameOverScreen();
    ResumeGame();

    spawner?.CheckWaveClear();

    // A continue can leave the board already empty — the last enemy may have
    // died in the same frame as the base fell — which would otherwise strand
    // the player in a level that can never end.
    CheckVictory();
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

  // Every way out of a level (quit, restart, next level) unloads this scene.
  private void OnDestroy()
  {
    if (Instance != this) return;
    Instance = null;
    Wallet.RepayLoan();
  }
}