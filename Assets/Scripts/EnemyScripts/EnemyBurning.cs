using UnityEngine;

// The fire Withering Light leaves on a living enemy. Built in code and fed a little heat every
// frame the beam is on it, so the enemy prefabs need nothing added to them. Once the beam moves
// off, it burns down and takes itself away.
public class EnemyBurning : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color HotColor = new Color(1f, 0.45f, 0.12f);

    [Tooltip("How fast the fire dies down once the beam leaves")]
    private const float CoolPerSecond = 1.6f;
    private const float MaxEmission = 45f;

    private ParticleSystem flames;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Color[] originalColors;
    private float heat;
    private float lastStokedTime = float.NegativeInfinity;

    // Called by LampSuck every frame the beam burns this enemy
    public static void Burn(GameObject enemy, Material particleMaterial, float heatPerSecond, float deltaTime)
    {
        if (enemy == null) return;

        EnemyBurning burning = enemy.GetComponent<EnemyBurning>();
        if (burning == null)
        {
            burning = enemy.AddComponent<EnemyBurning>();
            burning.Build(particleMaterial);
        }
        burning.Stoke(heatPerSecond * deltaTime);
    }

    private void Stoke(float amount)
    {
        heat = Mathf.Clamp01(heat + amount);
        lastStokedTime = Time.time;
    }

    private void Build(Material particleMaterial)
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            originalColors[i] = material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
        }

        BuildFlames(particleMaterial);
    }

    private void BuildFlames(Material particleMaterial)
    {
        // Wrap the fire around however big this enemy actually is
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        float height = Mathf.Max(bounds.size.y, 0.6f);
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.25f);

        GameObject go = new GameObject("Burning");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cones emit along +z

        flames = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = flames.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f * height, 0.5f * height);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = HotColor;
        main.gravityModifier = -0.08f; // fire climbs
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        ParticleSystem.ShapeModule shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = radius;
        shape.length = height;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f),
                new GradientColorKey(HotColor, 0.5f),
                new GradientColorKey(new Color(0.25f, 0.06f, 0.02f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f),
            });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flames.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flames.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        ParticleSystem.NoiseModule noise = flames.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 1.5f;

        ParticleSystemRenderer particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        if (particleMaterial != null) particleRenderer.sharedMaterial = particleMaterial;
    }

    void LateUpdate()
    {
        // A frame or two of slack, because the beam feeds this from its own Update
        bool onFire = Time.time - lastStokedTime < 0.15f;
        if (!onFire) heat = Mathf.Max(heat - CoolPerSecond * Time.deltaTime, 0f);

        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = MaxEmission * heat;

        // Property blocks rather than materials, or every enemy sharing Dark.mat would glow at once
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(originalColors[i], HotColor, heat * 0.85f));
            renderers[i].SetPropertyBlock(block);
        }

        if (!onFire && heat <= 0f) Extinguish();
    }

    private void Extinguish()
    {
        foreach (Renderer r in renderers)
        {
            if (r != null) r.SetPropertyBlock(null);
        }
        if (flames != null) Destroy(flames.gameObject);
        Destroy(this);
    }
}
