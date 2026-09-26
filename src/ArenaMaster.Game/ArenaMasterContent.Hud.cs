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

        if (_nearStation is { } near)
        {
            hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -120f), $"[E]  {near.Prompt}", Gold, 1.1f);
        }
        else
        {
            hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -120f),
                "Chest and quartermaster on the right, tree target and bounty board on the left, class rack behind you, the gate ahead starts a run",
                new Vector4D<float>(1f, 1f, 1f, 0.6f), 0.8f);
        }

        DrawDash(hud);
    }

    private void DrawRunHud(IHud hud)
    {
        if (_window?.Camera is { } camera)
        {
            _numbers.Draw(hud, camera);
        }

        // Experience across the top, with the level at its left end and the tree under it; the run clock under its middle; kills at the right.
        float width = hud.ScreenSize.X - 48f;
        hud.Bar(HudAnchor.TopLeft, new Vector2D<float>(24f, 14f), new Vector2D<float>(width, 12f), _experience.Progress,
            new Vector4D<float>(0.35f, 0.8f, 0.95f, 0.95f), Shade);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 32f), $"LV {_experience.Level}", Gold, 1f);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 60f), $"{_tree.Tree.Name} Lv {_tree.Level}  +{_runTreeExperience:N0} xp", Teal, 0.65f);
        hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 32f), $"{Clock(_runSeconds)} / {Clock(RunDirector.RunLength)}", White, 1.1f);
        hud.Text(HudAnchor.TopRight, new Vector2D<float>(-26f, 32f), $"Kills  {_enemies.Kills}", White, 0.9f);

        // The boss's health, under the clock, while one is on the field.
        if (_enemies.Boss is { } boss)
        {
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 64f), boss.Kind.Name.ToUpperInvariant(), Red, 0.9f);
            hud.Bar(HudAnchor.TopCenter, new Vector2D<float>(0f, 88f), new Vector2D<float>(520f, 16f), boss.Health / boss.MaxHealth,
                new Vector4D<float>(0.75f, 0.15f, 0.2f, 0.95f), Shade);
        }

        // Health, bottom left, flashing red when hit.
        float health = _health.Current / _health.Max;
        var healthColor = Vector4D.Lerp(new Vector4D<float>(0.78f, 0.2f, 0.22f, 0.95f), new Vector4D<float>(1f, 0.55f, 0.55f, 1f), _health.HurtFlash);
        hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -28f), new Vector2D<float>(260f, 18f), health, healthColor, Shade);
        string barrier = "";
        if (_health.Barrier > 0f)
        {
            // A barrier over the health (the Mage's Frost Shield): an icy band along the bottom of the bar, as a share of max health.
            hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -28f), new Vector2D<float>(260f, 6f), Math.Clamp(_health.Barrier / _health.Max, 0f, 1f),
                new Vector4D<float>(0.6f, 0.9f, 1f, 0.95f), new Vector4D<float>(0f, 0f, 0f, 0f));
            barrier = $"   +{MathF.Ceiling(_health.Barrier)} shield";
        }

        hud.Text(HudAnchor.BottomLeft, new Vector2D<float>(28f, -50f), $"HP  {MathF.Ceiling(_health.Current)} / {_health.Max}{barrier}", White, 0.75f);
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
