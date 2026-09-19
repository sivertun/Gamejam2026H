using UnityEngine;

public class Hitbox : MonoBehaviour
{
    private GameObject attacker;
    private GameObject target;

    void OnTriggerEnter(Collider other)
    {
        // Ignore trigger if triggered by attacker
        if (other.gameObject == attacker) return;
        // Ignore trigger if not triggered by correct target (if one is specified)
        if (target != null && other.gameObject != target) return;

        Debug.Log(other.gameObject.name);
    }

    public void Initialize(GameObject attacker, GameObject target, Vector3 size, float lifetime)
    {
        this.attacker = attacker;
        this.target = target;

        transform.localScale = size;
        Destroy(gameObject, lifetime);
    }
}
