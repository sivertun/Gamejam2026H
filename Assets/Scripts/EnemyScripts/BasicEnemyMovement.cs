using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BasicEnemyMovement : MonoBehaviour, IHasTarget
{
    private Rigidbody rb;
    [SerializeField] private Transform target;
    [SerializeField] private float moveSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float step = moveSpeed * Time.fixedDeltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target.position, step);
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }
}
