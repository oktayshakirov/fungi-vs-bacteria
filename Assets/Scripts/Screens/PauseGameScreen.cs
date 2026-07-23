using UnityEngine;
using UnityEngine.UI;
using System;

public class PauseGameScreen : MonoBehaviour
{
  [Header("Pause Game Screen")]
  [SerializeField] private Button resumeGameButton;
  [SerializeField] private Button settingsButton;
  [SerializeField] private Button returnToMainMenuButton;

  private Action onScreenClosed;
  private SettingScreen activeSettings;

  public void Initialize(Action onScreenClosed)
  {
    this.onScreenClosed = onScreenClosed;

    resumeGameButton.onClick.AddListener(ResumeGame);
    returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);

    if (settingsButton != null)
    {
      settingsButton.onClick.AddListener(OpenSettings);
    }
  }

  public void Show()
  {
    gameObject.SetActive(true);
    GameManager.Instance.PauseGame();
  }

  private void ResumeGame()
  {
    gameObject.SetActive(false);
    GameManager.Instance.ResumeGame();
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    onScreenClosed?.Invoke();
  }

  // Opens Settings on top of the paused game, without unloading the level.
  private void OpenSettings()
  {
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);

    SettingScreen prefab = Resources.Load<SettingScreen>("Screens/SettingsScreen");
    if (prefab == null)
    {
      Debug.LogError("SettingsScreen prefab not found at Resources/Screens/SettingsScreen!");
      return;
    }

    activeSettings = Instantiate(prefab, transform.parent);
    activeSettings.Initialize();
    // Show() hides this pause screen and re-shows it when settings close
    activeSettings.Show(gameObject);
  }

  private void ReturnToMainMenu()
  {
    gameObject.SetActive(false);
    GameManager.Instance.ReturnToMainMenu();
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    onScreenClosed?.Invoke();
  }
}
