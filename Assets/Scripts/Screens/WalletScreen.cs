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
  private const int RewardFallback = 75;

  private TMP_Text balanceLabel;
  private Button watchButton;
  private TMP_Text watchLabel;
  private TMP_Text statusLabel;
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

  private void Explainer(RectTransform parent)
  {
    var go = new GameObject("Explainer", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Body, UiSkin.TextMuted);
    label.alignment = TextAlignmentOptions.TopLeft;
    label.text =
      $"Coins carry over between levels.\n\n" +
      $"- Start a level with +{Boosters.StartBoostGold} gold for {Boosters.StartBoostCost} coins.\n" +
      $"- Continue a lost run with +{Boosters.ContinueHealth} health for {Boosters.ContinueCost} coins.\n" +
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

    watchButton.interactable = false;
    statusLabel.text = "Loading ad...";

    Ads.ShowRewarded(
      amount =>
      {
        Wallet.Add(amount);
        Ads.DeferInterstitial();
        Haptics.Play(Haptics.Style.Success);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        statusLabel.text = $"+{amount} coins!";
        RefreshWatchButton();
      },
      () =>
      {
        statusLabel.text = "No ad available right now. Try again shortly.";
        RefreshWatchButton();
      });
  }

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
    if (watchButton == null) return;

    bool ready = Ads.IsRewardedReady;
    watchButton.interactable = ready;
    watchLabel.text = ready ? $"WATCH AD  +{RewardFallback}" : "AD UNAVAILABLE";
  }

  private void Close()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    onClosed?.Invoke();
    Destroy(gameObject);
  }
}
