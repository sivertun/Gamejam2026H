using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// What's left when an enemy dies. Takes over the enemy's model frozen in its last pose and tips it
// over onto the ground. The body stays where it is; holding the suck beam on it drains its
// particles into the lamp and feeds the lantern. Once empty it sinks away.
public class EnemyCorpse : MonoBehaviour, IDrainable
{
    [Header("Body")]
    [SerializeField] private float fallTime = 0.6f;
    [SerializeField] private float sinkTime = 3f;
    [SerializeField] private float sinkDepth = 1.5f;
    [Tooltip("Bodies stay until drained, but when there are more than this the oldest one sinks away")]
    [SerializeField] private int maxCorpses = 20;

    [Header("Draining")]
    [Tooltip("Seconds of sucking to empty the body")]
    [SerializeField] private float drainTime = 2f;
    [Tooltip("Lantern light gained from a whole body (1 = a full lantern stage)")]
    [SerializeField] private float lightReward = 0.1f;

    private static readonly List<EnemyCorpse> corpses = new List<EnemyCorpse>();

    private ParticleSystem[] particles;
    private LanternController lantern;
    private float drained;
    private float lastDrainTime = float.NegativeInfinity;
    private bool pulled = true;
    private bool expired;

    void Awake()
    {
        // A body on the ground shouldn't block anyone. Triggers are still found by LampSuck
        foreach (Collider c in GetComponents<Collider>()) c.isTrigger = true;

        particles = GetComponentsInChildren<ParticleSystem>();
        SetPulled(false);
    }

    void OnEnable()
    {
        corpses.Add(this);
        if (corpses.Count > maxCorpses) corpses[0].Expire();
    }

    void OnDisable()
    {
        corpses.Remove(this);
    }

    void Update()
    {
        // The particles only follow the lamp's force field while the beam is actually on this body
        if (!expired) SetPulled(Time.time - lastDrainTime < 0.1f);
    }

    // Called by LampSuck every frame the suck beam covers this body
    public void OnDrain(LampSuck lamp, float deltaTime)
    {
        if (expired) return;
        lastDrainTime = Time.time;

        if (lantern == null) lantern = lamp.GetComponentInParent<LanternController>();
        float step = Mathf.Min(deltaTime, drainTime - drained);
        drained += step;
        if (lantern != null) lantern.UpgradeLightLevel(lightReward * step / drainTime);

        if (drained >= drainTime)
        {
            SetPulled(true); // the last particles still fly into the lamp
            Expire();
        }
    }

    // Called by EnemyHealth right after the corpse is spawned
    public void TakeModel(Transform model)
    {
        if (model == null) return; // no model, the placeholder mesh stays visible

        if (TryGetComponent(out MeshRenderer placeholder)) placeholder.enabled = false;

        model.SetParent(transform, true);
        foreach (Animator animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;

        StartCoroutine(FallOver(model));
    }

    private void SetPulled(bool value)
    {
        if (pulled == value) return;
        pulled = value;
        foreach (ParticleSystem system in particles)
        {
            ParticleSystem.ExternalForcesModule forces = system.externalForces;
            forces.enabled = value;
        }
    }

    private IEnumerator FallOver(Transform model)
    {
        // The model's origin is at its feet, so this tips it backwards like a felled tree
        Quaternion start = model.localRotation;
        Quaternion end = start * Quaternion.Euler(-90f, 0f, Random.Range(-25f, 25f));
        float elapsed = 0f;
        while (elapsed < fallTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallTime);
            model.localRotation = Quaternion.Slerp(start, end, t * t); // accelerates like a real fall
            yield return null;
        }
    }

    private void Expire()
    {
        if (expired) return;
        expired = true;
        corpses.Remove(this);
        foreach (ParticleSystem system in particles) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StartCoroutine(SinkAway());
    }

    private IEnumerator SinkAway()
    {
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.down * sinkDepth;
        float elapsed = 0f;
        while (elapsed < sinkTime)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, elapsed / sinkTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
