using System.Collections.Generic;
using UnityEngine;

public class LanternController : MonoBehaviour
{
    [SerializeField] private float lightLevelIncrement = 0.2f;
    [SerializeField] private float decayPerSec = 0.01f;
    [SerializeField] private int maxLightIntensity = 30;
    private int lightStage = 1;
    private float lightLevel = 0.2f;    
    private Light lightObject;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();

    void Awake()
    {
        lightObject = GetComponent<Light>();
    }


    void Update()
    {
        lightLevel -= decayPerSec*Time.deltaTime;
        lightObject.intensity = lightLevel*maxLightIntensity;
        if (CheckLightDead() == true)
        {
            // Lets kill the player!
            print("TODO: Player died!");
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

    public void UpgradeLightLevel() {
        lightLevel += lightLevelIncrement;
        if (lightLevel >= 1) {
            lightLevel = 0.2f;
            lightStage++;
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
}
