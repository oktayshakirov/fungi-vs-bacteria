using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
  [SerializeField] private Button playButton;
  [SerializeField] private Button settingsButton;

  [Header("Additional Screens")]
  [SerializeField] private GameObject environmentSelectionScreenPrefab;
  [SerializeField] private GameObject levelSelectionScreenPrefab;
  [SerializeField] private GameObject settingsScreenPrefab;

  [Header("References")]
  [SerializeField] private Canvas mainCanvas;

  private Transform screensTransform;

  private void Start()
  {
    if (mainCanvas == null)
    {
      Debug.LogWarning("Main Canvas reference is missing in MainMenu!");
      return;
    }
    screensTransform = mainCanvas.transform;

    playButton.onClick.AddListener(OnPlayClicked);
    settingsButton.onClick.AddListener(OnSettingsClicked);

    ScreenTheme.ApplyMainMenu(transform, playButton, settingsButton);

    // Hosted in the settings button's parent rather than on the menu root: that
    // is the rect MenuLayout anchors against, so the chip and the gear share a
    // coordinate space and stay in the same band on every aspect ratio.
    Transform cornerHost = settingsButton != null ? settingsButton.transform.parent : transform;
    // A readout now, not a button: the two labelled pills under it are the
    // way in, so nobody has to read a "+" as "shop".
    CoinChip.Create(cornerHost);

    // Two pills under the balance, in the order a player needs them: how to
    // GET coins, then what to SPEND them on.
    BuildCoinsButton(cornerHost);
    BuildStoreButton(cornerHost);

    // Cold launch only. The ad SDK does its main-thread startup work behind
    // this rather than over a live menu.
    if (BootSplash.ShouldShow && screensTransform != null)
    {
      BootSplash.Create(screensTransform);
    }
  }

  // Two compact pills directly under the coin chip, same left inset, stacked
  // as one column with the balance above them.
  //
  // Both are coloured from the skin's own palette: GET COINS is Gold, the
  // currency colour, and carries the "+" that used to live inside the chip;
  // STORE is Primary green, the same green as PLAY and the title, so the menu
  // reads as one system rather than three unrelated colours. A grey STORE
  // plate read as disabled next to the gold pill above it.
  // Matched to the coin chip above them, so the three read as one flush
  // column rather than a narrow chip over two wider plates.
  private const float PillWidth = 190f;
  private const float PillHeight = 54f;
  private const float ChipHeight = 76f;

  private void BuildCoinsButton(Transform parent)
  {
    Button button = BuildPill(parent, "GetCoinsButton", "GET COINS", UiSprites.Plus(),
      UiSkin.Gold, UiSkin.TextDark, MenuLayout.CornerInset + ChipHeight + 10f);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      if (screensTransform != null) WalletScreen.OpenCoins(screensTransform);
    });
  }

  private void BuildStoreButton(Transform parent)
  {
    Button button = BuildPill(parent, "StoreButton", "STORE", UiSprites.Bag(),
      UiSkin.Primary, UiSkin.TextDark,
      MenuLayout.CornerInset + ChipHeight + 10f + PillHeight + 8f);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      if (screensTransform != null) WalletScreen.OpenStore(screensTransform);
    });
  }

  private static Button BuildPill(Transform parent, string name, string text, Sprite icon,
    Color tint, Color textColor, float fromTop)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = new Vector2(MenuLayout.CornerInset, -fromTop);
    rect.sizeDelta = new Vector2(PillWidth, PillHeight);

    Button button = UiSkin.IconButton(go, icon, tint, out TMP_Text label,
      UiSkin.RadiusChip, textColor);
    label.text = text;
    label.alignment = TextAlignmentOptions.MidlineLeft;
    return button;
  }

  private void OnPlayClicked()
  {
    if (environmentSelectionScreenPrefab != null && screensTransform != null)
    {
      GameObject envScreenGO = Instantiate(environmentSelectionScreenPrefab, screensTransform);
      EnvironmentsScreen envScreen = envScreenGO.GetComponent<EnvironmentsScreen>();
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      if (envScreen != null)
      {
        envScreen.SetLevelSelectionPrefab(levelSelectionScreenPrefab);
        // The menu deactivates itself below, so the environments screen needs a
        // handle on it to come back — it has no other way to find it.
        envScreen.SetReturnTarget(gameObject);
      }
      gameObject.SetActive(false);
    }
  }

  private void OnSettingsClicked()
  {
    if (settingsScreenPrefab != null && screensTransform != null)
    {
      GameObject settingsScreenGO = Instantiate(settingsScreenPrefab, screensTransform);
      SettingScreen settingScreen = settingsScreenGO.GetComponent<SettingScreen>();
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      settingScreen.Initialize(() =>
      {
        gameObject.SetActive(true);
      });
      gameObject.SetActive(false);
    }
  }
}
