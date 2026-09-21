using System;
using System.Collections.Generic;
using UnityEngine;

// Turns purchases into coins, exactly once each.
//
// Everything arrives here as a CustomerInfo - from the purchase callback, from
// Restore, from the startup fetch, and from RevenueCat pushing an update - so
// the same code path covers all four and none of them can double-pay. The
// dedupe is by STORE TRANSACTION ID, which is stable per purchase, not by
// product id: a player can buy the same coin pack many times.
//
// The ledger is local (PlayerPrefs), which has one consequence worth being
// clear about: coins live on the device, so a reinstall loses both the coins
// and the record of having granted them. The stores do not require consumables
// to be restorable, and a fresh install starts from an anonymous RevenueCat
// user with an empty transaction list, so this cannot double-grant - it simply
// means bought coins do not follow the player to a new device.
public static class IapGrant
{
  private const string GrantedKey = "Iap_GrantedTransactions";
  private const string NoAdsGiftKey = "Iap_NoAdsGiftGranted";

  // Raised when a purchase actually paid out, with the coins added. The store
  // UI uses it to say what happened; nothing else should need it, since the
  // Wallet raises its own change event.
  public static event Action<string, int> OnCoinsGranted;

  public static void ProcessCustomerInfo(Purchases.CustomerInfo customerInfo)
  {
    if (customerInfo == null) return;

    GrantConsumables(customerInfo);
    NoAds.Apply(customerInfo);
    GrantNoAdsGift();
  }

  private static void GrantConsumables(Purchases.CustomerInfo customerInfo)
  {
    List<Purchases.StoreTransaction> transactions = customerInfo.NonSubscriptionTransactions;
    if (transactions == null) return;

    HashSet<string> granted = LoadGranted();
    bool changed = false;

    foreach (Purchases.StoreTransaction transaction in transactions)
    {
      if (transaction == null || string.IsNullOrEmpty(transaction.TransactionIdentifier)) continue;
      if (granted.Contains(transaction.TransactionIdentifier)) continue;

      if (!IapCatalog.TryGetCoins(transaction.ProductIdentifier, out int coins))
      {
        // The player has paid for something this build cannot pay out - an ID
        // typo, or a product added to the RevenueCat offering but not to
        // IapCatalog. Loud, because the alternative is taking money and
        // silently doing nothing. The transaction is deliberately NOT marked
        // as granted, so a build that knows the product will pay it out.
        Debug.LogError($"[IAP] Purchased product '{transaction.ProductIdentifier}' is not in " +
                       $"IapCatalog, so nothing was granted. Add it to the catalog.");
        continue;
      }

      Wallet.Add(coins);
      granted.Add(transaction.TransactionIdentifier);
      changed = true;
      Debug.Log($"[IAP] Granted {coins} coins for {transaction.ProductIdentifier}.");
      OnCoinsGranted?.Invoke(transaction.ProductIdentifier, coins);
    }

    if (changed) SaveGranted(granted);
  }

  // The thank-you coins that come with removing ads. Keyed off its own flag
  // rather than off a transaction, so restoring the purchase on a second device
  // does not hand out the gift again on the first.
  //
  // ENTITLED, not Active: Active is also true for a player who removed the ads
  // with coins, and handing 5,000 coins back to someone who just paid 12,500
  // for the same thing would make the coin route 40% cheaper than its label.
  // The gift is a thank-you for paying money.
  private static void GrantNoAdsGift()
  {
    if (!NoAds.Entitled) return;
    if (PlayerPrefs.GetInt(NoAdsGiftKey, 0) == 1) return;

    PlayerPrefs.SetInt(NoAdsGiftKey, 1);
    PlayerPrefs.Save();

    if (IapCatalog.NoAdsGiftCoins <= 0) return;
    Wallet.Add(IapCatalog.NoAdsGiftCoins);
    Debug.Log($"[IAP] Granted the no-ads gift of {IapCatalog.NoAdsGiftCoins} coins.");
    OnCoinsGranted?.Invoke(IapCatalog.NoAds, IapCatalog.NoAdsGiftCoins);
  }

  private static HashSet<string> LoadGranted()
  {
    var set = new HashSet<string>(StringComparer.Ordinal);
    string raw = PlayerPrefs.GetString(GrantedKey, string.Empty);
    if (string.IsNullOrEmpty(raw)) return set;

    foreach (string part in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
    {
      set.Add(part);
    }
    return set;
  }

  private static void SaveGranted(HashSet<string> granted)
  {
    PlayerPrefs.SetString(GrantedKey, string.Join("|", granted));
    PlayerPrefs.Save();
  }
}
