using System.Text.Json;

namespace ArenaMaster.Game.Progression;

/// <summary>One passive tree's saved progress: all the experience it has ever earned (its level follows from that) and the ranks spent in it.</summary>
internal sealed class TreeSave
{
    public long Experience { get; set; }

    public Dictionary<string, int> Ranks { get; set; } = new();
}

/// <summary>
/// Everything that lasts between runs: the items in the stash and how many of each, the loadout last taken on a run, which class and tree are chosen, and every
/// tree's progress. Saved as JSON by <see cref="ProfileStore"/>.
/// </summary>
internal sealed class Profile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Item id -> how many the player owns.</summary>
    public Dictionary<string, int> Stash { get; set; } = new();

    /// <summary>The item ids chosen to bring on the last run, remembered for the next one.</summary>
    public List<string> Loadout { get; set; } = new();

    public string ActiveClass { get; set; } = "ranger";

    public string ActiveTree { get; set; } = "sharpshooter";

    /// <summary>"class/tree" -> that tree's progress.</summary>
    public Dictionary<string, TreeSave> Trees { get; set; } = new();

    /// <summary>Silver: the currency earned at the end of every run and spent at the Quartermaster.</summary>
    public long Silver { get; set; }

    /// <summary>Shop upgrade id -> ranks bought (see <see cref="Shop"/>).</summary>
    public Dictionary<string, int> Shop { get; set; } = new();

    /// <summary>The ids of the bounties completed so far (see <see cref="Bounties"/>). Each pays out once.</summary>
    public List<string> Bounties { get; set; } = new();

    /// <summary>Lifetime totals the bounties read, kept across runs.</summary>
    public LifetimeRecord Lifetime { get; set; } = new();

    public int CountOf(string itemId) => Stash.GetValueOrDefault(itemId);

    public int ShopRank(string upgradeId) => Shop.GetValueOrDefault(upgradeId);

    public bool HasBounty(string bountyId) => Bounties.Contains(bountyId);

    public void AddToStash(string itemId, int count = 1) => Stash[itemId] = CountOf(itemId) + count;

    /// <summary>The progress of <paramref name="classId"/>'s tree <paramref name="treeId"/>, made empty the first time it is asked for.</summary>
    public TreeSave Tree(string classId, string treeId)
    {
        string key = $"{classId}/{treeId}";
        if (!Trees.TryGetValue(key, out var save))
        {
            save = new TreeSave();
            Trees[key] = save;
        }

        return save;
    }
}

/// <summary>
/// Reads and writes the <see cref="Profile"/>. It lives in the user's application data (<c>ArenaMaster/profile.json</c>), not the repo, so every player has their own.
/// A missing file is a new player; a file that can't be read is set aside as <c>profile.json.bad</c> rather than overwritten, and a new profile starts.
/// </summary>
internal static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArenaMaster", "profile.json");

    public static Profile Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Profile();
        }

        try
        {
            var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Options);
            if (profile is not null)
            {
                return profile;
            }
        }
        catch (JsonException)
        {
        }

        File.Copy(path, path + ".bad", overwrite: true);
        return new Profile();
    }

    /// <summary>
    /// Copies the save at <paramref name="path"/> aside as <c>profile.backup-&lt;date-time&gt;.json</c> in the same folder, and returns the copy's path - or null if there
    /// is no save to copy. Nothing is ever deleted: a new game starts over, but the old one can still be put back by hand.
    /// </summary>
    public static string? Backup(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string directory = Path.GetDirectoryName(path) ?? ".";
        string name = Path.GetFileNameWithoutExtension(path);
        string backup = Path.Combine(directory, $"{name}.backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        for (int n = 2; File.Exists(backup); n++)
        {
            backup = Path.Combine(directory, $"{name}.backup-{DateTime.Now:yyyyMMdd-HHmmss}-{n}.json");
        }

        File.Copy(path, backup);
        return backup;
    }

    /// <summary>Writes the profile through a temp file, so a crash mid-write can't leave half a save.</summary>
    public static void Save(Profile profile, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, Options));
        File.Move(temp, path, overwrite: true);
    }
}
