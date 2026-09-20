using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour, IHasVelocity
{
    private Rigidbody rb;
    private Camera mainCamera;

    [SerializeField] private float maxSpeed;
    [SerializeField] private float smoothTime;
    public Vector3 velocity;
    private Vector3 movementDerivative;

    public bool enable;

    [Header("Input Actions")]
    public InputActionReference moveAction;

    private LampSuck lampSuck;
    private PlayerUpgrades upgrades;
    private Vector3 dodgeDirection;
    private float dodgeSpeed;
    private float dodgeDuration;
    private float dodgeTimer;

    public bool IsDodging => dodgeTimer > 0f;

    // Which way you're asking to walk, for the roll to send you
    public Vector3 MoveInput
    {
        get
        {
            Vector2 raw = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            return new Vector3(raw.x, 0f, raw.y);
        }
    }

    // Called by PlayerDodge. Committed movement: input is ignored until the roll is done.
    public void StartDodge(Vector3 direction, float speed, float duration)
    {
        if (duration <= 0f) return;
        dodgeDirection = direction.normalized;
        dodgeSpeed = speed;
        dodgeDuration = duration;
        dodgeTimer = duration;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
        lampSuck = GetComponent<LampSuck>();
        upgrades = PlayerUpgrades.Ensure(gameObject);
    }

    // Holding the suck plants your feet. Steady Hands gives most of the speed back.
    private float SpeedMultiplier => lampSuck != null && lampSuck.IsSucking ? upgrades.suckMoveSpeed : 1f;

    void FixedUpdate()
    {
        if (dodgeTimer > 0f)
        {
            DodgeStep();
            return;
        }

        Vector3 moveInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0, moveAction.action.ReadValue<Vector2>().y);
        Vector3 targetVelocity = maxSpeed * SpeedMultiplier * moveInput.normalized;
        velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref movementDerivative, smoothTime);
        rb.MovePosition(transform.position + velocity * Time.fixedDeltaTime);

        rb.MoveRotation(FindLookRotation());
    }

    private void DodgeStep()
    {
        dodgeTimer = Mathf.Max(dodgeTimer - Time.fixedDeltaTime, 0f);

        // Quick off the mark then settling, so the roll lands instead of stopping dead
        float progress = 1f - dodgeTimer / dodgeDuration;
        velocity = dodgeDirection * dodgeSpeed * (1f - progress * progress);

        rb.MovePosition(transform.position + velocity * Time.fixedDeltaTime);
        // Face the way you're rolling, otherwise the clip plays sideways
        rb.MoveRotation(Quaternion.LookRotation(dodgeDirection));
    }

    private Quaternion FindLookRotation()
    {
        if (Pointer.current != null)
        {
            Vector2 pointerScreenPos = Pointer.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(pointerScreenPos);

            Plane mouseInteractionPlane = new Plane(Vector3.up, transform.position);

            if (mouseInteractionPlane.Raycast(ray, out float planeDistance))
            {
                Vector3 pointerWorldPos = ray.GetPoint(planeDistance);
                Vector3 direction = pointerWorldPos - transform.position;
                direction.y = 0;

                return Quaternion.LookRotation(direction);
            }
        }

        return Quaternion.identity;
    }

    public void SetVelocity(Vector3 velocity)
    {
        this.velocity = velocity;
    }
}