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
    [Tooltip("Seconds between the swing animation starting and the hitbox appearing")]
    [SerializeField] private float hitDelay;

    [Header("Bit spaghetti but dont worry about it")]
    [SerializeField] private float playerWidth;

    private float cooldownTimer;
    private CharacterAnimator characterAnimator;

    void Awake()
    {
        characterAnimator = GetComponent<CharacterAnimator>();
    }

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

        if (characterAnimator != null) characterAnimator.PlayAttack();
        if (hitDelay > 0) Invoke(nameof(SpawnHitbox), hitDelay);
        else SpawnHitbox();

        cooldownTimer = cooldown;
    }

    private void SpawnHitbox()
    {
        Vector3 hitboxPosition = transform.position + transform.rotation * hitboxOffset;
        GameObject hitbox = Instantiate(hitboxPrefab, hitboxPosition, transform.rotation, transform);
        Hitbox hitboxScript = hitbox.GetComponent<Hitbox>();
        hitboxScript.Initialize(gameObject, target, hitboxSize, damage, knockback, lifetime);
    }

    public void SetTarget(GameObject target)
    {
        this.target = target;
    }
}
