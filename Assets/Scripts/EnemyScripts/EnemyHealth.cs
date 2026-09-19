using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamagable
{
    [SerializeField] private float invincibilityTime;
    private float invincibilityTimer;
    [SerializeField] private float maxHealth;
    private float health;
    
    [SerializeField] private GameObject deadPrefab;

    void Awake()
    {
        health = maxHealth;
    }

    void Update()
    {
        if (invincibilityTimer != 0) invincibilityTimer = Mathf.Max(invincibilityTimer - Time.deltaTime, 0);
    }

    public void TakeDamage(float damage, float knockback, Transform source)
    {
        if (invincibilityTimer != 0) return;
        invincibilityTimer = invincibilityTime;

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
