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

  // Coins granted the first time a level is cleared, indexed by star rating.
  // Playing has to pay something, or the only way to earn is to watch ads and
  // the currency reads as a paywall rather than a reward.
  private static readonly int[] StarPayout = { 0, 10, 20, 35 };

  public static event Action<int> OnCoinsChanged;

  public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

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
  // Using the level's own startingGold as the floor keeps all 70 levels' tuning
  // meaningful - it becomes a guaranteed minimum rather than a fixed handout,
  // and anything earned above it genuinely carries over.
  public static int EnsureMinimum(int floor)
  {
    int shortfall = floor - Coins;
    if (shortfall <= 0) return 0;

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
