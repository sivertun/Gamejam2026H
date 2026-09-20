using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class LanternController : MonoBehaviour, IDamagable 
{
    [SerializeField] private float invincibilityTime;
    private float invincibilityTimer;
    [SerializeField] private float decayPerSec = 0.01f;
    [SerializeField] private int maxLightIntensity = 30;
    [SerializeField] private float damageLightLevelConversion = 0.01f;
    private int lightStage = 1;
    private float lightLevel = 0.2f;    
    private Light lightObject;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();
    private bool dead;

    void Awake()
    {
        lightObject = GetComponent<Light>();
    }


    void Update()
    {
        if (dead) return;
        if (invincibilityTimer != 0) invincibilityTimer = Mathf.Max(invincibilityTimer - Time.deltaTime, 0);

        lightLevel -= decayPerSec*Time.deltaTime;
        lightObject.intensity = lightLevel*maxLightIntensity;
        if (CheckLightDead() == true)
        {
            dead = true;
            DeathSequence.Play();
        }
    }

    private bool CheckLightDead()
    {
        if (lightLevel <= 0)
        {
            return true;
        }
        return false;
    }

    public void UpgradeLightLevel(float lightAmount) {
        lightLevel += lightAmount;
        if (lightLevel >= 1) {
            lightLevel = 0.2f;
            lightStage++;

            switch (lightStage)
            {
                case 2:
                    lightObject.color = Color.darkRed;
                    break;
                case 3:
                    lightObject.color = Color.darkGreen;
                    break;
                case 4:
                    lightObject.color = Color.darkMagenta;
                    break;
                case 5:
                    print("Game won!");
                    // TODO: Make end game code
                    break;
                default:
                    Debug.LogWarning("Initiated lightStage that does not exist! Warning!");
                    break;
            }
            NotifyOnLightStageUpgrade();
        }
    }

    public void DowngradeLightLevel(float decreaseAmount)
    {
        lightLevel -= decreaseAmount;
    }
    public void RegisterObserver(ILightStageObserver observer)
    {
        observers.Add(observer);
    }

    public void UnregisterObserver(ILightStageObserver observer)
    {
        observers.Remove(observer);
    }

    public void NotifyOnLightStageUpgrade()
    {
        foreach (var observer in observers)
        {
            observer.OnLightStageUpgraded(newStage: lightStage);
        }
    }

    public void TakeDamage(float damage, float knockback, Transform source)
    {
        if (invincibilityTimer != 0) return;
        invincibilityTimer = invincibilityTime;

        lightLevel -= damage * damageLightLevelConversion;

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }
}
