using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// The HUD: at camp, the tree's level and points and what E does where the player stands; on a run, experience, clock, kills, health, dash, items, the boss bar and
// the announcements.
public sealed partial class ArenaMasterContent
{
    private static readonly Vector4D<float> White = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4D<float> Gold = new(1f, 0.84f, 0.35f, 1f);
    private static readonly Vector4D<float> Red = new(0.95f, 0.3f, 0.3f, 1f);
    private static readonly Vector4D<float> Teal = new(0.33f, 0.82f, 0.76f, 1f);
    private static readonly Vector4D<float> Shade = new(0f, 0f, 0f, 0.45f);
    private static readonly Vector4D<float> Unique = new(1f, 0.56f, 0.18f, 1f);

    /// <summary>The run's clock, and what it is counting toward: 30:00 on a classic run; on a Delve node the King at 10:00, then slaying him, then his cache.</summary>
    private string RunClock()
    {
        string clock = Clock(_runSeconds);
        return _plan.Kind switch
        {
            Delve.RunKind.Classic => $"{clock} / {Clock(RunDirector.RunLength)}",
            Delve.RunKind.Arena => _cacheAt is not null ? $"{clock}  ·  open the cache" : $"{clock}  ·  {_plan.HuntBoss.Name}",
            _ when _cacheAt is not null => $"{clock}  ·  open the cache",
            _ when _delveDirector is { BossCalled: true } => $"{clock}  ·  slay the King",
            _ when _delveDirector is { King: false } => $"{clock}  ·  the cache at {Clock(Delve.DelveDirector.BossAt)}",
            _ => $"{clock}  ·  the King at {Clock(Delve.DelveDirector.BossAt)}",
        };
    }

    public void DrawHud(IHud hud)
    {
        switch (_mode)
        {
            case GameMode.Camp:
                DrawCampHud(hud);
                break;
            case GameMode.Run:
                DrawRunHud(hud);
                break;
        }

        if (_saveProblem is not null)
        {
            hud.Text(HudAnchor.BottomRight, new Vector2D<float>(-20f, -20f), _saveProblem, Red, 0.7f);
        }
    }

    private void DrawCampHud(IHud hud)
    {
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 20f), $"CAMP  ·  {_hero.Name.ToUpperInvariant()}", Gold, 1.2f);
        int free = _tree.FreePoints;
        string tree = $"{_tree.Tree.Name}  ·  Lv {_tree.Level}" + (free > 0 ? $"  ·  {free} point{(free == 1 ? "" : "s")} to spend" : "");
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 52f), tree, free > 0 ? Teal : White, 0.85f);
        int owned = _profile.Stash.Values.Sum();
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 78f), $"Item chest  ·  {owned} item{(owned == 1 ? "" : "s")}", White, 0.8f);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 102f), $"Silver  ·  {_profile.Silver:N0}", Gold, 0.8f);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 126f), $"Delve  ·  depth {_profile.Delve.Deepest}  ·  {_profile.Delve.Marks:N0} Marks", Unique, 0.8f);

        if (_nearStation is { } near)
        {
            hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -120f), $"[E]  {near.Prompt}", Gold, 1.1f);
        }
        else
        {
            hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -120f),
                "Chest, quartermaster and armour stand on the right, tree target and bounty board on the left, class rack behind you, the gate ahead opens the Delve",
                new Vector4D<float>(1f, 1f, 1f, 0.6f), 0.8f);
        }

        DrawDash(hud);
    }

    private void DrawRunHud(IHud hud)
    {
        if (_window?.Camera is { } camera)
        {
            DrawRarityTags(hud, camera);
            DrawPounceWarnings(hud, camera);
            _numbers.Draw(hud, camera);
        }

        // Experience across the top, with the level at its left end and the tree under it; the run clock under its middle; kills at the right.
        float width = hud.ScreenSize.X - 48f;
        hud.Bar(HudAnchor.TopLeft, new Vector2D<float>(24f, 14f), new Vector2D<float>(width, 12f), _experience.Progress,
            new Vector4D<float>(0.35f, 0.8f, 0.95f, 0.95f), Shade);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 32f), $"LV {_experience.Level}", Gold, 1f);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 60f), $"{_tree.Tree.Name} Lv {_tree.Level}  +{_runTreeExperience:N0} xp", Teal, 0.65f);
        hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 32f), RunClock(), White, 1.1f);
        if (_plan.Node is { } node)
        {
            hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 82f), $"Depth {node.Depth}  ·  {node.Name}", Unique, 0.75f);
        }
        else if (_plan.Kind == Delve.RunKind.Arena)
        {
            hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 82f), $"Boss hunt  ·  {_plan.HuntBoss.Name}", Unique, 0.75f);
        }
        hud.Text(HudAnchor.TopRight, new Vector2D<float>(-26f, 32f), $"Kills  {_enemies.Kills}", White, 0.9f);

        // A Monster Rush's countdown, under the clock (under the boss's bar, if one turned up during it).
        if (_rush.Active)
        {
            float pulse = 0.75f + 0.25f * MathF.Abs(MathF.Sin(_rush.Left * 4f));
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, _enemies.Boss is null ? 64f : 112f), $"MONSTER RUSH  {MathF.Ceiling(_rush.Left)}s",
                new Vector4D<float>(Red.X, Red.Y, Red.Z, pulse), 0.95f);
        }

        // The boss's health, under the clock, while one is on the field.
        if (_enemies.Boss is { } boss)
        {
            string state = boss.IsRoaring ? "  ·  IMMUNE" : boss.IsEnraged ? $"  ·  FRENZIED RAGE ({boss.Rage} RAGE)" : "";
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 64f), boss.Kind.Name.ToUpperInvariant() + state, Red, 0.9f);
            hud.Bar(HudAnchor.TopCenter, new Vector2D<float>(0f, 88f), new Vector2D<float>(520f, 16f), boss.Health / boss.MaxHealth,
                new Vector4D<float>(0.75f, 0.15f, 0.2f, 0.95f), Shade);
        }

        // Health, bottom left, flashing red when hit.
        float health = _health.Current / _health.Max;
        var healthColor = Vector4D.Lerp(new Vector4D<float>(0.78f, 0.2f, 0.22f, 0.95f), new Vector4D<float>(1f, 0.55f, 0.55f, 1f), _health.HurtFlash);
        hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -28f), new Vector2D<float>(260f, 18f), health, healthColor, Shade);
        string barrier = "";
        if (_health.Shield > 0f || _health.ShieldMax > 0f)
        {
            // A gear shield: a pale gold band along the top of the bar, full when it is whole, empty (and counting back) once it has broken.
            float share = _health.ShieldMax > 0f ? Math.Clamp(_health.Shield / _health.ShieldMax, 0f, 1f) : 0f;
            hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -42f), new Vector2D<float>(260f, 6f), share, new Vector4D<float>(1f, 0.78f, 0.35f, 0.95f), Shade);
            barrier += _health.Shield > 0f ? $"   +{MathF.Ceiling(_health.Shield)} gear shield" : $"   gear shield in {MathF.Ceiling(_itemEffects.ShieldReturnsIn)}s";
        }

        if (_health.Barrier > 0f)
        {
            // A barrier over the health (the Mage's Frost Shield): an icy band along the bottom of the bar, as a share of max health.
            hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -28f), new Vector2D<float>(260f, 6f), Math.Clamp(_health.Barrier / _health.Max, 0f, 1f),
                new Vector4D<float>(0.6f, 0.9f, 1f, 0.95f), new Vector4D<float>(0f, 0f, 0f, 0f));
            barrier += $"   +{MathF.Ceiling(_health.Barrier)} shield";
        }

        hud.Text(HudAnchor.BottomLeft, new Vector2D<float>(28f, -64f), $"HP  {MathF.Ceiling(_health.Current)} / {_health.Max}{barrier}", White, 0.75f);
        float armour = ArmourNow;
        hud.Text(HudAnchor.BottomLeft, new Vector2D<float>(28f, -86f), $"Armour  {armour:0}  (-{Armour.Reduction(armour) * 100f:0}% damage)",
            new Vector4D<float>(0.75f, 0.8f, 0.9f, 0.85f), 0.65f);
        if (_health.HurtFlash > 0f)
        {
            hud.Rect(HudAnchor.TopLeft, Vector2D<float>.Zero, new Vector2D<float>(hud.ScreenSize.X, hud.ScreenSize.Y), new Vector4D<float>(0.7f, 0f, 0f, 0.18f * _health.HurtFlash));
        }

        DrawDash(hud);

        // The items this run set out with (the ones giving bonuses), down the right side in their rarity's colour; then how many have been found for the chest.
        int row = 0;
        foreach (var (item, count) in _items.Carried.Items)
        {
            string label = count > 1 ? $"{item.Name}  x{count}" : item.Name;
            hud.Text(HudAnchor.TopRight, new Vector2D<float>(-26f, 66f + row * 22f), label, RarityColor(item.Rarity, 0.95f), 0.7f);
            row++;
        }

        if (_items.Found.Count > 0)
        {
            hud.Text(HudAnchor.TopRight, new Vector2D<float>(-26f, 72f + row * 22f), $"Found for the chest: {_items.Found.Count}", new Vector4D<float>(1f, 1f, 1f, 0.6f), 0.65f);
        }

        // The item just found, big, under the top bar: it goes to the chest, for a later run.
        if (_itemToastLeft > 0f && _itemToasts.TryPeek(out var found))
        {
            float alpha = MathF.Min(1f, _itemToastLeft * 2f);
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 124f), $"{found.Rarity.ToString().ToUpperInvariant()}:  {found.Name}", RarityColor(found.Rarity, alpha), 1.2f);
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 156f), $"{found.Description}  ·  Sent to your chest. Bring it on a later run.",
                new Vector4D<float>(1f, 1f, 1f, 0.9f * alpha), 0.85f);
        }

        if (_condition.IsStunned)
        {
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 60f), "STUNNED", new Vector4D<float>(1f, 0.85f, 0.3f, 1f), 1.2f);
        }
        else if (_hero.Status is { } status)
        {
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 34f), status, Teal, 0.8f);   // a readied shot, a shield up, a block
        }

        if (_announcementLeft > 0f)
        {
            float alpha = MathF.Min(1f, _announcementLeft);
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, -120f), _announcement, new Vector4D<float>(Red.X, Red.Y, Red.Z, alpha), 1.4f);
        }
    }

    /// <summary>How far off a Rare or Legendary enemy still shows its name tag.</summary>
    private const float RarityTagDistance = 45f;

    /// <summary>
    /// Over each Rare and Legendary enemy near enough, its name in its rarity's colour and a thin health bar (a Magic one only has its ring, or a swarm would
    /// be all tags).
    /// </summary>
    private void DrawRarityTags(IHud hud, CEngine.Core.Camera camera)
    {
        var screen = hud.ScreenSize;
        if (screen.X <= 0 || screen.Y <= 0 || _window is not { } window)
        {
            return;
        }

        var viewProjection = camera.GetView() * camera.GetProjection((float)screen.X / screen.Y);
        var feet = window.PlayerFeet;
        foreach (var enemy in _enemies.Enemies)
        {
            if (!enemy.IsAlive || enemy.Rarity.Rarity < MonsterRarity.Rare || Vector3D.Distance(enemy.Position, feet) > RarityTagDistance)
            {
                continue;
            }

            var above = enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * enemy.Rarity.Size + 0.45f, 0f);
            var clip = Vector4D.Transform(new Vector4D<float>(above, 1f), viewProjection);
            if (clip.W <= 0.1f)
            {
                continue;   // behind the camera
            }

            float x = (clip.X / clip.W * 0.5f + 0.5f) * screen.X;
            float y = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * screen.Y;
            var colour = MonsterRarityColor(enemy.Rarity.Rarity);
            float scale = enemy.Rarity.Rarity == MonsterRarity.Legendary ? 0.8f : 0.65f;
            var size = hud.MeasureText(enemy.Name, scale);
            hud.Text(HudAnchor.TopLeft, new Vector2D<float>(x - size.X / 2f, y - size.Y - 8f), enemy.Name, colour, scale);
            hud.Bar(HudAnchor.TopLeft, new Vector2D<float>(x - 40f, y - 4f), new Vector2D<float>(80f, 5f), enemy.Health / enemy.MaxHealth, colour, Shade);
        }
    }

    /// <summary>
    /// A red warning round the crosshair, on the side a stalker is winding up a pounce from - most often behind, where the player can't see it - so there is
    /// a moment to turn and face it or get out of the way.
    /// </summary>
    private void DrawPounceWarnings(IHud hud, CEngine.Core.Camera camera)
    {
        if (_window is not { } window)
        {
            return;
        }

        var front = new Vector3D<float>(camera.Front.X, 0f, camera.Front.Z);
        if (front.LengthSquared < 1e-6f)
        {
            return;
        }

        front = Vector3D.Normalize(front);
        var right = new Vector3D<float>(-front.Z, 0f, front.X);
        foreach (var enemy in _enemies.Enemies)
        {
            if (!enemy.IsAlive || enemy.Kind.Behaviour != EnemyBehaviour.Stalk || enemy.Attack is null || enemy.AttackPhase != AttackPhase.WindUp)
            {
                continue;
            }

            var toward = enemy.Position - window.PlayerFeet;
            float angle = MathF.Atan2(Vector3D.Dot(toward, right), Vector3D.Dot(toward, front));   // 0 ahead, +/- pi behind, positive to the right
            float pulse = 0.6f + 0.4f * MathF.Abs(MathF.Sin(enemy.PhaseTime * 18f));
            var at = new Vector2D<float>(MathF.Sin(angle) * 150f, -MathF.Cos(angle) * 150f);
            var size = hud.MeasureText("!", 2f);
            hud.Text(HudAnchor.Center, at - size / 2f, "!", new Vector4D<float>(1f, 0.2f, 0.15f, pulse), 2f);
        }
    }

    /// <summary>A Magic enemy's blue, a Rare's yellow, a Legendary's orange.</summary>
    private static Vector4D<float> MonsterRarityColor(MonsterRarity rarity) => rarity switch
    {
        MonsterRarity.Magic => new Vector4D<float>(0.45f, 0.65f, 1f, 1f),
        MonsterRarity.Rare => new Vector4D<float>(1f, 0.9f, 0.35f, 1f),
        MonsterRarity.Legendary => new Vector4D<float>(1f, 0.58f, 0.12f, 1f),
        _ => new Vector4D<float>(1f, 1f, 1f, 1f),
    };

    /// <summary>The Shift move's charge, bottom centre: fills back up after each use.</summary>
    private void DrawDash(IHud hud)
    {
        float ready = Math.Clamp(_hero.DashReadiness, 0f, 1f);
        var fill = ready >= 1f ? new Vector4D<float>(0.55f, 0.85f, 0.45f, 0.95f) : new Vector4D<float>(0.45f, 0.55f, 0.45f, 0.8f);
        hud.Bar(HudAnchor.BottomCenter, new Vector2D<float>(0f, -40f), new Vector2D<float>(160f, 8f), ready, fill, Shade);
        hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -54f), $"{_hero.DashLabel}  [Shift]", new Vector4D<float>(1f, 1f, 1f, ready >= 1f ? 0.85f : 0.45f), 0.7f);
    }

    private static Vector4D<float> RarityColor(ItemRarity rarity, float alpha) => rarity switch
    {
        ItemRarity.Rare => new Vector4D<float>(0.45f, 0.65f, 1f, alpha),
        ItemRarity.Epic => new Vector4D<float>(0.78f, 0.45f, 1f, alpha),
        ItemRarity.Legendary => new Vector4D<float>(1f, 0.72f, 0.2f, alpha),
        _ => new Vector4D<float>(0.92f, 0.92f, 0.9f, alpha),
    };
}
