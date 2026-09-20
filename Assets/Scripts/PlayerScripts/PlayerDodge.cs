using UnityEngine;
using UnityEngine.InputSystem;

// Shift to roll. Locked until the Tuck and Roll upgrade unlocks it, then it's a committed move:
// you go where you pointed, you can't swing out of it, and you're untouchable for the first part.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDodge : MonoBehaviour
{
    [Tooltip("Key that rolls. Right shift works too")]
    [SerializeField] private Key dodgeKey = Key.LeftShift;
    [Tooltip("How fast the roll throws you, in units a second")]
    [SerializeField] private float dodgeSpeed = 16f;
    [Tooltip("How long the roll lasts. The clip is sped up to match, see GameSetupTool")]
    [SerializeField] private float dodgeDuration = 0.6f;

    private PlayerMovement movement;
    private PlayerUpgrades upgrades;
    private CharacterAnimator characterAnimator;
    private LanternController lantern;
    private float cooldownTimer;

    // How much of the cooldown is left, 0 when it's ready
    public float CooldownRemaining => cooldownTimer;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        upgrades = PlayerUpgrades.Ensure(gameObject);
        characterAnimator = GetComponent<CharacterAnimator>();
        lantern = GetComponentInParent<LanternController>();
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(cooldownTimer - Time.deltaTime, 0f);

        if (!upgrades.hasDodgeRoll) return;
        if (cooldownTimer > 0f || movement.IsDodging) return;
        if (UpgradeChooser.IsChoosing || DeathSequence.IsDead) return;
        if (!Pressed()) return;

        Roll();
    }

    private bool Pressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return keyboard[dodgeKey].wasPressedThisFrame
            || (dodgeKey == Key.LeftShift && keyboard[Key.RightShift].wasPressedThisFrame);
    }

    private void Roll()
    {
        // Roll where you're walking, or straight ahead when you're standing still
        Vector3 direction = movement.MoveInput;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;

        movement.StartDodge(direction, dodgeSpeed, dodgeDuration);
        if (characterAnimator != null) characterAnimator.PlayRoll();

        // Rolling through an attack is the whole point of having one
        if (lantern != null) lantern.GrantInvincibility(upgrades.dodgeInvulnerability);

        cooldownTimer = upgrades.dodgeCooldown;
    }
}
