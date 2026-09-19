using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour, IHasTarget
{
    [SerializeField] private GameObject target;
    [SerializeField] private GameObject hitboxPrefab;
    [SerializeField] private Vector3 hitboxSize;
    [SerializeField] private Vector3 hitboxOffset;
    [SerializeField] private float preHitBuffer;
    [SerializeField] private float cooldown;
    [SerializeField] private float lifetime;
    [SerializeField] private float damage;
    [SerializeField] private float knockback;

    [Header("Bit spaghetti but dont worry about it")]
    [SerializeField] private float playerWidth;

    private float cooldownTimer;


    void Update()
    {
        if (cooldownTimer != 0) cooldownTimer = Mathf.Max(cooldownTimer - Time.deltaTime, 0);

        if ((target.transform.position - transform.position).magnitude <= hitboxSize.z + preHitBuffer + playerWidth / 2)
        {
            PerformAttack();
            
        }
    }

    private void PerformAttack()
    {
        if (cooldownTimer != 0) return;

        Vector3 hitboxPosition = transform.position + transform.rotation * hitboxOffset;
        GameObject hitbox = Instantiate(hitboxPrefab, hitboxPosition, transform.rotation, transform);
        Hitbox hitboxScript = hitbox.GetComponent<Hitbox>();
        hitboxScript.Initialize(gameObject, target, hitboxSize, damage, knockback, lifetime);

        cooldownTimer = cooldown;
    }

    public void SetTarget(GameObject target)
    {
        this.target = target;
    }
}
