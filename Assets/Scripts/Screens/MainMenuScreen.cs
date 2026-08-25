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
    CoinChip.Create(settingsButton != null ? settingsButton.transform.parent : transform,
      OnWalletClicked);

    // Cold launch only. The ad SDK does its main-thread startup work behind
    // this rather than over a live menu.
    if (BootSplash.ShouldShow && screensTransform != null)
    {
      BootSplash.Create(screensTransform);
    }
  }

  private void OnWalletClicked()
  {
    if (screensTransform == null) return;
    WalletScreen.Open(screensTransform);
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
