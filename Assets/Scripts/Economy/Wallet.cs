using System;
using UnityEngine;

// The game's single currency. Towers, boosts, continues and ad rewards all
// draw on this one balance, so the number the player sees never changes
// meaning between the menu and a level.
//
// Merging the old per-level gold into it removed a whole class of confusion but
// introduced a risk: spending on towers now drains a persistent balance, so a
// player who loses badly could arrive at the next level unable to afford
// anything and never recover. EnsureMinimum is the floor that makes that
// impossible - see its comment.
//
// PlayerPrefs is the store, matching LevelProgress. This is client-side and
// trivially editable by a determined player — acceptable for a single-player
// game with no server, and the same trade-off LevelProgress already makes.
public static class Wallet
{
  private const string CoinsKey = "Wallet_Coins";
  private const string LoanKey = "Wallet_LevelLoan";

  // Coins granted the first time a level is cleared, indexed by star rating.
  // Playing has to pay something, or the only way to earn is to watch ads and
  // the currency reads as a paywall rather than a reward. Lifetime total across
  // all 70 levels is 3,500 - a bonus, not a route to Remove Ads.
  private static readonly int[] StarPayout = { 0, 15, 30, 50 };

  public static event Action<int> OnCoinsChanged;

  public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

  // The part of Coins that EnsureMinimum lent for the current level. It is
  // tower money only: repaid when the level ends however it ends, and excluded
  // from anything bought in the store. Persisted, so killing the app mid-level
  // cannot keep it.
  public static int Loan => PlayerPrefs.GetInt(LoanKey, 0);

  // What the store may spend: boosters and Remove Ads must never be bought with
  // a level's loan, or start-a-level-then-quit would print coins.
  public static int OwnCoins => Mathf.Max(0, Coins - Loan);

  public static bool CanAffordOwn(int amount) => OwnCoins >= amount;

  public static bool TrySpendOwn(int amount)
  {
    if (amount <= 0 || OwnCoins < amount) return false;
    Set(Coins - amount);
    return true;
  }

  // Takes back whatever is left of the level's loan, up to the whole loan.
  // Kill gold therefore pays the loan off first, and only what a level earns
  // beyond it is kept.
  public static void RepayLoan()
  {
    int loan = Loan;
    if (loan <= 0) return;

    PlayerPrefs.SetInt(LoanKey, 0);
    Set(Coins - Mathf.Min(loan, Coins));
  }

  // A process that died mid-level never reached the level's own repayment.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void RepayOnLaunch() => RepayLoan();

  public static void Add(int amount)
  {
    if (amount <= 0) return;
    Set(Coins + amount);
  }

  public static bool TrySpend(int amount)
  {
    if (amount <= 0 || Coins < amount) return false;
    Set(Coins - amount);
    return true;
  }

  public static bool CanAfford(int amount) => Coins >= amount;

  // Guarantees a level always opens with at least the budget it was designed
  // around, topping up only when the player is short. Without it the merged
  // currency death-spirals: lose a level with an empty wallet and there is no
  // way to buy the towers needed to win the next one.
  //
  // The top-up is a LOAN (see Loan), not a gift. As a gift it was unlimited
  // free coins: spend to zero, start a late level, quit, repeat.
  public static int EnsureMinimum(int floor)
  {
    int shortfall = floor - Coins;
    if (shortfall <= 0) return 0;

    PlayerPrefs.SetInt(LoanKey, Loan + shortfall);
    Set(floor);
    return shortfall;
  }

  // Pays out for a level result, and only for the part that is new: clearing a
  // level you already 3-starred pays nothing, but improving 1 star to 3 pays
  // the difference. Without this, replaying the easiest level is the optimal
  // way to farm coins.
  public static int AwardForLevel(string environment, int levelNumber, int stars, int previousStars)
  {
    stars = Mathf.Clamp(stars, 0, 3);
    previousStars = Mathf.Clamp(previousStars, 0, 3);
    if (stars <= previousStars) return 0;

    int payout = StarPayout[stars] - StarPayout[previousStars];
    Add(payout);
    return payout;
  }

  private static void Set(int amount)
  {
    PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, amount));
    PlayerPrefs.Save();
    OnCoinsChanged?.Invoke(Coins);
  }
}
