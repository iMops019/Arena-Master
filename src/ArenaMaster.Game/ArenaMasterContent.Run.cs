using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// A run as the chosen class, with the loadout's items (and only those) and the worn gear giving bonuses: a classic run of 30 minutes in the middle of the map, or
// a Delve node's (see ArenaMasterContent.Delve.cs). Experience levels the run (a pick of three upgrades each time) and the active passive tree too. Items found go
// straight into the chest for a later run - they do nothing in this one. A classic run ends in victory at 30:00, a Delve run when its boss's cache is opened;
// either in death, or on "Return to Camp"; and its summary follows.
public sealed partial class ArenaMasterContent
{
    /// <summary>How much tougher the final boss is than the director's scaling alone would make it.</summary>
    private const float FinalBossHealthBonus = 1.5f;

    private const float AnnouncementSeconds = 3f;

    /// <summary>How long each "new item" popup stays up.</summary>
    private const float ItemToastSeconds = 3f;

    /// <summary>How often a run saves the tree's experience and the items found, so a crash or a quit loses little.</summary>
    private const float RunSaveInterval = 20f;

    private readonly Experience _experience = new();
    private readonly RunDirector _director = new();
    private readonly EnemyField _enemies;
    private readonly EnemyView _enemyView = new();
    private readonly MonsterRush _rush = new(new Random());
    private readonly XpGemField _gems = new();
    private readonly XpGemView _gemView = new();
    private readonly DamageNumbers _numbers = new();
    private readonly LevelUpScreen _levelUp = new();
    private readonly RunItems _items = new();
    private readonly LootField _loot;
    private readonly LootView _lootView = new();
    private readonly ItemEffects _itemEffects = new();
    private readonly ItemEffectsView _itemEffectsView = new();
    private readonly List<ItemHit> _itemHits = new();
    private readonly Queue<RunItem> _itemToasts = new();
    private float _itemToastLeft;

    /// <summary>The fraction of an experience point an experience bonus has built up but not yet paid out.</summary>
    private float _experienceCarry;
    private float _runSeconds;
    private int _pendingLevels;
    private long _runTreeExperience;
    private int _runTreeLevels;
    private float _saveIn;

    /// <summary>What the Quartermaster's upgrades give this run on the level-up screen. The class keeps what has been banished.</summary>
    private int _rerollsLeft;
    private int _banishesLeft;
    private int _elitesKilled;
    private int _bossesKilled;

    /// <summary>What this run counts for the stats page, and the player's lifetime health lost as it set out.</summary>
    private readonly RunTally _tally = new();
    private double _healthLostBefore;

    private string _announcement = "";
    private float _announcementLeft;

    /// <summary>The Battle Elixirs bought for this run join its bonuses, and leave the profile: they are for this run only.</summary>
    private void DrinkElixirs()
    {
        var drunk = Elixirs.Drink(_profile);
        foreach (var elixir in drunk)
        {
            _items.Carried.AddBonus(elixir.Apply);
        }

        if (drunk.Count > 0)
        {
            SaveProfile();
        }
    }

    /// <summary>Sets out down the stairs on <paramref name="plan"/>: a fresh run with the loadout's items, the gear, the tree's bonuses, full health, at the run start.</summary>
    private void BeginRun(EngineWindow window, Delve.RunPlan plan)
    {
        _plan = plan;
        RememberCampLook(window);
        EnterCave(window);   // every run is played underground
        BeginStirring();
        BeginHunger();
        _hero.UseTree(_tree.Save.Ranks);
        _items.Begin(Loadout.ItemsToBring(_profile));   // the loadout as it stands now: finds made on this run won't join it
        WearGear();
        DrinkElixirs();
        var bonuses = _items.Carried.Bonuses;
        _hero.BeginRun(bonuses, _health);
        _health.LastStands += bonuses.LastStands;   // the Phoenix Feather's, on top of any the class has
        _enemies.HitEffects = ItemEffects.HitEffectsOf(bonuses);
        _enemies.HealthBonus = bonuses.EnemyHealth;
        _enemies.RangedDamageTaken = bonuses.RangedDamageTaken;
        _enemies.Obstacles = (centre, radius) => window.TouchesObstacle(centre, radius, out _, out _);
        _enemies.Arena = null;   // a boss hunt sets its own
        _itemEffects.Begin();
        BeginCrates();
        _health.DamageTaken = _hero.DamageTaken * Armour.Cut(ArmourNow);
        _condition.Clear();
        _experience.Reset();
        _experienceCarry = 0f;
        _pendingLevels = 0;
        _enemies.ResetKills();
        _director.Reset();
        _rush.Reset();
        _itemToasts.Clear();
        _itemToastLeft = 0f;
        _runSeconds = 0f;
        _runTreeExperience = 0;
        _runTreeLevels = 0;
        _saveIn = RunSaveInterval;
        _rerollsLeft = Shop.RerollsPerRun(_profile) + bonuses.Rerolls;
        _banishesLeft = Shop.BanishesPerRun(_profile);
        _elitesKilled = 0;
        _bossesKilled = 0;
        _tally.Begin();
        _healthLostBefore = _health.HealthLost;

        DismissQuartermaster(window);
        _mode = GameMode.Run;
        if (window.Terrain is not { } terrain)
        {
            return;
        }

        if (plan.IsDelve)
        {
            BeginDelve(window, terrain);
            return;
        }

        window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.RunStart));
        Announce("Survive until 30:00");
    }

    private void UpdateRun(EngineWindow window, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        DevKeys(window, groundAt);
        _runSeconds += deltaSeconds;
        if (_plan.IsDelve)
        {
            UpdateDelve(window, deltaSeconds, groundAt);
        }
        else if (RunDirector.IsWon(_runSeconds))
        {
            EndRun(window, RunEnding.Won);
            return;
        }
        else
        {
            FollowOrders(_director.Update(_runSeconds, _enemies), window.PlayerFeet, groundAt);
        }

        // The Monster Rush, on top of whichever director set the numbers: not in the boss hunt's arena, and not while a boss is on the field.
        if (_rush.Update(deltaSeconds, allowed: _plan.Kind != Delve.RunKind.Arena && _enemies.Boss is null))
        {
            Announce("MONSTER RUSH!  Hold out for 30 seconds");
            _tally.Rushes++;
        }

        _rush.Apply(_enemies);
        UpdateStirring(window, deltaSeconds);
        UpdateHunger(window, deltaSeconds);

        // The class attacks; the enemies move and strike (against the block chance and damage cut as they stand this frame); the class answers their blows.
        bool standingStill = window.PlayerMoveDirection == Vector3D<float>.Zero && _hero.DashVelocity == Vector3D<float>.Zero && _condition.Knockback == Vector3D<float>.Zero;
        var frame = new RunFrame(window, deltaSeconds, _runSeconds, _enemies, _numbers, groundAt, _health, _condition, standingStill);
        _hero.Attack(frame);
        _itemHits.Clear();
        _itemEffects.Update(deltaSeconds, window.PlayerFeet, _items.Carried.Bonuses, _enemies, _health, _hero.KeepsOwnBarrier, _itemHits);
        _health.DamageTaken = _hero.DamageTaken * Armour.Cut(ArmourNow);
        var player = new PlayerTarget(window.PlayerFeet, window.PlayerGrounded, _health, _condition, _hero.BlockChance, LookingWay(window));
        var gone = _enemies.Update(deltaSeconds, player, groundAt);
        foreach (var strike in _enemies.Strikes)
        {
            _tally.Struck(strike);
        }

        foreach (var legendary in _enemies.TakeLegendarySpawns())
        {
            Announce($"A {legendary.Name} approaches!");
        }

        _hero.Answer(frame);
        _itemEffects.Answer(_enemies.Strikes, window.PlayerFeet, _items.Carried.Bonuses, _enemies, _health, _itemHits);
        foreach (var hit in _itemHits)
        {
            _numbers.Add(hit.Position, hit.Damage, hit.Killed);
        }

        _itemEffectsView.Sync(window, _itemEffects);
        foreach (var killed in _enemies.TakeNewlyKilled())
        {
            OnKill(killed);
        }

        _enemyView.Sync(window, _enemies, gone, deltaSeconds, groundAt);

        var collected = new List<XpGem>();
        GainExperience(_gems.Update(deltaSeconds, window.PlayerFeet, _hero.PickupRadius, collected));
        _gemView.Sync(window, _gems, deltaSeconds);

        var goneChests = new List<Chest>();
        var gonePickups = new List<ItemPickup>();
        foreach (var item in _loot.Update(deltaSeconds, window.PlayerFeet, groundAt, goneChests, gonePickups))
        {
            GainItem(item);
        }

        _lootView.Sync(window, _loot, goneChests, gonePickups, deltaSeconds);
        UpdateCrates(window, deltaSeconds, groundAt);
        _health.Heal(_hero.Regeneration * deltaSeconds);

        _saveIn -= deltaSeconds;
        if (_saveIn <= 0f)
        {
            _saveIn = RunSaveInterval;
            SaveProfile();
        }

        if (_health.IsDead)
        {
            EndRun(window, RunEnding.Slain);
            return;
        }

        if (_plan.IsDelve)
        {
            UpdateCache(window, deltaSeconds);   // which ends the run once the cache is open
            if (_mode != GameMode.Run)
            {
                return;
            }
        }

        if (_pendingLevels > 0 && !_levelUp.IsOpen)
        {
            OpenLevelUp(window);
        }
    }

    /// <summary>The run is over: clear the field, save, and show how it went. The summary's button returns to camp.</summary>
    private void EndRun(EngineWindow window, RunEnding ending)
    {
        ClearField(window);

        // What lasts: a cleared Delve node's cache, silver for the run, and any bounties it (or the lifetime totals, or the Delve's progress) completed.
        var delve = SettleDelve(ending);
        var record = new RunRecord(_enemies.Kills, _elitesKilled, _bossesKilled, _runSeconds, ending == RunEnding.Won, _experience.Level, _hero.Id,
            _plan.Depth, DelveCleared: ending == RunEnding.DelveCleared, DelveBoss: _plan.Kind == Delve.RunKind.Arena);
        var carried = _items.Carried.Bonuses;
        long silver = (long)MathF.Round(RunRewards.Silver(record) * (1f + carried.SilverGain) * carried.SilverMultiplier) + _runSilver;   // and what crates gave
        _profile.Silver += silver;
        var bounties = Bounties.Settle(record, _profile, _tree);
        _tally.Slain = ending == RunEnding.Slain;
        _tally.Returned = ending == RunEnding.ReturnedToCamp;
        _tally.DamageDealt = _enemies.DamageDealt;
        _tally.HealthLost = (float)(_health.HealthLost - _healthLostBefore);
        _tally.Silver = silver + (delve?.CacheSilver ?? 0) + bounties.Sum(b => b.Silver);
        _tally.ItemsFound = _items.Found.Count;
        _tally.GearFound = delve?.Gear is null ? 0 : 1;
        _profile.Stats.Settle(record, _tally);
        SaveProfile();

        _summaryScreen.Open(new RunSummary(ending, _runSeconds, _experience.Level, _enemies.Kills, _items.Found.ToList(),
            _runTreeExperience, _runTreeLevels, _tree.Level, _tree.Tree.Name, silver, bounties, delve));
        _mode = GameMode.Summary;
        window.GamePaused = true;
    }

    /// <summary>Takes everything of a run out of the world - enemies, gems, loot, arrows, numbers, a level-up in progress - with no rewards.</summary>
    private void ClearField(EngineWindow window)
    {
        var cleared = _enemies.Clear();
        _enemyView.Sync(window, _enemies, cleared, 0f, (_, _) => null);   // an empty field: every enemy crowd drawn empty, their telegraphs gone

        _gems.Clear();
        _gemView.Sync(window, _gems, 0f);
        var goneChests = new List<Chest>();
        var gonePickups = new List<ItemPickup>();
        _loot.Clear(goneChests, gonePickups);
        _lootView.Sync(window, _loot, goneChests, gonePickups, 0f);
        _hero.Clear(window);
        _itemEffects.Begin();
        _itemEffectsView.Clear(window);
        ClearCrates(window);
        RemoveCache(window);
        ClearStirringMarker(window);
        ClearMaw(window);
        _delveDirector = null;
        _enemies.HitEffects = HitEffects.None;
        _enemies.HealthBonus = 0f;
        _enemies.RangedDamageTaken = 1f;
        _enemies.DamageBoost = 1f;
        _numbers.Clear();
        _levelUp.Close();
        _pendingLevels = 0;
        _condition.Clear();
    }

    /// <summary>What a kill leaves behind: its experience gem, and by chance loot - an elite's or a boss's chest, or (very rarely) an item from fodder.</summary>
    private void OnKill(Enemy killed)
    {
        if (killed.Kind.IsProp)
        {
            OnCrateBroken(killed);   // a crate: its pickup, not a kill
            _tally.CratesBroken++;
            return;
        }

        _tally.Kill(killed.Kind, killed.Rarity.Rarity);

        _gems.Drop(killed.Position, killed.Experience);
        _health.Heal(_items.Carried.Bonuses.HealOnKill);
        _hero.OnKill(killed, _runSeconds);
        _itemEffects.OnKill(_items.Carried.Bonuses);
        HungerEvent.Feed(killed.Position);   // a Hungering Maw open near it eats it
        if (MiniBosses.Is(killed.Kind))
        {
            _elitesKilled++;
            OnMiniBossKilled(killed);   // its own reward, in place of an elite's chest
            return;
        }

        if (_loot.RollRarityDrop(killed.Rarity))
        {
            // A Rare's chest has the world's odds, a Legendary's an elite's; it takes the place of any drop its kind would have had.
            _loot.DropChest(killed.Position, killed.Rarity.Rarity == MonsterRarity.Legendary ? RarityWeights.Elite : RarityWeights.World);
            if (killed.Kind.Tier == EnemyTier.Elite)
            {
                _elitesKilled++;
            }

            return;
        }

        switch (killed.Kind.Tier)
        {
            case EnemyTier.Boss:
                _bossesKilled++;
                Announce($"{killed.Kind.Name} has fallen!");
                if (_plan.IsDelve && _window is { } window)
                {
                    DropCache(window, killed.Position);   // a Delve boss leaves the cache, not a chest
                }
                else if (_loot.RollBossDrop())
                {
                    _loot.DropChest(killed.Position, RarityWeights.Boss);
                }

                break;
            case EnemyTier.Elite:
                _elitesKilled++;
                if (_loot.RollEliteDrop())
                {
                    _loot.DropChest(killed.Position, RarityWeights.Elite);
                }

                break;
            case EnemyTier.Fodder when _loot.RollFodderDrop():
                _loot.DropItem(killed.Position);
                break;
        }
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

        _experienceCarry += amount * (1f + _items.Carried.Bonuses.ExperienceGain) * _items.Carried.Bonuses.ExperienceMultiplier;
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
        if (_hero.MaxHealth > _health.Max)
        {
            _health.RaiseMax(_hero.MaxHealth - _health.Max);
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
    /// <summary>The player's armour this moment: the class's Defense (from its tree's level) and what the body armour worn gives.</summary>
    private float ArmourNow => Armour.Defense(_tree.Level) + _items.Carried.Bonuses.Armour;

    /// <summary>The flat way the player is looking: the camera's (every hero faces its aim), or zero if there is no camera.</summary>
    private static Vector3D<float> LookingWay(EngineWindow window)
    {
        if (window.Camera is not { } camera)
        {
            return Vector3D<float>.Zero;
        }

        var flat = new Vector3D<float>(camera.Front.X, 0f, camera.Front.Z);
        return flat.LengthSquared > 1e-6f ? Vector3D.Normalize(flat) : Vector3D<float>.Zero;
    }

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
        Announce(_plan.IsDelve ? "The Hollow King rises: slay him to clear the floor" : final ? "The Hollow King returns, in full fury" : "The Hollow King rises");
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

        if (Pressed(window, Key.F9) && !_rush.Active)
        {
            _rush.Start();
            Announce("MONSTER RUSH!  Hold out for 30 seconds");
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
            _tally.Rerolls++;
            _levelUp.Refresh(_hero.RollLevelUp(_random), _rerollsLeft, _banishesLeft);
            return;
        }

        if (action.Banish is { } index && _banishesLeft > 0)
        {
            _banishesLeft--;
            _tally.Banishes++;
            _levelUp.Refresh(_hero.BanishCard(index, _random), _rerollsLeft, _banishesLeft);
            return;
        }

        if (action.Take is not { } choice)
        {
            return;
        }

        _hero.TakeCard(choice, _health);
        SyncMaxHealth();
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
        _levelUp.Open(_hero.RollLevelUp(_random), reached, _rerollsLeft, _banishesLeft);
        window.GamePaused = true;
    }
}
