using UnityEditor;

// Testing shortcuts for the coin economy. Editor-only so the runtime Wallet
// stays free of UnityEditor references.
public static class EconomyTools
{
  [MenuItem("Tools/Economy/Grant 5000 coins")]
  private static void GrantCoins() => Wallet.Add(5000);

  [MenuItem("Tools/Economy/Clear wallet")]
  private static void ClearWallet()
  {
    UnityEngine.PlayerPrefs.DeleteKey("Wallet_Coins");
    UnityEngine.PlayerPrefs.Save();
  }
}
