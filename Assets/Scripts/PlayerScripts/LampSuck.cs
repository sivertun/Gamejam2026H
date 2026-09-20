using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// One lamp light that morphs between two shapes:
// idle = a wide circle on the ground around the player, holding suck = a beam pointed forward.
public class LampSuck : MonoBehaviour
{
    [Header("Suck")]
    [Tooltip("How far the lamp reaches with a starting lantern. It grows and shrinks with the light")]
    [SerializeField] private float range = 6f;
    [Tooltip("Full angle of the area that sucks. Keep this narrower than the beam.")]
    [SerializeField, Range(1f, 120f)] private float suckAngle = 30f;
    [SerializeField] private float pullSpeed = 6f;
    [SerializeField] private float absorbDistance = 1f;
    [Tooltip("Sideways room either side of the beam for grabbing things close to you, in units")]
    [SerializeField] private float grabWidth = 1.5f;
    [Tooltip("Things that block sucking (e.g. trees). Leave as Everything to block on any collider.")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    [Header("Lamp Light")]
    [SerializeField] private Light lampLight;
    [Tooltip("Colour used when there's no lantern. With one, the lantern's stage colour wins")]
    [SerializeField] private Color lightColor = new Color(1f, 0.78f, 0.35f);
    [Tooltip("Seconds to morph between circle and beam")]
    [SerializeField] private float transitionTime = 0.3f;

    [Header("Circle (idle)")]
    [Tooltip("Radius of the lit circle on the ground, in units")]
    [SerializeField] private float circleRadius = 8f;
    [Tooltip("Brightness in the middle of the circle. Stays the same however big the circle is")]
    [SerializeField] private float circleBrightness = 5f;
    [Tooltip("How high above the player the light hangs. Higher = more even light across the circle")]
    [SerializeField] private float circleHeight = 3f;
    [Tooltip("0 = soft edge fading from the centre, 1 = hard edge")]
    [SerializeField, Range(0f, 1f)] private float circleEdgeHardness = 0.6f;

    [Header("Beam (holding suck)")]
    [SerializeField] private float lightIntensity = 40f;
    [Tooltip("Full angle of the beam. Wider than the suck angle so you can see around what you suck.")]
    [SerializeField, Range(1f, 179f)] private float beamAngle = 90f;
    [Tooltip("How far the beam points down toward the ground, in degrees")]
    [SerializeField, Range(0f, 60f)] private float beamTilt = 25f;
    [Tooltip("Beam reaches a bit further than you can suck")]
    [SerializeField] private float beamRangeMultiplier = 2f;
    [Tooltip("Must be outside the player's body or it shadows its own beam")]
    [SerializeField] private Vector3 lightOffset = new Vector3(0f, 0.3f, 0.7f);

    [Tooltip("0 = soft edge fading from the centre, 1 = hard edge")]
    [SerializeField, Range(0f, 1f)] private float beamEdgeHardness = 0.5f;

    [Header("Look")]
    [Tooltip("Rendering layer the player is moved to so the lamp doesn't light (and blow out) the player itself")]
    [SerializeField, Range(1, 31)] private int playerRenderingLayer = 7;
    [Tooltip("How much the lamp flickers like a flame. 0 = steady")]
    [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0.12f;
    [SerializeField] private float flickerSpeed = 3f;

    [Header("Particle Force Fields")]
    [Tooltip("Force fields that pull enemy particles into the lamp (ParticleSucker). Found on the player automatically if left empty")]
    [SerializeField] private ParticleSystemForceField[] particleFields;

    [Header("Lantern")]
    [Tooltip("Lantern whose light sets the size of the lamp. Found on the player automatically if left empty")]
    [SerializeField] private LanternController lantern;

    [Header("Input Actions")]
    public InputActionReference suckAction;
    [Tooltip("Key that fires the Vacuum Burst upgrade")]
    [SerializeField] private Key instantSuckKey = Key.Q;

    private InputAction action;
    private PlayerUpgrades upgrades;
    private float instantSuckTimer;

    // True while you're holding the suck, which slows your walk and stops you swinging
    public bool IsSucking { get; private set; }

    // What the upgrades have made of the lamp, for anything being sucked to read
    public PlayerUpgrades Upgrades => upgrades;
    public float SuckSpeedMultiplier => upgrades != null ? upgrades.suckSpeedMultiplier : 1f;
    private float beamAmount; // 0 = circle, 1 = beam
    private float[] particleFieldStartRanges;
    private float[] particleFieldEndRanges;
    private float largestParticleField;

    // The lantern works out how big its light has grown, and the lamp simply follows it
    private float Growth => lantern != null ? lantern.LightGrowth : 1f;

    // Colour comes from the lantern, which eases it across when you reach a new light stage
    private Color LampColor => lantern != null ? lantern.CurrentColor : lightColor;


    // How far the lamp reaches right now: the lantern's light, plus whatever upgrades add
    public float Range => range * Growth * (upgrades != null ? upgrades.rangeMultiplier : 1f);

    // The suck cone, widened by upgrades
    private float SuckAngle => suckAngle + (upgrades != null ? upgrades.suckAngleBonus : 0f);
    private readonly HashSet<Transform> seen = new HashSet<Transform>();

    private Vector3 Origin => transform.TransformPoint(lightOffset);

    // Where the lamp swallows things, used by drainables to fly their particles in
    public Vector3 LampOrigin => Origin;

    private List<IRunawayEnemy> runawayenemies = new List<IRunawayEnemy>();
    private bool hasCalledComeback = false;

    void Awake()
    {
        // Fall back to the project-wide Suck action (Space / right trigger) if nothing is assigned in the inspector
        action = suckAction != null ? suckAction.action : InputSystem.actions?.FindAction("Player/Suck");
        if (action == null) Debug.LogWarning("LampSuck: no suck action assigned or found", this);

        upgrades = PlayerUpgrades.Ensure(gameObject);
        if (lantern == null) lantern = GetComponentInParent<LanternController>();
        if (lantern == null) Debug.LogWarning("LampSuck: no lantern found, the lamp will stay its starting size", this);

        SetupLight();
        ExcludePlayerFromLamp();
        ApplyLight();
    }

    void Start()
    {
        SetupParticleFields();
    }

    void OnEnable()
    {
        action?.Enable();
    }

    void Update()
    {
        bool sucking = action != null && action.IsPressed() && !UpgradeChooser.IsChoosing;
        IsSucking = sucking;

        if (instantSuckTimer > 0f) instantSuckTimer = Mathf.Max(instantSuckTimer - Time.deltaTime, 0f);
        if (upgrades != null && upgrades.hasInstantSuck && instantSuckTimer <= 0f
            && Keyboard.current != null && Keyboard.current[instantSuckKey].wasPressedThisFrame
            && !UpgradeChooser.IsChoosing)
        {
            InstantSuck();
        }

        float step = transitionTime > 0f ? Time.deltaTime / transitionTime : 1f;
        beamAmount = Mathf.MoveTowards(beamAmount, sucking ? 1f : 0f, step);
        ApplyLight();
        ApplyParticleFields();

        if (!sucking && !hasCalledComeback)
        {
            foreach (IRunawayEnemy runawayenemy in runawayenemies)
            {
                runawayenemy.Comeback();
            }
            hasCalledComeback = true;
            runawayenemies.Clear();
        }

        // Only suck once the beam is mostly out
        if (sucking && beamAmount > 0.5f) 
        {
            SuckTick();
            hasCalledComeback = false;
        }
    }

    private void SuckTick()
    {
        Vector3 origin = Origin;
        float halfAngle = SuckAngle * 0.5f;
        seen.Clear();

        foreach (Collider collider in Physics.OverlapSphere(origin, Range))
        {
            if (collider.transform.IsChildOf(transform)) continue;

            ISuckable suckable = collider.GetComponentInParent<ISuckable>();
            IRunawayEnemy runawayEnemy = collider.GetComponentInParent<IRunawayEnemy>();
            if (runawayEnemy != null)
            {
                runawayEnemy.Runaway();
                runawayenemies.Add(runawayEnemy);
            }
            IDrainable drainable = collider.GetComponentInParent<IDrainable>();
            // A living enemy is neither suckable nor drainable, so Withering Light has to let it
            // through this filter or the beam would never reach it
            EnemyHealth burning = upgrades != null && upgrades.suckDamagePerSecond > 0f
                ? collider.GetComponentInParent<EnemyHealth>()
                : null;
            if (suckable == null && drainable == null && burning == null) continue;

            Transform target = collider.attachedRigidbody != null ? collider.attachedRigidbody.transform : collider.transform;
            if (!seen.Add(target)) continue;

            Vector3 toTarget = target.position - origin;
            // Bodies drain wherever the beam lights them, pickups need the narrower suck cone
            float allowedAngle = drainable != null || burning != null ? beamAngle * 0.5f : halfAngle;
            float angle = Vector3.Angle(transform.forward, toTarget);
            // A fixed angle closes to nothing right in front of you, so something being pulled in
            // would slip out of the cone just before it arrived. Allow a sideways wobble as well.
            float sideways = Mathf.Sin(angle * Mathf.Deg2Rad) * toTarget.magnitude;
            if (angle > allowedAngle && sideways > grabWidth) continue;
            if (IsBlocked(origin, toTarget, target)) continue;

            if (drainable != null)
            {
                drainable.OnDrain(this, Time.deltaTime * SuckSpeedMultiplier);
                continue;
            }

            // Withering Light: the beam burns whatever living thing it's held on
            if (burning != null) burning.TakeContinuousDamage(upgrades.suckDamagePerSecond * Time.deltaTime);

            if (suckable == null) continue;

            if (toTarget.magnitude <= absorbDistance)
            {
                suckable.OnSuck(this);
                continue;
            }

            // Pull the suckable toward the lamp
            Rigidbody rb = collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic) rb.isKinematic = true;
            target.position = Vector3.MoveTowards(target.position, origin, pullSpeed * SuckSpeedMultiplier * Time.deltaTime);
        }
    }

    // True if something that isn't the player or the target is between the lamp and the target
    private bool IsBlocked(Vector3 origin, Vector3 toTarget, Transform target)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude, blockingLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)) continue;
            return true;
        }
        return false;
    }

    private void SetupParticleFields()
    {
        if (particleFields == null || particleFields.Length == 0)
        {
            // The sucker normally sits on the lantern, next to this object under the player
            Transform player = transform.parent != null ? transform.parent : transform;
            particleFields = player.GetComponentsInChildren<ParticleSystemForceField>(true);
        }

        particleFieldStartRanges = new float[particleFields.Length];
        particleFieldEndRanges = new float[particleFields.Length];
        for (int i = 0; i < particleFields.Length; i++)
        {
            particleFieldStartRanges[i] = particleFields[i].startRange;
            particleFieldEndRanges[i] = particleFields[i].endRange;
            largestParticleField = Mathf.Max(largestParticleField, particleFields[i].endRange);
        }
    }

    // The biggest field reaches exactly as far as the suck does, smaller ones keep their proportions,
    // so particles are pulled from as far as bodies can be drained and it all grows with the lamp
    private void ApplyParticleFields()
    {
        if (particleFields == null || largestParticleField <= 0f) return;

        float scale = Range / largestParticleField;
        for (int i = 0; i < particleFields.Length; i++)
        {
            if (particleFields[i] == null) continue;
            particleFields[i].startRange = particleFieldStartRanges[i] * scale;
            particleFields[i].endRange = particleFieldEndRanges[i] * scale;
        }
    }

    // Vacuum Burst: swallow everything within reach at once, whatever direction it's in
    private void InstantSuck()
    {
        instantSuckTimer = upgrades.instantSuckCooldown;
        Vector3 origin = Origin;
        HashSet<Transform> hit = new HashSet<Transform>();

        foreach (Collider collider in Physics.OverlapSphere(origin, Range))
        {
            if (collider.transform.IsChildOf(transform)) continue;

            Transform target = collider.attachedRigidbody != null ? collider.attachedRigidbody.transform : collider.transform;
            if (!hit.Add(target)) continue;

            // A big time step drains a body in one go, see EnemyCorpse.OnDrain
            IDrainable drainable = collider.GetComponentInParent<IDrainable>();
            if (drainable != null) drainable.OnDrain(this, 999f);

            ISuckable suckable = collider.GetComponentInParent<ISuckable>();
            if (suckable != null) suckable.OnSuck(this);
        }

        Debug.Log("[LampSuck] vacuum burst, next one in " + instantSuckTimer + "s");
    }

    private void SetupLight()
    {
        if (lampLight != null) return;

        GameObject lightObject = new GameObject("LampLight");
        lightObject.transform.SetParent(transform, false);

        lampLight = lightObject.AddComponent<Light>();
        lampLight.type = LightType.Spot;
        lampLight.color = LampColor;
    }

    // Put the player's renderers on their own rendering layer and leave that layer out of the lamp,
    // so the lamp lights everything around the player but not the player
    private void ExcludePlayerFromLamp()
    {
        uint playerLayer = 1u << playerRenderingLayer;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.renderingLayerMask = playerLayer;
        }
        lampLight.GetUniversalAdditionalLightData().renderingLayers = ~playerLayer;
    }

    // Distance from the circle light down to the ground (ignoring the player)
    private float GroundDistance(Vector3 lightPosition)
    {
        float closest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(lightPosition, Vector3.down, circleHeight + 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            closest = Mathf.Min(closest, hit.distance);
        }
        return closest == float.MaxValue ? circleHeight + 1f : Mathf.Max(closest, 0.5f);
    }

    private void ApplyLight()
    {
        if (lampLight == null) return;

        // Ease in and out so the swing feels smooth
        float t = Mathf.SmoothStep(0f, 1f, beamAmount);

        float flicker = 1f + (Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 2f - 1f) * flickerAmount;
        float growth = Growth;
        float radius = circleRadius * growth;
        // Light fades with distance squared, so a beam twice as long needs four times the intensity
        float beamIntensity = lightIntensity * growth * growth;

        // Work out the circle light from the radius you want: spot angle from height and radius,
        // intensity scaled by distance squared so brightness doesn't depend on height
        // Hang the light higher as it grows. Widening the cone alone does nothing, because the
        // ground still falls off with distance squared, so the lit circle stayed the same size
        // however big the lantern got. Raising it scales the whole circle: the cone angle stays
        // put, groundDistance grows, and the intensity below grows with its square to match.
        Vector3 circlePosition = new Vector3(0f, circleHeight * growth, 0f);
        float groundDistance = GroundDistance(transform.TransformPoint(circlePosition));
        float circleAngle = Mathf.Min(2f * Mathf.Atan2(radius, groundDistance) * Mathf.Rad2Deg, 179f);
        float circleIntensity = circleBrightness * groundDistance * groundDistance;
        float circleRange = Mathf.Sqrt(groundDistance * groundDistance + radius * radius) * 1.2f;

        float angle = Mathf.Lerp(circleAngle, beamAngle, t);

        lampLight.color = LampColor;
        lampLight.intensity = Mathf.Lerp(circleIntensity, beamIntensity, t) * flicker;
        lampLight.range = Mathf.Lerp(circleRange, Range * beamRangeMultiplier, t);
        lampLight.spotAngle = angle;
        lampLight.innerSpotAngle = Mathf.Lerp(1f, angle, Mathf.Lerp(circleEdgeHardness, beamEdgeHardness, t));

        // Circle points straight down from above the player, beam points forward and a bit down
        lampLight.transform.localPosition = Vector3.Lerp(circlePosition, lightOffset, t);
        lampLight.transform.localRotation = Quaternion.Euler(Mathf.Lerp(90f, beamTilt, t), 0f, 0f);

        // Beam shadows make trees block the light so you can't see behind them
        lampLight.shadows = t > 0.5f ? LightShadows.Soft : LightShadows.None;
        lampLight.shadowNearPlane = 0.3f;
    }

    void OnDrawGizmos()
    {
        Vector3 origin = Origin;
        Vector3 forward = transform.forward * Range;

        // Suck area
        Gizmos.color = Color.red;
        float suckHalf = suckAngle * 0.5f;
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-suckHalf, transform.up) * forward);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(suckHalf, transform.up) * forward);

        // Beam
        Gizmos.color = lightColor;
        float beamHalf = beamAngle * 0.5f;
        Vector3 beamForward = Quaternion.AngleAxis(beamTilt, transform.right) * forward * beamRangeMultiplier;
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-beamHalf, transform.up) * beamForward);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(beamHalf, transform.up) * beamForward);

        // Circle
        Gizmos.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.3f);
        Gizmos.DrawWireSphere(transform.position, circleRadius * Growth);
    }
}
