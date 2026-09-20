using System;
using UnityEngine;

// Whether the player has bought "Remove Ads".
//
// Cached in PlayerPrefs and read from there at startup, because the entitlement
// only arrives once RevenueCat has answered - which needs a network round trip,
// and may never happen on a plane. Caching the LAST KNOWN state means an owner
// is not shown interstitials during that gap, which is the failure that matters;
// the opposite mistake (a non-owner briefly skipping an ad) costs nothing.
//
// It removes INTERSTITIALS only. Rewarded ads stay: the player chooses to watch
// those and they pay coins, so taking them away would remove a faucet from
// someone who has just paid, not a nuisance.
public static class NoAds
{
  private const string Key = "Iap_NoAds";

  public static event Action OnChanged;

  public static bool Active
  {
    get => PlayerPrefs.GetInt(Key, 0) == 1;
    private set
    {
      if (Active == value) return;
      PlayerPrefs.SetInt(Key, value ? 1 : 0);
      PlayerPrefs.Save();
      OnChanged?.Invoke();
    }
  }

  // Reads the entitlement out of a CustomerInfo. Only ever called with real
  // customer info from the SDK; an absent or failed fetch leaves the cached
  // value alone rather than clearing it.
  public static void Apply(Purchases.CustomerInfo customerInfo)
  {
    Purchases.EntitlementInfos entitlements = customerInfo?.Entitlements;
    if (entitlements?.Active == null) return;

    Active = entitlements.Active.ContainsKey(IapCatalog.NoAdsEntitlement);
  }

  // For the editor and for QA builds: lets the no-ads path be exercised without
  // a store account. Never called from game code.
  public static void SetForTesting(bool active)
  {
    Active = active;
  }
}
