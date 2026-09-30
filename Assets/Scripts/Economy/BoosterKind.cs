using UnityEngine;

public enum BoosterKind
{
  SporeBomb = 0,
  FrostWave = 1,
  Overclock = 2,
  Mend = 3,
  Shield = 4,
}

// How often a booster may be used inside one level.
//
// Limits exist because the coin wallet has no cap: a player can buy 50,000
// coins, and without a per-level limit that is simply "win every level by
// pressing the bomb". The limit is what keeps a booster a moment of relief
// rather than a replacement for playing.
public enum BoosterLimit
{
  OncePerLevel,
  OncePerWave,
}

// Everything that defines a booster: what it costs, what it does, how often,
// and how it looks. One table, so the store, the HUD bar and the effect code
// cannot disagree about any of it.
//
// Prices are sized against the money behind them as much as the towers the same
// coins buy (100-275 each): 2,500 coins is EUR 0.99, so the 2,500 pack is two
// bombs or six frosts - a real help for a few hard levels, not a win button,
// and the per-level limits below stop a big wallet from stacking them. Free
// income is ~1,650 a day (ads + streak), so a bomb is most of a day's watching.
// The bomb is the most expensive by a distance because it is the one that can
// rescue a lost wave outright.
public static class BoosterCatalog
{
  public static readonly BoosterKind[] All =
  {
    BoosterKind.SporeBomb, BoosterKind.FrostWave, BoosterKind.Overclock, BoosterKind.Mend,
    BoosterKind.Shield,
  };

  public static string Name(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => "SPORE BOMB",
    BoosterKind.FrostWave => "FROST WAVE",
    BoosterKind.Overclock => "OVERCLOCK",
    BoosterKind.Mend => "MEND",
    BoosterKind.Shield => "SHIELD",
    _ => kind.ToString(),
  };

  public static string Description(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => "Wipes out every bacterium on the board. Pays no gold.",
    // "Everything" was the wrong word and read as a warning: it sounded like
    // the towers stop too. They never did - only enemies are frozen.
    BoosterKind.FrostWave =>
      $"Freezes every bacterium on the board for {FreezeSeconds:0} seconds. " +
      "Your towers keep firing.",
    BoosterKind.Overclock =>
      $"All towers fire {Mathf.RoundToInt((OverclockMultiplier - 1f) * 100f)}% faster " +
      $"for {OverclockSeconds:0} seconds.",
    BoosterKind.Mend => $"Repairs {MendHealth} health on your base.",
    BoosterKind.Shield => $"Your base takes no damage for {ShieldSeconds:0} seconds.",
    _ => string.Empty,
  };

  public static int Price(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => 1000,
    BoosterKind.FrostWave => 400,
    BoosterKind.Overclock => 500,
    BoosterKind.Mend => 500,
    BoosterKind.Shield => 750,
    _ => 0,
  };

  public static BoosterLimit Limit(BoosterKind kind) => kind switch
  {
    // The two that can undo a mistake outright are once a level. The two that
    // only buy time are once a wave.
    BoosterKind.SporeBomb => BoosterLimit.OncePerLevel,
    BoosterKind.Mend => BoosterLimit.OncePerLevel,
    BoosterKind.Shield => BoosterLimit.OncePerLevel,
    _ => BoosterLimit.OncePerWave,
  };

  public static Sprite Icon(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => UiSprites.Bomb(),
    BoosterKind.FrostWave => UiSprites.Snowflake(),
    BoosterKind.Overclock => UiSprites.Bolt(),
    BoosterKind.Mend => UiSprites.Heart(),
    BoosterKind.Shield => UiSprites.Shield(),
    _ => UiSprites.Circle(),
  };

  public static Color Tint(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => UiSkin.Danger,
    BoosterKind.FrostWave => UiSkin.Accent,
    BoosterKind.Overclock => UiSkin.Gold,
    BoosterKind.Mend => UiSkin.Health,
    BoosterKind.Shield => UiSkin.Primary,
    _ => UiSkin.Neutral,
  };

  // --- magnitudes, all first guesses like the ad and continue numbers were

  public const float FreezeSeconds = 5f;
  public const float OverclockSeconds = 15f;
  public const float OverclockMultiplier = 1.5f;
  public const int MendHealth = 25;
  // Long enough to cover a leak burst at the end of a wave. Protects the star
  // rating as well as the base, which is what makes it worth buying.
  public const float ShieldSeconds = 10f;

  // Buying three at once is cheaper than buying three singles. Kept mild: the
  // discount is a convenience, not a reason to hoard.
  public const int BundleSize = 3;
  public static int BundlePrice(BoosterKind kind) =>
    Mathf.RoundToInt(Price(kind) * BundleSize * 0.85f);

  // One of every booster at 25% off - the store's best deal per coin, and
  // priced just under the 2,500 pack so that pack reads as "buy the kit".
  public const float KitDiscount = 0.25f;
  public static int KitPrice
  {
    get
    {
      int full = 0;
      foreach (BoosterKind kind in All) full += Price(kind);
      return Mathf.RoundToInt(full * (1f - KitDiscount) / 50f) * 50;
    }
  }
}
