using UnityEngine;
using System;
using System.Collections.Generic;

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
        BaseDamage
    }

    [Header("Volume Settings")]
    public VolumeData[] sounds;

    private Dictionary<SoundType, VolumeData> soundDictionary;
    private AudioSource sfxSource;
    private AudioSource musicSource;

    private bool isMusicMuted = false;
    private bool isSfxMuted = false;

    public bool IsBackgroundMusicEnabled => !isMusicMuted;
    public bool IsSfxEnabled => !isSfxMuted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
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
        if (isSfxMuted) return;

        if (soundDictionary.TryGetValue(type, out VolumeData data) && data.clip != null)
        {
            sfxSource.PlayOneShot(data.clip, data.volume);
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
        PlayerPrefs.SetInt("VibrationEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
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

        // Load individual sound volumes
        foreach (var sound in sounds)
        {
            float savedVolume = PlayerPrefs.GetFloat($"SoundVolume_{sound.type}", sound.volume);
            sound.volume = savedVolume;
        }
    }
}
