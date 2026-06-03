using System;
using UnityEngine;

public static class GameOptions
{
    public const float DefaultSensitivity = 1f;
    public const float DefaultFieldOfView = 70f;
    public const float DefaultMasterVolume = 1f;
    public const float DefaultMusicVolume = 0.85f;
    public const float DefaultSoundEffectsVolume = 1f;
    public const float DefaultBrightness = 1f;
    public const bool DefaultAutoAim = true;
    public const int DefaultUiScaleLevel = 2;
    public const int MinUiScaleLevel = 1;
    public const int MaxUiScaleLevel = 4;

    const string SensitivityKey = "options.sensitivity";
    const string FieldOfViewKey = "options.fov";
    const string MasterVolumeKey = "options.volume.master";
    const string MusicVolumeKey = "options.volume.music";
    const string SoundEffectsVolumeKey = "options.volume.sfx";
    const string BrightnessKey = "options.brightness";
    const string AutoAimKey = "options.autoAim";
    const string UiScaleKey = "options.uiScale";

    static bool loaded;
    static float sensitivity = DefaultSensitivity;
    static float fieldOfView = DefaultFieldOfView;
    static float masterVolume = DefaultMasterVolume;
    static float musicVolume = DefaultMusicVolume;
    static float soundEffectsVolume = DefaultSoundEffectsVolume;
    static float brightness = DefaultBrightness;
    static bool autoAim = DefaultAutoAim;
    static int uiScaleLevel = DefaultUiScaleLevel;

    public static event Action Changed;

    public static float Sensitivity
    {
        get
        {
            EnsureLoaded();
            return sensitivity;
        }
    }

    public static float FieldOfView
    {
        get
        {
            EnsureLoaded();
            return fieldOfView;
        }
    }

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return masterVolume;
        }
    }

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return musicVolume;
        }
    }

    public static float SoundEffectsVolume
    {
        get
        {
            EnsureLoaded();
            return soundEffectsVolume;
        }
    }

    public static float Brightness
    {
        get
        {
            EnsureLoaded();
            return brightness;
        }
    }

    public static bool AutoAim
    {
        get
        {
            EnsureLoaded();
            return autoAim;
        }
    }

    public static int UiScaleLevel
    {
        get
        {
            EnsureLoaded();
            return uiScaleLevel;
        }
    }

    public static float UiScaleFactor
    {
        get
        {
            EnsureLoaded();
            return GetUiScaleFactor(uiScaleLevel);
        }
    }

    public static void EnsureLoaded()
    {
        if (loaded)
            return;

        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        fieldOfView = PlayerPrefs.GetFloat(FieldOfViewKey, DefaultFieldOfView);
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        soundEffectsVolume = PlayerPrefs.GetFloat(SoundEffectsVolumeKey, DefaultSoundEffectsVolume);
        brightness = PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);
        autoAim = PlayerPrefs.GetInt(AutoAimKey, DefaultAutoAim ? 1 : 0) != 0;
        uiScaleLevel = PlayerPrefs.GetInt(UiScaleKey, DefaultUiScaleLevel);

        sensitivity = Mathf.Clamp(sensitivity, 0.1f, 3f);
        fieldOfView = Mathf.Clamp(fieldOfView, 45f, 110f);
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        soundEffectsVolume = Mathf.Clamp01(soundEffectsVolume);
        brightness = Mathf.Clamp(brightness, 0.5f, 1.5f);
        uiScaleLevel = ClampUiScaleLevel(uiScaleLevel);

        loaded = true;
        ApplyRuntimeSettings();
    }

    public static void SetSensitivity(float value)
    {
        EnsureLoaded();
        SetFloat(ref sensitivity, Mathf.Clamp(value, 0.1f, 3f), SensitivityKey);
    }

    public static void SetFieldOfView(float value)
    {
        EnsureLoaded();
        SetFloat(ref fieldOfView, Mathf.Clamp(value, 45f, 110f), FieldOfViewKey);
    }

    public static void SetMasterVolume(float value)
    {
        EnsureLoaded();
        SetFloat(ref masterVolume, Mathf.Clamp01(value), MasterVolumeKey);
    }

    public static void SetMusicVolume(float value)
    {
        EnsureLoaded();
        SetFloat(ref musicVolume, Mathf.Clamp01(value), MusicVolumeKey);
    }

    public static void SetSoundEffectsVolume(float value)
    {
        EnsureLoaded();
        SetFloat(ref soundEffectsVolume, Mathf.Clamp01(value), SoundEffectsVolumeKey);
    }

    public static void SetBrightness(float value)
    {
        EnsureLoaded();
        SetFloat(ref brightness, Mathf.Clamp(value, 0.5f, 1.5f), BrightnessKey);
    }

    public static void SetAutoAim(bool value)
    {
        EnsureLoaded();
        if (autoAim == value)
            return;

        autoAim = value;
        PlayerPrefs.SetInt(AutoAimKey, autoAim ? 1 : 0);
        PlayerPrefs.Save();
        ApplyRuntimeSettings();
        Changed?.Invoke();
    }

    public static void SetUiScaleLevel(int value)
    {
        EnsureLoaded();
        int clamped = ClampUiScaleLevel(value);
        if (uiScaleLevel == clamped)
            return;

        uiScaleLevel = clamped;
        PlayerPrefs.SetInt(UiScaleKey, uiScaleLevel);
        PlayerPrefs.Save();
        ApplyRuntimeSettings();
        Changed?.Invoke();
    }

    public static int ClampUiScaleLevel(int value)
    {
        return Mathf.Clamp(value, MinUiScaleLevel, MaxUiScaleLevel);
    }

    public static float GetUiScaleFactor(int level)
    {
        switch (ClampUiScaleLevel(level))
        {
            case 1:
                return 0.8f;
            case 3:
                return 1.2f;
            case 4:
                return 1.4f;
            default:
                return 1f;
        }
    }

    public static void ApplyRuntimeSettings()
    {
        EnsureLoadedForApply();
        AudioListener.volume = Mathf.Clamp01(masterVolume);
        RuntimeBrightnessOverlay.SetBrightness(brightness);
    }

    static void SetFloat(ref float current, float value, string key)
    {
        if (Mathf.Approximately(current, value))
            return;

        current = value;
        PlayerPrefs.SetFloat(key, current);
        PlayerPrefs.Save();
        ApplyRuntimeSettings();
        Changed?.Invoke();
    }

    static void EnsureLoadedForApply()
    {
        if (!loaded)
            EnsureLoaded();
    }
}
