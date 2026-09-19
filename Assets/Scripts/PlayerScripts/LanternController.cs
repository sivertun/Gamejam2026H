using System.Collections.Generic;
using UnityEngine;

public class LanternController : MonoBehaviour
{
    [SerializeField] private float decayPerSec = 0.01f;
    private int lightStage = 1;
    private float lightLevel = 0.2f;    
    private Light lightComponent;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();

    void Start()
    {
        lightComponent = transform.Find("LampLight").GetComponent<Light>();
    }


    void Update()
    {
        lightLevel -= decayPerSec*Time.deltaTime;
        if (CheckLightDead() == true)
        {
            // Lets kill the player!
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
                    lightComponent.color = Color.lightBlue;
                    break;
                case 3:
                    lightComponent.color = Color.lightPink;
                    break;
                case 4:
                    lightComponent.color = Color.lightGreen;
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

    public float getLightLevel()
    {
        return lightLevel;
    }
}
