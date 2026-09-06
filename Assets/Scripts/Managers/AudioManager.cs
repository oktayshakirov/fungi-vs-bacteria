using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
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
        Ads.OnFullScreenAdWillShow += DuckMusicForAd;
        Ads.OnFullScreenAdClosed += RestoreMusicAfterAd;
    }

    private void OnDisable()
    {
        Ads.OnFullScreenAdWillShow -= DuckMusicForAd;
        Ads.OnFullScreenAdClosed -= RestoreMusicAfterAd;
    }

    private Coroutine musicFade;

    // An ad taking over the audio session mid-playback is what the player hears
    // as a crackle, both going in and coming out. Fading the music down first
    // and back up afterwards means the switch happens in silence, so there is
    // nothing audible to glitch - far more reliable than trying to make the
    // session change itself seamless.
    private void DuckMusicForAd()
    {
        if (musicSource == null) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(0f, 0.15f, pauseAtEnd: true));
    }

    private void RestoreMusicAfterAd()
    {
        ApplyPlaybackAudioSession();

        if (musicSource == null || isMusicMuted) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(RestoreMusicRoutine());
    }

    private IEnumerator RestoreMusicRoutine()
    {
        // The session restore above is asynchronous and the ad SDK is still
        // tearing its player down; coming back instantly is what produced the
        // crackle on returning to the game.
        yield return new WaitForSecondsRealtime(0.35f);

        musicSource.UnPause();
        float target = soundDictionary != null
            && soundDictionary.TryGetValue(SoundType.BackgroundMusic, out VolumeData bg)
            ? bg.volume : 1f;
        yield return FadeMusic(target, 0.4f, pauseAtEnd: false);
    }

    private IEnumerator FadeMusic(float target, float seconds, bool pauseAtEnd)
    {
        float from = musicSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(from, target, t / seconds);
            yield return null;
        }
        musicSource.volume = target;

        // Paused rather than stopped: Stop() would restart the track from the
        // beginning when the player comes back from a 30 second ad.
        if (pauseAtEnd) musicSource.Pause();
        musicFade = null;
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
            StartCoroutine(PlayMusicAfterLaunchSettles());
        }
    }

    // Cold launch is the single busiest moment in the app's life: the engine is
    // still standing up its own audio session, and LevelPlayAds.Start() is
    // about to fire consent/init work (network calls, ATT, and native ad SDK
    // setup that spawns its own WebViews) on the very same frame. Starting
    // playback into that window is what produced the reported crackle - a
    // fresh AudioSource.Play() landing while the OS is still negotiating the
    // audio route glitches audibly. A short wait costs nothing a player would
    // notice and moves Play() past the worst of it.
    private IEnumerator PlayMusicAfterLaunchSettles()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        musicSource.Play();
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
    // per button. Gameplay events that already play a sound (kills, base damage)
    // are hooked here too, on the rate limit; only events with no sound of their
    // own - tower shots - call Haptics directly at their own site.
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

            // Gameplay events. These fire in bursts - a wave can kill a dozen
            // enemies inside a second - so they go through the rate limit
            // rather than buzzing once per event.
            case SoundType.EnemyDeath:
                Haptics.PlayThrottled(Haptics.Style.Selection, 0.12f);
                break;

            // Losing health is the one thing the player must never miss, so it
            // gets a heavier style and its own limit, and is not competing with
            // the kills happening around it.
            case SoundType.BaseDamage:
                Haptics.PlayThrottled(Haptics.Style.Warning, 0.25f);
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
