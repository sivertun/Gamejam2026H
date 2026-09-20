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
    [Tooltip("Light the lantern starts each stage with, and drops back to when you upgrade")]
    [SerializeField] private float startLightLevel = 0.2f;

    [Header("How far the light reaches")]
    [Tooltip("How sharply size follows the light. 0.5 is a square root: four times the light is " +
             "twice the reach. Lower is flatter, 1 is straight through.")]
    [SerializeField, Range(0.2f, 1f)] private float growthPower = 0.45f;
    [Tooltip("How much bigger the light gets for good with each stage reached. 1.35 = 35% a stage")]
    [SerializeField] private float stageGrowthBonus = 1.25f;

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
    [Tooltip("Reaching this stage wins the game and plays the rescue screen")]
    [SerializeField] private int winStage = 5;
    private int lightStage = 1;
    private float lightLevel = 0.2f;
    private Color colorFrom;
    private Color colorTo;
    private float colorBlend = 1f;
    private bool colorsReady;
    List<ILightStageObserver> observers = new List<ILightStageObserver>();
    private bool dead;
    private bool rescued;

    [SerializeField] private GameController gameController;

    private PlayerAudio playerAudio;

    void Awake()
    {
        playerAudio = PlayerAudio.Ensure(gameObject);
        lightLevel = startLightLevel;
        EnsureColors();
    }


    void Update()
    {
        if (dead || rescued) return;
        if (invincibilityTimer != 0) invincibilityTimer = Mathf.Max(invincibilityTimer - Time.deltaTime, 0);

        lightLevel -= decayPerSec*Time.deltaTime;

        // Ease into a new stage's colour rather than snapping to it
        colorBlend = colorTransitionTime > 0f
            ? Mathf.MoveTowards(colorBlend, 1f, Time.deltaTime / colorTransitionTime)
            : 1f;

        if (CheckLightDead() == true)
        {
            dead = true;
            if (playerAudio != null) playerAudio.PlayDie();
            DeathSequence.Play();
        }
    }

    // How full the lantern is, between 0 and 1. This is your health: decay and hits take it away,
    // fire and bodies put it back, filling it upgrades the lantern, and at 0 you die.
    public float TotalLight => lightLevel;

    // What TotalLight is at the very start of the game, so others can size themselves against it
    public float StartLight => startLightLevel;

    // How big the light is against the start of the game, and the only thing the lamp reads.
    // Two parts: what's in the lantern now, which you lose on every hit and give up when you
    // upgrade, and a bonus each stage keeps for good. So an upgrade sets you back but still
    // leaves you better off than the stage before, and the fill part answers to every hit.
    public float LightGrowth
    {
        get
        {
            if (startLightLevel <= 0f) return 1f;
            float fill = Mathf.Pow(Mathf.Max(lightLevel, 0f) / startLightLevel, growthPower);
            return fill * Mathf.Pow(stageGrowthBonus, lightStage - 1);
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

    // How far through the game you are: 0 at the start and +1 per stage, running smoothly across
    // an upgrade. Unlike the light itself this never falls back, so the enemies keep ramping up
    // even though upgrading costs you your fill. Used as the difficulty clock, see EnemySpawner.
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

        // Filling the lantern upgrades it, and the upgrade spends the fill: your light drops back
        // to a starting flame, so your health and your reach both fall. The stage bonus in
        // LightGrowth is what you keep, so you still come out ahead of the stage before.
        if (lightLevel >= 1f)
        {
            lightLevel = startLightLevel;
            ReachNextStage();
        }
    }

    private void ReachNextStage()
    {
        lightStage++;

        // Start the fade from whatever colour is on screen now, so back-to-back stages still blend
        colorFrom = CurrentColor;
        colorTo = StageColor(lightStage);
        colorBlend = 0f;

        if (lightStage >= winStage)
        {
            // The run is over: stop the decay and the spawners, and roll the win screen.
            rescued = true;
            if (gameController) gameController.dead();
            RescueSequence.Play();
        }

        Debug.Log("[Lantern] reached light stage " + lightStage);
        NotifyOnLightStageUpgrade();

        // The roguelite bit: every stage buys you an upgrade, except the one that ends the run,
        // where the rescue screen is already taking the clock
        if (!rescued) UpgradeChooser.Offer(gameObject);
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

    // Used by the dodge roll: nothing can touch you for this long
    public void GrantInvincibility(float seconds)
    {
        invincibilityTimer = Mathf.Max(invincibilityTimer, seconds);
    }

    public void TakeDamage(float damage, float knockback, Transform source)
    {
        if (dead || rescued) return;
        if (invincibilityTimer != 0) return;
        invincibilityTimer = invincibilityTime;

        if (playerAudio != null) playerAudio.PlayHurt();

        // Part flat, part a share of what you're carrying. The share is what makes a hit read the
        // same whether you're rich or poor in light: a flat cost alone is invisible when you have
        // plenty, so you'd feel untouchable right up until the last couple of hits killed you.
        DowngradeLightLevel(lightLostPerHit + lightLevel * shareLostPerHit);

        Vector3 heading = transform.position - source.position;
        Vector3 direction = heading.normalized;

        gameObject.GetComponent<IHasVelocity>().SetVelocity(direction * knockback);
    }
}
