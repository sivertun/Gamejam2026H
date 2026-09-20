using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// The win-screen twin of DeathSequence: "YOU GOT RESCUED!". Call RescueSequence.Play() from
/// anywhere; no scene setup needed. Assets are loaded by name from any Resources folder:
///   Assets/Resources/game-win-sound  (audio clip, any extension)
///   Assets/Resources/RescueFont      (font asset, optional, any extension)
/// Unlike death, this one is terminal: it ends on a held dawn-lit screen with the game frozen,
/// so the only way on from a win is to quit. All state resets whenever a scene loads.
public class RescueSequence : MonoBehaviour
{
    // Check this in your player/input scripts if you want to freeze controls.
    public static bool IsRescued { get; private set; }

    // What the screen settles on and holds forever. Kept well off pure white and tinted warm, so
    // it reads as morning light after the dark rather than as a lightbulb in the face.
    static readonly Color DawnColor = new Color(0.78f, 0.74f, 0.66f);

    static RescueSequence Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        // Needed when "Reload Domain" is off in Enter Play Mode options.
        IsRescued = false; Instance = null; Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A win never reloads, but a death does, and that scene load is a fresh run: clear the
        // flag and the freeze so nothing from a previous session can stay stuck.
        IsRescued = false;
        Time.timeScale = 1f;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Play()
    {
        if (IsRescued) { Debug.Log("[RescueSequence] Play() ignored, already running"); return; }
        if (Instance == null)
            new GameObject("RescueSequence").AddComponent<RescueSequence>(); // Awake sets Instance
        IsRescued = true;
        Debug.Log("[RescueSequence] Play() started");
        Instance.StartCoroutine(Instance.Run());
    }

    IEnumerator Run()
    {
        var sfx = Resources.Load<AudioClip>("game-win-sound");
        var font = Resources.Load<Font>("RescueFont");
        Debug.Log("[RescueSequence] sound: " + (sfx ? sfx.name : "NOT FOUND") + ", font: " + (font ? font.name : "default"));

        // ---------- build UI ----------
        Transform canvas = DeathSequence.MakeCanvas(transform);

        Image dim = DeathSequence.MakeImage(canvas, "Dim", Color.black);
        DeathSequence.Stretch(dim.rectTransform);

        Image band = DeathSequence.MakeImage(canvas, "Band", Color.black);
        band.rectTransform.anchorMin = new Vector2(0, 0.5f);
        band.rectTransform.anchorMax = new Vector2(1, 0.5f);
        band.rectTransform.sizeDelta = new Vector2(0, 320);
        band.rectTransform.anchoredPosition = Vector2.zero;

        // The wash sits under the text, not over it: this screen is the last thing the player
        // ever sees, so the words have to survive it instead of being covered by it. It's a dim
        // warm parchment rather than pure white — the player has spent the whole game in the
        // dark with their pupils wide open, and a full-bright screen held forever hurts.
        Image fade = DeathSequence.MakeImage(canvas, "Fade", DawnColor);
        DeathSequence.Stretch(fade.rectTransform);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(canvas, false);
        DeathSequence.Stretch((RectTransform)textGO.transform);
        var text = textGO.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "YOU GOT RESCUED!";
        text.fontSize = 130;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.color = new Color(1f, 0.93f, 0.7f, 0f); // the lantern's warm light, not the death red

        DeathSequence.SetAlpha(dim, 0); DeathSequence.SetAlpha(band, 0); DeathSequence.SetAlpha(fade, 0);

        // ---------- audio ----------
        if (sfx != null)
        {
            var src = GetComponent<AudioSource>();
            if (!src) src = gameObject.AddComponent<AudioSource>();
            src.ignoreListenerPause = true;
            src.spatialBlend = 0f; // 2D, so it doesn't fall off with distance from the listener
            src.PlayOneShot(sfx);
        }
        else Debug.LogWarning("[RescueSequence] game-win-sound not found under a Resources folder");

        // ---------- fade in: slow-mo, dim, band, text creeps in and swells ----------
        Debug.Log("[RescueSequence] UI built, fading in");
        Time.timeScale = 0.2f;
        const float fadeInTime = 4f;
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            DeathSequence.SetAlpha(dim, Mathf.Clamp01(t / 1.5f) * 0.55f);
            DeathSequence.SetAlpha(band, Mathf.Clamp01(t / 1.0f) * 0.85f);
            var c = text.color; c.a = Mathf.Clamp01((t - 0.5f) / 2f); text.color = c;
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, t / fadeInTime);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.2f);

        // ---------- fade up: you walk out into the dawn ----------
        // The text goes from warm-on-dark to ink-on-parchment as the wash comes up, so it stays
        // readable the whole way through and is still there on the final held frame.
        var warm = new Color(1f, 0.93f, 0.7f);
        var ink = new Color(0.16f, 0.13f, 0.11f);
        t = 0f;
        while (t < 1.5f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 1.5f);
            DeathSequence.SetAlpha(fade, k);
            text.color = Color.Lerp(warm, ink, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        text.color = ink;

        // ---------- hold ----------
        // The run is over for good: freeze the game under the wash and stay there. Winning is
        // the end of the session, so there's no way back out short of quitting.
        Debug.Log("[RescueSequence] holding on the end card, the run is over");
        Time.timeScale = 0f;
    }
}
