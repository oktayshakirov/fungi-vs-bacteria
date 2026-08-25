// The coin sinks.
//
// The start-gold boost that used to live here was removed when the currencies
// merged: paying 100 coins for 300 coins of starting budget is simply free
// money once both are the same balance.
public static class Boosters
{
  public const int FirstContinueCost = 200;
  public const int ContinueHealth = 50;

  // Continues are never blocked outright, they just get more expensive:
  // 200, 400, 800, ... Price is a better cap than a hard limit because a
  // player who is genuinely invested can push on, while nobody can refuse to
  // lose indefinitely.
  public static int ContinuesUsedThisRun { get; private set; }

  // The rewarded ad continue is free, so that one *is* limited - to once per
  // run. Everything after it costs coins.
  public static bool AdContinueUsedThisRun { get; private set; }

  public static bool CanContinueWithAd => !AdContinueUsedThisRun;

  public static int ContinueCost =>
    FirstContinueCost * (1 << Mathf_Min(ContinuesUsedThisRun, 5));

  public static void BeginRun()
  {
    ContinuesUsedThisRun = 0;
    AdContinueUsedThisRun = false;
  }

  public static void MarkContinueUsed(bool viaAd)
  {
    ContinuesUsedThisRun++;
    if (viaAd) AdContinueUsedThisRun = true;
  }

  // Clamped so the doubling cannot overflow into nonsense on a very long run.
  private static int Mathf_Min(int a, int b) => a < b ? a : b;
}
