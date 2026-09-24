using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// <see cref="Tuning.MovementBudget"/>, §5.3's one new key (S:82; settled.md's
/// Phase 2 pass: "the caster's 160 is MovementBudget"): the whole a party member
/// moves in a turn, read from the tuning wherever the constant was read — a new
/// member's budget, a turn's refill before Mire's cut, the bank's cap, the
/// enemies' cost scale — and by the Mire legend, so it agrees with the MOVE
/// readout under a tuned budget. <see cref="GameConstants.MaxDistance"/> is its
/// compiled default. In the content collection: the budget is swapped for most of
/// these through the current content.
/// </summary>
[Collection(TestContent.Collection)]
public class MovementBudgetTests
{
    private const float Tile = GameConstants.Tile;

    private static Tuning Budget(int units) => ContentDefaults.Tuning with { MovementBudget = units };

    private static void Put(ActorState actor, (int R, int C) tile) => (actor.X, actor.Y) = (tile.C * Tile + Tile / 2f, tile.R * Tile + Tile / 2f);

    /// <summary>How many swings a hatchet-holding dummy, seen and beside a member, makes in one enemy phase.</summary>
    private static int DummySwings()
    {
        var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
        var dummy = new EnemyState { Weapon = TestWeapons.Get("hatchet") };
        Put(member, (5, 5));
        Put(dummy, (5, 6));
        var turns = new TurnSystem(new int[12, 20], new[] { member }, new[] { dummy }, () => 10);
        int hits = 0;
        turns.CharacterHit += (_, _) => hits++;
        turns.NotifyEnemyVisible(dummy, true);
        turns.EndTurn();
        for (float t = 0f; t < 10f; t += 1f / 30f)
            turns.Update(1f / 30f);
        Assert.Equal(TurnPhase.Player, turns.Phase);
        return hits;
    }

    [Fact]
    public void TheBudgetIsTheTuningsKey()
    {
        // PHASE5-047: the budget is a key, its compiled default the constant every
        // fixture was written against.
        Assert.Equal((int)GameConstants.MaxDistance, ContentDefaults.Tuning.MovementBudget);
        Assert.Equal(160, GameContent.Current.Tuning.MovementBudget);

        using (TestContent.Use(tuning: Budget(200)))
        {
            // A member built under it opens on it; a turn banks half the unspent,
            // capped at half the budget — 100 now — and its start refills the budget
            // plus the bank; a rest is the bare budget again.
            var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
            Assert.Equal((200f, 200f), (member.DistLeft, member.EffectiveMax));

            member.DistLeft = 150f;
            Assert.Equal(75f, member.EndTurnSaveMovement());
            member.StartTurn();
            Assert.Equal((275f, 275f, 0f), (member.EffectiveMax, member.DistLeft, member.SavedMovement));
            Assert.Equal(100f, member.EndTurnSaveMovement());   // half of 275 is 137: the cap is half the budget

            member.Rest();
            Assert.Equal((200f, 200f, 0f), (member.DistLeft, member.EffectiveMax, member.SavedMovement));

            // The enemies' cost scale is 100 against the party's budget: a
            // hatchet's 54 costs a dummy 27 of its 100 under a budget of 200 —
            // three swings, where 33.75 buys two under the compiled 160.
            Assert.Equal(3, DummySwings());
        }

        Assert.Equal(2, DummySwings());
        Assert.Equal(GameConstants.MaxDistance, TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto")).DistLeft);

        // And a file turns it: the playtest's edit is the game's budget.
        var load = ContentFiles.Load(ContentFiles.WriteDefaults() with { Tuning = """{ "MovementBudget": 240 }""" });   // beside the shipped content files
        Assert.Empty(load.Notices);
        Assert.Equal(["MovementBudget 240"], load.TuningOverrides);
        Assert.Equal(240, load.Content!.Tuning.MovementBudget);
        Assert.Equal(10, load.Content.Tuning.MovementUnitsPerMana);   // the divisor does not follow it: re-derived by hand
    }

    [Fact]
    public void TheMireLegendReadsTheTunedBudget()
    {
        // The Mire legend names the whole a turn start cut: the tuned budget plus
        // what was banked, so under a budget of 200 with 30 banked it is 230 —
        // exactly what StartTurn cut the MOVE readout's cap from.
        Assert.Equal(230f, DungeonScene.BudgetBeforeMire(Budget(200), banked: 30f));
        Assert.Equal(190f, DungeonScene.BudgetBeforeMire(ContentDefaults.Tuning, banked: 30f));

        using (TestContent.Use(tuning: Budget(200)))
        {
            var member = TestPools.Holding("A", TestWeapons.Get("weakspot_stiletto"));
            member.SavedMovement = 30f;
            member.ApplyStatus(StatusEffectType.Mire, null, 2);
            member.StartTurn();

            float whole = DungeonScene.BudgetBeforeMire(GameContent.Current.Tuning, banked: 30f);
            Assert.Equal(230f, whole);
            Assert.Equal(StatusBehaviours.MiredBudget(member, whole), member.EffectiveMax);
            Assert.Equal($"MIRE 2: MOVEMENT {member.EffectiveMax:0} OF 230", DungeonHud.StatusLegend(member, whole).Single().Text);
        }
    }
}
