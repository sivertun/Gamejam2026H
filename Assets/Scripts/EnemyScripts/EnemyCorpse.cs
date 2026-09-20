using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// What's left when an enemy dies. Takes over the enemy's model frozen in its last pose and tips it
// over onto the ground. Holding the lamp beam on a body drains it: its particles stream into the
// lamp, the lantern gains light, and once it's empty the body fades away.
public class EnemyCorpse : MonoBehaviour, IDrainable
{
    [Header("Body")]
    [SerializeField] private float fallTime = 0.6f;
    [Tooltip("Bodies stay until they're drained, but when there are more than this the oldest fades away")]
    [SerializeField] private int maxCorpses = 20;

    [Header("Draining")]
    [Tooltip("Seconds of beam on the body to empty it")]
    [SerializeField] private float drainTime = 2f;
    [Tooltip("Lantern light gained from a whole body. 1 = a whole lantern stage")]
    [SerializeField] private float lightReward = 0.25f;
    [Tooltip("How fast the particles fly to the lamp, in units per second")]
    [SerializeField] private float pullSpeed = 10f;
    [Tooltip("Particles this close to the lamp are swallowed")]
    [SerializeField] private float absorbDistance = 0.5f;

    [Header("Fading away")]
    [SerializeField] private float fadeTime = 1.2f;
    [SerializeField] private float sinkDepth = 1f;

    // Oldest first, so the cap can retire the one that's been lying around longest
    private static readonly List<EnemyCorpse> corpses = new List<EnemyCorpse>();

    private ParticleSystem[] particles;
    private ParticleSystem.Particle[] buffer;
    private Transform model;
    private Vector3 modelStartScale = Vector3.one;
    private LanternController lantern;
    private float drained;
    private float lastDrainTime = float.NegativeInfinity;
    private bool emitting = true;
    private bool fading;

    void Awake()
    {
        // A body on the ground shouldn't block anyone. Triggers are still found by LampSuck
        foreach (Collider c in GetComponents<Collider>()) c.isTrigger = true;

        particles = GetComponentsInChildren<ParticleSystem>();
        // This script moves the particles itself, so no force field should fight it for them
        foreach (ParticleSystem system in particles)
        {
            ParticleSystem.ExternalForcesModule forces = system.externalForces;
            forces.enabled = false;
        }

        // A body gives nothing away until you put the beam on it
        SetEmitting(false);
    }

    void Update()
    {
        if (fading) return;
        // OnDrain runs from LampSuck's own Update, so leave a little slack before going dark again
        if (Time.time - lastDrainTime > 0.1f) SetEmitting(false);
    }

    private void SetEmitting(bool value)
    {
        if (emitting == value) return;
        emitting = value;

        foreach (ParticleSystem system in particles)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = value;

            if (value)
            {
                if (!system.isPlaying) system.Play();
            }
            else
            {
                FadeOutQuickly(system); // the leftovers wink out instead of hanging in the air
            }
        }
    }

    private void FadeOutQuickly(ParticleSystem system)
    {
        int count = system.particleCount;
        if (count == 0) return;
        if (buffer == null || buffer.Length < count) buffer = new ParticleSystem.Particle[count + 64];

        int read = system.GetParticles(buffer);
        for (int i = 0; i < read; i++)
        {
            buffer[i].remainingLifetime = Mathf.Min(buffer[i].remainingLifetime, 0.25f);
        }
        system.SetParticles(buffer, read);
    }

    void OnEnable()
    {
        corpses.Add(this);
        while (corpses.Count > maxCorpses && corpses[0] != this) corpses[0].BeginFade();
    }

    void OnDisable()
    {
        corpses.Remove(this);
    }

    // Called by LampSuck every frame the beam covers this body
    public void OnDrain(LampSuck lamp, float deltaTime)
    {
        if (fading) return;

        lastDrainTime = Time.time;
        SetEmitting(true);

        if (lantern == null) lantern = lamp.GetComponentInParent<LanternController>();

        float step = Mathf.Min(deltaTime, drainTime - drained);
        drained += step;
        float reward = lightReward * (lamp.Upgrades != null ? lamp.Upgrades.enemyLightMultiplier : 1f);
        if (lantern != null && step > 0f) lantern.UpgradeLightLevel(reward * step / drainTime);

        PullParticles(lamp.LampOrigin, deltaTime);

        // The body shrinks a little as it empties, so you can see the drain working
        if (model != null) model.localScale = modelStartScale * Mathf.Lerp(1f, 0.85f, drained / drainTime);

        if (drained >= drainTime) BeginFade();
    }

    // Walks the live particles and moves them toward the lamp by hand. Doing it here instead of with
    // a force field means it works in any scene and only while the beam is actually on this body.
    private void PullParticles(Vector3 lampOrigin, float deltaTime)
    {
        foreach (ParticleSystem system in particles)
        {
            int count = system.particleCount;
            if (count == 0) continue;
            if (buffer == null || buffer.Length < count) buffer = new ParticleSystem.Particle[count + 64];

            int read = system.GetParticles(buffer);
            // Particle positions are in the system's own simulation space
            Vector3 target = system.main.simulationSpace == ParticleSystemSimulationSpace.Local
                ? system.transform.InverseTransformPoint(lampOrigin)
                : lampOrigin;

            for (int i = 0; i < read; i++)
            {
                Vector3 toLamp = target - buffer[i].position;
                float distance = toLamp.magnitude;
                if (distance <= absorbDistance)
                {
                    buffer[i].remainingLifetime = 0f; // swallowed by the lamp
                    continue;
                }

                buffer[i].position += toLamp / distance * Mathf.Min(pullSpeed * deltaTime, distance);
                // Don't let them die halfway to the lamp
                buffer[i].remainingLifetime = Mathf.Max(buffer[i].remainingLifetime, 0.3f);
            }
            system.SetParticles(buffer, read);
        }
    }

    // Called by EnemyHealth right after the corpse is spawned
    public void TakeModel(Transform enemyModel)
    {
        if (enemyModel == null) return; // no model, the placeholder mesh stays visible

        if (TryGetComponent(out MeshRenderer placeholder)) placeholder.enabled = false;

        model = enemyModel;
        model.SetParent(transform, true);
        modelStartScale = model.localScale;
        foreach (Animator animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;

        StartCoroutine(FallOver());
    }

    private IEnumerator FallOver()
    {
        // The model's origin is at its feet, so this tips it backwards like a felled tree
        Quaternion start = model.localRotation;
        Quaternion end = start * Quaternion.Euler(-90f, 0f, Random.Range(-25f, 25f));
        float elapsed = 0f;
        while (elapsed < fallTime && !fading)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallTime);
            model.localRotation = Quaternion.Slerp(start, end, t * t); // accelerates like a real fall
            yield return null;
        }
    }

    private void BeginFade()
    {
        if (fading) return;
        fading = true;
        corpses.Remove(this);

        foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
        foreach (ParticleSystem system in particles) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        StartCoroutine(FadeAway());
    }

    private IEnumerator FadeAway()
    {
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.down * sinkDepth;
        Vector3 scaleFrom = model != null ? model.localScale : Vector3.one;

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);
            transform.position = Vector3.Lerp(from, to, t);
            if (model != null) model.localScale = Vector3.Lerp(scaleFrom, Vector3.zero, t);
            yield return null;
        }
        Destroy(gameObject);
    }
}
