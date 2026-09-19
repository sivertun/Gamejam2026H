using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Drop on any GameObject in a scene to make it dark and stylized at runtime:
// near-black ambient, black fog, dim moonlight and a post-processing volume.
public class DarkAtmosphere : MonoBehaviour
{
    [Header("Darkness")]
    [SerializeField] private Color ambientColor = new Color(0.07f, 0.07f, 0.1f);
    [SerializeField] private Color fogColor = Color.black;
    [SerializeField] private float fogDensity = 0.035f;

    [Header("Moonlight (directional lights)")]
    [SerializeField] private Color moonColor = new Color(0.45f, 0.55f, 0.85f);
    [SerializeField] private float moonIntensity = 0.2f;

    [Header("Post Processing")]
    [Tooltip("Overall brightness. Raise if it's too dark, lower for more darkness")]
    [SerializeField, Range(-2f, 3f)] private float exposure = 0.7f;
    [SerializeField, Range(0f, 1f)] private float vignette = 0.4f;
    [SerializeField] private float bloomIntensity = 1.2f;
    [SerializeField] private float bloomThreshold = 0.8f;
    [SerializeField, Range(0f, 1f)] private float filmGrain = 0.3f;
    [SerializeField, Range(-100f, 100f)] private float contrast = 20f;
    [SerializeField, Range(-100f, 100f)] private float saturation = 10f;
    [Tooltip("Tints dark areas blue and bright areas warm")]
    [SerializeField] private Color shadowTint = new Color(0.3f, 0.4f, 0.7f);
    [SerializeField] private Color highlightTint = new Color(1f, 0.75f, 0.45f);

    void Awake()
    {
        ApplyEnvironment();
        ApplyMoonlight();
        ApplyCamera();
        CreateVolume();
    }

    private void ApplyEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.reflectionIntensity = 0f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        DynamicGI.UpdateEnvironment();
    }

    private void ApplyMoonlight()
    {
        foreach (Light light in FindObjectsByType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            light.color = moonColor;
            light.intensity = moonIntensity;
            // Light every rendering layer, including the player's own layer (see LampSuck)
            light.GetUniversalAdditionalLightData().renderingLayers = uint.MaxValue;
        }
    }

    private void ApplyCamera()
    {
        foreach (Camera cam in FindObjectsByType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }

    private void CreateVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Vignette v = profile.Add<Vignette>(true);
        v.intensity.Override(vignette);
        v.smoothness.Override(0.5f);
        v.color.Override(Color.black);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(highlightTint);

        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(filmGrain);
        grain.response.Override(0.6f);

        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(exposure);
        colorAdjustments.contrast.Override(contrast);
        colorAdjustments.saturation.Override(saturation);

        SplitToning splitToning = profile.Add<SplitToning>(true);
        splitToning.shadows.Override(shadowTint);
        splitToning.highlights.Override(highlightTint);
        splitToning.balance.Override(-20f);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        GameObject volumeObject = new GameObject("DarkAtmosphereVolume");
        volumeObject.transform.SetParent(transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100; // win over any default volume in the scene
        volume.sharedProfile = profile;
    }
}
