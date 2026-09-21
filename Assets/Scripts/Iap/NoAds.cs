using System;
using UnityEngine;

// Whether the player has removed the ads, and the two ways they can do it:
// paying money (the `no_ads` entitlement, through RevenueCat) or paying coins.
//
// The two are stored SEPARATELY and Active is either one. That separation is
// the whole point of this file's shape. The entitlement is re-read from every
// CustomerInfo RevenueCat sends, and a player who unlocked with coins has no
// entitlement - so if both wrote one flag, the first refresh after a coin
// unlock would switch the ads straight back on.
//
// Both are cached in PlayerPrefs and read from there at startup, because the
// entitlement only arrives once RevenueCat has answered - a network round trip
// that may never happen on a plane. Caching the LAST KNOWN state means an owner
// is not shown interstitials during that gap, which is the failure that matters;
// the opposite mistake (a non-owner briefly skipping an ad) costs nothing.
//
// It removes INTERSTITIALS only. Rewarded ads stay: the player chooses to watch
// those and they pay coins, so taking them away would remove a faucet from
// someone who has just paid, not a nuisance.
public static class NoAds
{
  private const string EntitledKey = "Iap_NoAds";
  private const string CoinsKey = "Iap_NoAdsCoins";

  // Sized against both routes to the same thing. Earned for free it is about
  // four days of the daily rewarded-ad cap (10 x 300) plus the streak - a real
  // goal, not a formality. Bought, it is most of the 20,000-coin pack ($6.99),
  // so for someone spending money the $3.99 direct purchase stays the better
  // deal, which is the incentive that should hold: coins are the grinder's
  // route, money is the payer's.
  public const int CoinPrice = 12500;

  public static event Action OnChanged;

  public static bool Active => Entitled || BoughtWithCoins;

  // The paid route. Only this one carries the thank-you gift and survives a
  // reinstall through Restore.
  public static bool Entitled
  {
    get => PlayerPrefs.GetInt(EntitledKey, 0) == 1;
    private set => Set(EntitledKey, value);
  }

  // The coin route. Local, like the coins that bought it: a reinstall loses it
  // along with the wallet, and there is nothing to restore it from.
  public static bool BoughtWithCoins
  {
    get => PlayerPrefs.GetInt(CoinsKey, 0) == 1;
    private set => Set(CoinsKey, value);
  }

  // Reads the entitlement out of a CustomerInfo. Only ever called with real
  // customer info from the SDK; an absent or failed fetch leaves the cached
  // value alone rather than clearing it. Never touches the coin unlock.
  public static void Apply(Purchases.CustomerInfo customerInfo)
  {
    Purchases.EntitlementInfos entitlements = customerInfo?.Entitlements;
    if (entitlements?.Active == null) return;

    Entitled = entitlements.Active.ContainsKey(IapCatalog.NoAdsEntitlement);
  }

  // Spends the coins and removes the ads, or does nothing. Refused once ads are
  // already off by either route, so nobody can pay twice for the same thing.
  public static bool BuyWithCoins()
  {
    if (Active) return false;
    if (!Wallet.TrySpend(CoinPrice)) return false;

    BoughtWithCoins = true;
    return true;
  }

  // For the editor and for QA builds: lets the no-ads path be exercised without
  // a store account. Never called from game code.
  public static void SetForTesting(bool entitled, bool boughtWithCoins)
  {
    Entitled = entitled;
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
