using UnityEngine;

public static class AudioPlaybackUtility
{
    public static AudioSource EnsureChildAudioSource(
        Transform parent,
        string childName,
        bool loop,
        bool playOnAwake = false,
        float spatialBlend = 1f,
        float minDistance = 1f,
        float maxDistance = 25f)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        AudioSource source = child.GetComponent<AudioSource>();
        if (source == null)
            source = child.gameObject.AddComponent<AudioSource>();

        source.playOnAwake = playOnAwake;
        source.loop = loop;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = Mathf.Max(0.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        return source;
    }

    public static void PlayDetachedClip(
        AudioClip clip,
        Vector3 position,
        float volume = 1f,
        float pitch = 1f,
        float spatialBlend = 1f,
        float minDistance = 1f,
        float maxDistance = 25f)
    {
        if (clip == null)
            return;

        GameObject audioObject = new GameObject("DetachedAudio_" + clip.name);
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * GameOptions.SoundEffectsVolume);
        source.pitch = Mathf.Clamp(pitch, -3f, 3f);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = Mathf.Max(0.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        source.Play();

        float lifetime = clip.length;
        lifetime /= Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        Object.Destroy(audioObject, lifetime + 0.1f);
    }
}
