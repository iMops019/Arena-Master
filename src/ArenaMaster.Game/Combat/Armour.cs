namespace ArenaMaster.Game.Combat;

/// <summary>
/// Armour: every blow that reaches the player is cut by armour / (armour + <see cref="Scale"/>) - 100 armour takes a third off, 200 half, 400 two thirds, and
/// it never reaches all of it, so it keeps up however hard the enemies hit. Every class has a Defense of its own, armour that grows with its passive tree's
/// level (<see cref="Defense"/>), so a class levelled up is sturdier in deeper Delves; body armour gear adds more (<c>ItemBonuses.Armour</c>).
/// </summary>
internal static class Armour
{
    /// <summary>The armour that halves every blow.</summary>
    public const float Scale = 200f;

    /// <summary>Armour for every level of the class's passive tree.</summary>
    public const float PerTreeLevel = 8f;

    /// <summary>A class's own armour at <paramref name="treeLevel"/>.</summary>
    public static float Defense(int treeLevel) => PerTreeLevel * Math.Max(0, treeLevel);

    /// <summary>What a blow's damage is multiplied by through <paramref name="armour"/> (1 with none).</summary>
    public static float Cut(float armour) => Scale / (Scale + MathF.Max(0f, armour));

    /// <summary>The share of each blow <paramref name="armour"/> takes off (0 to just under 1).</summary>
    public static float Reduction(float armour) => 1f - Cut(armour);
}
