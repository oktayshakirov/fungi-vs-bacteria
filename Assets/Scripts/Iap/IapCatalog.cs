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
//   * a rewarded ad pays 150 (8 a day), so the smallest pack is two days of ads
//   * Remove Ads costs 18,000, which the 20,000 pack covers and nothing
//     smaller does
//   * the Survival Kit (2,350) sits just under the 2,500 pack
// The bigger packs give progressively more coins per euro; that ladder is what
// makes the middle packs read as reasonable.
//
// There is deliberately no real-money "remove ads" product: a direct purchase
// was dropped in favour of NoAds.CoinPrice only, so the game does not sell the
// same thing twice at two different effective prices.
public static class IapCatalog
{
  // Consumables. Buying one adds coins to the Wallet and is then gone.
  public const string Coins2500 = "fungivsbacteria.coins.2500";
  public const string Coins10000 = "fungivsbacteria.coins.10000";
  public const string Coins20000 = "fungivsbacteria.coins.20000";
  public const string Coins50000 = "fungivsbacteria.coins.50000";

  // The 10,000 pack pays 11,000. At its store price (EUR 3.99) 10,000 coins was
  // a WORSE rate than the 2,500 pack at 0.99, so four small packs beat it; the
  // 1,000 bonus puts it between the base pack and the 20,000 one (+9%). The ID
  // keeps its name - the store listing promises 10,000 and this pays more.
  private static readonly Dictionary<string, int> CoinsByProduct = new Dictionary<string, int>
  {
    { Coins2500, 2500 },
    { Coins10000, 11000 },
    { Coins20000, 20000 },
    { Coins50000, 50000 },
  };

  // Display order in the store, smallest first.
  public static readonly string[] CoinProducts =
  {
    Coins2500, Coins10000, Coins20000, Coins50000,
  };

  // Everything the SDK should fetch prices for.
  public static readonly string[] AllProducts = CoinProducts;

  // The coins-per-currency-unit ladder, as a percentage above the smallest
  // pack's rate. Shown as a "+13%" badge on the card; computed here from the
  // amounts rather than typed in, so the badge cannot disagree with the pack.
  //
  // Prices come from the store, not from this file, so the badge is only right
  // while the store prices hold (EUR 0.99 / 3.99 / 6.99 / 14.99 -> badges none /
  // +9% / +13% / +32%). A marketing cue, not an exact rate; hidden on the base.
  private static readonly Dictionary<string, float> IntendedPrice = new Dictionary<string, float>
  {
    { Coins2500, 0.99f },
    { Coins10000, 3.99f },
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
