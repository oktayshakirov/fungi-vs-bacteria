using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

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

        ScreenTheme.ApplySettingsScreen(transform, closeSettingsButton);

        if (PrivacyOptionsRequired())
        {
            BuildPrivacyButton(transform, () =>
            {
                AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
                LevelPlayAds.Instance?.ShowPrivacyOptionsForm();
            });
        }
    }

    // GDPR, and Google's EU user consent policy that the UMP SDK exists to
    // satisfy, require that a player who answered the consent form at launch
    // can change that answer later. LevelPlayAds always had the form; nothing
    // ever opened it.
    //
    // Shown ONLY where UMP says it is required (EEA/UK), so everyone else never
    // sees a setting that does nothing for them. Guarded because UMP has no
    // consent state in the editor or before its first update, and a throw here
    // would take the whole settings screen down with it.
    private static bool PrivacyOptionsRequired()
    {
        try
        {
            return LevelPlayAds.Instance != null && LevelPlayAds.IsPrivacyOptionsRequired;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // A corner plate, bottom-left, in the same style as every other corner
    // control in the game (ScreenTheme.CornerButton). Not a fourth row under
    // the toggles: their labels are display-font sized and the last row already
    // sits within ~80 units of the bottom edge, so a row there would crowd it.
    //
    // Public and static so UiPreview can build it without a live consent state,
    // which never exists in the editor.
    public static Button BuildPrivacyButton(Transform root, Action onClick)
    {
        Transform host = root.Find("SafeArea") ?? root;

        var go = new GameObject("PrivacyOptions", typeof(RectTransform));
        go.transform.SetParent(host, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(ScreenTheme.HeaderInset, ScreenTheme.HeaderInset);
        rect.sizeDelta = new Vector2(250f, 64f);

        go.AddComponent<Image>();
        var button = go.AddComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "PRIVACY OPTIONS";

        ScreenTheme.CornerButton(button);
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 24f;

        go.transform.SetAsLastSibling();
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
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
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void OnSfxToggleChanged(bool isOn)
    {
        AudioManager.Instance.SetSfxEnabled(isOn);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void OnVibrationToggleChanged(bool isOn)
    {
        AudioManager.Instance.SetVibrationEnabled(isOn);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.Toggle);
    }

    private void CloseSettings()
    {
        onScreenClosed?.Invoke();
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
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
