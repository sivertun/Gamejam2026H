using UnityEngine;


public class EnemyRunAwayFromLight : MonoBehaviour, IRunawayEnemy, IHasTarget, IHasVelocity
{
    private Rigidbody rb;

    [SerializeField] private Transform target;

    [SerializeField] private float maxSpeed;
    [SerializeField] private float smoothTime;
    private Vector3 velocity;
    private Vector3 movementDerivative;
    private int movementDirection = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 moveDirection = target.position - transform.position;
        Vector3 targetVelocity = maxSpeed * moveDirection.normalized * movementDirection;
        velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref movementDerivative, smoothTime);
        rb.MovePosition(transform.position + velocity * Time.fixedDeltaTime);
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    public void SetVelocity(Vector3 velocity)
    {
        this.velocity = velocity;
    }

    public void Runaway()
    {
        movementDirection = -1;
    }

    public void Comeback()
    {
        movementDirection = 1;
    }
}
