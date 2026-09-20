using UnityEngine;

// Fire lying in the world. Sucking it in feeds the lantern and makes the lamp reach further.
public class FirePickup : MonoBehaviour, ISuckable
{
    [Tooltip("How much further the lamp reaches after this fire, in units")]
    [SerializeField] private float rangeBonus = 1f;
    [Tooltip("Lantern light this fire gives. 1 = a whole lantern stage")]
    [SerializeField] private float lightBonus = 0.25f;

    public void OnSuck(LampSuck lamp)
    {
        lamp.AddRange(rangeBonus);

        // The lantern sits on the player, above the lamp
        LanternController lantern = lamp.GetComponentInParent<LanternController>();
        if (lantern != null) lantern.UpgradeLightLevel(lightBonus);
        else Debug.LogWarning("FirePickup: no LanternController above the lamp, so this fire gave no light", this);

        Destroy(gameObject);
    }
}
