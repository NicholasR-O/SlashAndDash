using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[AddComponentMenu("Game/Scene Transition Fader")]
public class SceneTransitionFader : MonoBehaviour
{
    private static SceneTransitionFader instance;

    [SerializeField] private float defaultFadeDuration = 0.75f;
    [SerializeField] private Color defaultFadeColor = Color.black;

    private Image fadeImage;
    private Coroutine fadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static IEnumerator FadeOut(float duration = -1f)
    {
        SceneTransitionFader fader = EnsureInstance();
        fader.StopActiveFade();
        yield return fader.FadeTo(1f, duration, fader.defaultFadeColor);
    }

    public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, float fadeDuration = -1f)
    {
        SceneTransitionFader fader = EnsureInstance();
        fader.StartCoroutine(fader.FadeOutAndLoadRoutine(sceneName, -1, mode, fadeDuration));
    }

    public static void LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single, float fadeDuration = -1f)
    {
        SceneTransitionFader fader = EnsureInstance();
        fader.StartCoroutine(fader.FadeOutAndLoadRoutine(null, buildIndex, mode, fadeDuration));
    }

    private static SceneTransitionFader EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject faderObject = new GameObject("Scene Transition Fader");
        DontDestroyOnLoad(faderObject);
        instance = faderObject.AddComponent<SceneTransitionFader>();
        instance.BuildCanvas();
        instance.SetAlpha(1f);
        return instance;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionFader fader = EnsureInstance();
        if (fader.isActiveAndEnabled)
            fader.StartFade(0f, fader.defaultFadeDuration);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
    }

    private IEnumerator FadeOutAndLoadRoutine(string sceneName, int buildIndex, LoadSceneMode mode, float duration)
    {
        StopActiveFade();
        yield return FadeTo(1f, duration, defaultFadeColor);

        if (buildIndex >= 0)
            SceneManager.LoadScene(buildIndex, mode);
        else
            SceneManager.LoadScene(sceneName, mode);
    }

    private void StartFade(float targetAlpha, float duration)
    {
        StopActiveFade();

        fadeRoutine = StartCoroutine(FadeTo(targetAlpha, duration, defaultFadeColor));
    }

    private void StopActiveFade()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, Color color)
    {
        BuildCanvas();

        duration = duration >= 0f ? duration : defaultFadeDuration;
        duration = Mathf.Max(0f, duration);

        Color current = fadeImage.color;
        current.r = color.r;
        current.g = color.g;
        current.b = color.b;
        fadeImage.color = current;
        fadeImage.enabled = true;
        fadeImage.raycastTarget = targetAlpha > 0.001f;

        float startAlpha = fadeImage.color.a;
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        BuildCanvas();

        Color color = fadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.enabled = color.a > 0.001f;
        fadeImage.raycastTarget = color.a > 0.001f;
    }

    private void BuildCanvas()
    {
        if (fadeImage != null)
            return;

        GameObject canvasObject = new GameObject("Scene Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("Scene Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.color = defaultFadeColor;
    }
}
