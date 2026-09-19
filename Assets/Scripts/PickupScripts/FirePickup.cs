using UnityEngine;

public class FirePickup : MonoBehaviour, ISuckable
{
    [SerializeField] private float rangeBonus = 1f;
    public void OnSuck(LampSuck lamp)
    {
        lamp.AddRange(rangeBonus);
        Destroy(gameObject);
    }
}