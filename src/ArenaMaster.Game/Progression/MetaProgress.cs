using ArenaMaster.Game.Items;

namespace ArenaMaster.Game.Progression;

/// <summary>Totals across every run, for bounties that count over many runs.</summary>
internal sealed class LifetimeRecord
{
    public long Kills { get; set; }

    public int Runs { get; set; }

    public int Wins { get; set; }

    /// <summary>The longest run survived, in seconds.</summary>
    public float LongestRun { get; set; }
}

/// <summary>What one run did, as the rewards and bounties see it at its end.</summary>
internal sealed record RunRecord(int Kills, int ElitesKilled, int BossesKilled, float Seconds, bool Won, int Level);

/// <summary>The silver a run earns, win or lose: a little per kill, more for elites and bosses, some for every minute survived, and a bonus for winning.</summary>
internal static class RunRewards
{
    public const int KillsPerSilver = 10;
    public const int SilverPerElite = 5;
    public const int SilverPerBoss = 60;
    public const int SilverPerMinute = 3;
    public const int SilverForVictory = 250;

    public static long Silver(RunRecord run) =>
        run.Kills / KillsPerSilver
        + run.ElitesKilled * SilverPerElite
        + run.BossesKilled * SilverPerBoss
        + (int)(run.Seconds / 60f) * SilverPerMinute
        + (run.Won ? SilverForVictory : 0);
}

/// <summary>One upgrade the Quartermaster sells: its ranks, what each rank costs, and what it does.</summary>
internal sealed record ShopUpgrade(string Id, string Name, string Description, IReadOnlyList<long> Costs)
{
    public int MaxRank => Costs.Count;
}

/// <summary>
/// The Quartermaster at camp: permanent upgrades bought with silver. More loadout slots, rerolls and banishes on the level-up screen, and luck for chests and drops.
/// </summary>
internal static class Shop
{
    public const string PackSlot = "pack_slot";
    public const string Reroll = "reroll";
    public const string Banish = "banish";
    public const string Luck = "luck";

    public static readonly IReadOnlyList<ShopUpgrade> All = new ShopUpgrade[]
    {
        new(PackSlot, "Bigger Pack", "+1 loadout slot: bring one more kind of item on every run.", new long[] { 600, 1800, 4500 }),
        new(Reroll, "Second Thoughts", "+1 reroll per run on the level-up screen: swap all three choices for new ones.", new long[] { 150, 350, 700, 1200, 2000 }),
        new(Banish, "Clear Mind", "+1 banish per run on the level-up screen: strike one choice from the pool for the rest of the run.", new long[] { 250, 700, 1500 }),
        new(Luck, "Lucky Charm", "Better odds from chests and drops: rarer items turn up more often.", new long[] { 300, 700, 1300, 2200, 3500 }),
    };

    public static ShopUpgrade Get(string id) => All.First(u => u.Id == id);

    /// <summary>What the next rank of <paramref name="upgrade"/> costs, or null if it is maxed.</summary>
    public static long? NextCost(Profile profile, ShopUpgrade upgrade)
    {
        int rank = profile.ShopRank(upgrade.Id);
        return rank < upgrade.MaxRank ? upgrade.Costs[rank] : null;
    }

    /// <summary>Why the next rank of <paramref name="upgrade"/> can't be bought, or null if it can.</summary>
    public static string? WhyNotBuy(Profile profile, ShopUpgrade upgrade) => NextCost(profile, upgrade) switch
    {
        null => "Fully bought.",
        { } cost when profile.Silver < cost => $"Needs {cost - profile.Silver:N0} more silver.",
        _ => null,
    };

    public static bool Buy(Profile profile, ShopUpgrade upgrade)
    {
        if (WhyNotBuy(profile, upgrade) is not null)
        {
            return false;
        }

        profile.Silver -= NextCost(profile, upgrade)!.Value;
        profile.Shop[upgrade.Id] = profile.ShopRank(upgrade.Id) + 1;
        return true;
    }

    public static int ExtraLoadoutSlots(Profile profile) => profile.ShopRank(PackSlot);

    public static int RerollsPerRun(Profile profile) => profile.ShopRank(Reroll);

    public static int BanishesPerRun(Profile profile) => profile.ShopRank(Banish);

    /// <summary>
    /// <paramref name="weights"/> with Lucky Charm's ranks in <paramref name="profile"/> applied: each rank makes rares 15% likelier, epics 30% and legendaries 50%,
    /// relative to commons.
    /// </summary>
    public static RarityWeights Lucky(RarityWeights weights, Profile profile)
    {
        int rank = profile.ShopRank(Luck);
        return rank == 0 ? weights : weights with
        {
            Rare = weights.Rare * (1f + 0.15f * rank),
            Epic = weights.Epic * (1f + 0.30f * rank),
            Legendary = weights.Legendary * (1f + 0.50f * rank),
        };
    }
}

/// <summary>A one-time challenge on the Bounty Board: what it asks, what it pays, and the item (if any) it unlocks into the drop pool.</summary>
internal sealed record Bounty(string Id, string Name, string Task, long Silver, string? UnlocksItem, Func<RunRecord, Profile, TreeProgress, bool> Met);

/// <summary>
/// The Bounty Board at camp: one-time challenges checked at the end of every run. Each pays silver once; some unlock an item into what chests and drops can give.
/// The epics and legendaries start locked, one bounty each - an item already owned stays owned either way.
/// </summary>
internal static class Bounties
{
    public static readonly IReadOnlyList<Bounty> All = new Bounty[]
    {
        new("first_hunt", "First Hunt", "Kill 100 enemies in one run.", 50, null, (run, _, _) => run.Kills >= 100),
        new("brute_force", "Brute Force", "Kill a Ghoul Brute.", 100, "ironbark_totem", (run, _, _) => run.ElitesKilled >= 1),
        new("holding_on", "Holding On", "Survive 10 minutes in one run.", 150, "swiftwind_sigil", (run, _, _) => run.Seconds >= 600f),
        new("regicide", "Regicide", "Defeat the Hollow King.", 300, "hunters_moon", (run, _, _) => run.BossesKilled >= 1),
        new("long_night", "The Long Night", "Survive 20 minutes in one run.", 300, "dragon_heart", (run, _, _) => run.Seconds >= 1200f),
        new("massacre", "Massacre", "Kill 1,000 enemies in one run.", 250, "rune_of_might", (run, _, _) => run.Kills >= 1000),
        new("collector", "Collector", "Own 10 different items.", 200, null, (_, profile, _) => profile.Stash.Count(kv => kv.Value > 0) >= 10),
        new("deep_roots", "Deep Roots", "Reach passive tree level 10.", 300, null, (_, _, tree) => tree.Level >= 10),
        new("thousand_cuts", "A Thousand Cuts", "Kill 10,000 enemies over all your runs.", 400, null, (_, profile, _) => profile.Lifetime.Kills >= 10_000),
        new("champion", "Champion", "Survive to 30:00 and win a run.", 1000, null, (run, _, _) => run.Won),
    };

    /// <summary>Whether <paramref name="item"/> can come out of chests and drops: yes unless a bounty not yet done unlocks it.</summary>
    public static bool IsUnlocked(RunItem item, Profile profile) =>
        All.FirstOrDefault(b => b.UnlocksItem == item.Id) is not { } bounty || profile.HasBounty(bounty.Id);

    /// <summary>
    /// The end of a run: adds it to the lifetime totals, then completes every bounty it (or the totals) now meets that wasn't done before, paying each one's silver.
    /// Returns the bounties completed just now.
    /// </summary>
    public static List<Bounty> Settle(RunRecord run, Profile profile, TreeProgress tree)
    {
        profile.Lifetime.Kills += run.Kills;
        profile.Lifetime.Runs++;
        profile.Lifetime.Wins += run.Won ? 1 : 0;
        profile.Lifetime.LongestRun = MathF.Max(profile.Lifetime.LongestRun, run.Seconds);

        var completed = new List<Bounty>();
        foreach (var bounty in All)
        {
            if (!profile.HasBounty(bounty.Id) && bounty.Met(run, profile, tree))
            {
                profile.Bounties.Add(bounty.Id);
                profile.Silver += bounty.Silver;
                completed.Add(bounty);
            }
        }

        return completed;
    }
}
