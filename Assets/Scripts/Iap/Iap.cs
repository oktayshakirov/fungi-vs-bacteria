using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The game's entry point to in-app purchases. Mirrors the shape of the `Ads`
// facade: one static surface the UI talks to, with the SDK behind it, so a
// screen never touches RevenueCat types and the whole integration can be
// swapped or stubbed in one place.
//
// Lives on the same object as the LevelPlay ads component in MainMenu.unity and
// survives scene loads, so the store is reachable from the menu and from the
// game-over screen without re-configuring the SDK.
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(Purchases))]
[RequireComponent(typeof(IapListener))]
public class Iap : MonoBehaviour
{
  public static Iap Instance { get; private set; }

  [Header("RevenueCat public SDK keys")]
  [Tooltip("Written by Tools -> IAP -> Apply Keys. Do not edit by hand.")]
  [SerializeField] private string iosApiKey;
  [SerializeField] private string androidApiKey;

  private Purchases purchases;
  private readonly Dictionary<string, Purchases.StoreProduct> products =
    new Dictionary<string, Purchases.StoreProduct>();

  // True once Configure has been called with a usable key. Everything public
  // below is a no-op until then, so a missing key degrades to "the store shows
  // no prices" rather than to an exception on a button press.
  public static bool IsReady { get; private set; }

  // Raised when the price list arrives or changes. The store redraws on it.
  public static event Action OnProductsChanged;

  // Raised when a purchase attempt finishes. `success` is false for a failure
  // AND for a user cancellation; `message` is null when the player simply
  // backed out, so the UI can stay silent rather than reporting an error the
  // player already knows about.
  public static event Action<bool, string> OnPurchaseFinished;

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);

    purchases = GetComponent<Purchases>();
    // Configured from here rather than from the component's inspector fields,
    // so the key and the product list have one source (IapCatalog / the editor
    // tool) instead of drifting between code and scene.
    purchases.useRuntimeSetup = true;
    purchases.listener = GetComponent<IapListener>();
    purchases.productIdentifiers = IapCatalog.AllProducts;
  }

  private IEnumerator Start()
  {
    // One frame, so every other Awake has run before the SDK starts calling
    // back into the game (the grant path touches Wallet).
    yield return null;

    string key = ApiKey();
    if (string.IsNullOrWhiteSpace(key))
    {
      Debug.LogWarning("[IAP] No RevenueCat key for this platform; purchases are disabled. " +
                       "Set them with Tools -> IAP -> Apply Keys.");
      yield break;
    }

    try
    {
      purchases.Configure(Purchases.PurchasesConfiguration.Builder.Init(key).Build());
      IsReady = true;
    }
    catch (Exception e)
    {
      Debug.LogException(e);
      yield break;
    }

    FetchProducts();

    // Catches up on anything bought while the app was closed, or on another
    // device, and re-applies the no-ads entitlement.
    purchases.GetCustomerInfo((info, error) =>
    {
      if (error != null)
      {
        Debug.LogWarning($"[IAP] GetCustomerInfo failed: {error.Message}");
        return;
      }
      IapGrant.ProcessCustomerInfo(info);
    });
  }

  private string ApiKey()
  {
#if UNITY_IOS
    return iosApiKey;
#elif UNITY_ANDROID
    return androidApiKey;
#else
    return !string.IsNullOrWhiteSpace(iosApiKey) ? iosApiKey : androidApiKey;
#endif
  }

  // "inapp", not the SDK's default "subs": everything this game sells is a
  // one-off. Asking for the wrong type returns an empty list on Android and is
  // a classic reason for a store with no prices in it.
  private void FetchProducts()
  {
    purchases.GetProducts(IapCatalog.AllProducts, (fetched, error) =>
    {
      if (error != null)
      {
        Debug.LogWarning($"[IAP] GetProducts failed: {error.Message}");
        return;
      }

      products.Clear();
      if (fetched != null)
      {
        foreach (Purchases.StoreProduct product in fetched)
        {
          if (product != null && !string.IsNullOrEmpty(product.Identifier))
          {
            products[product.Identifier] = product;
          }
        }
      }

      OnProductsChanged?.Invoke();
    }, "inapp");
  }

  // Prices for a preview or a QA build, used when there is no store to ask.
  // Set only by UiPreview: a device build never touches this, and the real
  // dictionary always wins over it below... except that in the editor there is
  // no real dictionary, which is the whole point - the store screen cannot be
  // render-verified at all otherwise, since the editor has no store account.
  private static Dictionary<string, string> previewPrices;

  public static void SetPreviewPrices(Dictionary<string, string> priceByProduct)
  {
    previewPrices = priceByProduct;
    OnProductsChanged?.Invoke();
  }

  // The localised price ("$2.99", "2,99 €"), or null until the store answers.
  // Never format a price by hand: the currency and its placement are the
  // store's to decide, and getting it wrong is a store-review rejection.
  public static string PriceString(string productIdentifier)
  {
    if (Instance != null &&
        Instance.products.TryGetValue(productIdentifier, out Purchases.StoreProduct product))
    {
      return product.PriceString;
    }

    if (previewPrices != null &&
        previewPrices.TryGetValue(productIdentifier, out string preview))
    {
      return preview;
    }
    return null;
  }

  public static bool HasPrice(string productIdentifier) => PriceString(productIdentifier) != null;

  // Only ever call this straight from a button press - both stores require
  // that a purchase begins with a deliberate user action.
  public static void Purchase(string productIdentifier)
  {
    if (Instance == null || !IsReady)
    {
      OnPurchaseFinished?.Invoke(false, "The store is not available right now.");
      return;
    }

    Instance.purchases.PurchaseProduct(productIdentifier, result =>
    {
      if (result.UserCancelled)
      {
        OnPurchaseFinished?.Invoke(false, null);
        return;
      }

      if (result.Error != null)
      {
        Debug.LogWarning($"[IAP] Purchase of {productIdentifier} failed: {result.Error.Message}");
        OnPurchaseFinished?.Invoke(false, "That purchase did not go through.");
        return;
      }

      // The grant runs off CustomerInfo, not off the result's product id, so a
      // purchase that arrives twice (here and through the listener) still pays
      // out once.
      IapGrant.ProcessCustomerInfo(result.CustomerInfo);
      OnPurchaseFinished?.Invoke(true, null);
    }, "inapp");
  }

  // Apple requires a visible Restore control for non-consumables, which here
  // means Remove Ads. Consumable coins are not restorable and are not expected
  // to be - they were spent.
  public static void Restore(Action<bool> onComplete = null)
  {
    if (Instance == null || !IsReady)
    {
      onComplete?.Invoke(false);
      return;
    }

    Instance.purchases.RestorePurchases((info, error) =>
    {
      if (error != null)
      {
        Debug.LogWarning($"[IAP] Restore failed: {error.Message}");
        onComplete?.Invoke(false);
        return;
      }

      IapGrant.ProcessCustomerInfo(info);
      onComplete?.Invoke(true);
    });
  }
}
