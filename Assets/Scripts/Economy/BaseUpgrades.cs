using UnityEngine;

// What the base's own panel sells, mid-level: a heal back to full, and two
// reinforcements that raise the health ceiling - like a tower's upgrades.
//
// First-guess numbers, to be tuned on a device. Anchors: the Mend booster is
// 25 health for 500 coins (20 a point, but bought ahead and limited to once a
// level), and towers cost 100-275. A heal from 50 back to 100 is 400 coins -
// about two towers - so it is a real choice rather than the obvious move.
public static class BaseUpgrades
{
  public const int CoinsPerHealth = 8;
  public const int ReinforceSteps = 2;

  // Half the starting health per step: 100 -> 150 -> 200.
  public static int ReinforceAmount(int startingHealth) =>
    Mathf.Max(1, Mathf.RoundToInt(startingHealth * 0.5f));

  public static int ReinforceCost(int level) => level <= 0 ? 400 : 700;
}
