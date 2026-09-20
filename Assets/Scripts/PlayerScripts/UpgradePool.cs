using System;
using System.Collections.Generic;
using UnityEngine;

// One roguelite upgrade: what it's called, what it does in a line, the actual numbers it moves,
// and the change itself.
public class Upgrade
{
    public string Id;
    public string Title;
    public string Description;
    // The numbers, read off what you already have: "range x1.35 -> x1.70"
    public Func<PlayerUpgrades, string> Detail;
    public int MaxTimes = 3;
    public Action<PlayerUpgrades> Apply;
}

// Every upgrade you can be offered. All of them work on the lantern and the suck rather than the
// melee swing: the swing's reach is already hard to read, so making it longer would only confuse.
// Three at a time are drawn from this, so it wants more entries than that to stay interesting.
public static class UpgradePool
{
    // Each step is used by both the card text and the change, so they can't drift apart
    private const float BurnStep = 6f;
    private const float CooldownCut = 0.7f;
    private const float IFrameStep = 0.1f;

    public static List<Upgrade> All()
    {
        return new List<Upgrade>
        {
            new Upgrade
            {
                Id = "withering_light",
                Title = "Withering Light",
                Description = "The beam sets living enemies alight while you hold it on them.",
                Detail = u => u.suckDamagePerSecond <= 0f
                    ? $"unlocks it  ·  {BurnStep:0} damage a second"
                    : $"burn {u.suckDamagePerSecond:0}  ->  {u.suckDamagePerSecond + BurnStep:0} damage a second",
                Apply = u => u.suckDamagePerSecond += BurnStep,
            },
            new Upgrade
            {
                Id = "tuck_and_roll",
                Title = "Tuck and Roll",
                Description = "Shift rolls you clear. Nothing can touch you mid-roll.",
                Detail = u => !u.hasDodgeRoll
                    ? $"unlocks it  ·  {u.dodgeCooldown:0.0}s cooldown, {u.dodgeInvulnerability:0.00}s untouchable"
                    : $"cooldown {u.dodgeCooldown:0.0}s -> {u.dodgeCooldown * CooldownCut:0.0}s  ·  " +
                      $"untouchable {u.dodgeInvulnerability:0.00}s -> {u.dodgeInvulnerability + IFrameStep:0.00}s",
                MaxTimes = 3,
                Apply = u =>
                {
                    // The first one unlocks it, the rest sharpen it
                    if (u.hasDodgeRoll)
                    {
                        u.dodgeCooldown *= CooldownCut;
                        u.dodgeInvulnerability += IFrameStep;
                    }
                    u.hasDodgeRoll = true;
                },
            },
            new Upgrade
            {
                Id = "vacuum_burst",
                Title = "Vacuum Burst",
                Description = "Q hauls in everything around you, from any direction.",
                Detail = u => !u.hasInstantSuck
                    ? $"unlocks it  ·  {u.instantSuckCooldown:0}s cooldown"
                    : $"cooldown {u.instantSuckCooldown:0}s  ->  {u.instantSuckCooldown * CooldownCut:0}s",
                MaxTimes = 3,
                Apply = u =>
                {
                    // The first one unlocks it, the rest cut the wait
                    if (u.hasInstantSuck) u.instantSuckCooldown *= CooldownCut;
                    u.hasInstantSuck = true;
                },
            },
        };
    }

    // A handful at random that you haven't already maxed out, or the whole pool while testing
    public static List<Upgrade> Offer(PlayerUpgrades upgrades, int count)
    {
        List<Upgrade> available = new List<Upgrade>();
        foreach (Upgrade upgrade in All())
        {
            if (upgrades == null || upgrades.TimesTaken(upgrade.Id) < upgrade.MaxTimes) available.Add(upgrade);
        }

        // Testing: every upgrade on screen at once, so you can go straight to the one you want
        if (upgrades != null && upgrades.offerEveryUpgrade) return available;

        List<Upgrade> chosen = new List<Upgrade>();
        while (chosen.Count < count && available.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, available.Count);
            chosen.Add(available[index]);
            available.RemoveAt(index);
        }
        return chosen;
    }
}
