using UnityEngine;
using System;

public class AssassinEnemy : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] private Transform target;

    [SerializeField] private float maxSpeed;
    [SerializeField] private float minSpeed;
    private float currentSpeed;
    [SerializeField] private float predictionAmount;
    private float margin = 0.1f;
    [SerializeField] private float slowdownDistance;
    [SerializeField] private float smoothTime;
    private Vector3 velocity;
    private Vector3 movementDerivative;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        currentSpeed = ((target.position-transform.position).magnitude < slowdownDistance) ? minSpeed : maxSpeed;
        
        Vector3 playerVelocity = target.gameObject.GetComponent<PlayerMovement>().velocity;
        Vector3 enemyToPlayerVector = target.position - transform.position;

        Vector3 moveDirection = (Math.Abs((-enemyToPlayerVector).normalized.x - playerVelocity.normalized.x) > margin || Math.Abs((-enemyToPlayerVector).normalized.y - playerVelocity.normalized.y) > margin) ? enemyToPlayerVector + playerVelocity * predictionAmount : enemyToPlayerVector;
        Vector3 targetVelocity = currentSpeed * moveDirection.normalized;
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
}
