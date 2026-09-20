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
    [Tooltip("Light every reached stage keeps for good, so upgrading never makes the lantern dimmer")]
    [SerializeField] private float stagePermanentLight = 0.15f;
    [Tooltip("Light the lantern starts each stage with")]
    [SerializeField] private float startLightLevel = 0.2f;

    [Header("How far the light reaches")]
    [Tooltip("Smallest the light shrinks to when it's nearly out, against its starting size")]
    [SerializeField] private float minGrowth = 0.5f;
    [Tooltip("Largest the light ever grows to, against its starting size")]
    [SerializeField] private float maxGrowth = 2f;
    [Tooltip("Total light at which it reaches its largest")]
    [SerializeField] private float lightForMaxGrowth = 1.5f;
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

    // All the light the lantern is burning: what's in it now plus what each stage keeps for good.
    // This is the one number the rest of the game reads, see LampSuck and EnemySpawner.
    public float TotalLight => lightLevel + (lightStage - 1) * stagePermanentLight;

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

    // 0 at the start of the game and +1 for every light stage gained, running smoothly across
    // upgrades. Used as the difficulty clock, see EnemySpawner.
    public float LightProgress
    {
        get
        {
            float stageSpan = Mathf.Max(1f - startLightLevel, 0.0001f);
            return (lightStage - 1) + Mathf.Clamp01((lightLevel - startLightLevel) / stageSpan);
        }
    }

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
        if (lightLevel >= 1) {
            lightLevel = startLightLevel;
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
                    print("Game won!");
                    // TODO: Make end game code
                    break;
                default:
                    Debug.LogWarning("Initiated lightStage that does not exist! Warning!");
                    break;
            }
            Debug.Log("[Lantern] reached light stage " + lightStage);
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

        // Every hit costs the same, however hard it hit, so the cost is easy to read while playing
        DowngradeLightLevel(lightLostPerHit);

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }
}
