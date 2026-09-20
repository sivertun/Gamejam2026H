using System.Collections.Generic;
using UnityEngine;

public class LanternController : MonoBehaviour, IDamagable 
{
    [SerializeField] private float invincibilityTime;
    private float invincibilityTimer;
    [SerializeField] private float decayPerSec = 0.01f;
    [Tooltip("Light lost every time you get hit, on top of the share below")]
    [SerializeField] private float lightLostPerHit = 0.04f;
    [Tooltip("Share of the light you're carrying that each hit takes. 0.18 = 18% of it")]
    [SerializeField, Range(0f, 1f)] private float shareLostPerHit = 0.18f;
    [Tooltip("Light the lantern starts the game with")]
    [SerializeField] private float startLightLevel = 0.2f;

    [Header("How far the light reaches")]
    [Tooltip("How sharply size follows the light. 0.5 is a square root: four times the light is " +
             "twice the reach. Lower is flatter, 1 is straight through.")]
    [SerializeField, Range(0.2f, 1f)] private float growthPower = 0.5f;

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

    // How big the light is against the start of the game, and the only thing the lamp reads.
    // It answers to every bit of light, with no floor and no ceiling: there is nowhere on the
    // curve where gaining or losing light does nothing, so every hit shows up on screen.
    public float LightGrowth
    {
        get
        {
            if (startLightLevel <= 0f) return 1f;
            return Mathf.Pow(Mathf.Max(TotalLight, 0f) / startLightLevel, growthPower);
        }
    }

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

        // Part flat, part a share of what you're carrying. The share is what makes a hit read the
        // same whether you're rich or poor in light: a flat cost alone is invisible when you have
        // plenty, so you'd feel untouchable right up until the last couple of hits killed you.
        DowngradeLightLevel(lightLostPerHit + lightLevel * shareLostPerHit);

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }
}
