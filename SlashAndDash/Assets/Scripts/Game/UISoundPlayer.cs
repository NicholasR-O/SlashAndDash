using UnityEngine;
using UnityEngine.EventSystems;

public static class UISoundPlayer
{
    const float HoverVolume = 0.45f;
    const float ClickVolume = 0.75f;
    const float SliderVolume = 0.5f;

    static GameObject sourceObject;
    static AudioSource oneShotSource;
    static AudioSource sliderSource;

    public static void PlayRandomHover(AudioClip[] clips)
    {
        PlayRandom(clips, HoverVolume);
    }

    public static void PlayRandomClick(AudioClip[] clips)
    {
        PlayRandom(clips, ClickVolume);
    }

    public static void PlaySlider(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null)
            return;

        AudioSource audioSource = GetSliderSource();
        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = Mathf.Clamp01(SliderVolume * GameOptions.SoundEffectsVolume);
        audioSource.Play();
    }

    static void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        Play(clip, volume);
    }

    static void Play(AudioClip clip, float volume)
    {
        if (!Application.isPlaying || clip == null)
            return;

        AudioSource audioSource = GetOneShotSource();
        if (audioSource == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume * GameOptions.SoundEffectsVolume));
    }

    static AudioSource GetOneShotSource()
    {
        if (oneShotSource != null)
            return oneShotSource;

        oneShotSource = CreateSource("One Shots");
        return oneShotSource;
    }

    static AudioSource GetSliderSource()
    {
        if (sliderSource != null)
            return sliderSource;

        sliderSource = CreateSource("Slider");
        return sliderSource;
    }

    static AudioSource CreateSource(string name)
    {
        GameObject root = GetSourceObject();
        AudioSource audioSource = root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        audioSource.ignoreListenerPause = true;
        audioSource.name = name;
        return audioSource;
    }

    static GameObject GetSourceObject()
    {
        if (sourceObject != null)
            return sourceObject;

        sourceObject = new GameObject("UI Sound Player");
        sourceObject.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(sourceObject);
        return sourceObject;
    }
}

public sealed class CanvasUIHoverSound : MonoBehaviour, IPointerEnterHandler
{
    AudioClip[] hoverClips;

    public void Configure(AudioClip[] clips)
    {
        hoverClips = clips;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UISoundPlayer.PlayRandomHover(hoverClips);
    }
}
