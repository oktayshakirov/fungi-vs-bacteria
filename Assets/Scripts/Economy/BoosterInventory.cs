using System;
using UnityEngine;

// How many of each booster the player owns, and the only way to buy one.
//
// Persistent, like the Wallet, and for the same reason: a booster bought before
// a hard level has to still be there after the app is killed. Stored one key
// per kind rather than as a blob, so adding a fifth booster cannot corrupt the
// other four's counts.
public static class BoosterInventory
{
  private const string KeyPrefix = "Booster_";

  // One of each, once, on a fresh install. A new player had no boosters, so
  // the booster bar was five greyed-out buttons that never did anything until
  // they went looking in a store they had no reason to open - the mechanic was
  // invisible for as long as it took to notice it. Owning one of each means it
  // is discovered by using it, and running out is what sends them to the store.
  //
  // Its own key, not a count check: granting it whenever the inventory is empty
  // would hand out a fresh kit every time a player spent their last booster.
  private const string StarterKey = "Booster_StarterGranted";
  public const int StarterAmount = 1;

  public static event Action OnChanged;

  // BeforeSceneLoad, so the bar and the store see the grant on the very first
  // frame of a fresh install rather than a frame after they have drawn.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void GrantStarterBoosters()
  {
    if (PlayerPrefs.GetInt(StarterKey, 0) == 1) return;

    // Written BEFORE the grant: a process killed midway through must not come
    // back and grant a second kit.
    PlayerPrefs.SetInt(StarterKey, 1);
    PlayerPrefs.Save();

    foreach (BoosterKind kind in BoosterCatalog.All) Add(kind, StarterAmount);
  }

  public static int Count(BoosterKind kind) =>
    Mathf.Max(0, PlayerPrefs.GetInt(Key(kind), 0));

  public static bool Has(BoosterKind kind) => Count(kind) > 0;

  public static void Add(BoosterKind kind, int amount)
  {
    if (amount <= 0) return;
    SetCount(kind, Count(kind) + amount);
  }

  // Spends coins and adds the boosters, or does nothing. One call so the two
  // halves cannot come apart - a version that spent first and added later would
  // eat the coins if anything in between threw.
  public static bool Buy(BoosterKind kind, int amount = 1)
  {
    if (amount <= 0) return false;

    int price = amount >= BoosterCatalog.BundleSize
      ? BoosterCatalog.BundlePrice(kind)
      : BoosterCatalog.Price(kind) * amount;

    if (!Wallet.TrySpendOwn(price)) return false;

    Add(kind, amount);
    return true;
  }

  public static bool BuyKit()
  {
    if (!Wallet.TrySpendOwn(BoosterCatalog.KitPrice)) return false;

    foreach (BoosterKind kind in BoosterCatalog.All) Add(kind, 1);
    return true;
  }

  // Called when a booster is actually used. Returns false if there were none,
  // which is the last line of defence behind the UI disabling the button.
  public static bool Consume(BoosterKind kind)
  {
    int count = Count(kind);
    if (count <= 0) return false;

    SetCount(kind, count - 1);
    return true;
  }

  private static void SetCount(BoosterKind kind, int value)
  {
    PlayerPrefs.SetInt(Key(kind), Mathf.Max(0, value));
    PlayerPrefs.Save();
    OnChanged?.Invoke();
  }

  // The enum's NAME, not its numeric value: renumbering the enum later would
  // silently shuffle everyone's inventory between boosters.
  private static string Key(BoosterKind kind) => KeyPrefix + kind;
}
