using System;
using UnityEngine;

// Whether the player has removed the ads, bought with coins only - there is no
// real-money route (see IapCatalog).
//
// Cached in PlayerPrefs and read from there at startup. Local, like the coins
// that bought it: a reinstall loses it along with the wallet, and there is
// nothing to restore it from.
//
// It removes INTERSTITIALS only. Rewarded ads stay: the player chooses to watch
// those and they pay coins, so taking them away would remove a faucet from
// someone who has just paid, not a nuisance.
public static class NoAds
{
  private const string CoinsKey = "Iap_NoAdsCoins";

  // Priced so the 20,000 pack (EUR 6.99) is the natural way to buy it, with
  // 2,000 left over for boosters. The 10,000 pack plus the 2,500 pack (13,500)
  // falls short, and two 10,000 packs cost more than one 20,000. Earned free it
  // is ~11 days of every ad (8 x 150) plus every streak claim, with nothing
  // spent on boosters meanwhile - reachable, but not by just playing.
  public const int CoinPrice = 18000;

  public static event Action OnChanged;

  public static bool Active => BoughtWithCoins;

  public static bool BoughtWithCoins
  {
    get => PlayerPrefs.GetInt(CoinsKey, 0) == 1;
    private set => Set(CoinsKey, value);
  }

  // Spends the coins and removes the ads, or does nothing. Refused once ads
  // are already off, so nobody can pay twice for the same thing.
  public static bool BuyWithCoins()
  {
    if (Active) return false;
    if (!Wallet.TrySpendOwn(CoinPrice)) return false;

    BoughtWithCoins = true;
    return true;
  }

  // For the editor and for QA builds: lets the no-ads path be exercised without
  // a store account. Never called from game code.
  public static void SetForTesting(bool boughtWithCoins)
  {
    BoughtWithCoins = boughtWithCoins;
  }

  private static void Set(string key, bool value)
  {
    bool wasActive = Active;
    PlayerPrefs.SetInt(key, value ? 1 : 0);
    PlayerPrefs.Save();
    if (Active != wasActive) OnChanged?.Invoke();
  }
}
