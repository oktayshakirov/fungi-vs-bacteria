using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
using GoogleMobileAds.Ump.Api;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

// LevelPlay 8.x still types its public events with the deprecated
// com.unity3d.mediation aliases, so referencing them is unavoidable here.
#pragma warning disable 0618

// The LevelPlay (ironSource) implementation of Ads.IAdProvider: consent, init,
// and both ad formats in one component, so there is a single place that knows
// whether an ad is on screen.
//
// Drop this on a GameObject in the MainMenu scene. It survives scene loads and
// registers itself with Ads, which is what the rest of the game talks to.
public class LevelPlayAds : MonoBehaviour, Ads.IAdProvider
{
  public static LevelPlayAds Instance { get; private set; }

  [Header("LevelPlay App Key (ironSource dashboard -> App settings)")]
  [SerializeField] private string androidAppKey = "";
  [SerializeField] private string iosAppKey = "";

  [Header("Ad Unit IDs (ironSource dashboard -> Ad units)")]
  [SerializeField] private string androidInterstitialAdUnitId = "";
  [SerializeField] private string iosInterstitialAdUnitId = "";
  [SerializeField] private string androidRewardedAdUnitId = "";
  [SerializeField] private string iosRewardedAdUnitId = "";

  [Header("Reward")]
  [Tooltip("Used when the mediation dashboard does not supply an amount.")]
  [SerializeField] private int fallbackRewardAmount = 300;

  [Header("Retry")]
  [SerializeField] private float retryDelay = 4f;
  [SerializeField] private int maxRetries = 5;

  [Header("Consent timeouts (seconds)")]
  [Tooltip("Every wait is bounded: a stalled consent step must never leave the game without ads.")]
  [SerializeField] private float consentUpdateTimeout = 15f;
  [SerializeField] private float consentFormTimeout = 60f;
  [SerializeField] private float attTimeout = 30f;

  [Header("Debug")]
  [Tooltip("Opens the LevelPlay mediation test suite after init. MUST be off for store builds.")]
  [SerializeField] private bool launchTestSuiteOnInit = false;
  [SerializeField] private bool verboseLogging = false;

  private LevelPlayInterstitialAd interstitial;
  private LevelPlayRewardedAd rewarded;

  private bool initialized;
  private bool initRequested;
  private int initRetryCount;

  private bool rewardedLoaded, rewardedLoading, rewardedShowing;
  private int rewardedRetryCount;
  private bool interstitialLoaded, interstitialLoading, interstitialShowing;
  private int interstitialRetryCount;

  private Action<int> pendingReward;
  private Action pendingFailure;
  private bool rewardGranted;

  public bool IsInitialized => initialized;
  public bool IsRewardedReady => rewardedLoaded && rewarded != null && rewarded.IsAdReady();
  public bool IsAnyAdShowing => rewardedShowing || interstitialShowing;

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);
    Ads.Register(this);
  }

  private void Start()
  {
    LevelPlay.OnInitSuccess += OnInitSuccess;
    LevelPlay.OnInitFailed += OnInitFailed;
    StartCoroutine(ConsentThenInit());
  }

  private void OnDestroy()
  {
    if (Instance != this) return;

    LevelPlay.OnInitSuccess -= OnInitSuccess;
    LevelPlay.OnInitFailed -= OnInitFailed;

    interstitial?.DestroyAd();
    rewarded?.DestroyAd();
  }

  // ------------------------------------------------------------ consent

  // App start order: ATT prompt (iOS) -> UMP consent form (GDPR regions) ->
  // LevelPlay init. The networks read the resulting TCF consent string and ATT
  // status themselves, so they show no prompt of their own afterwards.
  private IEnumerator ConsentThenInit()
  {
#if !UNITY_EDITOR
#if UNITY_IOS
    yield return RequestTrackingAuthorization();
#endif
    yield return GatherConsent();
#endif
    Initialize();
    yield break;
  }

#if UNITY_IOS && !UNITY_EDITOR
  private IEnumerator RequestTrackingAuthorization()
  {
    if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() !=
        ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
    {
      yield break;
    }

    ATTrackingStatusBinding.RequestAuthorizationTracking();

    float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, attTimeout);
    while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
           ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED &&
           Time.realtimeSinceStartup < deadline)
    {
      yield return new WaitForSecondsRealtime(0.2f);
    }

    Log($"ATT status: {ATTrackingStatusBinding.GetAuthorizationTrackingStatus()}");
  }
#endif

  private IEnumerator GatherConsent()
  {
    bool done = false;
    FormError updateError = null;
    bool unavailable = false;

    // The consent SDK is not a hard dependency of the game. If it is missing or
    // stripped it throws here, and ads must still initialize rather than the
    // whole flow dying with the coroutine.
    try
    {
      ConsentInformation.Update(new ConsentRequestParameters(), error =>
      {
        updateError = error;
        done = true;
      });
    }
    catch (Exception e)
    {
      Debug.LogError($"[Ads] UMP unavailable, continuing without it: {e.Message}");
      unavailable = true;
    }

    if (unavailable) yield break;

    float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, consentUpdateTimeout);
    while (!done && Time.realtimeSinceStartup < deadline) yield return null;

    if (!done || updateError != null)
    {
      Log($"UMP update did not complete ({updateError?.Message ?? "timeout"}); continuing.");
      yield break;
    }

    bool formDone = false;
    try
    {
      ConsentForm.LoadAndShowConsentFormIfRequired(error =>
      {
        if (error != null) Log($"Consent form error: {error.Message}");
        formDone = true;
      });
    }
    catch (Exception e)
    {
      Debug.LogError($"[Ads] Consent form unavailable, continuing without it: {e.Message}");
      yield break;
    }

    // The callback fires at once where no form is required. The generous cap
    // covers the modal case (the player reading it) while still guaranteeing
    // that ads initialize if the callback never arrives.
    float formDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, consentFormTimeout);
    while (!formDone && Time.realtimeSinceStartup < formDeadline) yield return null;
  }

  // Wire this to a settings button so EEA players can change their choice
  // later, as GDPR requires. Only show that button when this is true.
  public static bool IsPrivacyOptionsRequired =>
    ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

  public void ShowPrivacyOptionsForm() => ConsentForm.ShowPrivacyOptionsForm(error => { });

  // --------------------------------------------------------------- init

  private void Initialize()
  {
    // Guarded so a retry or a late consent callback cannot double-init.
    if (initRequested) return;
    initRequested = true;

#if UNITY_IOS
    string appKey = iosAppKey;
#else
    string appKey = androidAppKey;
#endif

    if (string.IsNullOrEmpty(appKey))
    {
      Debug.LogWarning("[Ads] No LevelPlay app key set; ads are disabled for this build.");
      return;
    }

    if (launchTestSuiteOnInit)
    {
      // Deliberately loud and not gated behind verboseLogging: shipping with
      // the test suite on puts a debug overlay in front of real players.
      Debug.LogWarning("[Ads] TEST SUITE ENABLED - turn launchTestSuiteOnInit off " +
                       "on the Ads object in MainMenu.unity before any real build.");
      IronSource.Agent.setMetaData("is_test_suite", "enable");
    }

    Log("Initializing LevelPlay...");
    LevelPlay.Init(appKey, null, new[]
    {
      com.unity3d.mediation.LevelPlayAdFormat.REWARDED,
      com.unity3d.mediation.LevelPlayAdFormat.INTERSTITIAL
    });
  }

  private void OnInitSuccess(com.unity3d.mediation.LevelPlayConfiguration configuration)
  {
    // Not gated behind verboseLogging: this is the one line that proves ads
    // ever got off the ground at all, and its absence from a device log is
    // the first thing to check when "ads don't work".
    Debug.Log("[Ads] LevelPlay init succeeded.");
    initialized = true;

    LogAdvertisingId();

    CreateInterstitial();
    CreateRewarded();
    LoadRewarded();
    LoadInterstitial();

    if (launchTestSuiteOnInit) LevelPlay.LaunchTestSuite();
  }

  private void OnInitFailed(com.unity3d.mediation.LevelPlayInitError error)
  {
    Debug.LogWarning($"[Ads] LevelPlay init failed: {error.ErrorCode} - {error.ErrorMessage}");

    if (initRetryCount >= maxRetries) return;
    initRetryCount++;
    StartCoroutine(RetryInit());
  }

  private IEnumerator RetryInit()
  {
    // Realtime: the menus and the pause screen run at timeScale 0.
    yield return new WaitForSecondsRealtime(retryDelay);
    initRequested = false;
    Initialize();
  }

  // ----------------------------------------------------------- rewarded

  private void CreateRewarded()
  {
    if (rewarded != null) return;

#if UNITY_IOS
    string adUnitId = iosRewardedAdUnitId;
#else
    string adUnitId = androidRewardedAdUnitId;
#endif
    if (string.IsNullOrEmpty(adUnitId)) return;

    rewarded = new LevelPlayRewardedAd(adUnitId);
    rewarded.OnAdLoaded += OnRewardedLoaded;
    rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
    rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
    rewarded.OnAdRewarded += OnRewardedEarned;
    rewarded.OnAdClosed += OnRewardedClosed;
  }

  private void LoadRewarded()
  {
    if (rewarded == null || rewardedLoading || rewardedLoaded || IsAnyAdShowing) return;
    rewardedLoading = true;
    rewarded.LoadAd();
  }

  private void OnRewardedLoaded(com.unity3d.mediation.LevelPlayAdInfo info)
  {
    rewardedLoaded = true;
    rewardedLoading = false;
    rewardedRetryCount = 0;
    Ads.NotifyRewardedAvailability();
  }

  private void OnRewardedLoadFailed(com.unity3d.mediation.LevelPlayAdError error)
  {
    // No fill is routine (empty inventory, no test ads configured) and not
    // worth alarming over, but it must be visible somewhere - this used to be
    // completely silent, which is exactly why "ads don't work" had no trail to
    // follow. LogWarning rather than LogError: expected during normal testing.
    Debug.LogWarning($"[Ads] Rewarded load failed: {error.ErrorCode} - {error.ErrorMessage}");
    rewardedLoading = false;
    Ads.NotifyRewardedAvailability();

    // Backs off rather than hammering: a failure is usually no fill, and
    // retrying instantly just burns battery.
    float delay = rewardedRetryCount < maxRetries ? retryDelay : retryDelay * 4f;
    rewardedRetryCount = rewardedRetryCount < maxRetries ? rewardedRetryCount + 1 : 0;
    StartCoroutine(RetryLoadRewarded(delay));
  }

  private IEnumerator RetryLoadRewarded(float delay)
  {
    yield return new WaitForSecondsRealtime(delay);
    LoadRewarded();
  }

  public void ShowRewarded(Action<int> onReward, Action onFailed)
  {
    if (!IsRewardedReady || IsAnyAdShowing)
    {
      onFailed?.Invoke();
      LoadRewarded();
      return;
    }

    pendingReward = onReward;
    pendingFailure = onFailed;
    rewardGranted = false;
    rewardedShowing = true;
    rewardedLoaded = false;
    Ads.NotifyRewardedAvailability();

    rewarded.ShowAd();
  }

  private void OnRewardedEarned(com.unity3d.mediation.LevelPlayAdInfo info,
                                com.unity3d.mediation.LevelPlayReward reward)
  {
    rewardGranted = true;
    int amount = reward != null && reward.Amount > 0 ? reward.Amount : fallbackRewardAmount;

    Action<int> callback = pendingReward;
    pendingReward = null;
    pendingFailure = null;
    callback?.Invoke(amount);
  }

  private void OnRewardedDisplayFailed(com.unity3d.mediation.LevelPlayAdDisplayInfoError error)
  {
    rewardedShowing = false;
    ResolveRewardedFailure();
    LoadRewarded();
  }

  private void OnRewardedClosed(com.unity3d.mediation.LevelPlayAdInfo info)
  {
    rewardedShowing = false;
    rewardedLoaded = false;

    // Closed without the reward event means the player skipped out early. The
    // caller has to hear about it, or a screen waiting on the callback hangs.
    ResolveRewardedFailure();

    Ads.NotifyRewardedAvailability();
    LoadRewarded();
  }

  private void ResolveRewardedFailure()
  {
    if (rewardGranted) return;

    Action failure = pendingFailure;
    pendingReward = null;
    pendingFailure = null;
    failure?.Invoke();
  }

  // ------------------------------------------------------- interstitial

  private void CreateInterstitial()
  {
    if (interstitial != null) return;

#if UNITY_IOS
    string adUnitId = iosInterstitialAdUnitId;
#else
    string adUnitId = androidInterstitialAdUnitId;
#endif
    if (string.IsNullOrEmpty(adUnitId)) return;

    interstitial = new LevelPlayInterstitialAd(adUnitId);
    interstitial.OnAdLoaded += info =>
    {
      interstitialLoaded = true;
      interstitialLoading = false;
      interstitialRetryCount = 0;
    };
    interstitial.OnAdLoadFailed += error =>
    {
      Debug.LogWarning($"[Ads] Interstitial load failed: {error.ErrorCode} - {error.ErrorMessage}");
      interstitialLoading = false;

      // Retries on a timer, like the rewarded does. Without this the only
      // other caller of LoadInterstitial is a *failed* ShowInterstitial, so a
      // failure at startup meant the first level-end that was allowed to show
      // an ad always found nothing loaded and merely kicked off a load - one
      // wasted opportunity per failure, every session.
      if (interstitialRetryCount < maxRetries)
      {
        interstitialRetryCount++;
        StartCoroutine(RetryLoadInterstitial(retryDelay));
      }
      else
      {
        interstitialRetryCount = 0;
        StartCoroutine(RetryLoadInterstitial(retryDelay * 4f));
      }
    };
    interstitial.OnAdDisplayFailed += error => { interstitialShowing = false; LoadInterstitial(); };
    interstitial.OnAdClosed += info => { interstitialShowing = false; LoadInterstitial(); };
  }

  private IEnumerator RetryLoadInterstitial(float delay)
  {
    // Realtime: the menus and the pause screen run at timeScale 0.
    yield return new WaitForSecondsRealtime(delay);
    LoadInterstitial();
  }

  private void LoadInterstitial()
  {
    if (interstitial == null || interstitialLoading || interstitialLoaded || IsAnyAdShowing) return;
    interstitialLoading = true;
    interstitial.LoadAd();
  }

  // Ads decides *whether* to show one; this only decides whether it *can*.
  public void ShowInterstitial()
  {
    if (interstitial == null) return;

    if (!interstitialLoaded || !interstitial.IsAdReady())
    {
      LoadInterstitial();
      return;
    }

    if (IsAnyAdShowing) return;

    interstitialShowing = true;
    interstitialLoaded = false;
    interstitial.ShowAd();
  }

  // The LevelPlay dashboard's Settings -> Test devices list is keyed by the
  // device's advertising ID (IDFA on iOS, AAID on Android). Until this device
  // is in that list, every network in the waterfall is asked for *real*
  // inventory - which a brand-new app with no traffic history simply does not
  // have, giving error 509 "Mediation No fill" forever.
  //
  // Reading it off the device is otherwise a nuisance (iOS hides the IDFA and
  // the usual advice is to install a third-party app to find it), so it is
  // printed here instead. Debug-flag gated: a shipping build must not log it.
  private void LogAdvertisingId()
  {
    if (!launchTestSuiteOnInit && !verboseLogging) return;

    try
    {
      Application.RequestAdvertisingIdentifierAsync((string id, bool trackingEnabled, string error) =>
      {
        if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(id))
        {
          Debug.Log($"[Ads] Advertising ID (paste into LevelPlay -> Settings -> " +
                    $"Test devices): {id}  [tracking enabled: {trackingEnabled}]");
        }
        else
        {
          Debug.LogWarning($"[Ads] Could not read the advertising ID: " +
                           $"{(string.IsNullOrEmpty(error) ? "empty id" : error)}");
        }
      });
    }
    catch (Exception e)
    {
      Debug.LogWarning($"[Ads] Advertising ID unavailable: {e.Message}");
    }
  }

  private void Log(string message)
  {
    if (verboseLogging) Debug.Log($"[Ads] {message}");
  }
}
