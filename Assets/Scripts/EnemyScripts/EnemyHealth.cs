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

    // For the lamp beam burning an enemy: no knockback, and invincibility doesn't apply, because
    // this arrives in tiny slices every frame rather than as one hit
    public void TakeContinuousDamage(float damage)
    {
        health -= damage;
        if (health <= 0) Die();
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

    private bool dying;

    private void Die()
    {
        if (dying) return;
        dying = true;

        Destroy(gameObject);
        GameObject dead = Instantiate(deadPrefab, transform.position, transform.rotation);

        // Hand the model over so the corpse is the enemy itself, not a placeholder
        Animator model = GetComponentInChildren<Animator>();
        if (model != null && dead.TryGetComponent(out EnemyCorpse corpse)) corpse.TakeModel(model.transform);
    }
}
