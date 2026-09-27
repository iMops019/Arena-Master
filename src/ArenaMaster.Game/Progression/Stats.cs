using ArenaMaster.Game.Combat;

namespace ArenaMaster.Game.Progression;

/// <summary>One class's own record over every run played as it.</summary>
internal sealed class ClassRecord
{
    public int Runs { get; set; }

    public int Wins { get; set; }

    public int Deaths { get; set; }

    public long Kills { get; set; }

    public double Seconds { get; set; }

    public int MostKills { get; set; }

    public int HighestLevel { get; set; }

    public int BestStreak { get; set; }

    public int NodesCleared { get; set; }

    /// <summary>The deepest Delve floor a node was cleared on as this class (0: none yet).</summary>
    public int DeepestCleared { get; set; }
}

/// <summary>
/// Everything the stats page shows that the rest of the save doesn't already keep: totals, bests, kills by kind and rarity, what killed the player, and each class's
/// own record. Kept in the profile (<see cref="Profile.Stats"/>) and added to at the end of every run by <see cref="Settle"/>, from that run's <see cref="RunTally"/>.
/// The totals the bounties read (kills, runs, wins, the longest run) stay in <see cref="LifetimeRecord"/>.
/// </summary>
internal sealed class StatsRecord
{
    public int Deaths { get; set; }

    public int ReturnedToCamp { get; set; }

    public int DelveRuns { get; set; }

    public int DelveCleared { get; set; }

    public double SecondsPlayed { get; set; }

    public int MostKills { get; set; }

    /// <summary>The most kills in a row with no blow landing on the player in between.</summary>
    public int BestStreak { get; set; }

    public int HighestLevel { get; set; }

    /// <summary>The quickest Delve node cleared, in seconds (0: none yet). A classic win is always at 30:00, so it has no fastest.</summary>
    public float FastestDelveClear { get; set; }

    public double DamageDealt { get; set; }

    public float MostDamage { get; set; }

    public double HealthLost { get; set; }

    public long HitsTaken { get; set; }

    public long Blocks { get; set; }

    public long ElitesKilled { get; set; }

    public long BossesKilled { get; set; }

    /// <summary>Rarity name (Magic, Rare, Legendary) -> how many of that rarity were killed.</summary>
    public Dictionary<string, long> KillsByRarity { get; set; } = new();

    /// <summary>Enemy kind's name -> how many were killed.</summary>
    public Dictionary<string, long> KillsByKind { get; set; } = new();

    /// <summary>Enemy kind's name -> how many runs it landed the killing blow on.</summary>
    public Dictionary<string, int> DeathsBy { get; set; } = new();

    public long SilverEarned { get; set; }

    public int ItemsFound { get; set; }

    public int GearFound { get; set; }

    public int CratesBroken { get; set; }

    public int Rushes { get; set; }

    public int LevelUps { get; set; }

    public int Rerolls { get; set; }

    public int Banishes { get; set; }

    /// <summary>Class id -> that class's record.</summary>
    public Dictionary<string, ClassRecord> Classes { get; set; } = new();

    public ClassRecord Class(string classId)
    {
        if (!Classes.TryGetValue(classId, out var record))
        {
            record = new ClassRecord();
            Classes[classId] = record;
        }

        return record;
    }

    /// <summary>The id of the class with the most runs (the most time on runs breaking a tie), or null before any run.</summary>
    public string? MostPlayedClass() => Classes.Where(kv => kv.Value.Runs > 0)
        .OrderByDescending(kv => kv.Value.Runs).ThenByDescending(kv => kv.Value.Seconds).Select(kv => kv.Key).FirstOrDefault();

    /// <summary>The kind that has killed the player most often, and how often, or null if nothing has yet.</summary>
    public (string Kind, int Times)? Nemesis() => DeathsBy.Count == 0 ? null
        : DeathsBy.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).First();

    public long KillsOf(string kindName) => KillsByKind.GetValueOrDefault(kindName);

    public long KillsOf(MonsterRarity rarity) => KillsByRarity.GetValueOrDefault(rarity.ToString());

    /// <summary>A run is over: adds <paramref name="run"/> and what <paramref name="tally"/> counted on it to the totals, the bests and its class's record.</summary>
    public void Settle(RunRecord run, RunTally tally)
    {
        bool delve = run.Depth > 0;
        Deaths += tally.Slain ? 1 : 0;
        ReturnedToCamp += tally.Returned ? 1 : 0;
        DelveRuns += delve ? 1 : 0;
        DelveCleared += run.DelveCleared ? 1 : 0;
        SecondsPlayed += run.Seconds;
        MostKills = Math.Max(MostKills, run.Kills);
        BestStreak = Math.Max(BestStreak, tally.BestStreak);
        HighestLevel = Math.Max(HighestLevel, run.Level);
        if (run.DelveCleared && (FastestDelveClear <= 0f || run.Seconds < FastestDelveClear))
        {
            FastestDelveClear = run.Seconds;
        }

        DamageDealt += tally.DamageDealt;
        MostDamage = MathF.Max(MostDamage, tally.DamageDealt);
        HealthLost += tally.HealthLost;
        HitsTaken += tally.HitsTaken;
        Blocks += tally.Blocks;
        ElitesKilled += run.ElitesKilled;
        BossesKilled += run.BossesKilled;
        foreach (var (rarity, count) in tally.KillsByRarity)
        {
            KillsByRarity[rarity] = KillsByRarity.GetValueOrDefault(rarity) + count;
        }

        foreach (var (kind, count) in tally.KillsByKind)
        {
            KillsByKind[kind] = KillsByKind.GetValueOrDefault(kind) + count;
        }

        if (tally.Slain && tally.LastHitBy is { } killer)
        {
            DeathsBy[killer] = DeathsBy.GetValueOrDefault(killer) + 1;
        }

        SilverEarned += tally.Silver;
        ItemsFound += tally.ItemsFound;
        GearFound += tally.GearFound;
        CratesBroken += tally.CratesBroken;
        Rushes += tally.Rushes;
        LevelUps += Math.Max(0, run.Level - 1);
        Rerolls += tally.Rerolls;
        Banishes += tally.Banishes;

        if (run.ClassId.Length == 0)
        {
            return;
        }

        var hero = Class(run.ClassId);
        hero.Runs++;
        hero.Wins += run.Won ? 1 : 0;
        hero.Deaths += tally.Slain ? 1 : 0;
        hero.Kills += run.Kills;
        hero.Seconds += run.Seconds;
        hero.MostKills = Math.Max(hero.MostKills, run.Kills);
        hero.HighestLevel = Math.Max(hero.HighestLevel, run.Level);
        hero.BestStreak = Math.Max(hero.BestStreak, tally.BestStreak);
        if (run.DelveCleared)
        {
            hero.NodesCleared++;
            hero.DeepestCleared = Math.Max(hero.DeepestCleared, run.Depth);
        }
    }
}

/// <summary>
/// What one run counts as it goes, for the stats page: kills by kind and rarity, the kill streak, the blows taken and blocked and who landed the last one, and
/// the odds and ends the content tells it about (crates, rushes, rerolls). The rest is filled in at the run's end, then it is <see cref="StatsRecord.Settle"/>d.
/// </summary>
internal sealed class RunTally
{
    public Dictionary<string, int> KillsByKind { get; } = new();

    public Dictionary<string, int> KillsByRarity { get; } = new();

    /// <summary>Kills since a blow last landed on the player.</summary>
    public int Streak { get; private set; }

    public int BestStreak { get; private set; }

    public int HitsTaken { get; private set; }

    public int Blocks { get; private set; }

    /// <summary>The kind of enemy whose blow last landed: the one to blame if the run ends in death.</summary>
    public string? LastHitBy { get; private set; }

    public int CratesBroken { get; set; }

    public int Rushes { get; set; }

    public int Rerolls { get; set; }

    public int Banishes { get; set; }

    // Filled in at the run's end.
    public bool Slain { get; set; }

    public bool Returned { get; set; }

    public float DamageDealt { get; set; }

    public float HealthLost { get; set; }

    public long Silver { get; set; }

    public int ItemsFound { get; set; }

    public int GearFound { get; set; }

    /// <summary>A fresh run: nothing counted yet.</summary>
    public void Begin()
    {
        KillsByKind.Clear();
        KillsByRarity.Clear();
        Streak = BestStreak = HitsTaken = Blocks = CratesBroken = Rushes = Rerolls = Banishes = ItemsFound = GearFound = 0;
        LastHitBy = null;
        Slain = Returned = false;
        DamageDealt = HealthLost = 0f;
        Silver = 0;
    }

    /// <summary>An enemy killed (a crate is not a kill: the caller counts those as <see cref="CratesBroken"/>).</summary>
    public void Kill(EnemyKind kind, MonsterRarity rarity)
    {
        KillsByKind[kind.Name] = KillsByKind.GetValueOrDefault(kind.Name) + 1;
        if (rarity != MonsterRarity.Normal)
        {
            string name = rarity.ToString();
            KillsByRarity[name] = KillsByRarity.GetValueOrDefault(name) + 1;
        }

        Streak++;
        BestStreak = Math.Max(BestStreak, Streak);
    }

    /// <summary>A blow that reached the player: a block counts as one, anything else as a hit, which ends the kill streak.</summary>
    public void Struck(Strike strike)
    {
        if (strike.Blocked)
        {
            Blocks++;
            return;
        }

        HitsTaken++;
        LastHitBy = strike.Attacker.Kind.Name;
        Streak = 0;
    }
}
