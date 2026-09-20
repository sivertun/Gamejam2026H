using UnityEngine;

// Ties a character's model animations to gameplay. Sits on the same object as the movement
// script (player or enemy) and drives the Animator on the model underneath it:
// idle/walk from how fast the character actually moves, attack from the melee scripts.
public class CharacterAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private Animator animator;
    [Tooltip("Seconds to smooth the walk/idle blend")]
    [SerializeField] private float speedDampTime = 0.1f;

    private Vector3 lastPosition;
    private float speed;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // Movement comes from the movement scripts, never from the clips
        if (animator != null) animator.applyRootMotion = false;
        lastPosition = transform.position;
    }

    // Movement happens in FixedUpdate, so measure there to get a steady speed
    void FixedUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0;
        lastPosition = transform.position;
        speed = delta.magnitude / Time.fixedDeltaTime;
    }

    void Update()
    {
        if (animator == null) return;
        animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);
    }

    public void PlayAttack()
    {
        if (animator == null) return;
        animator.SetTrigger(AttackHash);
    }
}
