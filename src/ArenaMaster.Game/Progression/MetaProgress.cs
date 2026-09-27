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

/// <summary>
/// What one run did, as the rewards and bounties see it at its end. <paramref name="ClassId"/> is the class it was played as. A Delve run has its floor's
/// <paramref name="Depth"/> (0 for a classic run) and whether its node was <paramref name="DelveCleared"/>; <paramref name="Won"/> is only ever a classic run's win.
/// </summary>
internal sealed record RunRecord(int Kills, int ElitesKilled, int BossesKilled, float Seconds, bool Won, int Level, string ClassId = "", int Depth = 0, bool DelveCleared = false,
    bool DelveBoss = false);

/// <summary>The silver a run earns, win or lose: a little per kill, more for elites and bosses, some for every minute survived, and a bonus for winning.</summary>
internal static class RunRewards
{
    public const int KillsPerSilver = 20;
    public const int SilverPerElite = 4;
    public const int SilverPerBoss = 40;
    public const int SilverPerMinute = 2;
    public const int SilverForVictory = 150;

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
        new(PackSlot, "Bigger Pack", "+1 loadout slot: bring one more kind of item on every run.", new long[] { 1500, 4000, 9000 }),
        new(Reroll, "Second Thoughts", "+1 reroll per run on the level-up screen: swap all three choices for new ones.", new long[] { 200, 500, 1000, 1800, 3000 }),
        new(Banish, "Clear Mind", "+1 banish per run on the level-up screen: strike one choice from the pool for the rest of the run.", new long[] { 400, 1200, 2500 }),
        new(Luck, "Lucky Charm", "Better odds from chests and drops: rarer items turn up more often.", new long[] { 400, 1000, 2000, 3500, 5500 }),
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

/// <summary>A Battle Elixir the Quartermaster sells: a boon for the next run only.</summary>
internal sealed record Elixir(string Id, string Name, string Description, long Cost, Action<ItemBonuses> Apply);

/// <summary>
/// Battle Elixirs: bought at the Quartermaster for the next run only, one of each at most (they don't stack), drunk as the run sets out and gone after it, when
/// they can be bought again. They are there to help a new class find its feet, so a class whose passive tree has reached <see cref="TooHighFrom"/> can't buy them.
/// </summary>
internal static class Elixirs
{
    /// <summary>The passive tree level from which the class is too strong for elixirs.</summary>
    public const int TooHighFrom = 6;

    public const string TooHighLevel = "Too High Level";

    public static readonly IReadOnlyList<Elixir> All = new Elixir[]
    {
        new("elixir_damage", "Damage Elixir", "+20% damage for the next run.", 100, b => b.DamageMultiplier *= 1.2f),
        new("elixir_speed", "Speed Elixir", "+20% attack and cast speed for the next run.", 100, b => b.AttackSpeedMultiplier *= 1.2f),
        new("elixir_life", "Life Elixir", "+20% max health for the next run.", 100, b => b.MaxHealthMultiplier *= 1.2f),
    };

    public static bool Bought(Profile profile, Elixir elixir) => profile.Elixirs.Contains(elixir.Id);

    /// <summary>Why <paramref name="elixir"/> can't be bought now, by a class whose tree is at <paramref name="treeLevel"/>, or null if it can.</summary>
    public static string? WhyNotBuy(Profile profile, Elixir elixir, int treeLevel) =>
        Bought(profile, elixir) ? "Bought: drunk on the next run."
        : treeLevel >= TooHighFrom ? TooHighLevel
        : profile.Silver < elixir.Cost ? $"Needs {elixir.Cost - profile.Silver:N0} more silver."
        : null;

    public static bool Buy(Profile profile, Elixir elixir, int treeLevel)
    {
        if (WhyNotBuy(profile, elixir, treeLevel) is not null)
        {
            return false;
        }

        profile.Silver -= elixir.Cost;
        profile.Elixirs.Add(elixir.Id);
        return true;
    }

    /// <summary>A run sets out: the elixirs bought for it, taken out of the profile (so they last for this run only, and can be bought again after it).</summary>
    public static IReadOnlyList<Elixir> Drink(Profile profile)
    {
        var drunk = All.Where(e => Bought(profile, e)).ToList();
        profile.Elixirs.Clear();
        return drunk;
    }
}

/// <summary>A one-time challenge on the Bounty Board: what it asks, what it pays, and the item (if any) it unlocks into the drop pool.</summary>
internal sealed record Bounty(string Id, string Name, string Task, long Silver, string? UnlocksItem, Func<RunRecord, Profile, TreeProgress, bool> Met);

/// <summary>
/// The Bounty Board at camp: one-time challenges checked at the end of every run. Each pays silver once; some unlock an item into what chests and drops can give.
/// Every epic and legendary starts locked, one bounty each - an item already owned stays owned either way. Some ask for a run as a given class.
/// </summary>
internal static class Bounties
{
    public static readonly IReadOnlyList<Bounty> All = new Bounty[]
    {
        new("first_hunt", "First Hunt", "Kill 250 enemies in one run.", 25, null, (run, _, _) => run.Kills >= 250),
        new("brute_force", "Brute Force", "Kill a Ghoul Brute.", 50, "ironbark_totem", (run, _, _) => run.ElitesKilled >= 1),
        new("holding_on", "Holding On", "Survive 10 minutes in one run.", 75, "swiftwind_sigil", (run, _, _) => run.Seconds >= 600f),
        new("regicide", "Regicide", "Defeat the Hollow King.", 150, "hunters_moon", (run, _, _) => run.BossesKilled >= 1),
        new("long_night", "The Long Night", "Survive 20 minutes in one run.", 150, "dragon_heart", (run, _, _) => run.Seconds >= 1200f),
        new("massacre", "Massacre", "Kill 2,500 enemies in one run.", 125, "rune_of_might", (run, _, _) => run.Kills >= 2500),
        new("collector", "Collector", "Own 10 different items.", 100, null, (_, profile, _) => profile.Stash.Count(kv => kv.Value > 0) >= 10),
        new("deep_roots", "Deep Roots", "Reach passive tree level 10.", 150, null, (_, _, tree) => tree.Level >= 10),
        new("thousand_cuts", "A Thousand Cuts", "Kill 25,000 enemies over all your runs.", 200, null, (_, profile, _) => profile.Lifetime.Kills >= 25_000),
        new("champion", "Champion", "Survive to 30:00 and win a run.", 500, null, (run, _, _) => run.Won),

        // The newer epics.
        new("shieldbearer", "Shieldbearer", "Survive 15 minutes as the Paladin.", 100, "bulwark_sigil", (run, _, _) => run.ClassId == Paladin && run.Seconds >= 900f),
        new("cold_snap", "Cold Snap", "Kill 1,500 enemies in one run as the Mage.", 100, "heart_of_winter", (run, _, _) => run.ClassId == Mage && run.Kills >= 1500),
        new("glass_cannon", "Glass Cannon", "Reach level 35 in one run.", 125, "glass_pendant", (run, _, _) => run.Level >= 35),
        new("brute_hunter", "Brute Hunter", "Kill 10 Ghoul Brutes in one run.", 125, "berserkers_band", (run, _, _) => run.ElitesKilled >= 10),
        new("dark_bargain", "Dark Bargain", "Set out on 25 runs.", 125, "cursed_idol", (_, profile, _) => profile.Lifetime.Runs >= 25),

        // The newer legendaries.
        new("martyr", "Martyr", "Defeat the Hollow King as the Paladin.", 200, "martyrs_crown", (run, _, _) => run.ClassId == Paladin && run.BossesKilled >= 1),
        new("dawnbringer", "Dawnbringer", "Win a run as the Paladin.", 400, "aegis_of_dawn", (run, _, _) => run.ClassId == Paladin && run.Won),
        new("endless_winter", "Endless Winter", "Win a run as the Mage.", 400, "staff_of_long_night", (run, _, _) => run.ClassId == Mage && run.Won),
        new("deadeye_prize", "Deadeye's Prize", "Win a run as the Ranger.", 400, "splintered_crown", (run, _, _) => run.ClassId == Ranger && run.Won),
        new("rise_again", "Rise Again", "Survive 25 minutes in one run.", 250, "phoenix_feather", (run, _, _) => run.Seconds >= 1500f),
        new("hoarder", "Hoarder", "Own 30 different items.", 300, "crown_of_plenty", (_, profile, _) => profile.Stash.Count(kv => kv.Value > 0) >= 30),

        // The Shaman's.
        new("conductor", "Conductor", "Kill 1,500 enemies in one run as the Shaman.", 125, "stormcallers_horn", (run, _, _) => run.ClassId == Shaman && run.Kills >= 1500),
        new("stormborn", "Stormborn", "Win a run as the Shaman.", 400, "crown_of_storms", (run, _, _) => run.ClassId == Shaman && run.Won),

        // The Delve.
        new("into_the_dark", "Into the Dark", "Clear your first Delve node.", 50, null, (_, profile, _) => profile.Delve.Cleared.Count >= 1),
        new("deeper_still", "Deeper Still", "Open Delve depth 5.", 100, null, (_, profile, _) => profile.Delve.Deepest >= 5),
        new("the_mistdeep", "The Mistdeep", "Open Delve depth 10.", 200, null, (_, profile, _) => profile.Delve.Deepest >= 10),
        new("night_walker", "Night Walker", "Open Delve depth 15.", 350, null, (_, profile, _) => profile.Delve.Deepest >= 15),
        new("frozen_deep", "The Frozen Deep", "Open Delve depth 20.", 500, null, (_, profile, _) => profile.Delve.Deepest >= 20),
        new("abyss_gazer", "Abyss Gazer", "Open Delve depth 30.", 1000, null, (_, profile, _) => profile.Delve.Deepest >= 30),
        new("delver", "Delver", "Clear 10 Delve nodes.", 125, null, (_, profile, _) => profile.Delve.Cleared.Count >= 10),
        new("veteran_delver", "Veteran Delver", "Clear 40 Delve nodes.", 400, null, (_, profile, _) => profile.Delve.Cleared.Count >= 40),
        new("every_path", "Every Path", "Clear a Currency, Knowledge and Relic Delve, and a Descent.", 200, null, (_, profile, _) =>
            new[] { "Currency", "Knowledge", "Relic", "Descent" }.All(k => profile.Delve.ClearedByKind.GetValueOrDefault(k) > 0)),
        new("swift_delve", "Swift Delve", "Clear a King's Delve node within 11 minutes.", 150, null, (run, _, _) => run.DelveCleared && !run.DelveBoss && run.BossesKilled > 0 && run.Seconds <= 660f),
        new("unbound", "Unbound", "Defeat the Hollow King Unbound on a boss hunt.", 250, null, (_, profile, _) => profile.Delve.BossesSlain >= 1),
        new("kingbreaker", "Kingbreaker", "Defeat the Hollow King Unbound 3 times.", 600, null, (_, profile, _) => profile.Delve.BossesSlain >= 3),
        new("deep_ranger", "Ranger of the Deep", "Clear a Delve node at depth 10 or deeper as the Ranger.", 200, null,
            (run, _, _) => run.ClassId == Ranger && run.DelveCleared && run.Depth >= 10),
        new("deep_paladin", "Paladin of the Deep", "Clear a Delve node at depth 10 or deeper as the Paladin.", 200, null,
            (run, _, _) => run.ClassId == Paladin && run.DelveCleared && run.Depth >= 10),
        new("deep_mage", "Mage of the Deep", "Clear a Delve node at depth 10 or deeper as the Mage.", 200, null,
            (run, _, _) => run.ClassId == Mage && run.DelveCleared && run.Depth >= 10),
        new("deep_shaman", "Shaman of the Deep", "Clear a Delve node at depth 10 or deeper as the Shaman.", 200, null,
            (run, _, _) => run.ClassId == Shaman && run.DelveCleared && run.Depth >= 10),
        new("deep_warrior", "Warrior of the Deep", "Clear a Delve node at depth 10 or deeper as the Warrior.", 200, "horn_of_fury",
            (run, _, _) => run.ClassId == Warrior && run.DelveCleared && run.Depth >= 10),

        // The Warrior's.
        new("bloodbath", "Bloodbath", "Kill 1,500 enemies in one run as the Warrior.", 125, "butchers_cleaver", (run, _, _) => run.ClassId == Warrior && run.Kills >= 1500),
        new("warlord", "Warlord", "Win a run as the Warrior.", 400, "bloodfury", (run, _, _) => run.ClassId == Warrior && run.Won),

        // The Priest's.
        new("plaguebringer", "Plaguebringer", "Kill 1,500 enemies in one run as the Priest.", 125, "tome_of_pestilence", (run, _, _) => run.ClassId == Priest && run.Kills >= 1500),
        new("lord_of_decay", "Lord of Decay", "Win a run as the Priest.", 400, "sepulchre_bell", (run, _, _) => run.ClassId == Priest && run.Won),
        new("deep_priest", "Priest of the Deep", "Clear a Delve node at depth 10 or deeper as the Priest.", 200, "bonebound_aegis",
            (run, _, _) => run.ClassId == Priest && run.DelveCleared && run.Depth >= 10),

        new("armourer", "Armourer", "Own 4 different pieces of gear.", 150, null, (_, profile, _) => Gear.GearCatalog.PiecesFound(profile) >= 4),
        new("fully_kitted", "Fully Kitted", "Wear gear in all three slots.", 125, null, (_, profile, _) => Gear.GearCatalog.Worn(profile).Count() >= 3),
        new("armoury_complete", "The Full Armoury", "Own every piece of gear.", 750, null,
            (_, profile, _) => Gear.GearCatalog.All.All(piece => Gear.GearCatalog.Owns(profile, piece))),
    };

    // The class ids the class bounties ask for (the classes' own ids, kept here as text so the board doesn't reach into any class's code).
    private const string Ranger = "ranger", Paladin = "paladin", Mage = "mage", Shaman = "shaman", Warrior = "warrior", Priest = "priest";

    /// <summary>Whether <paramref name="item"/> can come out of chests and drops: yes unless a bounty not yet done unlocks it.</summary>
    public static bool IsUnlocked(RunItem item, Profile profile) =>
        All.FirstOrDefault(b => b.UnlocksItem == item.Id) is not { } bounty || profile.HasBounty(bounty.Id);

    /// <summary>Whether <paramref name="item"/> can drop for <paramref name="profile"/>: unlocked, and not already owned as many times as can count, so a find is never wasted.</summary>
    public static bool CanDrop(RunItem item, Profile profile) => IsUnlocked(item, profile) && profile.CountOf(item.Id) < item.MaxStack;

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
