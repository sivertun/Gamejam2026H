using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamagable
{

    [SerializeField] private float maxHealth;
    private float health;
    
    [SerializeField] private GameObject deadPrefab;

    void Awake()
    {
        health = maxHealth;
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
        Instantiate(deadPrefab, transform.position, transform.rotation);
    }
}
