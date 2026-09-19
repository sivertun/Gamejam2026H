using UnityEngine;

public class BasicEnemyMovement : MonoBehaviour, IHasTarget
{
    [SerializeField] private Transform target;
    [SerializeField] private float moveSpeed;

    void FixedUpdate()
    {
        float step = moveSpeed * Time.fixedDeltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target.position, step);
        transform.LookAt(target);
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }
}
