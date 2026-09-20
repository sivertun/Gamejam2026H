using System.Collections.Generic;
using UnityEngine;

// Everything the roguelite upgrades change, gathered in one place. The lamp, the player's walking
// speed, the melee swing and dead enemies all read their numbers from here, so an upgrade only ever
// has to change a value on this component. You start a run with none of them taken, so these are
// the plain values the game is tuned around.
public class PlayerUpgrades : MonoBehaviour
{
    [Header("Reach")]
    [Tooltip("How far the lamp reaches, on top of the lantern's own growth")]
    public float rangeMultiplier = 1f;
    [Tooltip("Degrees added to the suck cone")]
    public float suckAngleBonus = 0f;

    [Header("Speed")]
    [Tooltip("How fast things are pulled in, and how fast bodies drain")]
    public float suckSpeedMultiplier = 1f;
    [Tooltip("How fast you walk while holding the suck. 1 would be no penalty at all")]
    [Range(0.1f, 1f)] public float suckMoveSpeed = 0.45f;

    [Header("Payoff")]
    [Tooltip("Light gained from draining bodies")]
    public float enemyLightMultiplier = 1f;
    [Tooltip("Damage a second the beam does to living enemies. 0 = the beam doesn't hurt them")]
    public float suckDamagePerSecond = 0f;

    [Header("Dodge roll")]
    [Tooltip("Whether shift rolls at all")]
    public bool hasDodgeRoll = false;
    [Tooltip("Seconds between rolls")]
    public float dodgeCooldown = 3f;
    [Tooltip("Seconds you can't be touched for once a roll starts")]
    public float dodgeInvulnerability = 0.4f;

    [Header("Vacuum burst")]
    [Tooltip("Whether the burst is unlocked at all")]
    public bool hasInstantSuck = false;
    [Tooltip("Seconds between bursts")]
    public float instantSuckCooldown = 20f;

    [Header("Testing")]
    [Tooltip("Offer the whole pool at once instead of three at random. Turn off for a real run.")]
    public bool offerEveryUpgrade = true;

    // How many times each upgrade has been taken, so the pool stops offering ones that are maxed
    private readonly Dictionary<string, int> timesTaken = new Dictionary<string, int>();

    // Test scenes don't have this on the player, so anything that needs it can just ask
    public static PlayerUpgrades Ensure(GameObject player)
    {
        if (player == null) return null;
        PlayerUpgrades existing = player.GetComponent<PlayerUpgrades>();
        return existing != null ? existing : player.AddComponent<PlayerUpgrades>();
    }

    public int TimesTaken(string id)
    {
        return timesTaken.TryGetValue(id, out int count) ? count : 0;
    }

    public void Take(Upgrade upgrade)
    {
        if (upgrade == null) return;
        timesTaken[upgrade.Id] = TimesTaken(upgrade.Id) + 1;
        upgrade.Apply(this);
        Debug.Log($"[Upgrade] took {upgrade.Title} ({TimesTaken(upgrade.Id)}/{upgrade.MaxTimes})");
    }
}
