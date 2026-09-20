using System.Collections.Generic;

// What the store sells, and what each purchase is worth in game.
//
// The IDs here must match the products created in App Store Connect and Google
// Play Console, and the products attached to the RevenueCat offering. Nothing
// resolves them by pattern: an ID that is not in this file is a product the
// game cannot pay out (see IapGrant, which treats that as an error rather than
// quietly doing nothing).
//
// Coin amounts are sized against the economy rather than picked round:
//   * a rewarded ad pays 300, so the smallest pack is worth ~8 ads
//   * one early level costs ~700-1,000 gold to play, one late level ~8,000
//   * continues cost 200/400/800
// so Handful is about one early level, Chest about two late ones. The bigger
// packs give progressively more coins per unit of currency; that ladder is what
// makes the middle packs read as reasonable.
public static class IapCatalog
{
  // Consumables. Buying one adds coins to the Wallet and is then gone.
  public const string Coins2500 = "fungivsbacteria.coins.2500";
  public const string Coins8000 = "fungivsbacteria.coins.8000";
  public const string Coins20000 = "fungivsbacteria.coins.20000";
  public const string Coins50000 = "fungivsbacteria.coins.50000";

  // One-time purchases. These carry an ENTITLEMENT on the RevenueCat dashboard
  // rather than being granted from the transaction list, so they survive a
  // reinstall and are what Restore restores.
  public const string NoAds = "fungivsbacteria.noads";

  // The entitlement identifier configured in RevenueCat, attached to NoAds.
  // A string, not a product id: entitlements are what the app should ever ask
  // about, so the product behind one can change without touching the game.
  public const string NoAdsEntitlement = "no_ads";

  // Removing the ads also hands over a gift, so the purchase reads as getting
  // something rather than as paying for an absence. Granted once, keyed off the
  // entitlement turning on for the first time (see IapGrant.GrantNoAdsGift).
  public const int NoAdsGiftCoins = 5000;

  private static readonly Dictionary<string, int> CoinsByProduct = new Dictionary<string, int>
  {
    { Coins2500, 2500 },
    { Coins8000, 8000 },
    { Coins20000, 20000 },
    { Coins50000, 50000 },
  };

  // Display order in the store, smallest first.
  public static readonly string[] CoinProducts =
  {
    Coins2500, Coins8000, Coins20000, Coins50000,
  };

  // Everything the SDK should fetch prices for.
  public static readonly string[] AllProducts =
  {
    Coins2500, Coins8000, Coins20000, Coins50000, NoAds,
  };

  // The coins-per-currency-unit ladder, as a percentage above the smallest
  // pack's rate. Shown as a "+13%" badge on the card; computed here from the
  // amounts rather than typed in, so the badge cannot disagree with the pack.
  //
  // Prices come from the store, not from this file, so the badge is only right
  // while the intended price ladder holds ($0.99 / $2.99 / $6.99 / $14.99). It
  // is a marketing cue, not an exact rate, and it is hidden on the base pack.
  private static readonly Dictionary<string, float> IntendedPrice = new Dictionary<string, float>
  {
    { Coins2500, 0.99f },
    { Coins8000, 2.99f },
    { Coins20000, 6.99f },
    { Coins50000, 14.99f },
  };

  public static bool TryGetCoins(string productIdentifier, out int coins)
  {
    coins = 0;
    return !string.IsNullOrEmpty(productIdentifier)
           && CoinsByProduct.TryGetValue(productIdentifier, out coins);
  }

  public static bool IsCoinProduct(string productIdentifier) =>
    !string.IsNullOrEmpty(productIdentifier) && CoinsByProduct.ContainsKey(productIdentifier);

  // 0 on the base pack (no badge) and on anything unpriced.
  public static int BonusPercent(string productIdentifier)
  {
    if (!CoinsByProduct.TryGetValue(productIdentifier, out int coins)) return 0;
    if (!IntendedPrice.TryGetValue(productIdentifier, out float price) || price <= 0f) return 0;
    if (!CoinsByProduct.TryGetValue(Coins2500, out int baseCoins)) return 0;
    if (!IntendedPrice.TryGetValue(Coins2500, out float basePrice) || basePrice <= 0f) return 0;

    float baseRate = baseCoins / basePrice;
    float rate = coins / price;
    return UnityEngine.Mathf.RoundToInt((rate / baseRate - 1f) * 100f);
  }
}
