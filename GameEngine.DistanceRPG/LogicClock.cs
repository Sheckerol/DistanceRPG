namespace GameEngine.DistanceRPG;

/// <summary>
/// The scene's fixed logic clock (Phase 5.8, 1c): how many whole logic steps a
/// frame's time buys. The turn system is stepped only in whole steps of one
/// length (<see cref="Logic.SaveGame.LogicStepSeconds"/>, 1/120 s), never by a
/// frame's own variable time, so where an enemy's walk stops, where a brace
/// fires and how many d20s the enemies' turn draws are the same at 30 frames a
/// second as at 240 — and the same as a quit's settle, which steps the same
/// step (<see cref="Logic.SaveGame.SettleToSafePoint"/>). What a frame leaves
/// over, less than one step, waits for the next frame; nothing is interpolated.
/// <para>
/// A frame never takes more than <see cref="MaxStepsPerFrame"/> steps. A longer
/// one — a stall, a window dragged, a debugger's pause — drops the whole steps
/// past the cap: the enemies' turn then runs slower on the wall clock, never
/// differently, since what happens is a function of the steps taken and not of
/// when they were taken. Engine-free, so it is tested with no window.
/// </para>
/// </summary>
internal static class LogicClock
{
    /// <summary>The most steps one frame takes: 24 of 1/120 s is a fifth of a second, a 5-frames-a-second stall's worth. Past it the rest of the frame is dropped.</summary>
    public const int MaxStepsPerFrame = 24;

    /// <summary>
    /// The whole steps of <paramref name="step"/> seconds a frame of
    /// <paramref name="deltaTime"/> seconds takes, with what the frames before it
    /// left over in <paramref name="accumulator"/>: at most
    /// <see cref="MaxStepsPerFrame"/>, and <paramref name="accumulator"/> left
    /// holding the remainder — always less than one step. A frame of no time (or
    /// of none that can be read: negative, not a number, or infinite) takes none
    /// and leaves the remainder as it was.
    /// </summary>
    /// <param name="accumulator">The time carried from the frames before, in seconds, under one step; this frame's remainder on return.</param>
    /// <param name="deltaTime">The frame's time, in seconds.</param>
    /// <param name="step">The fixed step, in seconds.</param>
    /// <returns>How many steps to take this frame, from 0 to <see cref="MaxStepsPerFrame"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is not a positive number of seconds.</exception>
    public static int Steps(ref float accumulator, float deltaTime, float step)
    {
        if (!(step > 0f) || float.IsInfinity(step))
            throw new ArgumentOutOfRangeException(nameof(step), step, "A logic step is a positive number of seconds.");
        // No time passed, or none that means anything: nothing to step. An
        // infinite frame is refused too. Added to the remainder it would leave
        // Infinity % step, which is NaN, and a NaN remainder never reaches a step
        // again, so the enemies' turn would stand still for good.
        if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
            return 0;

        accumulator += deltaTime;
        int steps = 0;
        while (accumulator >= step)
        {
            accumulator -= step;
            if (++steps < MaxStepsPerFrame)
                continue;
            // The cap: a stalled frame's whole steps past it are dropped rather
            // than owed, so a stall is never followed by a burst. Only the
            // part of a step it had over is kept, as any frame's is.
            accumulator %= step;
            break;
        }
        return steps;
    }
}
