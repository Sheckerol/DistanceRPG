using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The scene's fixed logic clock (Phase 5.8, 1c): the turn system is stepped in
/// whole steps of <see cref="SaveGame.LogicStepSeconds"/>, never by a frame's own
/// time, so the enemies' turn plays out the same at any frame rate — and the same
/// as a quit's settle, which steps the same step. Engine-free: frames are numbers.
/// </summary>
public class LogicClockTests
{
    private const float Step = SaveGame.LogicStepSeconds;

    /// <summary>Run <paramref name="frames"/> frames of <paramref name="seconds"/> each through a fresh clock: the steps each took, and the remainder left.</summary>
    private static (int[] Steps, float Remainder) Frames(float seconds, int frames)
    {
        float accumulator = 0f;
        var steps = new int[frames];
        for (int i = 0; i < frames; i++)
        {
            steps[i] = LogicClock.Steps(ref accumulator, seconds, Step);
            Assert.InRange(accumulator, 0f, Step);
            Assert.True(accumulator < Step, $"a remainder of {accumulator} is a whole step or more");
        }
        return (steps, accumulator);
    }

    [Fact]
    public void TheLogicAdvancesInWholeFixedSteps_WhateverTheFrames()
    {
        Assert.Equal(1f / 120f, SaveGame.LogicStepSeconds);
        Assert.Equal(24, LogicClock.MaxStepsPerFrame);

        // Frames of 7, 16.7 and 33 ms — 140, 60 and 30 frames a second — each take
        // the whole steps in their time, the fraction carried to the next: every
        // step the time elapsed holds is taken, and none it does not. The frame
        // counts keep the total clear of a step's edge, where a float's last bit
        // could tip the count either way.
        foreach (var (ms, frames) in new[] { (7f, 101), (16.7f, 97), (33f, 53) })
        {
            float seconds = ms / 1000f;
            var (steps, remainder) = Frames(seconds, frames);
            double elapsed = (double)seconds * frames;
            Assert.Equal((int)Math.Floor(elapsed / Step), steps.Sum());
            Assert.Equal(elapsed, steps.Sum() * (double)Step + remainder, 4);
            Assert.All(steps, taken => Assert.InRange(taken, (int)Math.Floor(seconds / Step), (int)Math.Ceiling(seconds / Step)));
        }

        // A 7 ms frame is shorter than a step: most take one, some none, and the
        // enemies still move once per step and never by a frame's own length.
        var (short7, _) = Frames(0.007f, 101);
        Assert.Contains(0, short7);
        Assert.Contains(1, short7);
        Assert.All(short7, taken => Assert.InRange(taken, 0, 1));

        // A 250 ms frame — a stall — holds 30 steps and takes the cap, 24: the rest
        // of it is dropped, never owed to the frames after, so a stall is not
        // followed by a burst. The remainder is still under a step.
        var (stalled, left) = Frames(0.25f, 5);
        Assert.All(stalled, taken => Assert.Equal(LogicClock.MaxStepsPerFrame, taken));
        Assert.InRange(left, 0f, Step);
        float afterStall = left;
        Assert.InRange(LogicClock.Steps(ref afterStall, 1f / 60f, Step), 2, 3);   // the next ordinary frame takes an ordinary frame's steps, the remainder with them

        // The same time cut into different frames takes the same steps, while no
        // frame is past the cap: 1.23 s as 7 frames, 41, 123 or 1230.
        const float total = 1.23f;
        int whole = (int)Math.Floor(total / (double)Step);
        foreach (int cut in new[] { 7, 41, 123, 1230 })
        {
            var (steps, _) = Frames(total / cut, cut);
            Assert.Equal(whole, steps.Sum());
            Assert.All(steps, taken => Assert.InRange(taken, 0, LogicClock.MaxStepsPerFrame));
        }

        // As one frame it is past the cap: 24 steps, the rest dropped.
        var (once, _) = Frames(total, 1);
        Assert.Equal([LogicClock.MaxStepsPerFrame], once);

        // A frame of no time — or none that means anything — takes no step and
        // leaves the remainder alone; a step that is no step is refused.
        float held = Step / 2f;
        Assert.Equal(0, LogicClock.Steps(ref held, 0f, Step));
        Assert.Equal(0, LogicClock.Steps(ref held, -0.5f, Step));
        Assert.Equal(0, LogicClock.Steps(ref held, float.NaN, Step));
        Assert.Equal(Step / 2f, held);
        Assert.Throws<ArgumentOutOfRangeException>(() => LogicClock.Steps(ref held, 1f / 60f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogicClock.Steps(ref held, 1f / 60f, -Step));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogicClock.Steps(ref held, 1f / 60f, float.NaN));
    }
}
