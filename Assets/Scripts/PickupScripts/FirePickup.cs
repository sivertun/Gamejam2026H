using UnityEngine;

// Fire lying in the world. Sucking it in feeds the lantern, and because the lamp is sized by the
// lantern's light, that is also what makes the lamp reach further.
public class FirePickup : MonoBehaviour, ISuckable
{
    [Tooltip("Lantern light this fire gives. 1 = a whole light stage")]
    [SerializeField] private float lightBonus = 0.25f;

    public void OnSuck(LampSuck lamp)
    {
        // The lantern sits on the player, above the lamp
        LanternController lantern = lamp.GetComponentInParent<LanternController>();
        if (lantern != null) lantern.UpgradeLightLevel(lightBonus);
        else Debug.LogWarning("FirePickup: no LanternController above the lamp, so this fire gave no light", this);

        Destroy(gameObject);
    }
}
