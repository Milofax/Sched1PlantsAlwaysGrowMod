namespace PlantsAlwaysGrow;

// Pure logic, shared by the game build and the regression tests.
internal static class GrowthPolicy
{
    public static bool CanAdvance(bool loaded, bool host, bool endOfDay, bool sleeping, int clock) =>
        loaded && host && endOfDay && !sleeping && clock == 400;

    public static float NextProgress(float current, int minutes, float growthHours, float multiplier)
    {
        if (!float.IsFinite(current) || current < 0 || current >= 1 || minutes <= 0 ||
            !float.IsFinite(growthHours) || growthHours <= 0 ||
            !float.IsFinite(multiplier) || multiplier <= 0)
            return current;

        // Native Plant.MinPass in 0.4.6f13: minutes * 1/(hours*60), not 1/(hours*60*minutes).
        double increment = minutes / ((double)growthHours * 60) * multiplier;
        return (float)Math.Min(1, current + increment);
    }
}

internal sealed class MinuteClock
{
    private double elapsed;

    public void Reset() => elapsed = 0;

    public int Advance(bool allowed, float deltaSeconds, float minuteSeconds)
    {
        if (!allowed || !float.IsFinite(deltaSeconds) || deltaSeconds < 0 ||
            !float.IsFinite(minuteSeconds) || minuteSeconds <= 0)
        {
            Reset();
            return 0;
        }

        // Do not turn a suspended application, debugger break or large loading hitch into growth.
        if (deltaSeconds > 1)
        {
            Reset();
            return 0;
        }

        elapsed += deltaSeconds;
        if (elapsed < minuteSeconds) return 0;
        int minutes = (int)Math.Min(60, Math.Floor(elapsed / minuteSeconds));
        elapsed %= minuteSeconds;
        return minutes;
    }
}
