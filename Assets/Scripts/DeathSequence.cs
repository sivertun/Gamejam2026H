using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Dark Souls-style "YOU DIED". Put this on an empty GameObject in your scene,
/// assign the sound and font (both optional), then call DeathSequence.Play().
/// Nothing persists across scene loads; all state resets whenever a scene loads.
public class DeathSequence : MonoBehaviour
{
    [SerializeField] private AudioClip sfx;  // optional
    [SerializeField] private Font font;      // optional, a serif looks way better

    // Check this in your player/input scripts if you want to freeze controls.
    public static bool IsDead { get; private set; }

    static DeathSequence Instance;
    static bool fadeInPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        // Needed when "Reload Domain" is off in Enter Play Mode options.
        IsDead = false; Instance = null; fadeInPending = false; Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Every scene load is a fresh life, so nothing can stay stuck.
        IsDead = false;
        Time.timeScale = 1f;
        if (!fadeInPending) return;
        fadeInPending = false;
        new GameObject("DeathFadeIn").AddComponent<DeathFadeIn>();
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Play()
    {
        if (IsDead) { Debug.Log("[DeathSequence] Play() ignored, already running"); return; }
        if (Instance == null)
        {
            Debug.LogWarning("[DeathSequence] No DeathSequence in scene, using defaults (no sound, default font)");
            new GameObject("DeathSequence").AddComponent<DeathSequence>(); // Awake sets Instance
        }
        IsDead = true;
        Debug.Log("[DeathSequence] Play() started");
        Instance.StartCoroutine(Instance.Run());
    }

    IEnumerator Run()
    {
        // ---------- build UI ----------
        Transform canvas = MakeCanvas(transform);

        Image dim = MakeImage(canvas, "Dim", Color.black);
        Stretch(dim.rectTransform);

        Image band = MakeImage(canvas, "Band", Color.black);
        band.rectTransform.anchorMin = new Vector2(0, 0.5f);
        band.rectTransform.anchorMax = new Vector2(1, 0.5f);
        band.rectTransform.sizeDelta = new Vector2(0, 320);
        band.rectTransform.anchoredPosition = Vector2.zero;

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(canvas, false);
        Stretch((RectTransform)textGO.transform);
        var text = textGO.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "YOU DIED";
        text.fontSize = 150;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.color = new Color(0.65f, 0.05f, 0.05f, 0f);

        Image fade = MakeImage(canvas, "Fade", Color.black); // on top of everything
        Stretch(fade.rectTransform);
        SetAlpha(dim, 0); SetAlpha(band, 0); SetAlpha(fade, 0);

        // ---------- audio (optional) ----------
        if (sfx != null)
        {
            var src = GetComponent<AudioSource>();
            if (!src) src = gameObject.AddComponent<AudioSource>();
            src.ignoreListenerPause = true;
            src.PlayOneShot(sfx);
        }

        // ---------- fade in: slow-mo, dim, band, text creeps in and swells ----------
        Debug.Log("[DeathSequence] UI built, fading in");
        Time.timeScale = 0.2f;
        const float fadeInTime = 4f;
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(dim, Mathf.Clamp01(t / 1.5f) * 0.55f);
            SetAlpha(band, Mathf.Clamp01(t / 1.0f) * 0.85f);
            var c = text.color; c.a = Mathf.Clamp01((t - 0.5f) / 2f); text.color = c;
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, t / fadeInTime);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.2f);

        // ---------- fade to black ----------
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(fade, Mathf.Clamp01(t));
            yield return null;
        }

        // ---------- reload scene ----------
        int idx = SceneManager.GetActiveScene().buildIndex;
        if (idx < 0)
        {
            Debug.LogError("[DeathSequence] Active scene is not in the build list, can't reload. Add it under File > Build Profiles > Scene List.");
            yield break;
        }
        Debug.Log("[DeathSequence] reloading scene, buildIndex = " + idx);
        Time.timeScale = 1f;
        fadeInPending = true;
        SceneManager.LoadScene(idx);
    }

    // ---------- shared UI helpers ----------
    internal static Transform MakeCanvas(Transform parent)
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return go.transform;
    }

    internal static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    internal static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }
}

/// Spawned automatically after the reload: starts black, fades into the fresh scene.
class DeathFadeIn : MonoBehaviour
{
    IEnumerator Start()
    {
        Transform canvas = DeathSequence.MakeCanvas(transform);
        Image fade = DeathSequence.MakeImage(canvas, "Fade", Color.black);
        DeathSequence.Stretch(fade.rectTransform);

        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            DeathSequence.SetAlpha(fade, 1f - Mathf.Clamp01(t / 0.8f));
            yield return null;
        }
        Destroy(gameObject);
    }
}