using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The coin wallet: current balance, a rewarded ad to earn more, and a plain
// statement of what coins are for.
//
// Built entirely in code, with no prefab. Every other screen is a prefab, but
// those predate the code-built skin and each one needed a runtime pass
// (ScreenTheme, HudTheme, MenuLayout) to look right anyway. A new screen has
// nothing to inherit from the prefabs, and building it here means it can be
// changed and re-rendered through UiPreview without opening the editor.
public class WalletScreen : MonoBehaviour
{
  private const int RewardFallback = 300;

  private TMP_Text balanceLabel;
  private Button watchButton;
  private TMP_Text watchLabel;
  private TMP_Text statusLabel;
  private Button streakButton;
  private TMP_Text streakLabel;
  private bool awaitingAd;
  private Action onClosed;

  public static WalletScreen Open(Transform parent, Action onClosed = null)
  {
    var go = new GameObject("WalletScreen", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    UiSkin.Stretch((RectTransform)go.transform);

    var screen = go.AddComponent<WalletScreen>();
    screen.onClosed = onClosed;
    screen.Build();
    return screen;
  }

  private void Build()
  {
    // Scrim, and a raycast blocker: without a graphic on the full-rect root the
    // menu behind stays clickable through the dialog.
    var scrim = gameObject.AddComponent<Image>();
    scrim.color = UiSkin.Scrim;

    RectTransform card = Panel();
    Title(card, "WALLET");
    BalanceRow(card);
    StreakRow(card);
    WatchAdRow(card);
    Explainer(card);
    CloseButton();

    RefreshBalance(Wallet.Coins);
    RefreshWatchButton();
  }

  private RectTransform Panel()
  {
    var go = new GameObject("Card", typeof(RectTransform));
    go.transform.SetParent(transform, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(720f, 0f);

    UiSkin.Panel(go.AddComponent<Image>(), UiSkin.PanelDark, UiSkin.RadiusPanel);

    var layout = go.AddComponent<VerticalLayoutGroup>();
    layout.padding = new RectOffset(36, 36, 30, 34);
    layout.spacing = 20f;
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    // Height follows the content, so adding a row later needs no re-measuring.
    var fitter = go.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

    UiSkin.AddBorder(rect, UiSkin.RadiusPanel);
    return rect;
  }

  private void Title(RectTransform parent, string text)
  {
    var go = new GameObject("Title", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Title);
    label.text = text;
    label.alignment = TextAlignmentOptions.Center;
    go.AddComponent<LayoutElement>().preferredHeight = 76f;
  }

  private void BalanceRow(RectTransform parent)
  {
    var go = new GameObject("Balance", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    UiSkin.Panel(go.AddComponent<Image>(), UiSkin.PanelRaised, UiSkin.RadiusChip);

    var layout = go.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(24, 24, 12, 12);
    layout.spacing = 16f;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    Image coin = UiSkin.Icon(go.transform, UiSprites.Coin(), UiSkin.Gold, 64f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 64f;
    coinElement.flexibleWidth = 0f;

    var labelGo = new GameObject("Amount", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    balanceLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(balanceLabel, UiSkin.Role.Title, UiSkin.Gold);
    balanceLabel.alignment = TextAlignmentOptions.MidlineLeft;
    labelGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    go.AddComponent<LayoutElement>().preferredHeight = 92f;
  }

  private void WatchAdRow(RectTransform parent)
  {
    var go = new GameObject("WatchAd", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    watchButton = UiSkin.IconButton(go, UiSprites.Coin(), UiSkin.Primary, out watchLabel,
      UiSkin.RadiusButton, UiSkin.Gold);
    watchLabel.alignment = TextAlignmentOptions.Center;
    go.AddComponent<LayoutElement>().preferredHeight = 96f;

    watchButton.onClick.AddListener(OnWatchClicked);

    var statusGo = new GameObject("Status", typeof(RectTransform));
    statusGo.transform.SetParent(parent, false);
    statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(statusLabel, UiSkin.Role.Caption, UiSkin.TextMuted);
    statusLabel.alignment = TextAlignmentOptions.Center;
    statusLabel.text = "";
    statusGo.AddComponent<LayoutElement>().preferredHeight = 30f;
  }

  // Five pips, one per streak day, with the reward under each. Claimed days are
  // filled gold, today's is outlined in the call-to-action colour, and future
  // days are dimmed - readable at a glance without any text explaining it.
  private void StreakRow(RectTransform parent)
  {
    var go = new GameObject("Streak", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    UiSkin.Panel(go.AddComponent<Image>(), UiSkin.PanelRaised, UiSkin.RadiusChip);

    var column = go.AddComponent<VerticalLayoutGroup>();
    column.padding = new RectOffset(16, 16, 12, 12);
    column.spacing = 8f;
    column.childAlignment = TextAnchor.UpperCenter;
    column.childControlWidth = true;
    column.childControlHeight = true;
    column.childForceExpandWidth = true;
    column.childForceExpandHeight = false;

    var headingGo = new GameObject("Heading", typeof(RectTransform));
    headingGo.transform.SetParent(go.transform, false);
    var heading = headingGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(heading, UiSkin.Role.Caption, UiSkin.TextMuted);
    heading.alignment = TextAlignmentOptions.Center;
    heading.text = DailyStreak.ClaimedToday
      ? "DAILY STREAK - COME BACK TOMORROW"
      : "DAILY STREAK";
    headingGo.AddComponent<LayoutElement>().preferredHeight = 26f;

    var pipsGo = new GameObject("Pips", typeof(RectTransform));
    pipsGo.transform.SetParent(go.transform, false);
    var row = pipsGo.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 10f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = true;
    row.childForceExpandHeight = false;
    pipsGo.AddComponent<LayoutElement>().preferredHeight = 74f;

    int claimed = DailyStreak.ClaimedInStreak;
    int todayIndex = DailyStreak.CurrentDay - 1;

    for (int i = 0; i < DailyStreak.Length; i++)
    {
      bool isDone = i < claimed;
      bool isToday = !DailyStreak.ClaimedToday && i == todayIndex;
      BuildPip(pipsGo.transform, DailyStreak.Rewards[i], isDone, isToday);
    }

    streakButton = BuildStreakButton(go.transform);
    go.AddComponent<LayoutElement>().preferredHeight = 210f;
  }

  private void BuildPip(Transform parent, int reward, bool isDone, bool isToday)
  {
    var go = new GameObject("Day", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var column = go.AddComponent<VerticalLayoutGroup>();
    column.spacing = 2f;
    column.childAlignment = TextAnchor.UpperCenter;
    column.childControlWidth = true;
    column.childControlHeight = true;
    column.childForceExpandWidth = true;
    column.childForceExpandHeight = false;

    var discGo = new GameObject("Disc", typeof(RectTransform));
    discGo.transform.SetParent(go.transform, false);
    var disc = discGo.AddComponent<Image>();
    disc.sprite = UiSprites.Circle();
    // The row stretches each column to equal width, which would flatten the
    // disc into an ellipse.
    disc.preserveAspect = true;
    disc.color = isDone ? UiSkin.Gold
               : isToday ? UiSkin.Primary
               : UiSkin.Neutral;
    discGo.AddComponent<LayoutElement>().preferredHeight = 40f;

    var labelGo = new GameObject("Reward", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Caption,
      isDone || isToday ? UiSkin.TextPrimary : UiSkin.TextMuted);
    label.alignment = TextAlignmentOptions.Center;
    label.text = reward.ToString();
    labelGo.AddComponent<LayoutElement>().preferredHeight = 24f;
  }

  private Button BuildStreakButton(Transform parent)
  {
    var go = new GameObject("ClaimStreak", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    Button button = UiSkin.IconButton(go, UiSprites.Coin(), UiSkin.Primary,
      out streakLabel, UiSkin.RadiusButton, UiSkin.Gold);
    streakLabel.alignment = TextAlignmentOptions.Center;
    go.AddComponent<LayoutElement>().preferredHeight = 74f;

    button.onClick.AddListener(OnClaimStreakClicked);
    return button;
  }

  private void OnClaimStreakClicked()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    if (DailyStreak.ClaimedToday) return;

    awaitingAd = true;
    RefreshWatchButton();

    Ads.ShowRewarded(
      _ =>
      {
        awaitingAd = false;
        int reward = DailyStreak.Claim();
        Ads.DeferInterstitial();
        Haptics.Play(Haptics.Style.Success);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        statusLabel.text = $"Day {DailyStreak.CurrentDay - 1} claimed: +{reward}!";
        RefreshWatchButton();
      },
      () =>
      {
        awaitingAd = false;
        statusLabel.text = "No ad available right now. Try again shortly.";
        Ads.Prewarm();
        RefreshWatchButton();
      });
  }

  private void Explainer(RectTransform parent)
  {
    var go = new GameObject("Explainer", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Body, UiSkin.TextMuted);
    label.alignment = TextAlignmentOptions.TopLeft;
    label.text =
      $"Coins are your only currency - the same balance buys towers in a level, " +
      $"revives you, and is what ads pay out.\n\n" +

      $"- Continue a lost run with +{Boosters.ContinueHealth} health, from {Boosters.FirstContinueCost} coins.\n" +
      $"- {RewardedGate.WatchesLeftToday} ad rewards left today.\n" +
      $"- Earn coins by clearing levels and by raising your star rating.";

    go.AddComponent<LayoutElement>().preferredHeight = 210f;
  }

  private void CloseButton()
  {
    var go = new GameObject("Close", typeof(RectTransform));
    go.transform.SetParent(transform, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-28f, -28f);
    rect.sizeDelta = new Vector2(150f, 76f);

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.Neutral, UiSkin.RadiusChip);

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel);
    label.text = "BACK";
    label.alignment = TextAlignmentOptions.Center;
    label.raycastTarget = false;
    UiSkin.Stretch((RectTransform)labelGo.transform);

    button.onClick.AddListener(Close);
  }

  private void OnWatchClicked()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    if (!RewardedGate.IsReady) return;

    awaitingAd = true;
    statusLabel.text = "";
    RefreshWatchButton();

    Ads.ShowRewarded(
      amount =>
      {
        awaitingAd = false;

        // Recorded only on a completed watch, so a failed or skipped ad never
        // costs the player a slot or starts a cooldown.
        RewardedGate.RecordWatch();

        Wallet.Add(amount);
        Ads.DeferInterstitial();
        Haptics.Play(Haptics.Style.Success);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        statusLabel.text = $"+{amount} coins!";
        RefreshWatchButton();
      },
      () =>
      {
        awaitingAd = false;
        statusLabel.text = "No ad available right now. Try again shortly.";
        Ads.Prewarm();
        RefreshWatchButton();
      });
  }

  // Drives the countdown. Realtime, because the menu can sit at timeScale 0.
  private void Update()
  {
    if (Time.unscaledTime < nextTick) return;
    nextTick = Time.unscaledTime + 1f;
    RefreshWatchButton();
  }

  private float nextTick;

  private void OnEnable()
  {
    Wallet.OnCoinsChanged += RefreshBalance;
    Ads.OnRewardedAvailabilityChanged += RefreshWatchButton;
  }

  private void OnDisable()
  {
    Wallet.OnCoinsChanged -= RefreshBalance;
    Ads.OnRewardedAvailabilityChanged -= RefreshWatchButton;
  }

  private void RefreshBalance(int coins)
  {
    if (balanceLabel != null) balanceLabel.text = coins.ToString();
  }

  // The button stays visible when no ad is loaded, just disabled and labelled.
  // Hiding it makes the wallet look broken and gives the player nothing to
  // understand; a greyed button with a reason does not.
  private void RefreshWatchButton()
  {
    if (streakButton != null)
    {
      bool streakClaimable = !DailyStreak.ClaimedToday && !awaitingAd;
      streakButton.interactable = streakClaimable && Ads.IsRewardedReady;
      streakLabel.text = DailyStreak.ClaimedToday
        ? "CLAIMED TODAY"
        : $"DAY {DailyStreak.CurrentDay}: WATCH AD  +{DailyStreak.TodayReward}";
    }

    if (watchButton == null) return;

    // Four states, in the order the player runs into them: out of watches for
    // today, waiting out a cooldown, no ad loaded, or good to go.
    if (RewardedGate.CapReached)
    {
      watchButton.interactable = false;
      watchLabel.text = "BACK TOMORROW";
    }
    else if (!RewardedGate.IsReady)
    {
      watchButton.interactable = false;
      watchLabel.text = $"NEXT IN  {RewardedGate.RemainingText()}";
    }
    else if (awaitingAd || Ads.IsRewardedLoading)
    {
      watchButton.interactable = false;
      watchLabel.text = "LOADING AD...";
    }
    else if (!Ads.IsRewardedReady)
    {
      watchButton.interactable = false;
      watchLabel.text = "AD UNAVAILABLE";
    }
    else
    {
      watchButton.interactable = true;
      watchLabel.text = $"WATCH AD  +{RewardFallback}";
    }
  }

  private void Close()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    onClosed?.Invoke();
    Destroy(gameObject);
  }
}
