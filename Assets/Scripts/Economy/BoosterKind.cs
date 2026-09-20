using UnityEngine;

public enum BoosterKind
{
  SporeBomb = 0,
  FrostWave = 1,
  Overclock = 2,
  Mend = 3,
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
// Prices are sized against what the same coins buy in towers (100-275 each) and
// against the continue ladder (200/400/800). The bomb is the most expensive by
// a distance because it is the one that can rescue a lost wave outright.
public static class BoosterCatalog
{
  public static readonly BoosterKind[] All =
  {
    BoosterKind.SporeBomb, BoosterKind.FrostWave, BoosterKind.Overclock, BoosterKind.Mend,
  };

  public static string Name(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => "SPORE BOMB",
    BoosterKind.FrostWave => "FROST WAVE",
    BoosterKind.Overclock => "OVERCLOCK",
    BoosterKind.Mend => "MEND",
    _ => kind.ToString(),
  };

  public static string Description(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => "Wipes out every bacterium on the board. Pays no gold.",
    BoosterKind.FrostWave => $"Freezes everything solid for {FreezeSeconds:0} seconds.",
    BoosterKind.Overclock =>
      $"All towers fire {Mathf.RoundToInt((OverclockMultiplier - 1f) * 100f)}% faster " +
      $"for {OverclockSeconds:0} seconds.",
    BoosterKind.Mend => $"Repairs {MendHealth} health on your base.",
    _ => string.Empty,
  };

  public static int Price(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => 600,
    BoosterKind.FrostWave => 250,
    BoosterKind.Overclock => 350,
    BoosterKind.Mend => 300,
    _ => 0,
  };

  public static BoosterLimit Limit(BoosterKind kind) => kind switch
  {
    // The two that can undo a mistake outright are once a level. The two that
    // only buy time are once a wave.
    BoosterKind.SporeBomb => BoosterLimit.OncePerLevel,
    BoosterKind.Mend => BoosterLimit.OncePerLevel,
    _ => BoosterLimit.OncePerWave,
  };

  public static Sprite Icon(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => UiSprites.Bomb(),
    BoosterKind.FrostWave => UiSprites.Snowflake(),
    BoosterKind.Overclock => UiSprites.Bolt(),
    BoosterKind.Mend => UiSprites.Heart(),
    _ => UiSprites.Circle(),
  };

  public static Color Tint(BoosterKind kind) => kind switch
  {
    BoosterKind.SporeBomb => UiSkin.Danger,
    BoosterKind.FrostWave => UiSkin.Accent,
    BoosterKind.Overclock => UiSkin.Gold,
    BoosterKind.Mend => UiSkin.Health,
    _ => UiSkin.Neutral,
  };

  // --- magnitudes, all first guesses like the ad and continue numbers were

  public const float FreezeSeconds = 5f;
  public const float OverclockSeconds = 15f;
  public const float OverclockMultiplier = 1.5f;
  public const int MendHealth = 25;

  // Buying three at once is cheaper than buying three singles. Kept mild: the
  // discount is a convenience, not a reason to hoard.
  public const int BundleSize = 3;
  public static int BundlePrice(BoosterKind kind) =>
    Mathf.RoundToInt(Price(kind) * BundleSize * 0.85f);
}
