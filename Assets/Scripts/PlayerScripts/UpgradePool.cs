using System;
using System.Collections.Generic;
using UnityEngine;

// One roguelite upgrade: what it's called, what the card says, and what it changes.
public class Upgrade
{
    public string Id;
    public string Title;
    public string Description;
    public int MaxTimes = 3;
    public Action<PlayerUpgrades> Apply;
}

// Every upgrade you can be offered. All of them work on the lantern and the suck rather than the
// melee swing: the swing's reach is already hard to read, so making it longer would only confuse.
public static class UpgradePool
{
    public static List<Upgrade> All()
    {
        return new List<Upgrade>
        {
            new Upgrade
            {
                Id = "long_reach",
                Title = "Long Reach",
                Description = "The lamp pulls from a third further out.",
                Apply = u => u.rangeMultiplier += 0.35f,
            },
            new Upgrade
            {
                Id = "wide_draw",
                Title = "Wide Draw",
                Description = "The suck spreads 20 degrees wider, so you needn't aim so exactly.",
                Apply = u => u.suckAngleBonus += 20f,
            },
            new Upgrade
            {
                Id = "hungry_flame",
                Title = "Hungry Flame",
                Description = "Fire flies in half again as fast, and bodies drain quicker.",
                Apply = u => u.suckSpeedMultiplier += 0.5f,
            },
            new Upgrade
            {
                Id = "steady_hands",
                Title = "Steady Hands",
                Description = "You keep much more of your walking speed while holding the suck.",
                MaxTimes = 3,
                Apply = u => u.suckMoveSpeed = Mathf.Min(u.suckMoveSpeed + 0.18f, 1f),
            },
            new Upgrade
            {
                Id = "withering_light",
                Title = "Withering Light",
                Description = "The beam burns the living: enemies caught in it lose health.",
                Apply = u => u.suckDamagePerSecond += 6f,
            },
            new Upgrade
            {
                Id = "rich_embers",
                Title = "Rich Embers",
                Description = "Draining a body gives half again as much light.",
                Apply = u => u.enemyLightMultiplier += 0.5f,
            },
            new Upgrade
            {
                Id = "tuck_and_roll",
                Title = "Tuck and Roll",
                Description = "Shift to roll, untouchable while you go. Again for a shorter wait " +
                              "and a longer window.",
                MaxTimes = 3,
                Apply = u =>
                {
                    // The first one unlocks it, the rest sharpen it
                    if (u.hasDodgeRoll)
                    {
                        u.dodgeCooldown *= 0.7f;
                        u.dodgeInvulnerability += 0.1f;
                    }
                    u.hasDodgeRoll = true;
                },
            },
            new Upgrade
            {
                Id = "vacuum_burst",
                Title = "Vacuum Burst",
                Description = "Press Q to swallow everything around you at once. Long cooldown.",
                MaxTimes = 3,
                Apply = u =>
                {
                    // The first one unlocks it, the rest cut the wait
                    if (u.hasInstantSuck) u.instantSuckCooldown *= 0.7f;
                    u.hasInstantSuck = true;
                },
            },
        };
    }

    // Three at random that you haven't already maxed out, or the whole pool while testing
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
