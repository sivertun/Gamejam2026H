using System;
using UnityEngine;


public class TestLantern : MonoBehaviour, ILightStageObserver
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    LanternController lanternController;
    void Start()
    {
        lanternController = GetComponent<LanternController>();
        lanternController.RegisterObserver(this);
    }

    private float count = 0;
    // Update is called once per frame
    void Update()
    {
        count += 1*Time.deltaTime;
        if (count >= 1)
        {
            print("Light increased");
            count = 0;
            lanternController.UpgradeLightLevel(0.4f);
        }
    }

    public void OnLightStageUpgraded(int newStage)
    {
        print("Light stage announced as upgraded to: " + newStage);
    }
}
