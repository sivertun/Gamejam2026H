using System.Collections.Generic;
using UnityEngine;

public class LanternController : MonoBehaviour, IDamagable 
{
    [SerializeField] private float invincibilityTime;
    private float invincibilityTimer;
    [SerializeField] private float decayPerSec = 0.01f;
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

    [Header("Stage colours")]
    [Tooltip("Colour of the light at each stage. The first one is what you start the game with")]
    [SerializeField] private Color[] stageColors =
    {
        new Color(1f, 0.78f, 0.35f),    // stage 1, the warm lamp you start with
        new Color(0.68f, 0.85f, 0.9f),  // stage 2, light blue
        new Color(1f, 0.71f, 0.76f),    // stage 3, light pink
        new Color(0.56f, 0.93f, 0.56f), // stage 4, light green
        Color.white,                    // stage 5
    };
    [Tooltip("Seconds the light takes to bleed from one stage's colour into the next")]
    [SerializeField] private float colorTransitionTime = 1.5f;
    private int lightStage = 1;
    private float lightLevel = 0.2f;
    private Color colorFrom;
    private Color colorTo;
    private float colorBlend = 1f;
    private bool colorsReady;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();
    private bool dead;

    [SerializeField] private GameController gameController;

    void Awake()
    {
        lightLevel = startLightLevel;
        EnsureColors();
    }


    void Update()
    {
        if (dead) return;
        if (invincibilityTimer != 0) invincibilityTimer = Mathf.Max(invincibilityTimer - Time.deltaTime, 0);

        lightLevel -= decayPerSec*Time.deltaTime;

        // Ease into a new stage's colour rather than snapping to it
        colorBlend = colorTransitionTime > 0f
            ? Mathf.MoveTowards(colorBlend, 1f, Time.deltaTime / colorTransitionTime)
            : 1f;

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

    // How brightly the light burns, 1 while you're healthy and fading to 0 as the last of it goes.
    // Growth handles getting bigger, this only ever dims, so the two never fight each other.
    public float LightBrightness => startLightLevel > 0f ? Mathf.Clamp01(TotalLight / startLightLevel) : 1f;

    // The colour the light is right now, easing across when you reach a new stage
    public Color CurrentColor
    {
        get
        {
            EnsureColors();
            return Color.Lerp(colorFrom, colorTo, Mathf.SmoothStep(0f, 1f, colorBlend));
        }
    }

    private void EnsureColors()
    {
        if (colorsReady) return;
        colorsReady = true;
        colorFrom = colorTo = StageColor(lightStage);
    }

    private Color StageColor(int stage)
    {
        if (stageColors == null || stageColors.Length == 0) return Color.white;
        return stageColors[Mathf.Clamp(stage - 1, 0, stageColors.Length - 1)];
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

        // Start the fade from whatever colour is on screen now, so back-to-back stages still blend
        colorFrom = CurrentColor;
        colorTo = StageColor(lightStage);
        colorBlend = 0f;

        if (lightStage == 5)
        {
            print("Game won!");
            // TODO: Make end game code
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
