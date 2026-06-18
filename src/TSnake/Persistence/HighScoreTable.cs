using System.Text.Json;
using System.Text.Json.Serialization;
using TSnake.Settings;

namespace TSnake.Persistence;

/// <summary>
/// The high-score table (plan 06): a single shared leaderboard sorted by raw score, capped at
/// <see cref="MaxEntries"/>, persisted to JSON. It owns loading (empty on missing/corrupt),
/// inserting with sort + trim, querying whether a score places, and saving atomically. It does
/// no drawing and no prompting — assembling the entry and showing the table are the Screens plan.
/// </summary>
public sealed class HighScoreTable
{
    /// <summary>Fixed cap on stored entries (plan decision #2).</summary>
    public const int MaxEntries = 10;

    /// <summary>Returned by <see cref="Add"/> when the score did not make the cut.</summary>
    public const int NoPlace = -1;

    private readonly List<HighScoreEntry> _entries;

    /// <summary>Creates an empty table.</summary>
    public HighScoreTable() => _entries = new List<HighScoreEntry>();

    private HighScoreTable(IEnumerable<HighScoreEntry> entries) =>
        _entries = Sort(entries).Take(MaxEntries).ToList();

    /// <summary>The entries, sorted by score descending (ties broken by earlier date), capped.</summary>
    public IReadOnlyList<HighScoreEntry> Entries => _entries;

    /// <summary>
    /// Would <paramref name="score"/> place? True when the table isn't full, or the score beats the
    /// current lowest (plan §2.2). Strictly-greater matches what <see cref="Add"/> does: a score
    /// equal to the lowest sorts after it (later date) and would be trimmed, so it does not place.
    /// </summary>
    public bool Qualifies(int score) =>
        _entries.Count < MaxEntries || score > _entries[^1].Score;

    /// <summary>
    /// Inserts <paramref name="entry"/>, re-sorts (score desc, earlier date first), and trims to
    /// <see cref="MaxEntries"/>. Returns the entry's 1-based rank, or <see cref="NoPlace"/> when it
    /// fell outside the cap.
    /// </summary>
    public int Add(HighScoreEntry entry)
    {
        List<HighScoreEntry> sorted = Sort(_entries.Append(entry)).ToList();

        // Reference identity, not value equality: an entry value-equal to an existing row must
        // still report its own position rather than the other's.
        int rank = sorted.FindIndex(e => ReferenceEquals(e, entry)) + 1;

        if (sorted.Count > MaxEntries)
        {
            sorted.RemoveRange(MaxEntries, sorted.Count - MaxEntries);
        }

        _entries.Clear();
        _entries.AddRange(sorted);

        return rank <= MaxEntries ? rank : NoPlace;
    }

    // Score descending; ties broken by earlier date first ("first to reach it" wins, plan §2.2).
    private static IEnumerable<HighScoreEntry> Sort(IEnumerable<HighScoreEntry> entries) =>
        entries.OrderByDescending(e => e.Score).ThenBy(e => e.Date);

    // ---- Load / Save -------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="path"/> (defaulting to the AppData location). Load is total: a missing
    /// file or corrupt JSON yields an empty table without throwing, and individual invalid entries
    /// (negative score, null/blank name, level below 1, unknown difficulty) are dropped while the
    /// valid ones are kept (plan §2.3).
    /// </summary>
    public static HighScoreTable LoadOrEmpty(string? path = null)
    {
        path ??= DefaultPath;

        if (!File.Exists(path))
        {
            return new HighScoreTable();
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new HighScoreTable();
            }

            var entries = new List<HighScoreEntry>();
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
            {
                if (TryReadEntry(element) is { } entry)
                {
                    entries.Add(entry);
                }
            }

            return new HighScoreTable(entries);
        }
        catch
        {
            // Malformed JSON, an I/O error — anything. A high-score file must never crash the game.
            return new HighScoreTable();
        }
    }

    /// <summary>
    /// Persists the table to <paramref name="path"/> (folder auto-created). The write is atomic
    /// (plan §2.3): the JSON goes to a temp file first, then atomically replaces the real file, so a
    /// crash mid-write can't truncate or destroy the existing scores.
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_entries, WriteOptions));

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    // Reads one array element defensively: a single bad row (e.g. an unknown difficulty string that
    // throws) returns null and is dropped, never failing the whole load.
    private static HighScoreEntry? TryReadEntry(JsonElement element)
    {
        try
        {
            EntryDto? dto = element.Deserialize<EntryDto>(ReadOptions);
            if (dto is null)
            {
                return null;
            }

            if (dto.Score is not { } score || score < 0)
            {
                return null;
            }

            if (dto.Difficulty is not { } difficulty || !Enum.IsDefined(difficulty))
            {
                return null;
            }

            if (dto.Level is not { } level || level < 1)
            {
                return null;
            }

            string name = HighScoreEntry.Sanitize(dto.Name);
            if (name.Length == 0)
            {
                return null;
            }

            return new HighScoreEntry(name, score, difficulty, level, dto.Date ?? DateTimeOffset.MinValue);
        }
        catch
        {
            return null;
        }
    }

    // ---- File location & JSON shape ----------------------------------------

    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "t-snake",
        "highscores.json");

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Nullable members so an absent or wrongly-typed field reads as null and the entry is dropped,
    // rather than silently defaulting to a bogus value.
    private sealed class EntryDto
    {
        public string? Name { get; set; }
        public int? Score { get; set; }
        public Difficulty? Difficulty { get; set; }
        public int? Level { get; set; }
        public DateTimeOffset? Date { get; set; }
    }
}
