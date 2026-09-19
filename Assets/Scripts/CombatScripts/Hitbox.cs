using UnityEngine;

public class Hitbox : MonoBehaviour
{
    private GameObject attacker;
    private GameObject target;
    private float damage;
    private float knockback;

    void OnTriggerEnter(Collider other)
    {
        // Ignore trigger if triggered by attacker
        if (other.gameObject == attacker) return;
        // Ignore trigger if not triggered by correct target (if one is specified)
        if (target != null && other.gameObject != target) return;

        Debug.Log(other.gameObject.name);
        
        other.gameObject.GetComponent<IDamagable>().TakeDamage(damage, knockback, attacker.transform);

    }

    public void Initialize(GameObject attacker, GameObject target, Vector3 size, float damage, float knockback, float lifetime)
    {
        this.attacker = attacker;
        this.target = target;

        transform.localScale = size;
        this.damage = damage;
        this.knockback = knockback;

        Destroy(gameObject, lifetime);
    }
}
