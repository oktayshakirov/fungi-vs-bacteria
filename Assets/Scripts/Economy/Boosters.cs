using UnityEngine;

// The coin sinks. Two of them, both bought at the moment they are wanted rather
// than stockpiled in a shop: a currency with a stockpile needs an inventory UI,
// a "which do I equip" decision, and balancing across 70 levels that have not
// been balanced yet.
//
// StartBoost is armed on the level screen and consumed by GameManager on level
// start. Continue is bought on the game over screen and applied immediately.
public static class Boosters
{
  public const int StartBoostCost = 100;
  public const int StartBoostGold = 300;

  public const int ContinueCost = 200;
  public const int ContinueHealth = 50;

  // Armed, not owned: paid for when the level launches, so backing out of the
  // level screen never charges the player. Cleared on consumption.
  public static bool StartBoostArmed { get; private set; }

  // One continue per run. Without a cap a player with coins can never lose, and
  // the difficulty curve stops meaning anything.
  public static bool ContinueUsedThisRun { get; private set; }

  public static bool ArmStartBoost()
  {
    if (StartBoostArmed) return true;
    if (!Wallet.TrySpend(StartBoostCost)) return false;
    StartBoostArmed = true;
    return true;
  }

  // Refunds rather than silently pockets the coins: the player armed it and
  // then changed their mind before playing.
  public static void DisarmStartBoost()
  {
    if (!StartBoostArmed) return;
    StartBoostArmed = false;
    Wallet.Add(StartBoostCost);
  }

  // Called by GameManager at level start. Returns the extra gold to grant.
  public static int ConsumeStartBoost()
  {
    if (!StartBoostArmed) return 0;
    StartBoostArmed = false;
    return StartBoostGold;
  }

  public static void BeginRun()
  {
    ContinueUsedThisRun = false;
  }

  public static void MarkContinueUsed()
  {
    ContinueUsedThisRun = true;
  }
}
