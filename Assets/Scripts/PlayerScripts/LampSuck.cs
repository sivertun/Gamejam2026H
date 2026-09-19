using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LampSuck : MonoBehaviour
{
    [SerializeField] private float range = 6f;
    [SerializeField] private float width = 3f;
    [SerializeField] private float pullSpeed = 6f;
    [SerializeField] private float absorbDistance = 1f;

    [Header("Cone Light")]
    [SerializeField] private Light coneLight;
    [SerializeField] private Color coneColor = new Color(1f, 0.78f, 0.35f);
    [SerializeField] private float lightIntensity = 30f;
    [SerializeField] private Vector3 lightOffset = new Vector3(0f, 0.2f, 0.4f);

    [Header("Cone Visual")]
    [SerializeField] private Material coneMaterial;
    [SerializeField, Range(0f, 1f)] private float coneAlpha = 0.25f;

    [Header("Input Actions")]
    public InputActionReference suckAction;

    private InputAction action;
    private readonly HashSet<Collider> seen = new HashSet<Collider>();

    private Vector3 Origin => transform.TransformPoint(lightOffset);
    private float HalfAngle => Mathf.Atan2(Mathf.Max(width, 0.01f), Mathf.Max(range, 0.01f)) * Mathf.Rad2Deg;

    void Awake()
    {
        // Fall back to the project-wide Suck action (Space / right trigger) if nothing is assigned in the inspector
        action = suckAction != null ? suckAction.action : InputSystem.actions?.FindAction("Player/Suck");
        if (action == null) Debug.LogWarning("LampSuck: no suck action assigned or found", this);

        SetupConeLight();
        ApplyCone();
        SetSucking(false);
    }

    void OnEnable()
    {
        action?.Enable();
    }

    void Update()
    {
        bool sucking = action != null && action.IsPressed();
        SetSucking(sucking);
        if (sucking) SuckTick();
    }

    private void SetSucking(bool sucking)
    {
        if (coneLight != null) coneLight.gameObject.SetActive(sucking);
    }

    private void SuckTick()
    {
        Vector3 origin = Origin;
        float halfAngle = HalfAngle;
        seen.Clear();

        foreach (Collider collider in Physics.OverlapSphere(origin, range))
        {
            if (collider.transform.IsChildOf(transform)) continue;

            Transform target = collider.attachedRigidbody != null ? collider.attachedRigidbody.transform : collider.transform;
            Vector3 toTarget = target.position - origin;
            if (Vector3.Angle(transform.forward, toTarget) > halfAngle) continue;

            ISuckable suckable = collider.GetComponentInParent<ISuckable>();
            if (suckable == null || !seen.Add(collider)) continue;

            if (toTarget.magnitude <= absorbDistance)
            {
                suckable.OnSuck(this);
                continue;
            }

            // Pull the suckable toward the lamp
            Rigidbody rb = collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true;
            }
            target.position = Vector3.MoveTowards(target.position, origin, pullSpeed * Time.deltaTime);
        }
    }

    public void AddRange(float rangeBonus)
    {
        // Grow width proportionally so the cone keeps its angle
        float ratio = width / Mathf.Max(range, 0.01f);
        range += rangeBonus;
        width = range * ratio;
        ApplyCone();
    }

    private void SetupConeLight()
    {
        if (coneLight != null) return;

        GameObject lightObject = new GameObject("LampConeLight");
        lightObject.transform.SetParent(transform, false);

        coneLight = lightObject.AddComponent<Light>();
        coneLight.type = LightType.Spot;
        coneLight.shadows = LightShadows.Soft;
    }

    // Unit cone: tip at origin, opening along +Z, length 1, radius 1
    private static Mesh BuildConeMesh(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 1f);

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        Mesh mesh = new Mesh { name = "LampCone", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ApplyCone()
    {
        if (coneLight != null)
        {
            coneLight.color = coneColor;
            coneLight.intensity = lightIntensity;
            coneLight.range = range;
            coneLight.spotAngle = Mathf.Clamp(HalfAngle * 2f, 1f, 179f);
            coneLight.innerSpotAngle = coneLight.spotAngle * 0.5f;
            coneLight.transform.localPosition = lightOffset;
            coneLight.transform.localRotation = Quaternion.identity;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = coneColor;
        Vector3 origin = Origin;
        float halfAngle = HalfAngle;
        Vector3 forward = transform.forward * range;
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-halfAngle, transform.up) * forward);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(halfAngle, transform.up) * forward);
        Gizmos.DrawRay(origin, forward);
    }
}
