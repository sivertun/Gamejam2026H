using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class LanternController : MonoBehaviour, IDamagable 
{
    [SerializeField] private float invincibilityTime;
    private float invincibilityTimer;
    [SerializeField] private float decayPerSec = 0.01f;
    [SerializeField] private int maxLightIntensity = 30;
    [Tooltip("Light lost every time you get hit, whatever hit you. 1 = a whole light stage")]
    [SerializeField] private float lightLostPerHit = 0.0625f;
    [Tooltip("Light the lantern starts the game with")]
    [SerializeField] private float startLightLevel = 0.2f;

    [Header("How far the light reaches")]
    [Tooltip("Smallest the light shrinks to when it's nearly out, against its starting size")]
    [SerializeField] private float minGrowth = 0.8f;
    [Tooltip("Largest the light ever grows to, against its starting size")]
    [SerializeField] private float maxGrowth = 4f;
    [Tooltip("Light level at which it reaches its largest. Light stage 5 is at 4")]
    [SerializeField] private float lightForMaxGrowth = 4f;
    private int lightStage = 1;
    private float lightLevel;
    private Light lightObject;
    private float startRange;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();
    private bool dead;

    [SerializeField] private GameController gameController;

    void Awake()
    {
        lightObject = GetComponent<Light>();
        lightLevel = startLightLevel;
        startRange = lightObject.range;
    }


    void Update()
    {
        if (dead) return;
        if (invincibilityTimer != 0) invincibilityTimer = Mathf.Max(invincibilityTimer - Time.deltaTime, 0);

        lightLevel -= decayPerSec*Time.deltaTime;
        // Brighter and further together, so a dying lantern lights less ground as well as less well
        lightObject.intensity = TotalLight * maxLightIntensity;
        lightObject.range = startRange * LightGrowth;
        if (CheckLightDead() == true)
        {
            dead = true;
            DeathSequence.Play();
        }
    }

    // All the light in the lantern, and the one number the rest of the game reads. It is your
    // health, it sets how far and how brightly you see, and the whole numbers it passes are the
    // light stages. Only decay and damage take it away.
    public float TotalLight => lightLevel;

    // What TotalLight is at the very start of the game, so others can size themselves against it
    public float StartLight => startLightLevel;

    // How big the light is against the start of the game. Drives this lantern's own range and
    // everything the lamp does, see LampSuck, so all of it grows and shrinks as one.
    public float LightGrowth
    {
        get
        {
            if (startLightLevel <= 0f) return 1f;
            float light = TotalLight;

            // A starting lantern gives exactly the tuned sizes. Below that the darkness closes in
            // on you, above it the light opens out but levels off.
            if (light < startLightLevel) return Mathf.Lerp(minGrowth, 1f, Mathf.Clamp01(light / startLightLevel));
            return Mathf.Lerp(1f, maxGrowth,
                Mathf.InverseLerp(startLightLevel, Mathf.Max(lightForMaxGrowth, startLightLevel + 0.01f), light));
        }
    }

    // Which stage the lantern is on, 1 at the start of the game
    public int LightStage => lightStage;

    // How full the lantern is within the current stage
    public float LightLevel => lightLevel;

    // Light banked since the game began: 0 at the start, about +1 per light stage. Used as the
    // difficulty clock, see EnemySpawner.
    public float LightProgress => Mathf.Max(0f, lightLevel - startLightLevel);

    private bool CheckLightDead()
    {
        if (lightLevel <= 0)
        {
            if (gameController) gameController.dead();
            return true;
        }
        return false;
    }

    public void UpgradeLightLevel(float lightAmount) {
        lightLevel += lightAmount;

        // Stages are milestones the light passes, not a bar that empties, so reaching one never
        // costs you brightness or reach. Stage 2 is at 1 light, stage 3 at 2, and so on.
        while (lightLevel >= lightStage) ReachNextStage();
    }

    private void ReachNextStage()
    {
        lightStage++;

        switch (lightStage)
        {
            // Each stage burns hotter: deeper orange through to near-white
            case 2:
                lightObject.color = new Color(1f, 0.72f, 0.35f);
                break;
            case 3:
                lightObject.color = new Color(1f, 0.84f, 0.5f);
                break;
            case 4:
                lightObject.color = new Color(1f, 0.93f, 0.72f);
                break;
            case 5:
                lightObject.color = Color.white;
                print("Game won!");
                // TODO: Make end game code
                break;
        }

        Debug.Log("[Lantern] reached light stage " + lightStage);
        NotifyOnLightStageUpgrade();
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

        // Every hit costs the same, however hard it hit, so the cost is easy to read while playing
        DowngradeLightLevel(lightLostPerHit);

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }
}
