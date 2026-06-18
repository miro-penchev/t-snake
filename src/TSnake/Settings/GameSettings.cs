using System.Text.Json;
using System.Text.Json.Serialization;
using TSnake.Core;
using TSnake.Loop;

namespace TSnake.Settings;

/// <summary>
/// The config-translation layer (plan 05). It holds a complete tuning table and the player's
/// <see cref="Preferences"/>, merges a chosen (difficulty, level) into a runnable
/// <see cref="SessionConfig"/>, and loads/saves preferences to a JSON file that <b>never</b>
/// crashes the game: defaults live in code, the file only overlays, and bad input degrades
/// field-by-field back to the default.
/// </summary>
public sealed class GameSettings
{
    private readonly IReadOnlyDictionary<Difficulty, DifficultyProfile> _difficulties;
    private readonly IReadOnlyDictionary<int, LevelProfile> _levels;

    private GameSettings(
        Preferences preferences,
        IReadOnlyDictionary<Difficulty, DifficultyProfile> difficulties,
        IReadOnlyDictionary<int, LevelProfile> levels)
    {
        Preferences = preferences;
        _difficulties = difficulties;
        _levels = levels;
        AvailableLevels = levels.Keys.OrderBy(k => k).ToArray();
    }

    /// <summary>
    /// The persisted preferences. The menu (plan 07) replaces this whole record as the player cycles
    /// values, then calls <see cref="Save"/> on the way into a game.
    /// </summary>
    public Preferences Preferences { get; set; }

    /// <summary>The level numbers the player may pick, ascending — the keys of the tuning table.</summary>
    public IReadOnlyList<int> AvailableLevels { get; }

    /// <summary>A fresh <see cref="GameSettings"/> on the baked-in defaults, touching no file.</summary>
    public static GameSettings CreateDefault() =>
        new(DefaultPreferences, DefaultDifficulties(), DefaultLevels());

    /// <summary>
    /// Merges what difficulty governs with what level governs (plan §2.1) into the three records
    /// composition needs. The score multiplier is difficulty × level; the start position is the
    /// board centre; the seed is passed in per run (decision #5), never stored.
    /// </summary>
    public SessionConfig BuildSession(Difficulty difficulty, int level, int seed)
    {
        DifficultyProfile d = _difficulties[difficulty];
        LevelProfile l = _levels[level];

        var engine = new GameConfig(
            Width: l.Width,
            Height: l.Height,
            BasePointsPerFood: l.BasePointsPerFood,
            GrowthPerFood: l.GrowthPerFood,
            DifficultyMultiplier: d.Multiplier,
            LevelMultiplier: l.Multiplier,
            ObstaclesEnabled: d.ObstaclesEnabled,
            ObstacleSpawnRate: d.ObstacleSpawnRate,
            ObstacleLifetimeTicks: d.ObstacleLifetimeTicks,
            MaxObstacles: d.MaxObstacles,
            InitialSnakeLength: l.InitialSnakeLength,
            StartPosition: new Point(l.Width / 2, l.Height / 2),
            Seed: seed);

        var speed = new SpeedProfile(l.BaseIntervalMs, l.MinIntervalMs, d.ShrinkRate, SpeedDriver.FoodEaten);

        return new SessionConfig(engine, speed, d.PauseAllowed);
    }

    // ---- Load / Save -------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="path"/> (defaulting to the AppData location), overlays it onto the
    /// code defaults, and validates field-by-field. Any failure — a missing file, malformed JSON,
    /// or an out-of-range value — degrades silently to the default. On first run (no file) the
    /// defaults are written back so the file is discoverable and editable.
    /// </summary>
    public static GameSettings LoadOrDefault(string? path = null)
    {
        path ??= DefaultPath;
        GameSettings defaults = CreateDefault();

        if (!File.Exists(path))
        {
            TrySave(defaults, path);
            return defaults;
        }

        try
        {
            SettingsFileDto? file = JsonSerializer.Deserialize<SettingsFileDto>(File.ReadAllText(path), ReadOptions);
            if (file is null)
            {
                return defaults;
            }

            Dictionary<Difficulty, DifficultyProfile> difficulties = OverlayDifficulties(file.Difficulties);
            Dictionary<int, LevelProfile> levels = OverlayLevels(file.Levels);
            Preferences preferences = OverlayPreferences(file.Preferences, levels);

            return new GameSettings(preferences, difficulties, levels);
        }
        catch
        {
            // Malformed JSON, an unknown enum string, an I/O error — anything. The game must run.
            return defaults;
        }
    }

    /// <summary>
    /// Persists preferences (plan §2.3) to <paramref name="path"/>, creating the folder if missing.
    /// Tuning sections are written only when they differ from the code defaults, so a normal save
    /// stays a small preferences block while a tinkerer's overrides are preserved.
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var file = new SettingsFileOut
        {
            Preferences = Preferences,
            Difficulties = DifficultiesAreDefault() ? null : new Dictionary<Difficulty, DifficultyProfile>(_difficulties),
            Levels = LevelsAreDefault() ? null : new Dictionary<int, LevelProfile>(_levels),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(file, WriteOptions));
    }

    private static void TrySave(GameSettings settings, string path)
    {
        try
        {
            settings.Save(path);
        }
        catch
        {
            // First-run convenience only; never fatal if the folder is read-only.
        }
    }

    // ---- Defaults ----------------------------------------------------------

    private static readonly Preferences DefaultPreferences =
        new(RenderMode.UnicodeColor, Difficulty.Easy, Level: 1, PlayerName: "Player");

    // Easy: calm and pausable, no obstacles, constant speed. Pro and Terminator turn obstacles on
    // and ramp the speed, with no pause on the hardest tier (plan §2.1 / DoD §7).
    private static Dictionary<Difficulty, DifficultyProfile> DefaultDifficulties() => new()
    {
        [Difficulty.Easy] = new DifficultyProfile(
            Multiplier: 1, PauseAllowed: true, ObstaclesEnabled: false,
            ObstacleSpawnRate: 0, ObstacleLifetimeTicks: 0, MaxObstacles: 0, ShrinkRate: 0.0),
        [Difficulty.Pro] = new DifficultyProfile(
            Multiplier: 2, PauseAllowed: false, ObstaclesEnabled: true,
            ObstacleSpawnRate: 5, ObstacleLifetimeTicks: 40, MaxObstacles: 4, ShrinkRate: 0.02),
        [Difficulty.Terminator] = new DifficultyProfile(
            Multiplier: 3, PauseAllowed: false, ObstaclesEnabled: true,
            ObstacleSpawnRate: 10, ObstacleLifetimeTicks: 60, MaxObstacles: 8, ShrinkRate: 0.05),
    };

    // Levels grow the board, quicken the base pace, and lengthen the starting snake.
    private static Dictionary<int, LevelProfile> DefaultLevels() => new()
    {
        [1] = new LevelProfile(Width: 24, Height: 16, BaseIntervalMs: 150, MinIntervalMs: 70,
            InitialSnakeLength: 4, BasePointsPerFood: 10, GrowthPerFood: 1, Multiplier: 1),
        [2] = new LevelProfile(Width: 32, Height: 20, BaseIntervalMs: 130, MinIntervalMs: 60,
            InitialSnakeLength: 5, BasePointsPerFood: 10, GrowthPerFood: 1, Multiplier: 2),
        [3] = new LevelProfile(Width: 40, Height: 24, BaseIntervalMs: 110, MinIntervalMs: 50,
            InitialSnakeLength: 6, BasePointsPerFood: 10, GrowthPerFood: 1, Multiplier: 3),
    };

    // ---- Overlay + validation ----------------------------------------------

    private static Preferences OverlayPreferences(PreferencesDto? dto, IReadOnlyDictionary<int, LevelProfile> levels)
    {
        Preferences def = DefaultPreferences;
        if (dto is null)
        {
            return def;
        }

        RenderMode renderMode = dto.RenderMode is { } rm && Enum.IsDefined(rm) ? rm : def.RenderMode;
        Difficulty difficulty = dto.Difficulty is { } d && Enum.IsDefined(d) ? d : def.Difficulty;
        int level = dto.Level is { } lvl && levels.ContainsKey(lvl) ? lvl : def.Level;
        string name = dto.PlayerName is { Length: > 0 } n ? Truncate(n.Trim(), 24) : def.PlayerName;
        if (name.Length == 0)
        {
            name = def.PlayerName;
        }

        return new Preferences(renderMode, difficulty, level, name);
    }

    private static Dictionary<Difficulty, DifficultyProfile> OverlayDifficulties(
        Dictionary<Difficulty, DifficultyProfileDto>? dtos)
    {
        Dictionary<Difficulty, DifficultyProfile> result = DefaultDifficulties();
        if (dtos is null)
        {
            return result;
        }

        foreach ((Difficulty key, DifficultyProfileDto dto) in dtos)
        {
            if (result.TryGetValue(key, out DifficultyProfile? def))
            {
                result[key] = OverlayDifficulty(def, dto);
            }
        }

        return result;
    }

    private static DifficultyProfile OverlayDifficulty(DifficultyProfile def, DifficultyProfileDto dto) => def with
    {
        Multiplier = Positive(dto.Multiplier) ?? def.Multiplier,
        PauseAllowed = dto.PauseAllowed ?? def.PauseAllowed,
        ObstaclesEnabled = dto.ObstaclesEnabled ?? def.ObstaclesEnabled,
        ObstacleSpawnRate = Percent(dto.ObstacleSpawnRate) ?? def.ObstacleSpawnRate,
        ObstacleLifetimeTicks = NonNegative(dto.ObstacleLifetimeTicks) ?? def.ObstacleLifetimeTicks,
        MaxObstacles = NonNegative(dto.MaxObstacles) ?? def.MaxObstacles,
        ShrinkRate = Shrink(dto.ShrinkRate) ?? def.ShrinkRate,
    };

    private static Dictionary<int, LevelProfile> OverlayLevels(Dictionary<int, LevelProfileDto>? dtos)
    {
        Dictionary<int, LevelProfile> result = DefaultLevels();
        if (dtos is null)
        {
            return result;
        }

        foreach ((int key, LevelProfileDto dto) in dtos)
        {
            if (result.TryGetValue(key, out LevelProfile? def))
            {
                result[key] = OverlayLevel(def, dto);
            }
        }

        return result;
    }

    private static LevelProfile OverlayLevel(LevelProfile def, LevelProfileDto dto)
    {
        LevelProfile merged = def with
        {
            Width = Positive(dto.Width) ?? def.Width,
            Height = Positive(dto.Height) ?? def.Height,
            BaseIntervalMs = Positive(dto.BaseIntervalMs) ?? def.BaseIntervalMs,
            MinIntervalMs = Positive(dto.MinIntervalMs) ?? def.MinIntervalMs,
            InitialSnakeLength = Positive(dto.InitialSnakeLength) ?? def.InitialSnakeLength,
            BasePointsPerFood = NonNegative(dto.BasePointsPerFood) ?? def.BasePointsPerFood,
            GrowthPerFood = Positive(dto.GrowthPerFood) ?? def.GrowthPerFood,
            Multiplier = Positive(dto.Multiplier) ?? def.Multiplier,
        };

        // Cross-field rules: a speed floor above the base is nonsense, and the snake must fit the
        // board. When violated, reject the whole offending pair back to the default (plan §2.2).
        if (merged.MinIntervalMs > merged.BaseIntervalMs)
        {
            merged = merged with { BaseIntervalMs = def.BaseIntervalMs, MinIntervalMs = def.MinIntervalMs };
        }

        if (merged.InitialSnakeLength >= merged.Width || merged.Height < 1)
        {
            merged = merged with { Width = def.Width, Height = def.Height, InitialSnakeLength = def.InitialSnakeLength };
        }

        return merged;
    }

    private static int? Positive(int? value) => value is > 0 ? value : null;
    private static int? NonNegative(int? value) => value is >= 0 ? value : null;
    private static int? Percent(int? value) => value is >= 0 and <= 100 ? value : null;
    private static double? Shrink(double? value) => value is >= 0.0 and < 1.0 ? value : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private bool DifficultiesAreDefault() => DictionaryEquals(_difficulties, DefaultDifficulties());
    private bool LevelsAreDefault() => DictionaryEquals(_levels, DefaultLevels());

    private static bool DictionaryEquals<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b)
        where TKey : notnull
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach ((TKey key, TValue value) in a)
        {
            if (!b.TryGetValue(key, out TValue? other) || !Equals(value, other))
            {
                return false;
            }
        }

        return true;
    }

    // ---- File location & JSON shape ----------------------------------------

    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "t-snake",
        "settings.json");

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // Read DTOs use nullable members so an absent field is distinguishable from a zero and can fall
    // back to the default, rather than overwriting it (plan §2.2 overlay semantics).
    private sealed class SettingsFileDto
    {
        public PreferencesDto? Preferences { get; set; }
        public Dictionary<Difficulty, DifficultyProfileDto>? Difficulties { get; set; }
        public Dictionary<int, LevelProfileDto>? Levels { get; set; }
    }

    private sealed class PreferencesDto
    {
        public RenderMode? RenderMode { get; set; }
        public Difficulty? Difficulty { get; set; }
        public int? Level { get; set; }
        public string? PlayerName { get; set; }
    }

    private sealed class DifficultyProfileDto
    {
        public int? Multiplier { get; set; }
        public bool? PauseAllowed { get; set; }
        public bool? ObstaclesEnabled { get; set; }
        public int? ObstacleSpawnRate { get; set; }
        public int? ObstacleLifetimeTicks { get; set; }
        public int? MaxObstacles { get; set; }
        public double? ShrinkRate { get; set; }
    }

    private sealed class LevelProfileDto
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? BaseIntervalMs { get; set; }
        public int? MinIntervalMs { get; set; }
        public int? InitialSnakeLength { get; set; }
        public int? BasePointsPerFood { get; set; }
        public int? GrowthPerFood { get; set; }
        public int? Multiplier { get; set; }
    }

    // The write shape serializes the real records directly; null tuning sections are omitted.
    private sealed class SettingsFileOut
    {
        public Preferences Preferences { get; set; } = null!;
        public Dictionary<Difficulty, DifficultyProfile>? Difficulties { get; set; }
        public Dictionary<int, LevelProfile>? Levels { get; set; }
    }
}
