using UnityEngine;

public interface IDamagable
{
    public void TakeDamage(float damage, float knockback, Transform source);
}
