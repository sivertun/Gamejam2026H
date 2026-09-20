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
    [Tooltip("Seconds between the swing animation starting and the hitbox appearing")]
    [SerializeField] private float hitDelay;

    [Header("Input Actions")]
    public InputActionReference attackAction;

    private float cooldownTimer;
    private CharacterAnimator characterAnimator;
    private LampSuck lampSuck;
    private PlayerMovement movement;
    private PlayerAudio playerAudio;

    void Awake()
    {
        characterAnimator = GetComponent<CharacterAnimator>();
        lampSuck = GetComponent<LampSuck>();
        movement = GetComponent<PlayerMovement>();
        playerAudio = PlayerAudio.Ensure(gameObject);
    }

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
        // Both hands are busy holding the lamp up
        if (lampSuck != null && lampSuck.IsSucking) return;
        if (movement != null && movement.IsDodging) return;
        if (UpgradeChooser.IsChoosing) return;

        if (characterAnimator != null) characterAnimator.PlayAttack();
        if (playerAudio != null) playerAudio.PlaySwing();
        if (hitDelay > 0) Invoke(nameof(SpawnHitbox), hitDelay);
        else SpawnHitbox();

        cooldownTimer = cooldown;
    }

    private void SpawnHitbox()
    {
        Vector3 hitboxPosition = transform.position + transform.rotation * hitboxOffset;
        GameObject hitbox = Instantiate(hitboxPrefab, hitboxPosition, transform.rotation, transform);
        Hitbox hitboxScript = hitbox.GetComponent<Hitbox>();
        hitboxScript.Initialize(gameObject, null, hitboxSize, damage, knockback, lifetime);
    }
}
