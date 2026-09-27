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
    // The "+" is a player saying "I want more coins" - it opens straight to
    // the Coins tab rather than landing on Boosters like every other entry
    // point into the store.
    CoinChip.Create(cornerHost, () => OpenWallet(WalletScreen.TabCoins));

    // The coin chip's "+" already opened the store, but nothing on the menu
    // said a store existed - a player had to notice a plus sign meant "spend
    // coins". A labelled button right under the balance says so directly.
    BuildStoreButton(cornerHost);

    // Cold launch only. The ad SDK does its main-thread startup work behind
    // this rather than over a live menu.
    if (BootSplash.ShouldShow && screensTransform != null)
    {
      BootSplash.Create(screensTransform);
    }
  }

  private void OpenWallet(int tab)
  {
    if (screensTransform == null) return;
    WalletScreen.Open(screensTransform, tab: tab);
  }

  // A compact "STORE" pill directly under the coin chip, same width, same
  // corner inset - reads as one column with the balance above it rather than
  // a second, unrelated control.
  private void BuildStoreButton(Transform parent)
  {
    var go = new GameObject("StoreButton", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = new Vector2(MenuLayout.CornerInset, -MenuLayout.CornerInset - 76f - 10f);
    rect.sizeDelta = new Vector2(230f, 54f);

    // Gold, matching the "VS" in the menu title.
    Button button = UiSkin.IconButton(go, UiSprites.Bag(), UiSkin.Gold, out TMP_Text label,
      UiSkin.RadiusChip, UiSkin.TextDark);
    label.text = "STORE";
    label.alignment = TextAlignmentOptions.MidlineLeft;

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      OpenWallet(WalletScreen.TabBoosters);
    });
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
