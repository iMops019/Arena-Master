using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// A run: 30 minutes as the Ranger in the middle of the map, with the loadout's items (and only those) giving bonuses. Experience levels the run (a pick of three
// upgrades each time) and the active passive tree too. Items found go straight into the chest for a later run - they do nothing in this one. It ends in victory
// at 30:00, in death, or on "Return to Camp", and its summary follows.
public sealed partial class ArenaMasterContent
{
    /// <summary>How much tougher the final boss is than the director's scaling alone would make it.</summary>
    private const float FinalBossHealthBonus = 1.5f;

    private const float AnnouncementSeconds = 3f;

    /// <summary>How long each "new item" popup stays up.</summary>
    private const float ItemToastSeconds = 3f;

    /// <summary>How often a run saves the tree's experience and the items found, so a crash or a quit loses little.</summary>
    private const float RunSaveInterval = 20f;

    /// <summary>How long a kill counts toward Momentum.</summary>
    private const float MomentumWindow = 3f;

    private readonly RangerBow _bow;
    private readonly Experience _experience = new();
    private readonly RunDirector _director = new();
    private readonly EnemyField _enemies;
    private readonly EnemyView _enemyView = new();
    private readonly XpGemField _gems = new();
    private readonly XpGemView _gemView = new();
    private readonly DamageNumbers _numbers = new();
    private readonly LevelUpScreen _levelUp = new();
    private readonly RunItems _items = new();
    private readonly LootField _loot;
    private readonly LootView _lootView = new();
    private readonly Queue<RunItem> _itemToasts = new();
    private readonly Queue<float> _recentKills = new();
    private float _itemToastLeft;

    /// <summary>The fraction of an experience point an experience bonus has built up but not yet paid out.</summary>
    private float _experienceCarry;
    private float _runSeconds;
    private int _pendingLevels;
    private long _runTreeExperience;
    private int _runTreeLevels;
    private float _saveIn;

    /// <summary>What the Quartermaster's upgrades give this run on the level-up screen, and what has been banished so far.</summary>
    private int _rerollsLeft;
    private int _banishesLeft;
    private readonly HashSet<RangerUpgrade> _banished = new();
    private List<UpgradeChoice> _levelUpChoices = new();
    private int _elitesKilled;
    private int _bossesKilled;

    private string _announcement = "";
    private float _announcementLeft;

    /// <summary>Sets out from the gate: a fresh run with the loadout's items, the tree's bonuses, full health, at the run start.</summary>
    private void BeginRun(EngineWindow window)
    {
        _stats.Reset();
        _stats.Tree = SharpshooterBonuses.From(_tree.Save.Ranks);
        _items.Begin(Loadout.ItemsToBring(_profile));   // the loadout as it stands now: finds made on this run won't join it
        _stats.Items = _items.Carried.Bonuses;
        _health.Reset(_stats.MaxHealth);
        _health.DamageTaken = _stats.DamageTaken;
        _condition.Clear();
        _experience.Reset();
        _experienceCarry = 0f;
        _pendingLevels = 0;
        _enemies.ResetKills();
        _director.Reset();
        _recentKills.Clear();
        _itemToasts.Clear();
        _itemToastLeft = 0f;
        _runSeconds = 0f;
        _runTreeExperience = 0;
        _runTreeLevels = 0;
        _saveIn = RunSaveInterval;
        _rerollsLeft = Shop.RerollsPerRun(_profile);
        _banishesLeft = Shop.BanishesPerRun(_profile);
        _banished.Clear();
        _elitesKilled = 0;
        _bossesKilled = 0;

        if (window.Terrain is { } terrain)
        {
            window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.RunStart));
        }

        _mode = GameMode.Run;
        Announce("Survive until 30:00");
    }

    private void UpdateRun(EngineWindow window, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        DevKeys(window, groundAt);
        _runSeconds += deltaSeconds;
        if (RunDirector.IsWon(_runSeconds))
        {
            EndRun(window, RunEnding.Won);
            return;
        }

        FollowOrders(_director.Update(_runSeconds, _enemies), window.PlayerFeet, groundAt);
        UpdateMomentum();

        bool standingStill = window.PlayerMoveDirection == Vector3D<float>.Zero && _ranger.DashVelocity == Vector3D<float>.Zero && _condition.Knockback == Vector3D<float>.Zero;
        foreach (var hit in _bow.Update(window, deltaSeconds, _stats, _enemies, _numbers, groundAt, canFire: !_condition.IsStunned, standingStill))
        {
            if (hit.Crit && hit.Enemy.Kind.Tier != EnemyTier.Fodder)
            {
                _health.Heal(_stats.Tree.EliteCritHeal);   // Headhunter
            }
        }

        var player = new PlayerTarget(window.PlayerFeet, window.PlayerGrounded, _health, _condition);
        var gone = _enemies.Update(deltaSeconds, player, groundAt);
        foreach (var killed in _enemies.TakeNewlyKilled())
        {
            OnKill(killed);
        }

        _enemyView.Sync(window, _enemies, gone, deltaSeconds, groundAt);

        var collected = new List<XpGem>();
        GainExperience(_gems.Update(deltaSeconds, window.PlayerFeet, _stats.PickupRadius, collected));
        _gemView.Sync(window, _gems, deltaSeconds);

        var goneChests = new List<Chest>();
        var gonePickups = new List<ItemPickup>();
        foreach (var item in _loot.Update(deltaSeconds, window.PlayerFeet, groundAt, goneChests, gonePickups))
        {
            GainItem(item);
        }

        _lootView.Sync(window, _loot, goneChests, gonePickups, deltaSeconds);
        _health.Heal(_stats.Regeneration * deltaSeconds);

        _saveIn -= deltaSeconds;
        if (_saveIn <= 0f)
        {
            _saveIn = RunSaveInterval;
            SaveProfile();
        }

        if (_health.IsDead)
        {
            EndRun(window, RunEnding.Slain);
        }
        else if (_pendingLevels > 0 && !_levelUp.IsOpen)
        {
            OpenLevelUp(window);
        }
    }

    /// <summary>The run is over: clear the field, save, and show how it went. The summary's button returns to camp.</summary>
    private void EndRun(EngineWindow window, RunEnding ending)
    {
        foreach (var enemy in _enemies.Clear())
        {
            _enemyView.Remove(window, enemy);
        }

        _enemyView.Sync(window, _enemies, Array.Empty<Enemy>(), 0f, (_, _) => null);   // an empty field: every enemy crowd drawn empty

        _gems.Clear();
        _gemView.Sync(window, _gems, 0f);
        var goneChests = new List<Chest>();
        var gonePickups = new List<ItemPickup>();
        _loot.Clear(goneChests, gonePickups);
        _lootView.Sync(window, _loot, goneChests, gonePickups, 0f);
        _bow.Clear(window);
        _numbers.Clear();
        _levelUp.Close();
        _pendingLevels = 0;
        _condition.Clear();

        // What lasts: silver for the run, and any bounties it (or the lifetime totals) completed.
        var record = new RunRecord(_enemies.Kills, _elitesKilled, _bossesKilled, _runSeconds, ending == RunEnding.Won, _experience.Level);
        long silver = RunRewards.Silver(record);
        _profile.Silver += silver;
        var bounties = Bounties.Settle(record, _profile, _tree);
        SaveProfile();

        _summaryScreen.Open(new RunSummary(ending, _runSeconds, _experience.Level, _enemies.Kills, _items.Found.ToList(),
            _runTreeExperience, _runTreeLevels, _tree.Level, _tree.Tree.Name, silver, bounties));
        _mode = GameMode.Summary;
        window.GamePaused = true;
    }

    /// <summary>What a kill leaves behind: its experience gem, and loot - an elite's or a boss's chest, or now and then an item from fodder.</summary>
    private void OnKill(Enemy killed)
    {
        _gems.Drop(killed.Position, killed.Kind.Experience);
        _health.Heal(_items.Carried.Bonuses.HealOnKill);
        _recentKills.Enqueue(_runSeconds);

        switch (killed.Kind.Tier)
        {
            case EnemyTier.Boss:
                _bossesKilled++;
                _loot.DropChest(killed.Position, RarityWeights.Boss);
                Announce($"{killed.Kind.Name} has fallen!");
                break;
            case EnemyTier.Elite:
                _elitesKilled++;
                _loot.DropChest(killed.Position, RarityWeights.Elite);
                break;
            case EnemyTier.Fodder when _loot.RollFodderDrop():
                _loot.DropItem(killed.Position);
                break;
        }
    }

    /// <summary>Momentum: how many kills in the last few seconds, which the stats turn into attack speed.</summary>
    private void UpdateMomentum()
    {
        while (_recentKills.Count > 0 && _recentKills.Peek() < _runSeconds - MomentumWindow)
        {
            _recentKills.Dequeue();
        }

        _stats.MomentumStacks = Math.Min(RangerStats.MaxMomentumStacks, _recentKills.Count);
    }

    /// <summary>
    /// Adds experience: to the run (raised by any experience bonus, fractions carried to the next pickup, a level-up screen queued per level) and, at its base amount,
    /// to the active passive tree.
    /// </summary>
    private void GainExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _experienceCarry += amount * (1f + _items.Carried.Bonuses.ExperienceGain);
        int whole = (int)_experienceCarry;
        _experienceCarry -= whole;
        _pendingLevels += _experience.Add(whole);

        _runTreeExperience += amount;
        int treeLevels = _tree.AddExperience(amount);
        if (treeLevels > 0)
        {
            _runTreeLevels += treeLevels;
            Announce($"{_tree.Tree.Name} reached level {_tree.Level}! Spend the point at camp.");
            SaveProfile();
        }
    }

    /// <summary>
    /// Takes an item found on the run: into the chest for good and up on screen. It gives nothing on this run - the run's bonuses are the loadout's, fixed when it
    /// set out - so bringing it is a choice for the next run.
    /// </summary>
    private void GainItem(RunItem item)
    {
        _items.Find(item, _profile);

        _itemToasts.Enqueue(item);
        if (_itemToasts.Count == 1)
        {
            _itemToastLeft = ItemToastSeconds;
        }
    }

    /// <summary>A higher max health (an item, an upgrade) heals the difference.</summary>
    private void SyncMaxHealth()
    {
        if (_stats.MaxHealth > _health.Max)
        {
            _health.RaiseMax(_stats.MaxHealth - _health.Max);
        }
    }

    private void UpdateItemToasts(float deltaSeconds)
    {
        if (_itemToasts.Count == 0)
        {
            return;
        }

        _itemToastLeft -= deltaSeconds;
        if (_itemToastLeft <= 0f)
        {
            _itemToasts.Dequeue();
            _itemToastLeft = _itemToasts.Count > 0 ? ItemToastSeconds : 0f;
        }
    }

    /// <summary>Spawns what the director asked for: a wave of elites, a boss.</summary>
    private void FollowOrders(DirectorOrders orders, Vector3D<float> playerFeet, Func<float, float, float?> groundAt)
    {
        for (int i = 0; i < orders.Elites; i++)
        {
            _enemies.SpawnAround(EnemyKind.Brute, playerFeet, groundAt);
        }

        if (orders.Elites > 0)
        {
            Announce(orders.Elites == 1 ? "A Ghoul Brute approaches" : $"{orders.Elites} Ghoul Brutes approach");
        }

        if (orders.Boss)
        {
            SpawnBoss(playerFeet, groundAt, orders.FinalBoss);
        }
    }

    private void SpawnBoss(Vector3D<float> playerFeet, Func<float, float, float?> groundAt, bool final)
    {
        var scaling = _enemies.Scaling;
        if (final)
        {
            _enemies.Scaling = scaling with { Health = scaling.Health * FinalBossHealthBonus };
        }

        _enemies.SpawnAround(EnemyKind.HollowKing, playerFeet, groundAt);
        _enemies.Scaling = scaling;
        Announce(final ? "The Hollow King returns, in full fury" : "The Hollow King rises");
    }

    private void Announce(string text)
    {
        _announcement = text;
        _announcementLeft = AnnouncementSeconds;
    }

    /// <summary>
    /// Testing shortcuts, for now: F5 skips a minute ahead on the run clock, F6 brings in an elite, F7 brings in the boss. They go before a real release.
    /// </summary>
    private void DevKeys(EngineWindow window, Func<float, float, float?> groundAt)
    {
        if (Pressed(window, Key.F5))
        {
            _runSeconds = MathF.Min(_runSeconds + 60f, RunDirector.RunLength - 1f);
        }

        if (Pressed(window, Key.F6))
        {
            _enemies.SpawnAround(EnemyKind.Brute, window.PlayerFeet, groundAt);
            Announce("A Ghoul Brute approaches");
        }

        if (Pressed(window, Key.F7))
        {
            SpawnBoss(window.PlayerFeet, groundAt, final: false);
        }
    }

    private void DrawLevelUp(EngineWindow window)
    {
        if (_levelUp.Draw() is not { } action)
        {
            return;
        }

        if (action.Reroll && _rerollsLeft > 0)
        {
            _rerollsLeft--;
            _levelUp.Refresh(RangerUpgrades.Roll(_stats, _random, excluded: _banished), _rerollsLeft, _banishesLeft);
            return;
        }

        if (action.Banish is { } index && _banishesLeft > 0 && _levelUpChoices[index].Upgrade is { } banished)
        {
            _banishesLeft--;
            _banished.Add(banished);
            _levelUpChoices = RangerUpgrades.Replace(_levelUpChoices, index, _stats, _random, _banished);
            _levelUp.Refresh(_levelUpChoices, _rerollsLeft, _banishesLeft);
            return;
        }

        if (action.Take is not { } choice)
        {
            return;
        }

        Apply(choice);
        _pendingLevels--;
        if (_pendingLevels > 0)
        {
            OpenLevelUp(window);   // several levels at once: one pick each
        }
        else
        {
            window.GamePaused = false;
        }
    }

    /// <summary>Pauses the world and offers three upgrades for the next level still to be picked for.</summary>
    private void OpenLevelUp(EngineWindow window)
    {
        int reached = _experience.Level - _pendingLevels + 1;
        _levelUpChoices = RangerUpgrades.Roll(_stats, _random, excluded: _banished);
        _levelUp.Open(_levelUpChoices, reached, _rerollsLeft, _banishesLeft);
        window.GamePaused = true;
    }

    private void Apply(UpgradeChoice choice)
    {
        if (choice.Upgrade is not { } upgrade)
        {
            _health.Heal(RangerUpgrades.SecondWindHeal);
            return;
        }

        _stats.Increase(upgrade);
        SyncMaxHealth();
    }
}
