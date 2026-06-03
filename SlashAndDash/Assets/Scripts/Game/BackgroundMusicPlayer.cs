using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("Audio/Background Music Player")]
public class BackgroundMusicPlayer : MonoBehaviour
{
    [Serializable]
    sealed class LevelMusicTracks
    {
        [SerializeField] private string sceneName = string.Empty;
        [SerializeField] private AudioClip drivingTrack;
        [SerializeField] private AudioClip arenaTrack;

        public string SceneName => sceneName;
        public AudioClip DrivingTrack => drivingTrack;
        public AudioClip ArenaTrack => arenaTrack;
    }

    [Header("Tracks")]
    [SerializeField] private AudioClip drivingTrack;
    [SerializeField] private AudioClip arenaTrack;
    [SerializeField] private LevelMusicTracks[] levelTracks;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.75f;
    [SerializeField] private float fadeDuration = 1.25f;
    [SerializeField, Range(1f, 3f)] private float sceneTransitionFadePower = 1.4f;
    [SerializeField] private bool playWhilePaused = true;

    private AudioSource drivingSource;
    private AudioSource arenaSource;
    private AudioSource transitionSource;
    private AudioClip currentDrivingTrack;
    private AudioClip currentArenaTrack;
    private AudioClip transitionDrivingTrack;
    private string currentSceneName;
    private bool arenaMusicActive;
    private bool transitionMusicActive;
    private float transitionDuration;
    private float transitionElapsed;
    private float transitionStartDrivingVolume;
    private float transitionStartArenaVolume;

    void Awake()
    {
        GameOptions.EnsureLoaded();
        EnsureSources();
        ResolveCurrentSceneTracks(force: true);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ArenaTrigger.ArenaStarted += OnArenaStarted;
        ArenaTrigger.ArenaEnded += OnArenaEnded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ArenaTrigger.ArenaStarted -= OnArenaStarted;
        ArenaTrigger.ArenaEnded -= OnArenaEnded;
    }

    void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        sceneTransitionFadePower = Mathf.Max(1f, sceneTransitionFadePower);
    }

    void Update()
    {
        EnsureSources();
        ResolveCurrentSceneTracks();

        bool pausedMuted = !playWhilePaused && GameState.IsPaused;
        float deltaTime = playWhilePaused ? Time.unscaledDeltaTime : Time.deltaTime;

        if (transitionMusicActive)
        {
            UpdateSceneTransitionMusic(pausedMuted, deltaTime);
            return;
        }

        UpdateMusicSource(drivingSource, currentDrivingTrack, !arenaMusicActive && !pausedMuted, deltaTime);
        UpdateMusicSource(arenaSource, currentArenaTrack, arenaMusicActive && !pausedMuted, deltaTime);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        arenaMusicActive = false;
        currentSceneName = null;
        transitionMusicActive = false;
        transitionDrivingTrack = null;
        ResolveCurrentSceneTracks(force: true);
        ResetMusicSource(arenaSource);
        ResetMusicSource(transitionSource);
    }

    public void BeginSceneTransitionMusic(string targetSceneName, float duration)
    {
        EnsureSources();
        ResolveCurrentSceneTracks();

        transitionDrivingTrack = FindSpecificDrivingTrack(targetSceneName);
        transitionDuration = Mathf.Max(0f, duration);
        transitionElapsed = 0f;
        transitionStartDrivingVolume = drivingSource != null ? drivingSource.volume : 0f;
        transitionStartArenaVolume = arenaSource != null ? arenaSource.volume : 0f;
        transitionMusicActive = true;
        arenaMusicActive = false;

        if (transitionSource == null)
            EnsureSources();

        if (transitionSource != null)
        {
            if (transitionSource.clip != transitionDrivingTrack)
            {
                transitionSource.Stop();
                transitionSource.clip = transitionDrivingTrack;
                if (transitionDrivingTrack != null)
                    transitionSource.time = 0f;
            }

            transitionSource.volume = 0f;
            if (transitionDrivingTrack != null && !transitionSource.isPlaying)
                transitionSource.Play();
        }
    }

    public static void BeginSceneTransitionMusicForActivePlayer(string targetSceneName, float duration)
    {
        BackgroundMusicPlayer player = FindFirstObjectByType<BackgroundMusicPlayer>();
        if (player != null)
            player.BeginSceneTransitionMusic(targetSceneName, duration);
    }

    void OnArenaStarted(int remainingEnemies)
    {
        arenaMusicActive = true;
    }

    void OnArenaEnded()
    {
        arenaMusicActive = false;
    }

    void EnsureSources()
    {
        if (drivingSource == null)
        {
            drivingSource = AudioPlaybackUtility.EnsureChildAudioSource(
                transform,
                "DrivingMusicAudio",
                loop: true,
                playOnAwake: false,
                spatialBlend: 0f);
        }
        drivingSource.ignoreListenerPause = playWhilePaused;

        if (arenaSource == null)
        {
            arenaSource = AudioPlaybackUtility.EnsureChildAudioSource(
                transform,
                "ArenaMusicAudio",
                loop: true,
                playOnAwake: false,
                spatialBlend: 0f);
        }
        arenaSource.ignoreListenerPause = playWhilePaused;

        if (transitionSource == null)
        {
            transitionSource = AudioPlaybackUtility.EnsureChildAudioSource(
                transform,
                "TransitionMusicAudio",
                loop: true,
                playOnAwake: false,
                spatialBlend: 0f);
        }
        transitionSource.ignoreListenerPause = playWhilePaused;
    }

    void ResolveCurrentSceneTracks(bool force = false)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string sceneName = activeScene.IsValid() ? activeScene.name : string.Empty;
        if (!force && currentSceneName == sceneName)
            return;

        currentSceneName = sceneName;
        currentDrivingTrack = drivingTrack;
        currentArenaTrack = arenaTrack;

        LevelMusicTracks tracks = FindLevelTracks(sceneName);
        if (tracks == null)
            return;

        if (tracks.DrivingTrack != null)
            currentDrivingTrack = tracks.DrivingTrack;
        if (tracks.ArenaTrack != null)
            currentArenaTrack = tracks.ArenaTrack;
    }

    LevelMusicTracks FindLevelTracks(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || levelTracks == null)
            return null;

        for (int i = 0; i < levelTracks.Length; i++)
        {
            LevelMusicTracks tracks = levelTracks[i];
            if (tracks == null || string.IsNullOrWhiteSpace(tracks.SceneName))
                continue;

            if (string.Equals(tracks.SceneName.Trim(), sceneName, StringComparison.OrdinalIgnoreCase))
                return tracks;
        }

        return null;
    }

    AudioClip FindSpecificDrivingTrack(string sceneName)
    {
        LevelMusicTracks tracks = FindLevelTracks(sceneName);
        return tracks != null ? tracks.DrivingTrack : null;
    }

    void UpdateSceneTransitionMusic(bool pausedMuted, float deltaTime)
    {
        float t = transitionDuration > 0f ? Mathf.Clamp01(transitionElapsed / transitionDuration) : 1f;
        float fadeT = Mathf.Pow(t, sceneTransitionFadePower);
        float targetVolume = pausedMuted ? 0f : masterVolume * GameOptions.MusicVolume;

        if (drivingSource != null)
        {
            drivingSource.volume = pausedMuted ? 0f : Mathf.Lerp(transitionStartDrivingVolume, 0f, fadeT);
            if (drivingSource.volume <= 0.001f && drivingSource.isPlaying)
                drivingSource.Stop();
        }

        if (arenaSource != null)
        {
            arenaSource.volume = pausedMuted ? 0f : Mathf.Lerp(transitionStartArenaVolume, 0f, fadeT);
            if (arenaSource.volume <= 0.001f && arenaSource.isPlaying)
                arenaSource.Stop();
        }

        if (transitionSource != null)
        {
            if (transitionDrivingTrack == null)
            {
                transitionSource.volume = 0f;
                if (transitionSource.isPlaying)
                    transitionSource.Stop();
            }
            else
            {
                if (transitionSource.clip != transitionDrivingTrack)
                    transitionSource.clip = transitionDrivingTrack;

                transitionSource.loop = true;
                transitionSource.spatialBlend = 0f;
                transitionSource.ignoreListenerPause = playWhilePaused;
                transitionSource.volume = pausedMuted ? 0f : Mathf.Lerp(0f, targetVolume, fadeT);

                if (!transitionSource.isPlaying)
                    transitionSource.Play();
            }
        }

        transitionElapsed += Mathf.Max(0f, deltaTime);
    }

    void UpdateMusicSource(AudioSource source, AudioClip clip, bool shouldBeAudible, float deltaTime)
    {
        if (source == null)
            return;

        if (source.clip != clip)
        {
            if (source.isPlaying)
                source.Stop();

            source.clip = clip;
            source.volume = 0f;
            if (clip != null)
                source.time = 0f;
        }

        float targetVolume = clip != null && shouldBeAudible ? masterVolume * GameOptions.MusicVolume : 0f;
        float fadeStep = fadeDuration > 0f ? deltaTime / fadeDuration : 1f;
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, Mathf.Max(0.001f, masterVolume) * Mathf.Max(0.001f, fadeStep));

        if (clip == null)
        {
            if (source.isPlaying)
                source.Stop();
            return;
        }

        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = playWhilePaused;

        if (!source.isPlaying && shouldBeAudible)
            source.Play();

        if (!shouldBeAudible && source.volume <= 0.001f && source.isPlaying)
            source.Stop();
    }

    static void ResetMusicSource(AudioSource source)
    {
        if (source == null)
            return;

        if (source.isPlaying)
            source.Stop();

        source.volume = 0f;
        if (source.clip != null)
            source.time = 0f;
    }
}
