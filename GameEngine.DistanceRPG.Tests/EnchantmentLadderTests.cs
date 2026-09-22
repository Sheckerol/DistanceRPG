using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// §3.3's tier ladder as arithmetic, wired to nothing: what a tier costs, what
/// crediting mana does to it, what a tier is worth, and the load-time rule that
/// keeps the bar meaningful. Nothing here fires an enchantment — the credit
/// sites are a later sub-step's — so every assertion is a claim about the type.
/// In the content collection because the last test loads a broken catalogue.
/// </summary>
[Collection(TestContent.Collection)]
public class EnchantmentLadderTests
{
    /// <summary>
    /// The §3.3 Arcane row's numbers: lock 30, trigger 8, and
    /// <see cref="Tuning.ArcanePotency"/> at <c>ApplyPercent</c> 100, so a
    /// tier-3 locks 90 and a tier-6 locks 180 exactly as the doc says. Stood up
    /// as a def rather than read from the catalogue because the entry itself is
    /// a later sub-step's; its <see cref="EffectKind"/> is the closest shipped
    /// shape (a flat-trigger catalogue entry) and nothing here reads it.
    /// </summary>
    private static EnchantmentDef Arcane => new(
        Id: "arcane", Name: "Arcane", Effect: EffectKind.Vampiric, Targets: TargetSide.Enemy,
        Lock: 30, Trigger: 8,
        Potency: GameContent.Current.Tuning.ArcanePotency, ApplyPercent: 100,
        Applies: null, DamageType: null, Unique: false);

    /// <summary>Serrated: shipped, unique, and therefore pinned at tier 1 whatever it is credited.</summary>
    private static EnchantmentDef Serrated => GameContent.Current.Enchantments["serrated"];

    [Fact]
    public void XpToNextTier_IsTheLockedPoolOverInt()
    {
        // "xpToNextTier = currentTierCost / INT", where the tier's current cost
        // is the pool it locks — the only cost a tier has. It is
        // Progression.XpToNext again, a fifth application of the one threshold
        // function, so INT divides a bar and never multiplies a credit.
        var tier1 = new Enchantment(Arcane, Tier: 1);
        var tier3 = new Enchantment(Arcane, Tier: 3);

        Assert.Equal(30, tier1.EffectiveLock);
        Assert.Equal(7, tier1.XpToNextTier(4));     // 30 / 4 = 7.5 -> 7
        Assert.Equal(Progression.XpToNext(tier1.EffectiveLock, 4), tier1.XpToNextTier(4));

        Assert.Equal(90, tier3.EffectiveLock);
        Assert.Equal(22, tier3.XpToNextTier(4));    // 90 / 4 = 22.5 -> 22

        // A flat 1 pays the whole bar, which is what a thing with no nature
        // costs — an enemy's spend, and the farm's grant.
        Assert.Equal(30, tier1.XpToNextTier(1));
        Assert.Equal(90, tier3.XpToNextTier(1));
    }

    [Fact]
    public void WithXp_SelfSlows_EachTierCostingTheNewLock()
    {
        // The replay is the definition: each tier costs the bar it just made, so
        // the climb slows on its own with no curve authored anywhere. At INT 4
        // off a lock of 30 the steps are 7, 15, 22, 30, 37.
        var entry = new Enchantment(Arcane, Tier: 1);

        Assert.Equal((1, 0), Tiered(entry.WithXp(0, 4)));
        Assert.Equal((1, 6), Tiered(entry.WithXp(6, 4)));      // Xp is the remainder into the current tier
        Assert.Equal((2, 0), Tiered(entry.WithXp(7, 4)));
        Assert.Equal((2, 14), Tiered(entry.WithXp(21, 4)));    // one short of the second tier's 15
        Assert.Equal((3, 0), Tiered(entry.WithXp(22, 4)));
        Assert.Equal((4, 0), Tiered(entry.WithXp(44, 4)));
        Assert.Equal((5, 0), Tiered(entry.WithXp(74, 4)));
        Assert.Equal((6, 0), Tiered(entry.WithXp(111, 4)));

        // One lump and a hundred and eleven credits agree, because the tier is
        // stored and the XP is the remainder into it.
        var drip = entry;
        for (int i = 0; i < 111; i++)
            drip = drip.WithXp(1, 4);
        Assert.Equal(Tiered(entry.WithXp(111, 4)), Tiered(drip));

        // The wielder's INT is the divisor, so the same spend buys a wizard four
        // times the depth it buys a thing with no nature.
        Assert.Equal((1, 29), Tiered(entry.WithXp(29, 1)));
        Assert.Equal((2, 0), Tiered(entry.WithXp(30, 1)));

        Assert.Throws<ArgumentOutOfRangeException>(() => entry.WithXp(-1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.WithXp(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.WithXp(1, 5));
    }

    [Fact]
    public void TheBarAReadoutShowsIsTheXpTheClimbCharges()
    {
        // One tier, one price. XpToNextTier is the bar a readout prints and
        // WithXp is what actually charges, and the replay walks the entry itself
        // so the second is the first floored at 1 — nothing restates the
        // arithmetic. For every shipped entry the floor never bites and the two
        // are equal, so a future low-lock content row fails here rather than
        // quietly displaying a bar of 0 while charging 1.
        foreach (var def in GameContent.Current.Enchantments.All.Where(e => !e.Unique))
            for (int tier = 1; tier <= 6; tier++)
                for (int stat = InnateStats.Low; stat <= InnateStats.High; stat++)
                {
                    var entry = new Enchantment(def, tier);
                    int bar = entry.XpToNextTier(stat);

                    Assert.True(bar >= 1, $"{def.Id} at tier {tier}, INT {stat} prices a tier at {bar}.");
                    Assert.Equal((tier, bar - 1), Tiered(entry.WithXp(bar - 1, stat)));
                    Assert.Equal((tier + 1, 0), Tiered(entry.WithXp(bar, stat)));
                }

        // The one band where the two legitimately differ, stated rather than
        // discovered: at a lock of 1 and INT 4 the bar reads 0 — XpToNextTier is
        // the doc's line and carries no floor, which is what
        // ALockOfZeroIsRefusedAtLoad pins — while the climb charges the floored 1
        // it must, or the replay would never leave the tier. No shipped entry is
        // in that band (the catalogue's smallest lock is 15) and the content rule
        // only refuses 0, so the relationship is pinned here instead.
        var thin = new Enchantment(Arcane with { Lock = 1 }, Tier: 1);
        Assert.Equal(0, thin.XpToNextTier(4));
        Assert.Equal((1, 0), Tiered(thin.WithXp(0, 4)));
        Assert.Equal((2, 0), Tiered(thin.WithXp(1, 4)));
        Assert.Equal((5, 0), Tiered(thin.WithXp(4, 4)));   // 1, 1, 1, 1: the floor, four times
    }

    [Fact]
    public void AUniqueBanksXpAndBuysNothing()
    {
        // "They still accrue mana spent, since every trigger still costs; it
        // simply buys nothing." It is not a dead stack: the same spend still
        // grows the wielder's max mana, which is the other ladder that number
        // feeds.
        var soul = new Enchantment(Serrated, Tier: 1);
        Assert.True(soul.Unique);

        var spent = soul.WithXp(10_000, 4);
        Assert.Equal(Enchantment.UniqueTier, spent.Tier);
        Assert.Equal(10_000, spent.Xp);
        Assert.Equal(Serrated.Lock, spent.EffectiveLock);   // and so the lock never grows either

        // The pin holds through the ladder as it holds through construction: a
        // second credit banks on top rather than finding a door the first missed.
        Assert.Equal((Enchantment.UniqueTier, 10_005), Tiered(spent.WithXp(5, 4)));
        Assert.Equal(Enchantment.UniqueTier, new Enchantment(Serrated, Tier: 6).WithXp(10_000, 1).Tier);
    }

    [Fact]
    public void EffectiveLockAndPotency_AreBaseTimesTier_WithNoMaximum()
    {
        // Lock and potency both scale linearly with tier. Potency has no field
        // of its own, deliberately: Tier is already a factor inside LevelsFor,
        // and a second idiom for the same idea would invite squaring it.
        Assert.Equal(4, GameContent.Current.Tuning.ArcanePotency);

        var tier1 = new Enchantment(Arcane, Tier: 1);
        var tier6 = new Enchantment(Arcane, Tier: 6);

        Assert.Equal(30, tier1.EffectiveLock);
        Assert.Equal(4, tier1.LevelsFor(Arcane.Potency));      // 4 damage for a trigger of 8
        Assert.Equal(180, tier6.EffectiveLock);                // the doc's tier-6 Arcane
        Assert.Equal(24, tier6.LevelsFor(Arcane.Potency));     // 24 damage for a lock of 180

        // Nothing in the type expresses a maximum: max mana is the only cap
        // there is, and it is a cap on what you can carry rather than on depth.
        var deep = new Enchantment(Arcane, Tier: 40);
        Assert.Equal(1200, deep.EffectiveLock);
        Assert.Equal(160, deep.LevelsFor(Arcane.Potency));
        Assert.Equal(40, deep.Tier);
    }

    [Fact]
    public void ALockOfZeroIsRefusedAtLoad()
    {
        // The lock is load-bearing twice now: it reserves mana, and it is the
        // bar the tier ladder divides. A row at 0 prices every tier at nothing
        // for every stat, which is exactly the hazard StartingPool's own rule
        // already closes — so it is a named startup failure, not a guard buried
        // in the replay.
        var broken = new EnchantmentDef(
            Id: "voidlock", Name: "Voidlock", Effect: EffectKind.Vampiric, Targets: TargetSide.Ally,
            Lock: 0, Trigger: 2, Potency: 1, ApplyPercent: 100,
            Applies: null, DamageType: null, Unique: false);

        Assert.Equal(0, new Enchantment(broken, Tier: 1).XpToNextTier(4));   // what the rule exists to refuse
        Assert.Equal(0, new Enchantment(broken, Tier: 9).XpToNextTier(1));

        var before = GameContent.Current;
        var data = new EnchantmentsData([.. ContentDefaults.Enchantments.Enchantments, broken]);
        var ex = Assert.Throws<ContentException>(() => TestContent.Use(enchantments: data));

        Assert.Equal(("voidlock", ContentValidator.RuleEnchantmentLockIsPositive), (ex.EntryId, ex.Rule));
        Assert.Contains(ex.Rule, ex.Message);
        Assert.Same(before, GameContent.Current);   // an abort leaves the running content alone

        // Every shipped entry clears the bar, so the rule costs the catalogue nothing.
        Assert.All(GameContent.Current.Enchantments.All, e => Assert.True(e.Lock >= 1));
    }

    private static (int Tier, int Xp) Tiered(Enchantment e) => (e.Tier, e.Xp);
}
