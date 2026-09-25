namespace ArenaMaster.Game.Combat;

/// <summary>
/// What one kind of enemy is: its model and its numbers. Distances are metres, speeds metres per second, times seconds. The body is a standing cylinder of
/// <see cref="Radius"/> and <see cref="Height"/> from its feet - what arrows hit and what keeps it off the player and off other enemies. <see cref="Experience"/> is what its gem is worth.
/// </summary>
internal sealed record EnemyKind(
    string Name,
    string Model,
    float MaxHealth,
    float Speed,
    float Radius,
    float Height,
    float ContactDamage,
    float ContactInterval,
    int Experience)
{
    /// <summary>The first enemy: slow, fragile fodder that shambles straight at the player and claws on contact.</summary>
    public static readonly EnemyKind Ghoul = new(
        Name: "Ghoul",
        Model: "ghoul_placeholder.glb",
        MaxHealth: 30f,
        Speed: 3.6f,
        Radius: 0.4f,
        Height: 1.45f,
        ContactDamage: 8f,
        ContactInterval: 0.8f,
        Experience: 1);
}
