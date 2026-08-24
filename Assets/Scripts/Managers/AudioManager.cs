using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[Serializable]
public class VolumeData
{
    public AudioManager.SoundType type;
    public AudioClip clip;
    [Range(0f, 1f)]
    public float volume = 1f;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public enum SoundType
    {
        BackgroundMusic,
        ButtonClick,
        EnemyDeath,
        EnvironmentPicked,
        GameOver,
        LevelPicked,
        Loading,
        Projectile,
        Sell,
        StartWave,
        TowerDrop,
        TowerDrag,
        TargetHit,
        Toggle,
        BaseDamage,
        Victory
    }

    [Header("Volume Settings")]
    public VolumeData[] sounds;

    private Dictionary<SoundType, VolumeData> soundDictionary;
    private AudioSource sfxSource;
    private AudioSource musicSource;

    private bool isMusicMuted = false;
    private bool isSfxMuted = false;
    private bool isVibrationEnabled = true;

    public bool IsBackgroundMusicEnabled => !isMusicMuted;
    public bool IsSfxEnabled => !isSfxMuted;
    public bool IsVibrationEnabled => isVibrationEnabled;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _fvbSetAudioSessionPlayback();
#endif

    // iOS silences the Ambient and SoloAmbient categories when the hardware
    // Ring/Silent switch is on, and those are the only two Unity's "Mute Other
    // Audio Sources" setting picks between - so the game was silent on a
    // switched-off phone no matter what that setting said. Playback is the
    // category that ignores the switch, and it can only be selected natively.
    //
    // Public because it has to be re-applied: a full-screen video ad
    // reconfigures the shared session, leaving the game muted again afterwards.
    public void ApplyPlaybackAudioSession()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            _fvbSetAudioSessionPlayback();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Audio] Could not apply the playback session: {e.Message}");
        }
#endif
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyPlaybackAudioSession();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // Ads are the only thing in the game that takes the audio session away.
        Ads.OnFullScreenAdClosed += ApplyPlaybackAudioSession;
    }

    private void OnDisable()
    {
        Ads.OnFullScreenAdClosed -= ApplyPlaybackAudioSession;
    }

    private void Start()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();
        BuildSoundDictionary();
        LoadSettings();

        if (soundDictionary.TryGetValue(SoundType.BackgroundMusic, out VolumeData bgMusic) && bgMusic.clip != null)
        {
            musicSource.clip = bgMusic.clip;
            musicSource.loop = true;
            musicSource.volume = bgMusic.volume;
            musicSource.Play();
        }
    }

    private void BuildSoundDictionary()
    {
        soundDictionary = new Dictionary<SoundType, VolumeData>();
        foreach (var sound in sounds)
        {
            soundDictionary[sound.type] = sound;
        }
    }

    public void PlaySound(SoundType type)
    {
        // Haptics run before the mute check on purpose: silencing sound effects
        // should not silence the buttons' feel. The vibration toggle is what
        // gates this, inside Haptics.
        PlayHaptic(type);

        if (isSfxMuted) return;

        if (soundDictionary.TryGetValue(type, out VolumeData data) && data.clip != null)
        {
            sfxSource.PlayOneShot(data.clip, data.volume);
        }
    }

    // Every UI press in the game already routes through PlaySound, so hooking
    // haptics in here gives the whole interface feedback in one place instead of
    // per button. Gameplay haptics (hits, deaths) should call Haptics directly.
    private static void PlayHaptic(SoundType type)
    {
        switch (type)
        {
            // Toggles and list selections: the lightest tick.
            case SoundType.Toggle:
            case SoundType.EnvironmentPicked:
            case SoundType.LevelPicked:
                Haptics.Play(Haptics.Style.Selection);
                break;

            // Ordinary taps.
            case SoundType.ButtonClick:
            case SoundType.TowerDrag:
                Haptics.Play(Haptics.Style.Light);
                break;

            // Presses that commit to something.
            case SoundType.TowerDrop:
            case SoundType.StartWave:
            case SoundType.Sell:
                Haptics.Play(Haptics.Style.Medium);
                break;

            case SoundType.Victory:
                Haptics.Play(Haptics.Style.Success);
                break;

            case SoundType.GameOver:
                Haptics.Play(Haptics.Style.Failure);
                break;

            // Everything else (music, projectiles, per-hit effects) stays silent
            // to the touch — firing on those would buzz continuously.
        }
    }

    public void ToggleMusic(bool isMuted)
    {
        isMusicMuted = isMuted;
        musicSource.mute = isMuted;
        PlayerPrefs.SetInt("MusicEnabled", isMuted ? 0 : 1);
        PlayerPrefs.Save();
    }

    public void ToggleSFX(bool isMuted)
    {
        isSfxMuted = isMuted;
        sfxSource.mute = isMuted;
        PlayerPrefs.SetInt("SoundEffectsEnabled", isMuted ? 0 : 1);
        PlayerPrefs.Save();
    }

    public void SetBackgroundMusicEnabled(bool enabled)
    {
        ToggleMusic(!enabled);
    }

    public void SetSfxEnabled(bool enabled)
    {
        ToggleSFX(!enabled);
    }

    public void SetVibrationEnabled(bool enabled)
    {
        isVibrationEnabled = enabled;
        PlayerPrefs.SetInt("VibrationEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Fires a short device vibration for meaningful events (base hit, defeat).
    // Handheld.Vibrate is only meaningful on a real handheld, so it is guarded.
    public void Vibrate()
    {
        if (!isVibrationEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
        if (Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Handheld.Vibrate();
        }
#endif
    }

    public void SetSoundVolume(SoundType type, float volume)
    {
        if (soundDictionary.TryGetValue(type, out VolumeData data))
        {
            data.volume = volume;
            PlayerPrefs.SetFloat($"SoundVolume_{type}", volume);
            PlayerPrefs.Save();

            // Update music volume immediately if it's the background music
            if (type == SoundType.BackgroundMusic)
            {
                musicSource.volume = volume;
            }
        }
    }

    private void LoadSettings()
    {
        // Load mute states
        bool musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        ToggleMusic(!musicEnabled);

        bool sfxEnabled = PlayerPrefs.GetInt("SoundEffectsEnabled", 1) == 1;
        ToggleSFX(!sfxEnabled);

        isVibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;

        // Load individual sound volumes
        foreach (var sound in sounds)
        {
            float savedVolume = PlayerPrefs.GetFloat($"SoundVolume_{sound.type}", sound.volume);
            sound.volume = savedVolume;
        }
    }
}
