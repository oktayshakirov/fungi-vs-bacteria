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

    // This screen is a plain child of the menu's canvas, not its own Canvas, so
    // DisplaySetup's edit-time pass never wraps it in a SafeArea. The card is
    // centred and mostly clear of a notch anyway, but the close button in the
    // corner is not.
    RectTransform safeArea = ScreenTheme.EnsureSafeArea(transform);

    RectTransform card = Panel(safeArea);
    Title(card, "STORE");

    // The rows below the title add up to roughly 860 units of content, and the
    // canvas is matched-height, so its vertical extent is EXACTLY the device's
    // full height on every phone - a fixed-height card was therefore
    // guaranteed to overflow every device by the same amount, not just small
    // ones. That is what cropped the title and ran the ad row into "PLAY"
    // underneath. Capped and scrollable instead.
    // Order is deliberate: balance, then the things that cost money, then the
    // things that are free, then the small print. This screen used to be the
    // WALLET and opened on the daily streak; as the STORE, burying the packs
    // under two rows of free coins means scrolling past the giveaway to reach
    // the shelf.
    RectTransform body = ScrollBody(card);
    BalanceRow(body);
    PacksSection(body);
    NoAdsRow(body);
    StreakRow(body);
    WatchAdRow(body);
    Explainer(body);
    RestoreRow(body);

    CloseButton(safeArea);

    RefreshBalance(Wallet.Coins);
    RefreshWatchButton();
    RefreshStore();
  }

  private RectTransform Panel(RectTransform parent)
  {
    var go = new GameObject("Card", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;

    // Sized off the screen rather than a flat constant: the canvas is
    // matched-height, so its width varies with device aspect while its height
    // is always the full screen - a size that fit one device would clip or
    // float tiny on every other one.
    //
    // Through ScreenTheme, NOT `parent.rect`: a rect read before the canvas has
    // been through a layout pass still reports RAW PIXELS rather than canvas
    // units (see HANDOFF section 6), and a card sized 1920-wide inside a
    // 1280-unit canvas renders half off the screen with its title cropped.
    // That is exactly what this screen did in the preview, where it is built
    // the moment the canvas is created.
    float available = ScreenTheme.LayoutWidth(parent);
    float width = Mathf.Clamp(available * 0.74f, 640f, 900f);
    float height = Mathf.Min(ScreenTheme.LayoutHeight(parent) * 0.90f, 800f);
    rect.sizeDelta = new Vector2(width, height);

    UiSkin.Panel(go.AddComponent<Image>(), UiSkin.PanelDark, UiSkin.RadiusPanel);

    var layout = go.AddComponent<VerticalLayoutGroup>();
    layout.padding = new RectOffset(36, 36, 30, 28);
    layout.spacing = 16f;
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    UiSkin.AddBorder(rect, UiSkin.RadiusPanel);
    return rect;
  }

  // The title is a fixed-height sibling; this is the flexible one that soaks
  // up whatever height is left in the (now fixed-height, not content-fitted)
  // card, and scrolls internally rather than pushing the card taller than the
  // screen.
  private RectTransform ScrollBody(RectTransform parent)
  {
    var go = new GameObject("Body", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<LayoutElement>().flexibleHeight = 1f;

    var viewportGo = new GameObject("Viewport", typeof(RectTransform));
    viewportGo.transform.SetParent(go.transform, false);
    UiSkin.Stretch((RectTransform)viewportGo.transform);
    viewportGo.AddComponent<RectMask2D>();

    var contentGo = new GameObject("Content", typeof(RectTransform));
    contentGo.transform.SetParent(viewportGo.transform, false);
    var content = (RectTransform)contentGo.transform;
    content.anchorMin = new Vector2(0f, 1f);
    content.anchorMax = new Vector2(1f, 1f);
    content.pivot = new Vector2(0.5f, 1f);
    content.anchoredPosition = Vector2.zero;
    // A new RectTransform starts at sizeDelta (100, 100), and on a
    // stretch-anchored axis sizeDelta is an offset ON TOP of the parent's
    // width - so without this the content was 100 units wider than the
    // viewport and every row in the dialog was clipped 50 units at each end by
    // the RectMask2D. The ContentSizeFitter below drives the height, so only
    // the width has to be cleared, but both are zeroed to leave nothing stale.
    content.sizeDelta = Vector2.zero;

    var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
    contentLayout.spacing = 20f;
    contentLayout.childAlignment = TextAnchor.UpperCenter;
    contentLayout.childControlWidth = true;
    contentLayout.childControlHeight = true;
    contentLayout.childForceExpandWidth = true;
    contentLayout.childForceExpandHeight = false;

    var fitter = contentGo.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

    var scroll = go.AddComponent<ScrollRect>();
    scroll.horizontal = false;
    scroll.vertical = true;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.viewport = (RectTransform)viewportGo.transform;
    scroll.content = content;

    // AutoHide fades the bar via a CanvasGroup and leaves layout alone;
    // AutoHideAndExpandViewport resizes the viewport around the bar every time
    // it shows or hides, which drags the content width with it (see HANDOFF).
    // Not hidden entirely: an invisible scrollbar is how this dialog first
    // shipped "scrollable" and just looked cropped, with nothing to say there
    // was more below.
    Scrollbar bar = UiSkin.BuildScrollbar(go.transform);
    scroll.verticalScrollbar = bar;
    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

    return content;
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
    go.AddComponent<LayoutElement>().preferredHeight = 250f;
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

  // ------------------------------------------------------------ purchases

  // Rebuilt from scratch whenever prices arrive: the store answers
  // asynchronously and may answer after this screen is already open, so the
  // section cannot be built once from whatever was known at Build() time.
  private RectTransform packsSection;
  private TMP_Text packsStatus;
  private Button noAdsButton;
  private TMP_Text noAdsLabel;
  private Button restoreButton;
  private TMP_Text restoreLabel;

  private void PacksSection(RectTransform parent)
  {
    var go = new GameObject("Packs", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    packsSection = (RectTransform)go.transform;

    var layout = go.AddComponent<VerticalLayoutGroup>();
    layout.spacing = 10f;
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    var statusGo = new GameObject("PacksStatus", typeof(RectTransform));
    statusGo.transform.SetParent(go.transform, false);
    packsStatus = statusGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(packsStatus, UiSkin.Role.Caption, UiSkin.TextMuted);
    packsStatus.alignment = TextAlignmentOptions.Center;
    statusGo.AddComponent<LayoutElement>().preferredHeight = 34f;
  }

  // One coin pack. The amount on the left, the localised price on the right,
  // and a bonus badge between them on everything above the base pack.
  //
  // The price string comes from the store, never from the game: the currency,
  // its symbol and where that symbol goes are the store's to decide, and a
  // hand-formatted price is both wrong abroad and a review rejection.
  private void BuildPackRow(Transform parent, string productId, int coins, string price)
  {
    var go = new GameObject("Pack_" + coins, typeof(RectTransform));
    go.transform.SetParent(parent, false);

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();

    var layout = go.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(16, 16, 8, 8);
    layout.spacing = 10f;
    layout.childAlignment = TextAnchor.MiddleLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;
    go.AddComponent<LayoutElement>().preferredHeight = 78f;

    Image coin = UiSkin.Icon(go.transform, UiSprites.Coin(), UiSkin.Gold, 30f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 30f;
    coinElement.flexibleWidth = 0f;

    var amountGo = new GameObject("Amount", typeof(RectTransform));
    amountGo.transform.SetParent(go.transform, false);
    var amount = amountGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(amount, UiSkin.Role.Value, UiSkin.Gold);
    amount.text = coins.ToString("N0");
    amount.alignment = TextAlignmentOptions.MidlineLeft;
    amount.raycastTarget = false;
    amountGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    int bonus = IapCatalog.BonusPercent(productId);
    if (bonus > 0)
    {
      var badgeGo = new GameObject("Bonus", typeof(RectTransform));
      badgeGo.transform.SetParent(go.transform, false);
      var badge = badgeGo.AddComponent<TextMeshProUGUI>();
      UiSkin.Label(badge, UiSkin.Role.Caption, UiSkin.Primary);
      badge.text = $"+{bonus}%";
      badge.alignment = TextAlignmentOptions.Midline;
      badge.raycastTarget = false;
      badgeGo.AddComponent<LayoutElement>().preferredWidth = 74f;
    }

    var priceGo = new GameObject("Price", typeof(RectTransform));
    priceGo.transform.SetParent(go.transform, false);
    var priceLabel = priceGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(priceLabel, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
    priceLabel.text = price;
    priceLabel.alignment = TextAlignmentOptions.MidlineRight;
    priceLabel.textWrappingMode = TextWrappingModes.NoWrap;
    priceLabel.enableAutoSizing = true;
    priceLabel.fontSizeMin = 16f;
    priceLabel.fontSizeMax = priceLabel.fontSize;
    priceLabel.raycastTarget = false;
    priceGo.AddComponent<LayoutElement>().preferredWidth = 150f;

    UiSkin.StyleButton(button, UiSkin.Neutral, UiSkin.RadiusButton);
    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      Iap.Purchase(productId);
    });
  }

  private void NoAdsRow(RectTransform parent)
  {
    var go = new GameObject("NoAds", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    // A plain label button, not an IconButton: the only icon in the set that
    // fits is the padlock, and a padlock on a purchase reads as "locked
    // content you cannot have" rather than "buy your way out of the ads".
    go.AddComponent<Image>();
    noAdsButton = go.AddComponent<Button>();
    UiSkin.StyleButton(noAdsButton, UiSkin.Primary, UiSkin.RadiusButton);
    go.AddComponent<LayoutElement>().preferredHeight = 88f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    noAdsLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(noAdsLabel, UiSkin.Role.ButtonLabel, UiSkin.TextDark);
    noAdsLabel.alignment = TextAlignmentOptions.Midline;
    noAdsLabel.textWrappingMode = TextWrappingModes.NoWrap;
    noAdsLabel.enableAutoSizing = true;
    noAdsLabel.fontSizeMin = 16f;
    noAdsLabel.fontSizeMax = 30f;
    noAdsLabel.raycastTarget = false;
    UiSkin.Stretch(noAdsLabel.rectTransform);

    noAdsButton.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      Iap.Purchase(IapCatalog.NoAds);
    });
  }

  // Apple requires a visible way to restore non-consumable purchases, which
  // here is Remove Ads. Coins are consumable and are not restored - they were
  // spent - which is what the label under the button says out loud, because a
  // player who taps Restore expecting their coins back and gets nothing will
  // otherwise read it as the game losing their purchase.
  private void RestoreRow(RectTransform parent)
  {
    var go = new GameObject("Restore", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    go.AddComponent<Image>();
    restoreButton = go.AddComponent<Button>();
    ScreenTheme.CornerButton(restoreButton);
    go.AddComponent<LayoutElement>().preferredHeight = 60f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    restoreLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(restoreLabel, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
    restoreLabel.text = "RESTORE PURCHASES";
    restoreLabel.alignment = TextAlignmentOptions.Midline;
    restoreLabel.enableAutoSizing = true;
    restoreLabel.fontSizeMin = 14f;
    restoreLabel.fontSizeMax = 22f;
    restoreLabel.raycastTarget = false;
    UiSkin.Stretch(restoreLabel.rectTransform);

    restoreButton.onClick.AddListener(OnRestoreClicked);
  }

  private void OnRestoreClicked()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    restoreButton.interactable = false;
    restoreLabel.text = "RESTORING...";

    Iap.Restore(success =>
    {
      // The screen can be closed while the store is still answering.
      if (restoreLabel == null) return;
      restoreLabel.text = success
        ? (NoAds.Active ? "PURCHASES RESTORED" : "NOTHING TO RESTORE")
        : "RESTORE FAILED";
      if (restoreButton != null) restoreButton.interactable = true;
      RefreshStore();
    });
  }

  // Draws the purchase rows from whatever the store currently knows. Called at
  // build time and again on every price update and finished purchase.
  private void RefreshStore()
  {
    if (packsSection == null) return;

    for (int i = packsSection.childCount - 1; i >= 0; i--)
    {
      Transform child = packsSection.GetChild(i);
      if (child == packsStatus.transform) continue;
      Destroy(child.gameObject);
    }

    int shown = 0;
    foreach (string productId in IapCatalog.CoinProducts)
    {
      string price = Iap.PriceString(productId);
      if (price == null) continue;
      if (!IapCatalog.TryGetCoins(productId, out int coins)) continue;

      BuildPackRow(packsSection, productId, coins, price);
      shown++;
    }

    // The status line doubles as the empty state. No prices means the store has
    // not answered yet, has no network, or the build has no RevenueCat key -
    // all of which look the same to the player, so they get one honest line
    // instead of four dead buttons.
    packsStatus.text = shown > 0 ? "BUY COINS" : "Coin packs are unavailable right now.";
    packsStatus.transform.SetAsFirstSibling();

    RefreshNoAdsRow();
  }

  private void RefreshNoAdsRow()
  {
    if (noAdsButton == null) return;

    if (NoAds.Active)
    {
      noAdsButton.gameObject.SetActive(true);
      noAdsButton.interactable = false;
      noAdsLabel.text = "ADS REMOVED - THANK YOU";
      return;
    }

    string price = Iap.PriceString(IapCatalog.NoAds);
    noAdsButton.gameObject.SetActive(price != null);
    noAdsButton.interactable = price != null;
    if (price != null) noAdsLabel.text = $"REMOVE ADS   {price}";
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
      $"- Earn coins by clearing levels and by raising your star rating.\n" +
      // Said plainly, because the alternative is a player tapping Restore after
      // a reinstall, getting nothing back, and concluding the game ate their
      // purchase. Consumables are not restorable on either store.
      $"- Coins are saved on this device. Restore brings back Remove Ads, not coins.";

    go.AddComponent<LayoutElement>().preferredHeight = 210f;
  }

  private void CloseButton(RectTransform parent)
  {
    var go = new GameObject("Close", typeof(RectTransform));
    go.transform.SetParent(parent, false);

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

  private void OnStoreChanged() => RefreshStore();

  private void OnPurchaseFinished(bool success, string message)
  {
    RefreshStore();
    if (statusLabel == null) return;

    // A cancellation carries no message: the player closed the sheet on
    // purpose and does not need to be told what they just did.
    if (!success && message == null) return;
    statusLabel.text = success ? "Purchase complete." : message;
  }

  private void OnEnable()
  {
    Wallet.OnCoinsChanged += RefreshBalance;
    Ads.OnRewardedAvailabilityChanged += RefreshWatchButton;
    Iap.OnProductsChanged += OnStoreChanged;
    Iap.OnPurchaseFinished += OnPurchaseFinished;
    NoAds.OnChanged += OnStoreChanged;
  }

  private void OnDisable()
  {
    Wallet.OnCoinsChanged -= RefreshBalance;
    Ads.OnRewardedAvailabilityChanged -= RefreshWatchButton;
    Iap.OnProductsChanged -= OnStoreChanged;
    Iap.OnPurchaseFinished -= OnPurchaseFinished;
    NoAds.OnChanged -= OnStoreChanged;
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
