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
  }

  private void OnPlayClicked()
  {
    if (environmentSelectionScreenPrefab != null && screensTransform != null)
    {
      GameObject envScreenGO = Instantiate(environmentSelectionScreenPrefab, screensTransform);
      EnvironmentsScreen envScreen = envScreenGO.GetComponent<EnvironmentsScreen>();
      AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
      if (envScreen != null)
      {
        envScreen.SetLevelSelectionPrefab(levelSelectionScreenPrefab);
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
      AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
      settingScreen.Initialize(() =>
      {
        gameObject.SetActive(true);
      });
      gameObject.SetActive(false);
    }
  }
}
