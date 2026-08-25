using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Covers the menu on cold launch until the ad SDK has finished starting up.
//
// The ad SDKs do real work on the main thread while they initialise - consent,
// network calls, and provisioning the WebViews that render ads - and none of it
// is under this project's control. That work has to happen somewhere, so this
// puts it behind a splash rather than over a live, interactive menu, where it
// showed up as a stutter and an audible glitch.
//
// Every wait is bounded. A splash that outlives a slow network is worse than
// the stutter it was added to hide, so this always dismisses itself.
public class BootSplash : MonoBehaviour
{
  [Tooltip("Hard ceiling. The splash always dismisses by this point, ready or not.")]
  [SerializeField] private float maxSeconds = 6f;

  [Tooltip("Shown even on an instant boot, so the splash never flashes past.")]
  [SerializeField] private float minSeconds = 1.2f;

  private static bool alreadyShown;

  private Slider bar;
  private TMP_Text status;
  private CanvasGroup group;

  // Cold launch only: coming back to the menu between levels has nothing to
  // wait for, and a splash there would just be a delay.
  public static bool ShouldShow => !alreadyShown;

  public static BootSplash Create(Transform parent)
  {
    alreadyShown = true;

    var go = new GameObject("BootSplash", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    UiSkin.Stretch((RectTransform)go.transform);
    go.transform.SetAsLastSibling();

    var splash = go.AddComponent<BootSplash>();
    splash.Build();
    return splash;
  }

  private void Build()
  {
    group = gameObject.AddComponent<CanvasGroup>();

    var backdrop = gameObject.AddComponent<Image>();
    backdrop.color = new Color(0.04f, 0.05f, 0.09f, 1f);

    var titleGo = new GameObject("Title", typeof(RectTransform));
    titleGo.transform.SetParent(transform, false);
    var title = titleGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(title, UiSkin.Role.Title);
    title.text = "FUNGI VS BACTERIA";
    title.alignment = TextAlignmentOptions.Center;
    Place(title.rectTransform, new Vector2(0f, 60f), new Vector2(900f, 120f));

    var statusGo = new GameObject("Status", typeof(RectTransform));
    statusGo.transform.SetParent(transform, false);
    status = statusGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(status, UiSkin.Role.Caption, UiSkin.TextMuted);
    status.alignment = TextAlignmentOptions.Center;
    status.text = "Starting up...";
    Place(status.rectTransform, new Vector2(0f, -96f), new Vector2(700f, 40f));

    BuildBar();
    StartCoroutine(Run());
  }

  private void BuildBar()
  {
    var go = new GameObject("Progress", typeof(RectTransform));
    go.transform.SetParent(transform, false);
    Place((RectTransform)go.transform, new Vector2(0f, -40f), new Vector2(620f, 28f));

    bar = go.AddComponent<Slider>();
    bar.transition = Selectable.Transition.None;
    bar.interactable = false;
    bar.minValue = 0f;
    bar.maxValue = 1f;
    bar.value = 0f;

    var trackGo = new GameObject("Track", typeof(RectTransform));
    trackGo.transform.SetParent(go.transform, false);
    var track = trackGo.AddComponent<Image>();
    UiSkin.Panel(track, UiSkin.Neutral, UiSkin.RadiusChip);
    track.raycastTarget = false;
    UiSkin.Stretch(track.rectTransform);

    var areaGo = new GameObject("Fill Area", typeof(RectTransform));
    areaGo.transform.SetParent(go.transform, false);
    UiSkin.Stretch((RectTransform)areaGo.transform);

    var fillGo = new GameObject("Fill", typeof(RectTransform));
    fillGo.transform.SetParent(areaGo.transform, false);
    var fill = fillGo.AddComponent<Image>();
    UiSkin.Panel(fill, UiSkin.Primary, UiSkin.RadiusChip);
    fill.raycastTarget = false;
    UiSkin.Stretch(fill.rectTransform);

    bar.fillRect = fill.rectTransform;
    bar.targetGraphic = fill;
  }

  private IEnumerator Run()
  {
    float started = Time.realtimeSinceStartup;
    float deadline = started + Mathf.Max(0.5f, maxSeconds);

    // Progress is deliberately time-based rather than a real percentage: the ad
    // SDKs expose no progress, and a bar that sat at 0 then jumped to 100 would
    // read as a hang. It is capped below full until the work actually finishes,
    // so it never claims to be done while it is not.
    while (Time.realtimeSinceStartup < deadline)
    {
      // Done means either an ad is in hand, or init finished and the first load
      // has settled one way or the other. Waiting for a *successful* load would
      // hold the splash open whenever there is no fill, which is most of the
      // time before the app is live.
      bool loadSettled = Ads.IsInitialized && !Ads.IsRewardedLoading;
      bool ready = Ads.IsRewardedReady || loadSettled;

      float elapsed = Time.realtimeSinceStartup - started;
      bar.value = Mathf.Min(0.92f, elapsed / Mathf.Max(0.5f, maxSeconds));
      status.text = Ads.IsInitialized ? "Preparing..." : "Starting up...";

      if (ready && elapsed >= minSeconds) break;
      yield return null;
    }

    bar.value = 1f;
    status.text = "Ready";
    yield return new WaitForSecondsRealtime(0.15f);

    // Faded rather than cut, to match the music coming up underneath it.
    for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
    {
      group.alpha = 1f - (t / 0.3f);
      yield return null;
    }

    Destroy(gameObject);
  }

  private static void Place(RectTransform rect, Vector2 position, Vector2 size)
  {
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = position;
    rect.sizeDelta = size;
  }
}
