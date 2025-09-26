using UnityEngine;
using UnityEngine.UI;
using System;

public class SettingScreen : MonoBehaviour
{
    [Header("Setting Screen")]
    [SerializeField] private Toggle backgroundMusicToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Button closeSettingsButton;

    private GameObject previousMenu;
    private Action onScreenClosed;

    public void Initialize(Action onScreenClosed = null)
    {
        this.onScreenClosed = onScreenClosed;
        backgroundMusicToggle.isOn = AudioManager.Instance.IsBackgroundMusicEnabled;
        sfxToggle.isOn = AudioManager.Instance.IsSfxEnabled;
        vibrationToggle.isOn = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;
        backgroundMusicToggle.onValueChanged.AddListener(OnBackgroundMusicToggleChanged);
        sfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
        vibrationToggle.onValueChanged.AddListener(OnVibrationToggleChanged);
        closeSettingsButton.onClick.AddListener(CloseSettings);
    }

    public void Show(GameObject originatingMenu = null)
    {
        previousMenu = originatingMenu;

        if (previousMenu != null)
            previousMenu.SetActive(false);

        gameObject.SetActive(true);
    }

    private void OnBackgroundMusicToggleChanged(bool isOn)
    {
        AudioManager.Instance.SetBackgroundMusicEnabled(isOn);
        AudioManager.Instance.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void OnSfxToggleChanged(bool isOn)
    {
        AudioManager.Instance.SetSfxEnabled(isOn);
        AudioManager.Instance.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void OnVibrationToggleChanged(bool isOn)
    {
        AudioManager.Instance.SetVibrationEnabled(isOn);
        AudioManager.Instance.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void CloseSettings()
    {
        onScreenClosed?.Invoke();
        AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
        if (previousMenu != null)
            previousMenu.SetActive(true);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        backgroundMusicToggle.onValueChanged.RemoveListener(OnBackgroundMusicToggleChanged);
        sfxToggle.onValueChanged.RemoveListener(OnSfxToggleChanged);
        vibrationToggle.onValueChanged.RemoveListener(OnVibrationToggleChanged);
        closeSettingsButton.onClick.RemoveListener(CloseSettings);
    }
}
