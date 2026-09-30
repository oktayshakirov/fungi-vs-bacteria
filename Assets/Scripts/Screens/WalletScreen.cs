using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Two screens built from one class, because they share a card, a header, a
// balance chip and a scroll body and differed only in what goes inside:
//
//   STORE     - what coins buy. Boosters and Remove Ads, no tabs.
//   GET COINS - how to get coins. Two tabs: FREE COINS (rewarded ads and the
//               daily streak) and COIN PACKS (the real-money products).
//
// They used to be a single dialog with three tabs (BOOSTERS / COINS / FREE),
// which made "spend" and "earn" the same place and asked the player to find
// the right tab for whichever one they came for. Splitting them means each
// entry point lands somewhere that already answers the question it was
// opened with.
//
// Built entirely in code, with no prefab. Every other screen is a prefab, but
// those predate the code-built skin and each one needed a runtime pass
// (ScreenTheme, HudTheme, MenuLayout) to look right anyway. A new screen has
// nothing to inherit from the prefabs, and building it here means it can be
// changed and re-rendered through UiPreview without opening the editor.
public class WalletScreen : MonoBehaviour
{
  private TMP_Text balanceLabel;
  private Button watchButton;
  private TMP_Text watchLabel;
  private TMP_Text statusLabel;
  private TMP_Text streakCaption;
  private Transform streakPips;
  private bool awaitingAd;
  private Action onClosed;
  private int initialTab;
  private Mode mode;

  // Which of the two screens this instance is.
  public enum Mode { Store, Coins }

  // The shelves and the buttons that switch between them. The store has a
  // single shelf and no tab bar; Get Coins has two.
  private RectTransform storeShelf;
  private RectTransform freeTab;
  private RectTransform packsTab;
  private readonly System.Collections.Generic.List<(Button button, TMP_Text label)> tabButtons =
    new System.Collections.Generic.List<(Button, TMP_Text)>();

  // Tab indices within GET COINS. Free is first and is the default: a player
  // who taps "get coins" is not necessarily saying "sell me something", and
  // the free options should not be the tab they have to go looking for.
  public const int TabFree = 0;
  public const int TabPacks = 1;

  // What coins buy: boosters, and Remove Ads. No tabs.
  public static WalletScreen OpenStore(Transform parent, Action onClosed = null) =>
    Create(parent, Mode.Store, TabFree, onClosed);

  // How to get coins: free first, packs second.
  public static WalletScreen OpenCoins(Transform parent, Action onClosed = null, int tab = TabFree) =>
    Create(parent, Mode.Coins, tab, onClosed);

  private static WalletScreen Create(Transform parent, Mode mode, int tab, Action onClosed)
  {
    var go = new GameObject(mode == Mode.Store ? "StoreScreen" : "GetCoinsScreen",
      typeof(RectTransform));
    go.transform.SetParent(parent, false);
    UiSkin.Stretch((RectTransform)go.transform);

    var screen = go.AddComponent<WalletScreen>();
    screen.onClosed = onClosed;
    screen.mode = mode;
    screen.initialTab = Mathf.Clamp(tab, TabFree, TabPacks);
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
    Header(card, mode == Mode.Store ? "STORE" : "GET COINS",
      mode == Mode.Store ? UiSprites.Store() : UiSprites.Plus());

    // The store is one shelf, so it gets no tab bar at all - a single tab is
    // a control that cannot do anything.
    if (mode == Mode.Coins) TabBar(card);

    RectTransform body = ScrollBody(card);

    if (mode == Mode.Store)
    {
      storeShelf = TabPanel(body, "StoreShelf");
      BoostersSection(storeShelf);
      SectionHeader(storeShelf, "EXTRAS");
      NoAdsRow(storeShelf);
      StatusLine(storeShelf);
    }
    else
    {
      freeTab = TabPanel(body, "FreeTab");
      StreakRow(freeTab);
      WatchAdRow(freeTab);
      Explainer(freeTab);

      packsTab = TabPanel(body, "PacksTab");
      PacksSection(packsTab);

      SelectTab(initialTab);
    }

    RefreshBalance(Wallet.OwnCoins);
    RefreshStreak();
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
    // A gutter down the right for the scrollbar, which is drawn over the
    // content rather than beside it - without this the right-hand end of every
    // row (a price, a buy button) sits underneath the bar.
    contentLayout.padding = new RectOffset(0, 16, 0, 0);
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

  // Title bar: the balance on the left, the title in the middle and a close
  // cross on the right, all inside the card. The balance used to be a
  // full-width row of its own (a coin and three digits in a 92-unit strip that
  // was otherwise empty) and the close button a "BACK" plate outside the card,
  // where it read as part of the screen behind the dialog.
  private void Header(RectTransform parent, string text, Sprite glyph)
  {
    var go = new GameObject("Header", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<LayoutElement>().preferredHeight = 76f;

    // Icon and title ride a centred row rather than the title being a
    // stretched label with the icon hung off a measured offset: TMP's width
    // depends on its own autosizing, so anything that positions the glyph from
    // a predicted text width drifts the moment the font shrinks on a narrower
    // canvas. A layout group measures for us, and the pair stays centred as
    // one unit.
    var rowGo = new GameObject("TitleRow", typeof(RectTransform));
    rowGo.transform.SetParent(go.transform, false);
    var rowRect = UiSkin.Stretch((RectTransform)rowGo.transform);
    // Clear of the balance chip and the cross on either side.
    rowRect.offsetMin = new Vector2(230f, 0f);
    rowRect.offsetMax = new Vector2(-90f, 0f);

    var row = rowGo.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 16f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = false;

    // The same shopfront as every button that opens this screen, so the
    // header answers "which of the two screens is this".
    Image mark = UiSkin.Icon(rowGo.transform, glyph, UiSkin.Gold, 46f);
    mark.raycastTarget = false;
    var markElement = mark.gameObject.AddComponent<LayoutElement>();
    markElement.preferredWidth = 46f;
    markElement.preferredHeight = 46f;
    markElement.flexibleWidth = 0f;

    var titleGo = new GameObject("Title", typeof(RectTransform));
    titleGo.transform.SetParent(rowGo.transform, false);
    var title = titleGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(title, UiSkin.Role.Title);
    title.text = text;
    title.alignment = TextAlignmentOptions.Midline;
    title.fontSizeMax = 64f;
    title.textWrappingMode = TextWrappingModes.NoWrap;
    title.raycastTarget = false;

    // Balance chip
    var chipGo = new GameObject("Balance", typeof(RectTransform));
    chipGo.transform.SetParent(go.transform, false);
    var chip = (RectTransform)chipGo.transform;
    chip.anchorMin = new Vector2(0f, 0.5f);
    chip.anchorMax = new Vector2(0f, 0.5f);
    chip.pivot = new Vector2(0f, 0.5f);
    chip.anchoredPosition = Vector2.zero;
    chip.sizeDelta = new Vector2(210f, 58f);
    UiSkin.Panel(chipGo.AddComponent<Image>(), UiSkin.PanelRaised, UiSkin.RadiusChip);

    Image coin = UiSkin.Icon(chipGo.transform, UiSprites.Coin(), UiSkin.Gold, 38f);
    var coinRect = (RectTransform)coin.transform;
    coinRect.anchorMin = new Vector2(0f, 0.5f);
    coinRect.anchorMax = new Vector2(0f, 0.5f);
    coinRect.pivot = new Vector2(0f, 0.5f);
    coinRect.anchoredPosition = new Vector2(12f, 0f);

    var amountGo = new GameObject("Amount", typeof(RectTransform));
    amountGo.transform.SetParent(chipGo.transform, false);
    var amountRect = UiSkin.Stretch((RectTransform)amountGo.transform);
    amountRect.offsetMin = new Vector2(58f, 0f);
    amountRect.offsetMax = new Vector2(-12f, 0f);
    balanceLabel = amountGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(balanceLabel, UiSkin.Role.Value, UiSkin.Gold);
    balanceLabel.fontSizeMax = 34f;
    balanceLabel.alignment = TextAlignmentOptions.MidlineLeft;
    balanceLabel.raycastTarget = false;

    // Close cross
    var closeGo = new GameObject("Close", typeof(RectTransform));
    closeGo.transform.SetParent(go.transform, false);
    var closeRect = (RectTransform)closeGo.transform;
    closeRect.anchorMin = new Vector2(1f, 0.5f);
    closeRect.anchorMax = new Vector2(1f, 0.5f);
    closeRect.pivot = new Vector2(1f, 0.5f);
    closeRect.anchoredPosition = Vector2.zero;
    closeRect.sizeDelta = new Vector2(64f, 64f);
    closeGo.AddComponent<Image>();
    var close = closeGo.AddComponent<Button>();
    UiSkin.StyleButton(close, UiSkin.Neutral, UiSkin.RadiusChip);
    Image cross = UiSkin.Icon(closeGo.transform, UiSprites.Cross(), UiSkin.TextPrimary, 34f);
    ((RectTransform)cross.transform).anchoredPosition = Vector2.zero;
    close.onClick.AddListener(Close);
  }

  // The two tab buttons - FREE COINS / COIN PACKS - sitting between the title
  // and the scrollable body. A fixed-height row rather than part of the
  // scroll, so it is always visible regardless of which shelf is open.
  // Built only in Coins mode; the store has nothing to switch between.
  private void TabBar(RectTransform parent)
  {
    const float tabHeight = 46f;

    var go = new GameObject("Tabs", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rowElement = go.AddComponent<LayoutElement>();
    // All three pinned explicitly rather than just preferredHeight: left at
    // their defaults (-1, "unset"), min/flexible silently fell through to the
    // HorizontalLayoutGroup on this same object's own computed size instead of
    // this LayoutElement's, and the row rendered as tall as its buttons wanted
    // to be rather than the 46 units asked for here - nearly as tall as the
    // card itself.
    rowElement.minHeight = tabHeight;
    rowElement.preferredHeight = tabHeight;
    rowElement.flexibleHeight = 0f;

    var row = go.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 10f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = true;
    row.childForceExpandHeight = true;

    string[] labels = { "FREE COINS", "COIN PACKS" };
    for (int i = 0; i < labels.Length; i++)
    {
      int index = i;
      Button button = LabelButton(go.transform, "Tab_" + labels[i], UiSkin.PanelRaised,
        UiSkin.TextPrimary, out TMP_Text label);
      label.text = labels[i];
      label.fontSizeMax = 18f;

      // Same fix as the row itself: pin all three so the button cannot come
      // out taller than the row that is supposed to contain it.
      var buttonElement = button.GetComponent<LayoutElement>();
      if (buttonElement == null) buttonElement = button.gameObject.AddComponent<LayoutElement>();
      buttonElement.minHeight = tabHeight;
      buttonElement.preferredHeight = tabHeight;
      buttonElement.flexibleHeight = 0f;

      button.onClick.AddListener(() =>
      {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        SelectTab(index);
      });
      tabButtons.Add((button, label));
    }
  }

  // One shelf's content, stacked and sized to its own content rather than the
  // body's - the body's ContentSizeFitter only sees whichever panel is
  // active, so switching tabs resizes the scrollable area to match.
  private static RectTransform TabPanel(RectTransform parent, string name)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var layout = go.AddComponent<VerticalLayoutGroup>();
    layout.spacing = 20f;
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    return (RectTransform)go.transform;
  }

  private void SelectTab(int index)
  {
    if (freeTab == null || packsTab == null) return;   // store mode: no tabs

    freeTab.gameObject.SetActive(index == TabFree);
    packsTab.gameObject.SetActive(index == TabPacks);

    for (int i = 0; i < tabButtons.Count; i++)
    {
      var (button, label) = tabButtons[i];
      bool active = i == index;
      var image = button.GetComponent<Image>();
      UiSkin.Panel(image, active ? UiSkin.Primary : UiSkin.PanelRaised, UiSkin.RadiusButton);
      label.color = active ? UiSkin.TextDark : UiSkin.TextPrimary;
    }
  }

  // A section label with a hairline running to the right edge, so the long
  // dialog reads as four shelves instead of one undifferentiated list.
  private static void SectionHeader(RectTransform parent, string text)
  {
    var go = new GameObject("Section_" + text, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<LayoutElement>().preferredHeight = 40f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel, UiSkin.TextMuted);
    label.fontSizeMax = 24f;
    label.text = text;
    label.alignment = TextAlignmentOptions.BottomLeft;
    label.raycastTarget = false;
    var labelRect = UiSkin.Stretch((RectTransform)labelGo.transform);
    labelRect.offsetMin = new Vector2(6f, 4f);

    var lineGo = new GameObject("Rule", typeof(RectTransform));
    lineGo.transform.SetParent(go.transform, false);
    var line = (RectTransform)lineGo.transform;
    line.anchorMin = new Vector2(0f, 0f);
    line.anchorMax = new Vector2(1f, 0f);
    line.pivot = new Vector2(0.5f, 0f);
    line.anchoredPosition = Vector2.zero;
    line.sizeDelta = new Vector2(0f, 2f);
    var lineImage = lineGo.AddComponent<Image>();
    lineImage.color = new Color(1f, 1f, 1f, 0.08f);
    lineImage.raycastTarget = false;
  }

  // THE button. There is only one "watch an ad" offer in the game, and what
  // it pays changes: the first ad of the day claims the next rung of the daily
  // streak (200 -> 1,000), and every ad after that pays the flat rate.
  //
  // This used to be two buttons stacked on top of each other, both saying
  // WATCH AD with different numbers, which asked the player to work out which
  // ad they wanted to watch - a question the game already knows the answer to.
  private void WatchAdRow(RectTransform parent)
  {
    var go = new GameObject("WatchAd", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    watchButton = UiSkin.IconButton(go, UiSprites.Coin(), UiSkin.Primary, out watchLabel,
      UiSkin.RadiusButton, UiSkin.Gold);
    watchLabel.alignment = TextAlignmentOptions.Center;
    go.AddComponent<LayoutElement>().preferredHeight = 72f;

    watchButton.onClick.AddListener(OnWatchClicked);

    StatusLine(parent);
  }

  // One line of feedback per screen - "+150 coins!", "Not enough coins.", a
  // purchase result. Both screens need it, and several handlers write to it
  // without knowing which screen they are on, so it is never left null: the
  // store used to report "Not enough coins" into a label that only the free
  // tab had built, which meant a failed purchase said nothing at all.
  private void StatusLine(RectTransform parent)
  {
    var go = new GameObject("Status", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    statusLabel = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(statusLabel, UiSkin.Role.Caption, UiSkin.TextMuted);
    statusLabel.alignment = TextAlignmentOptions.Center;
    statusLabel.text = "";
    go.AddComponent<LayoutElement>().preferredHeight = 30f;
  }

  // What the next ad is worth: the streak rung while today's check-in is still
  // unclaimed, the flat rate afterwards.
  private static int TodayAdReward =>
    DailyStreak.ClaimedToday ? RewardedGate.CoinsPerAd : DailyStreak.TodayReward;

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
    heading.text = "DAILY STREAK";
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

    // Filled by RefreshStreak, which runs at the end of Build() - building
    // them here too would leave ten pips in the row for a frame, which is
    // exactly one frame too many for the editor's preview renderer.
    streakPips = pipsGo.transform;

    // One line saying where in the ladder the player is and what it means for
    // the button below - the card is otherwise five dots and five numbers with
    // nothing tying them to the offer underneath.
    var captionGo = new GameObject("Caption", typeof(RectTransform));
    captionGo.transform.SetParent(go.transform, false);
    streakCaption = captionGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(streakCaption, UiSkin.Role.Caption, UiSkin.TextMuted);
    streakCaption.alignment = TextAlignmentOptions.Center;
    streakCaption.enableAutoSizing = false;
    streakCaption.fontSize = 20f;
    captionGo.AddComponent<LayoutElement>().preferredHeight = 28f;

    // heading(26) + spacing(8) + pips(74) + spacing(8) + caption(28) +
    // padding(12 top + 12 bottom) - recomputed rather than left as a round
    // guess, so changing a row above doesn't leave dead space or clip it.
    go.AddComponent<LayoutElement>().preferredHeight = 168f;
  }

  // Rebuilt rather than restyled in place: claiming moves which pip is filled
  // AND which one is highlighted as today, so every pip in the row changes
  // state anyway. Five objects is not worth an incremental update.
  private void BuildPips()
  {
    if (streakPips == null) return;

    for (int i = streakPips.childCount - 1; i >= 0; i--)
    {
      GameObject pip = streakPips.GetChild(i).gameObject;
      // Destroy() is deferred to the end of the frame, which is a frame too
      // late for the editor's preview renderer - it lays out and shoots
      // immediately and would catch both sets of pips in the row.
      if (Application.isPlaying) Destroy(pip);
      else DestroyImmediate(pip);
    }

    int claimed = DailyStreak.ClaimedInStreak;
    int todayIndex = DailyStreak.CurrentDay - 1;

    for (int i = 0; i < DailyStreak.Length; i++)
    {
      bool isDone = i < claimed;
      bool isToday = !DailyStreak.ClaimedToday && i == todayIndex;
      BuildPip(streakPips, DailyStreak.Rewards[i], isDone, isToday);
    }
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

  // ------------------------------------------------------------- boosters

  // Bought with COINS, not money, which is why this sits above the coin packs:
  // it is the reason to own coins at all. A player who wants a bomb and cannot
  // afford one has the packs directly underneath.
  private readonly System.Collections.Generic.List<Button> boosterButtons =
    new System.Collections.Generic.List<Button>();

  private void BoostersSection(RectTransform parent)
  {
    SectionHeader(parent, "BOOSTERS");
    BuildKitRow(parent);
    foreach (BoosterKind kind in BoosterCatalog.All) BuildBoosterRow(parent, kind);
  }

  // First on the shelf because it is the best deal in the store, and built as
  // a two-line card rather than one long label: "SURVIVAL KIT - 1 OF EACH
  // -25%  1530" auto-sized itself down to fit a single line, so the best offer
  // in the store was also its smallest text.
  private void BuildKitRow(RectTransform parent)
  {
    var go = new GameObject("SurvivalKit", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();
    var kit = go.AddComponent<Button>();
    UiSkin.StyleButton(kit, UiSkin.Primary, UiSkin.RadiusButton);
    go.AddComponent<LayoutElement>().preferredHeight = 88f;

    var row = go.AddComponent<HorizontalLayoutGroup>();
    row.padding = new RectOffset(18, 16, 10, 10);
    row.spacing = 12f;
    row.childAlignment = TextAnchor.MiddleLeft;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = true;

    var textGo = new GameObject("Text", typeof(RectTransform));
    textGo.transform.SetParent(go.transform, false);
    var stack = textGo.AddComponent<VerticalLayoutGroup>();
    stack.spacing = 0f;
    stack.childAlignment = TextAnchor.MiddleLeft;
    stack.childControlWidth = true;
    stack.childControlHeight = true;
    stack.childForceExpandWidth = true;
    stack.childForceExpandHeight = false;
    textGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    var nameGo = new GameObject("Name", typeof(RectTransform));
    nameGo.transform.SetParent(textGo.transform, false);
    var name = nameGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(name, UiSkin.Role.ButtonLabel, UiSkin.TextDark);
    name.text = "SURVIVAL KIT";
    name.alignment = TextAlignmentOptions.MidlineLeft;
    name.textWrappingMode = TextWrappingModes.NoWrap;
    name.fontSizeMax = 30f;
    name.raycastTarget = false;
    nameGo.AddComponent<LayoutElement>().preferredHeight = 34f;

    int percent = Mathf.RoundToInt(BoosterCatalog.KitDiscount * 100f);
    var descGo = new GameObject("Desc", typeof(RectTransform));
    descGo.transform.SetParent(textGo.transform, false);
    var desc = descGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(desc, UiSkin.Role.Caption, new Color(0f, 0f, 0f, 0.62f));
    desc.text = $"One of every booster  -  save {percent}%";
    desc.alignment = TextAlignmentOptions.TopLeft;
    desc.enableAutoSizing = false;
    desc.fontSize = 21f;
    desc.raycastTarget = false;
    descGo.AddComponent<LayoutElement>().preferredHeight = 28f;

    // The price on its own dark plate, so it reads as the thing being paid
    // rather than more of the green button's copy.
    PricePlate(go.transform, BoosterCatalog.KitPrice, 150f);

    kit.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      if (!BoosterInventory.BuyKit())
      {
        if (statusLabel != null) statusLabel.text = "Not enough coins.";
        return;
      }

      Haptics.Play(Haptics.Style.Success);
      boosterRefreshers?.Invoke();
    });

    boosterRefreshers += () => kit.interactable = Wallet.CanAffordOwn(BoosterCatalog.KitPrice);
  }

  // A coin and a number on a dark plate, for prices that are not themselves a
  // button (the kit's, whose whole row is the button).
  private static void PricePlate(Transform parent, int price, float width)
  {
    var go = new GameObject("Price", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var plate = go.AddComponent<Image>();
    UiSkin.Panel(plate, UiSkin.PanelDark, UiSkin.RadiusChip);
    plate.raycastTarget = false;

    var element = go.AddComponent<LayoutElement>();
    element.preferredWidth = width;
    element.flexibleWidth = 0f;

    var row = go.AddComponent<HorizontalLayoutGroup>();
    row.padding = new RectOffset(10, 10, 0, 0);
    row.spacing = 8f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = false;

    Image coin = UiSkin.Icon(go.transform, UiSprites.Coin(), UiSkin.Gold, 26f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 26f;
    coinElement.preferredHeight = 26f;

    var valueGo = new GameObject("Value", typeof(RectTransform));
    valueGo.transform.SetParent(go.transform, false);
    var value = valueGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(value, UiSkin.Role.Value, UiSkin.Gold);
    value.enableAutoSizing = false;
    value.fontSize = 28f;
    value.text = price.ToString("N0");
    value.raycastTarget = false;
  }

  private void BuildBoosterRow(RectTransform parent, BoosterKind kind)
  {
    var go = new GameObject("Booster_" + kind, typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var background = go.AddComponent<Image>();
    UiSkin.Panel(background, UiSkin.PanelRaised, UiSkin.RadiusButton);
    background.raycastTarget = false;

    var row = go.AddComponent<HorizontalLayoutGroup>();
    row.padding = new RectOffset(14, 12, 8, 8);
    row.spacing = 10f;
    row.childAlignment = TextAnchor.MiddleLeft;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = true;
    go.AddComponent<LayoutElement>().preferredHeight = 96f;

    // The glyph on a dark disc ringed in its own colour, so a 38-unit red dot
    // reads as an item icon rather than a stray bullet point.
    var discGo = new GameObject("IconDisc", typeof(RectTransform));
    discGo.transform.SetParent(go.transform, false);
    var discElement = discGo.AddComponent<LayoutElement>();
    discElement.preferredWidth = 66f;
    discElement.flexibleWidth = 0f;
    var discHolder = new GameObject("Disc", typeof(RectTransform));
    discHolder.transform.SetParent(discGo.transform, false);
    var discRect = (RectTransform)discHolder.transform;
    discRect.anchorMin = discRect.anchorMax = new Vector2(0.5f, 0.5f);
    discRect.sizeDelta = new Vector2(66f, 66f);
    var disc = discHolder.AddComponent<Image>();
    disc.sprite = UiSprites.Circle(128);
    Color tint = BoosterCatalog.Tint(kind);
    disc.color = new Color(tint.r * 0.28f, tint.g * 0.28f, tint.b * 0.28f, 1f);
    disc.raycastTarget = false;
    UiSkin.Icon(discHolder.transform, BoosterCatalog.Icon(kind), tint, 40f);

    // Name over description, so the row says what the booster DOES rather than
    // relying on an icon the player has never seen before.
    var textGo = new GameObject("Text", typeof(RectTransform));
    textGo.transform.SetParent(go.transform, false);
    var stack = textGo.AddComponent<VerticalLayoutGroup>();
    stack.spacing = 0f;
    stack.childAlignment = TextAnchor.MiddleLeft;
    stack.childControlWidth = true;
    stack.childControlHeight = true;
    stack.childForceExpandWidth = true;
    stack.childForceExpandHeight = false;
    textGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    var nameGo = new GameObject("Name", typeof(RectTransform));
    nameGo.transform.SetParent(textGo.transform, false);
    var nameLabel = nameGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(nameLabel, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
    nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
    nameLabel.raycastTarget = false;
    nameGo.AddComponent<LayoutElement>().preferredHeight = 30f;

    var descGo = new GameObject("Desc", typeof(RectTransform));
    descGo.transform.SetParent(textGo.transform, false);
    var desc = descGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(desc, UiSkin.Role.Caption, UiSkin.TextMuted);
    desc.text = BoosterCatalog.Description(kind);
    desc.alignment = TextAlignmentOptions.TopLeft;
    desc.raycastTarget = false;
    desc.fontSizeMax = 21f;
    descGo.AddComponent<LayoutElement>().preferredHeight = 44f;

    Button single = BuyButton(go.transform, kind, 1);
    Button bundle = BuyButton(go.transform, kind, BoosterCatalog.BundleSize);

    // The owned count lives in the name line, so the row does not need a fifth
    // column for a number that is usually zero.
    void RefreshRow()
    {
      int owned = BoosterInventory.Count(kind);
      nameLabel.text = owned > 0
        ? $"{BoosterCatalog.Name(kind)}   x{owned}"
        : BoosterCatalog.Name(kind);

      single.interactable = Wallet.CanAffordOwn(BoosterCatalog.Price(kind));
      bundle.interactable = Wallet.CanAffordOwn(BoosterCatalog.BundlePrice(kind));
    }

    boosterRefreshers += RefreshRow;
    RefreshRow();
  }

  private System.Action boosterRefreshers;

  // A two-line price plate: how many on top, the coin price underneath. The
  // bundle's line carries its saving, which was the whole point of offering it
  // and was nowhere on screen - it used to render as "1530" over "x3" squeezed
  // into a single auto-sized label.
  private Button BuyButton(Transform parent, BoosterKind kind, int amount)
  {
    bool bundle = amount >= BoosterCatalog.BundleSize;
    int price = bundle ? BoosterCatalog.BundlePrice(kind) : BoosterCatalog.Price(kind) * amount;
    int full = BoosterCatalog.Price(kind) * amount;
    int saving = full > 0 ? Mathf.RoundToInt((1f - price / (float)full) * 100f) : 0;

    var go = new GameObject(bundle ? "BuyBundle" : "Buy", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, bundle ? new Color(0.30f, 0.36f, 0.20f) : UiSkin.Neutral,
      UiSkin.RadiusButton);

    var element = go.AddComponent<LayoutElement>();
    element.preferredWidth = 136f;
    element.flexibleWidth = 0f;

    var capGo = new GameObject("Amount", typeof(RectTransform));
    capGo.transform.SetParent(go.transform, false);
    var capRect = (RectTransform)capGo.transform;
    capRect.anchorMin = new Vector2(0f, 0.55f);
    capRect.anchorMax = new Vector2(1f, 1f);
    capRect.offsetMin = new Vector2(6f, 0f);
    capRect.offsetMax = new Vector2(-6f, -4f);
    var cap = capGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(cap, UiSkin.Role.ButtonLabel, bundle ? UiSkin.Primary : UiSkin.TextMuted);
    cap.fontSizeMax = 20f;
    cap.text = bundle && saving > 0 ? $"x{amount}  -{saving}%" : $"x{amount}";
    cap.alignment = TextAlignmentOptions.Midline;
    cap.raycastTarget = false;

    var priceGo = new GameObject("Price", typeof(RectTransform));
    priceGo.transform.SetParent(go.transform, false);
    var priceRect = (RectTransform)priceGo.transform;
    priceRect.anchorMin = new Vector2(0f, 0f);
    priceRect.anchorMax = new Vector2(1f, 0.58f);
    priceRect.offsetMin = new Vector2(8f, 6f);
    priceRect.offsetMax = new Vector2(-8f, 0f);
    var row = priceGo.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 6f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = false;

    Image coin = UiSkin.Icon(priceGo.transform, UiSprites.Coin(), UiSkin.Gold, 22f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 22f;
    coinElement.preferredHeight = 22f;

    var valueGo = new GameObject("Value", typeof(RectTransform));
    valueGo.transform.SetParent(priceGo.transform, false);
    var value = valueGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(value, UiSkin.Role.Value, UiSkin.Gold);
    value.enableAutoSizing = false;
    value.fontSize = 26f;
    value.text = price.ToString("N0");
    value.raycastTarget = false;

    button.onClick.AddListener(() =>
    {
      if (!BoosterInventory.Buy(kind, amount))
      {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        if (statusLabel != null) statusLabel.text = "Not enough coins.";
        return;
      }

      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      Haptics.Play(Haptics.Style.Success);
      boosterRefreshers?.Invoke();
    });

    boosterButtons.Add(button);
    return button;
  }

  // ------------------------------------------------------------ purchases

  // Rebuilt from scratch whenever prices arrive: the store answers
  // asynchronously and may answer after this screen is already open, so the
  // section cannot be built once from whatever was known at Build() time.
  private RectTransform packsSection;
  private TMP_Text packsStatus;

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
      // A lime pill, so the bonus reads as a deal sticker rather than a stray
      // caption floating between the amount and the price.
      var slotGo = new GameObject("Bonus", typeof(RectTransform));
      slotGo.transform.SetParent(go.transform, false);
      slotGo.AddComponent<LayoutElement>().preferredWidth = 110f;

      var pillGo = new GameObject("Pill", typeof(RectTransform));
      pillGo.transform.SetParent(slotGo.transform, false);
      var pillRect = (RectTransform)pillGo.transform;
      pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 0.5f);
      pillRect.sizeDelta = new Vector2(104f, 40f);
      var pill = pillGo.AddComponent<Image>();
      UiSkin.Panel(pill, UiSkin.Primary, UiSkin.RadiusChip);
      pill.raycastTarget = false;

      var badgeGo = new GameObject("Label", typeof(RectTransform));
      badgeGo.transform.SetParent(pillGo.transform, false);
      UiSkin.Stretch((RectTransform)badgeGo.transform);
      var badge = badgeGo.AddComponent<TextMeshProUGUI>();
      UiSkin.Label(badge, UiSkin.Role.ButtonLabel, UiSkin.TextDark);
      badge.fontSizeMax = 22f;
      badge.text = $"+{bonus}%";
      badge.alignment = TextAlignmentOptions.Midline;
      badge.raycastTarget = false;
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

  // Remove Ads, bought with coins only (NoAds.CoinPrice) - there is no
  // real-money product for it.
  //
  // Once bought the row collapses to a single disabled "ADS REMOVED" plate and
  // the buy button is not offered again: NoAds refuses a second purchase
  // anyway, but a live button that does nothing reads as a bug.
  private GameObject noAdsRow;
  private Button noAdsCoinsButton;
  private TMP_Text noAdsCoinsLabel;
  private GameObject noAdsDone;

  private void NoAdsRow(RectTransform parent)
  {
    noAdsRow = new GameObject("NoAds", typeof(RectTransform));
    noAdsRow.transform.SetParent(parent, false);
    noAdsRow.AddComponent<LayoutElement>().preferredHeight = 88f;

    var row = noAdsRow.AddComponent<HorizontalLayoutGroup>();
    row.spacing = 10f;
    row.childAlignment = TextAnchor.MiddleCenter;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = true;
    row.childForceExpandHeight = true;

    var coinsGo = new GameObject("Coins", typeof(RectTransform));
    coinsGo.transform.SetParent(noAdsRow.transform, false);
    noAdsCoinsButton = UiSkin.IconButton(coinsGo, UiSprites.Coin(), UiSkin.Primary,
      out noAdsCoinsLabel, UiSkin.RadiusButton, UiSkin.Gold);
    UiSkin.Label(noAdsCoinsLabel, UiSkin.Role.ButtonLabel, UiSkin.TextDark);
    noAdsCoinsLabel.alignment = TextAlignmentOptions.Midline;
    noAdsCoinsLabel.textWrappingMode = TextWrappingModes.NoWrap;
    noAdsCoinsLabel.enableAutoSizing = true;
    noAdsCoinsLabel.fontSizeMin = 14f;
    noAdsCoinsLabel.fontSizeMax = 26f;
    noAdsCoinsLabel.text = $"REMOVE ADS   {NoAds.CoinPrice:N0}";
    coinsGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
    noAdsCoinsButton.onClick.AddListener(OnNoAdsWithCoins);

    // The "already done" state, built once and swapped in; a separate plate
    // rather than relabelling the buy button, so it spans the row.
    Button done = LabelButton(parent, "NoAdsDone", UiSkin.Neutral, UiSkin.TextPrimary,
      out TMP_Text doneLabel);
    done.interactable = false;
    doneLabel.text = "ADS REMOVED - THANK YOU";
    done.GetComponent<LayoutElement>().preferredHeight = 88f;
    noAdsDone = done.gameObject;
  }

  private static Button LabelButton(Transform parent, string name, Color tint, Color text,
    out TMP_Text label)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, tint, UiSkin.RadiusButton);
    go.AddComponent<LayoutElement>();

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel, text);
    label.alignment = TextAlignmentOptions.Midline;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.enableAutoSizing = true;
    label.fontSizeMin = 16f;
    label.fontSizeMax = 30f;
    label.raycastTarget = false;
    UiSkin.Stretch(label.rectTransform);
    label.margin = new Vector4(12f, 0f, 12f, 0f);
    return button;
  }

  private void OnNoAdsWithCoins()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

    if (!NoAds.BuyWithCoins())
    {
      if (statusLabel != null) statusLabel.text = "Not enough coins.";
      return;
    }

    if (statusLabel != null) statusLabel.text = "Ads removed.";
    RefreshStore();
  }

  // Draws the purchase rows from whatever the store currently knows. Called at
  // build time and again on every price update and finished purchase.
  private void RefreshStore()
  {
    // The store has no packs section and the coins screen has no no-ads row,
    // so each half is guarded on its own - an early return on a null
    // packsSection left the store's Remove Ads row never refreshed.
    RefreshNoAdsRow();
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
    // The section has its own header now, so the line only speaks when there
    // is nothing to show.
    packsStatus.text = shown > 0 ? "" : "Coin packs are unavailable right now.";
    packsStatus.gameObject.SetActive(shown == 0);
    packsStatus.transform.SetAsFirstSibling();
  }

  private void RefreshNoAdsRow()
  {
    if (noAdsRow == null) return;

    bool removed = NoAds.Active;
    noAdsRow.SetActive(!removed);
    noAdsDone.SetActive(removed);
    if (removed) return;

    // Dimmed, not hidden, when the player is short, so the price reads as a
    // goal rather than the button vanishing.
    noAdsCoinsButton.interactable = Wallet.CanAffordOwn(NoAds.CoinPrice);
  }


  // The small print. Two lines: what coins are for, and the one thing about
  // them that can surprise a player. The cooldown, the daily cap, the level
  // loan and the continue price all used to be spelled out here - five lines
  // of rules for a screen whose buttons already state their own terms.
  private void Explainer(RectTransform parent)
  {
    var go = new GameObject("Explainer", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Caption, UiSkin.TextMuted);
    label.enableAutoSizing = false;
    label.fontSize = 20f;
    label.alignment = TextAlignmentOptions.TopLeft;
    label.margin = new Vector4(6f, 0f, 6f, 0f);
    label.text =
      "Coins buy towers, boosters and a continue after a loss. Earn them by " +
      "clearing levels, watching ads or buying a pack.\n" +
      // Said plainly, because a reinstall loses the wallet along with anything
      // bought with it, including Remove Ads, and there is nothing to restore
      // it from - unlike a subscription or a real-money purchase.
      "Coins are saved on this device and do not carry over to a reinstall.";
  }

  private void OnWatchClicked()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

    // The streak claim is the first ad of the day and is NOT gated by the
    // wallet's cooldown or daily cap (see DailyStreak); everything after it is.
    bool streakClaim = !DailyStreak.ClaimedToday;
    if (!streakClaim && !RewardedGate.IsReady) return;

    awaitingAd = true;
    statusLabel.text = "";
    RefreshWatchButton();

    Ads.ShowRewarded(
      _ =>
      {
        awaitingAd = false;

        int amount;
        if (streakClaim)
        {
          // Claim() pays the wallet itself and advances the ladder.
          amount = DailyStreak.Claim();
          statusLabel.text = $"Day {DailyStreak.CurrentDay} claimed: +{amount}!";
        }
        else
        {
          // Recorded only on a completed watch, so a failed or skipped ad never
          // costs the player a slot or starts a cooldown.
          RewardedGate.RecordWatch();
          amount = RewardedGate.CoinsPerAd;
          Wallet.Add(amount);
          statusLabel.text = $"+{amount} coins!";
        }

        Ads.DeferInterstitial();
        Haptics.Play(Haptics.Style.Success);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        RefreshStreak();
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

    // Midnight can pass with this screen open: the streak becomes claimable
    // again and the whole card changes meaning. Compared rather than rebuilt
    // every tick, so the pips are not thrown away once a second.
    if (streakCaption != null && lastClaimedToday != DailyStreak.ClaimedToday)
    {
      lastClaimedToday = DailyStreak.ClaimedToday;
      RefreshStreak();
    }

    RefreshWatchButton();
  }

  private bool lastClaimedToday;

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
    // The store balance excludes a level's loan, which only buys towers.
    if (balanceLabel != null) balanceLabel.text = Wallet.OwnCoins.ToString("N0");
    // Affordability moves with the balance, so the booster buttons have to be
    // re-evaluated here and not only when something is bought - watching an ad
    // in this same dialog can make a booster affordable.
    boosterRefreshers?.Invoke();
    RefreshNoAdsRow();
  }

  // Says where the player is in the ladder, and what the button under it is
  // about to pay. Rebuilding the pips would mean rebuilding the row, so only
  // the caption moves - the pips are correct until midnight either way.
  private void RefreshStreak()
  {
    if (streakCaption == null) return;

    lastClaimedToday = DailyStreak.ClaimedToday;
    BuildPips();

    streakCaption.text = DailyStreak.ClaimedToday
      ? $"Day {DailyStreak.CurrentDay} claimed - come back tomorrow for more"
      : $"Day {DailyStreak.CurrentDay} of {DailyStreak.Length} - today's check-in is worth {DailyStreak.TodayReward}";
  }

  // The button stays visible when no ad is loaded, just disabled and labelled.
  // Hiding it makes the wallet look broken and gives the player nothing to
  // understand; a greyed button with a reason does not.
  private void RefreshWatchButton()
  {
    if (watchButton == null) return;

    bool streakClaim = !DailyStreak.ClaimedToday;
    int reward = TodayAdReward;

    // The states, in the order the player runs into them. The cap and the
    // cooldown are skipped while the streak is unclaimed: that ad is the daily
    // check-in and the gate has no say over it.
    if (!streakClaim && RewardedGate.CapReached)
    {
      watchButton.interactable = false;
      watchLabel.text = "BACK TOMORROW";
    }
    else if (!streakClaim && !RewardedGate.IsReady)
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
      // Same words either way, because it is the same action - only the number
      // moves, which is the whole point of merging the two buttons.
      watchLabel.text = $"WATCH AD  +{reward}";
    }
  }

  private void Close()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    onClosed?.Invoke();
    Destroy(gameObject);
  }
}
