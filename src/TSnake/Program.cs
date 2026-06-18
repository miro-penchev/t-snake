// Composition root for plan 07 (screens & composition). Main is deliberately thin: it constructs the
// shared, long-lived services and wraps the entire run in a single TerminalSession so the terminal is
// restored on *every* exit — normal, quit, or exception. Everything else — the menu, the game
// sessions, the results, the high-score view — is sequenced by the AppController state machine.
//
//   Menu : ↑↓ move · ←→ change a value · Enter select        In game : WASD/arrows · Space pause · Esc quit
using TSnake.Input;
using TSnake.Persistence;
using TSnake.Rendering;
using TSnake.Screens;
using TSnake.Settings;

using var term = new TerminalSession();           // restores the terminal on any exit path
using var reader = new KeyboardReader(new ConsoleKeySource());

GameSettings settings = GameSettings.LoadOrDefault();
HighScoreTable scores = HighScoreTable.LoadOrEmpty();

new AppController(term, reader, settings, scores).Run();
