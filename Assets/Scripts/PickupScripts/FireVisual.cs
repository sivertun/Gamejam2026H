using UnityEngine;

// Turns the object into a small fire: flame particles plus a flickering point light.
// Everything is built in code, so it works on any prefab it's dropped on.
public class FireVisual : MonoBehaviour
{
    [Header("Flame")]
    [Tooltip("Particle material, CustomAssets/Materials/ParticlesUnlit")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private float flameSize = 1f;
    [SerializeField] private Color hotColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color coolColor = new Color(1f, 0.25f, 0.05f);
    [Tooltip("Hide this object's own mesh (the placeholder sphere)")]
    [SerializeField] private bool hideMesh = true;

    [Header("Light")]
    [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField] private float lightIntensity = 10f;
    [SerializeField] private float lightRange = 8f;
    [SerializeField, Range(0f, 0.6f)] private float flickerAmount = 0.25f;
    [SerializeField] private float flickerSpeed = 6f;
    [Tooltip("The light turns off further than this from the camera, there can be a lot of fires on the map")]
    [SerializeField] private float lightCullDistance = 26f;

    private Transform flame;
    private Light fireLight;
    private Transform cameraTransform;
    private float flickerSeed;

    void Awake()
    {
        flickerSeed = Random.value * 100f;

        if (hideMesh && TryGetComponent(out MeshRenderer meshRenderer)) meshRenderer.enabled = false;

        BuildFlame();
        BuildLight();
    }

    private void BuildFlame()
    {
        GameObject flameObject = new GameObject("Flame");
        flame = flameObject.transform;
        flame.SetParent(transform, false);

        ParticleSystem particles = flameObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f * flameSize, 0.8f * flameSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = hotColor;
        main.gravityModifier = -0.05f;
        main.maxParticles = 60;
        // World space so the flame trails behind when the fire gets sucked in
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Ignore the parent's scale, flameSize decides how big the fire is
        main.scalingMode = ParticleSystemScalingMode.Shape;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.2f * flameSize;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(hotColor, 0f), new GradientColorKey(coolColor, 0.6f), new GradientColorKey(coolColor * 0.4f, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 1.2f;

        ParticleSystemRenderer particleRenderer = flameObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        if (particleMaterial != null) particleRenderer.sharedMaterial = particleMaterial;
        else Debug.LogWarning("FireVisual: no particle material assigned", this);
    }

    private void BuildLight()
    {
        GameObject lightObject = new GameObject("FireLight");
        lightObject.transform.SetParent(transform, false);
        fireLight = lightObject.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = lightColor;
        fireLight.range = lightRange;
        fireLight.shadows = LightShadows.None;
    }

    void LateUpdate()
    {
        // The pickup is a rolling ball, the flame should still burn straight up (cone emits along +z)
        flame.rotation = Quaternion.Euler(-90f, 0f, 0f);

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        bool visible = cameraTransform == null || (cameraTransform.position - transform.position).sqrMagnitude < lightCullDistance * lightCullDistance;
        fireLight.enabled = visible;
        if (!visible) return;

        float flicker = 1f + (Mathf.PerlinNoise(Time.time * flickerSpeed, flickerSeed) * 2f - 1f) * flickerAmount;
        fireLight.intensity = lightIntensity * flicker;
    }
}
