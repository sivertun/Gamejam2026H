using System.Drawing;
using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeAttack : MonoBehaviour
{
    [SerializeField] private GameObject hitboxPrefab;
    [SerializeField] private Vector3 hitboxSize;
    [SerializeField] private Vector3 hitboxOffset;
    [SerializeField] private float cooldown;
    [SerializeField] private float lifetime;
    [SerializeField] private float damage;
    [SerializeField] private float knockback;

    [Header("Input Actions")]
    public InputActionReference attackAction;

    private float cooldownTimer;


    void Update()
    {
        if (cooldownTimer != 0) cooldownTimer = Mathf.Max(cooldownTimer - Time.deltaTime, 0);

        if (attackAction.action.WasPerformedThisFrame())
        {
            PerformAttack();
            
        }
    }

    private void PerformAttack()
    {
        if (cooldownTimer != 0) return;

        Vector3 hitboxPosition = transform.position + transform.rotation * hitboxOffset;
        GameObject hitbox = Instantiate(hitboxPrefab, hitboxPosition, transform.rotation, transform);
        Hitbox hitboxScript = hitbox.GetComponent<Hitbox>();
        hitboxScript.Initialize(gameObject, null, hitboxSize, damage, knockback, lifetime);

        cooldownTimer = cooldown;
    }
}
