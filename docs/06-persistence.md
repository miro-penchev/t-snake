# Detailed Plan 06 — Persistence (High Scores)

The sixth per-module plan, and the smallest. Persistence stores the high-score table to disk and answers a couple of questions about it. It consumes the `GameOutcome` the loop already returns (plan 04) and shares the `%APPDATA%\t-snake\` folder with Settings (plan 05).

It lives in the console app (`src/TSnake/Persistence/`) and references the app's `Difficulty` enum (from Settings) for entry data. It does **no** drawing and **no** prompting.

---

## 1. What persistence owns, and what it does not

**Owns:** the high-score table — loading it (empty on missing/corrupt), inserting a result with sorting and trimming, querying whether a score qualifies and at what rank, and saving safely.

**Does NOT own:** computing the score (the engine does that; persistence stores the final number), deciding when a game ends (the loop), prompting for the player's name or *displaying* the table (Screens plan), and assembling the entry from game state — composition hands persistence a finished entry.

---

## 2. The core design decisions

### 2.1 One shared table, difficulty stored per entry

A single global table, sorted by raw score, with each entry recording its difficulty and level. This is deliberate, not lazy: the whole reason the engine applies difficulty and level **multipliers** is so harder play earns a higher number on a *shared* leaderboard. Splitting into per-difficulty boards would make those multipliers meaningless. Difficulty and level are still stored on every entry, so the Screens plan can offer a filtered view (e.g. "Pro only") without persistence needing to change.

### 2.2 Insert → sort → trim, with a fixed cap

`Add` inserts the entry, sorts by **score descending**, breaks ties by **earlier date first** ("first to reach it" wins), and trims to a fixed `MaxEntries` (10). It returns the new entry's **rank** (1-based), or a sentinel when the score didn't make the cut — so the results screen can highlight the fresh entry or say "no place this time."

`Qualifies(score)` answers the same cut question *before* an entry exists, so the results screen can decide whether to prompt for a name at all: a score qualifies if the table isn't full **or** it beats the current lowest.

### 2.3 Never crash; never lose the table to a half-write

Two robustness rules, mirroring Settings:

- **Load is total.** Missing file → empty table. Corrupt JSON → empty table (no throw). Individual invalid entries (negative score, null/blank name, level out of range) are dropped rather than failing the whole load.
- **Save is atomic.** Write to a temp file, then atomically replace the real file (`File.Replace`/move). A crash mid-write can't leave a truncated, unreadable high-score file — players care about these, so a half-write must never destroy the existing table.

### 2.4 Composition assembles the entry

Persistence stays decoupled from game internals: it doesn't read `GameOutcome` or `Preferences` itself. Composition builds the entry from the score (from `GameOutcome`), the name/difficulty/level (from `Preferences`), and the current timestamp, then hands it over. The player name is lightly **sanitized** on the way in — length-capped and stripped of control characters/newlines — so a stray value can't corrupt the file or break the renderer's grid.

---

## 3. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `HighScoreEntry` | record | `Name`, `Score`, `Difficulty`, `Level`, `Date` (`DateTimeOffset`) |
| `HighScoreTable` | class | the sorted, capped collection; `LoadOrEmpty`, `Save`, `Qualifies`, `Add` |

Entry fields are exactly the spec's five (name, score, difficulty, level, date). Extras like `EndReason` or run duration are easy to add later if the results screen wants them, but they're out for now.

---

## 4. File location & format

- **Path:** `%APPDATA%\t-snake\highscores.json` (same folder as `settings.json`); folder auto-created.
- **Serialization:** `System.Text.Json`; `Difficulty` written as a string, `DateTimeOffset` in round-trippable ISO form, so the file is readable.
- **Injectable path:** `LoadOrEmpty(path)` / `Save(path)` default to the AppData location but accept an override, so tests use a temp directory. No hidden global state.
- **Single-process assumption:** a local single-player game, so no file locking or multi-writer concurrency handling — out of scope.

---

## 5. Public surface

```csharp
public sealed record HighScoreEntry(
    string Name, int Score, Difficulty Difficulty, int Level, DateTimeOffset Date);

public sealed class HighScoreTable
{
    public const int MaxEntries = 10;
    public IReadOnlyList<HighScoreEntry> Entries { get; }   // sorted desc, capped

    public static HighScoreTable LoadOrEmpty(string? path = null);
    public void Save(string? path = null);

    public bool Qualifies(int score);     // would this score place?
    public int  Add(HighScoreEntry entry); // insert+sort+trim; returns 1-based rank, or -1 if it didn't place
}
```

The results-screen flow this surface is built for (the *UI* is the Screens plan):

```
var table = HighScoreTable.LoadOrEmpty();
if (table.Qualifies(outcome.Score))
{
    var name  = /* prompt — Screens */;
    var entry = new HighScoreEntry(Sanitize(name), outcome.Score,
                                   prefs.Difficulty, prefs.Level, DateTimeOffset.Now);
    int rank  = table.Add(entry);
    table.Save();
    /* show table with rank highlighted — Screens */
}
```

---

## 6. Testing plan

Pure and fully testable with a temp path — no Console:

- **Ordering** — `Add` keeps the list sorted by score desc; equal scores break by earlier date first.
- **Cap** — adding beyond `MaxEntries` drops the lowest; the table never exceeds the cap.
- **`Qualifies`** — true when the table isn't full or the score beats the lowest; false otherwise; consistent with what `Add` actually does.
- **Rank** — `Add` returns the correct 1-based rank, and the sentinel when the score doesn't place.
- **Load resilience** — missing file → empty; corrupt JSON → empty (no throw); invalid entries dropped, valid ones kept.
- **Round-trip** — add several, `Save`, `LoadOrEmpty` → identical entries in identical order.
- **Sanitization** — over-long names are capped; control characters/newlines stripped.

What stays manual: confirming scores actually survive across real launches and land in the AppData file.

---

## 7. Definition of done

- High scores persist across runs in `%APPDATA%\t-snake\highscores.json`.
- A qualifying score added after a game is present on the next launch; a non-qualifying one isn't.
- A missing or corrupt file yields an empty table with no crash, and a mid-write crash can't destroy the existing table (atomic save).
- All §6 pure tests pass.

---

## Decisions — confirmed

1. ✅ **Single shared table**, difficulty/level stored per entry (multipliers exist to share one board). Per-difficulty *display* filtering left to Screens; separate boards rejected.
2. ✅ **`MaxEntries` = 10.**
3. ✅ **Sort = score desc, ties broken by earlier date first.**
4. ✅ **`%APPDATA%\t-snake\highscores.json`**, `System.Text.Json`, injectable path, folder auto-created, **atomic write** (temp + replace).
5. ✅ **Load never throws** — empty on missing/corrupt, invalid entries dropped.
6. ✅ **Surface = `Qualifies` + `Add`(returns rank)**; composition assembles the `HighScoreEntry`; name sanitized on entry.
7. ✅ **Entry fields = name, score, difficulty, level, date**; extras (EndReason, duration) deferred.
8. ✅ **Scope:** storage + query only; the name-entry prompt and table display are the Screens plan.

Once settled, the session is: define `HighScoreEntry` and `HighScoreTable` (`LoadOrEmpty`/`Save`/`Qualifies`/`Add`) with atomic writes and resilient loads; write the pure tests; then have composition save a qualifying outcome and confirm it survives a relaunch.
