using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyHealth : MonoBehaviour, IDamagable
{
    [SerializeField] private float maxHealth;
    private float health;
    private Rigidbody rb;

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody>();
    }

    public void TakeDamage(float damage, float knockback, Transform source)
    {
        health -= damage;
        if (health <= 0)
        {
            Die();
            return;
        }

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }

    private void Die()
    {
        Destroy(gameObject);

        // TODO: spawn dead enemy prefab
    }
}
