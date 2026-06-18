using TSnake.Core;
using TSnake.Loop;

namespace TSnake.Tests;

public class SpeedProfileTests
{
    private static SpeedProfile Profile(int @base = 100, int min = 50, double shrink = 0.1)
        => new(@base, min, shrink, SpeedDriver.FoodEaten);

    [Fact]
    public void StartsAtBaseWhenNoProgress()
    {
        Assert.Equal(100, Profile().IntervalAt(0));
    }

    [Fact]
    public void ShrinksMultiplicativelyWithProgress()
    {
        var profile = Profile(@base: 100, min: 1, shrink: 0.1);

        Assert.Equal(90, profile.IntervalAt(1));  // round(100 * 0.9)
        Assert.Equal(59, profile.IntervalAt(5));  // round(100 * 0.9^5 = 59.049)
    }

    [Fact]
    public void NeverDropsBelowTheFloor()
    {
        var profile = Profile(@base: 100, min: 50, shrink: 0.1);

        // 100 * 0.9^50 ≈ 0.5, well under the floor.
        Assert.Equal(50, profile.IntervalAt(50));
    }

    [Fact]
    public void IsClampedToTheFloorAtLargeProgress()
    {
        // Monotonic non-increasing, and pinned to the floor once there.
        var profile = Profile(@base: 100, min: 50, shrink: 0.1);
        Assert.True(profile.IntervalAt(100) == 50 && profile.IntervalAt(1000) == 50);
    }

    [Fact]
    public void ZeroShrinkHoldsAConstantSpeed()
    {
        // Easy: zero shrink rate -> constant at base regardless of progress.
        var easy = Profile(shrink: 0.0);

        Assert.Equal(100, easy.IntervalAt(0));
        Assert.Equal(100, easy.IntervalAt(1));
        Assert.Equal(100, easy.IntervalAt(50));
    }

    [Fact]
    public void IntervalForStartsAtBaseAndRampsWithFoodEaten()
    {
        // The GameState path uses FoodEaten as the driver: a fresh run (none eaten) is exactly the
        // base, and each food shrinks it one step — not pre-shrunk by the snake's starting length.
        var config = new GameConfig(
            Width: 5, Height: 1, BasePointsPerFood: 10, GrowthPerFood: 1,
            DifficultyMultiplier: 1, LevelMultiplier: 1, ObstaclesEnabled: false,
            ObstacleSpawnRate: 0, ObstacleLifetimeTicks: 0, MaxObstacles: 0,
            InitialSnakeLength: 1, StartPosition: new Point(0, 0), Seed: 0);

        // FakeRandom yields free-cell index 0; on a 5×1 board the snake eats by moving right.
        var engine = new GameEngine(config, new FakeRandom(0, 0, 0));
        var profile = Profile(@base: 100, min: 1, shrink: 0.1);

        Assert.Equal(0, engine.State.FoodEaten);
        Assert.Equal(profile.IntervalAt(0), profile.IntervalFor(engine.State)); // == base, 100

        engine.SetDirection(Direction.Right);
        engine.Tick(); // moves onto the food spawned at the first free cell -> eats one

        Assert.Equal(1, engine.State.FoodEaten);
        Assert.Equal(profile.IntervalAt(1), profile.IntervalFor(engine.State)); // shrunk one step
    }
}
