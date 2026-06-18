namespace TSnake.Loop;

/// <summary>
/// What the speed-up curve is a function of. <see cref="FoodEaten"/> is the chosen driver
/// (plan decision #2): the interval shrinks as the snake grows, so the speed you face is the
/// speed you earned. Kept as an enum purely as a future seam — it is not a branch we need now.
/// </summary>
public enum SpeedDriver
{
    FoodEaten,
}
