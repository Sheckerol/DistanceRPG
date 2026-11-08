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
    /// The §3.3 Arcane row as the catalogue ships it: lock 30, trigger 8, and
    /// <see cref="Tuning.ArcanePotency"/> laid over its potency at
    /// <c>ApplyPercent</c> 100, so a tier-3 locks 90 and a tier-6 locks 180
    /// exactly as the doc says. Read from the catalogue, not stood up: the
    /// ladder's arithmetic is only worth pinning against the numbers the game
    /// actually charges.
    /// </summary>
    private static EnchantmentDef Arcane => GameContent.Current.Enchantments["arcane"];

    /// <summary>Serrated: shipped, unique, and therefore pinned at tier 1 whatever it is credited.</summary>
    private static EnchantmentDef Serrated => GameContent.Current.Enchantments["serrated"];

    [Fact]
    public void XpToNextTier_IsTheLockedPoolTimesTheScale_AndNoStat()
    {
        // The bar is the pool the tier locks, times EnchantmentTierScale. INT
        // used to divide it, which tiered a staff of renewal after two casts in
        // a wizard's hands; ruled after play (2026-10) that INT grows the
        // character's mana pool and never the weapon's ladder, so every
        // enchantment climbs at one rate in any hands.
        using var scaled = TestContent.UsePremise(tuning: t => t with { EnchantmentTierScale = 5 });
        var tier1 = new Enchantment(Arcane, Tier: 1);
        var tier3 = new Enchantment(Arcane, Tier: 3);

        Assert.Equal(30, tier1.EffectiveLock);
        Assert.Equal(150, tier1.XpToNextTier());    // 30 x 5
        Assert.Equal(90, tier3.EffectiveLock);
        Assert.Equal(450, tier3.XpToNextTier());    // 90 x 5

        // The staff that tiered too soon: 75 trigger mana to its second tier, in
        // anyone's hands — the shipped scale.
        Assert.Equal(5, ShippedContent.Tuning.EnchantmentTierScale);
        Assert.Equal(75, new Enchantment(GameContent.Current.Enchantments["regeneration"], Tier: 1).XpToNextTier());
    }

    [Fact]
    public void WithXp_SelfSlows_EachTierCostingTheNewLock()
    {
        // The replay is the definition: each tier costs the bar it just made, so
        // the climb slows on its own with no curve authored anywhere. At a scale
        // of 1 off a lock of 30 the steps are 30, 60, 90, 120, 150.
        using var flat = TestContent.UsePremise(tuning: t => t with { EnchantmentTierScale = 1 });
        var entry = new Enchantment(Arcane, Tier: 1);

        Assert.Equal((1, 0), Tiered(entry.WithXp(0)));
        Assert.Equal((1, 29), Tiered(entry.WithXp(29)));     // Xp is the remainder into the current tier
        Assert.Equal((2, 0), Tiered(entry.WithXp(30)));
        Assert.Equal((2, 59), Tiered(entry.WithXp(89)));     // one short of the second tier's 60
        Assert.Equal((3, 0), Tiered(entry.WithXp(90)));
        Assert.Equal((4, 0), Tiered(entry.WithXp(180)));
        Assert.Equal((5, 0), Tiered(entry.WithXp(300)));
        Assert.Equal((6, 0), Tiered(entry.WithXp(450)));

        // One lump and four hundred and fifty credits agree, because the tier is
        // stored and the XP is the remainder into it.
        var drip = entry;
        for (int i = 0; i < 450; i++)
            drip = drip.WithXp(1);
        Assert.Equal(Tiered(entry.WithXp(450)), Tiered(drip));

        Assert.Throws<ArgumentOutOfRangeException>(() => entry.WithXp(-1));
    }

    [Fact]
    public void TheBarAReadoutShowsIsTheXpTheClimbCharges()
    {
        // One tier, one price. XpToNextTier is the line, TierCost is what the
        // climb charges and what a readout prints, and the replay walks the
        // entry itself so the second is the first floored at 1 — nothing
        // restates the arithmetic. With a lock of 1 at least (the load rule) and
        // a scale of 1 at least (its clamp) the floor never bites, so for every
        // entry, at every scale, the two are equal.
        foreach (int scale in new[] { 1, 5, 1000 })
        {
            using var scaled = TestContent.UsePremise(tuning: t => t with { EnchantmentTierScale = scale });
            foreach (var def in GameContent.Current.Enchantments.All.Where(e => !e.Unique).Append(Arcane with { Lock = 1 }))
                for (int tier = 1; tier <= 6; tier++)
                {
                    var entry = new Enchantment(def, tier);
                    int bar = entry.XpToNextTier();

                    Assert.Equal(def.Lock * tier * scale, bar);
                    Assert.Equal(bar, entry.TierCost());
                    Assert.Equal((tier, bar - 1), Tiered(entry.WithXp(bar - 1)));
                    Assert.Equal((tier + 1, 0), Tiered(entry.WithXp(bar)));
                }
        }
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

        var spent = soul.WithXp(10_000);
        Assert.Equal(Enchantment.UniqueTier, spent.Tier);
        Assert.Equal(10_000, spent.Xp);
        Assert.Equal(Serrated.Lock, spent.EffectiveLock);   // and so the lock never grows either

        // The pin holds through the ladder as it holds through construction: a
        // second credit banks on top rather than finding a door the first missed.
        Assert.Equal((Enchantment.UniqueTier, 10_005), Tiered(spent.WithXp(5)));
        Assert.Equal(Enchantment.UniqueTier, new Enchantment(Serrated, Tier: 6).WithXp(10_000).Tier);
    }

    [Fact]
    public void EffectiveLockAndPotency_AreBaseTimesTier_WithNoMaximum()
    {
        // Lock and potency both scale linearly with tier. Potency has no field
        // of its own, deliberately: Tier is already a factor inside LevelsFor,
        // and a second idiom for the same idea would invite squaring it. The
        // potency a tier multiplies is tuning.json's ArcanePotency, laid over the
        // row: whatever it is, tier six is six of it.
        int potency = GameContent.Current.Tuning.ArcanePotency;

        var tier1 = new Enchantment(Arcane, Tier: 1);
        var tier6 = new Enchantment(Arcane, Tier: 6);

        Assert.Equal(30, tier1.EffectiveLock);
        Assert.Equal(potency, tier1.LevelsFor(Arcane.Potency));       // its potency in damage for a trigger of 8
        Assert.Equal(180, tier6.EffectiveLock);                       // the doc's tier-6 Arcane
        Assert.Equal(6 * potency, tier6.LevelsFor(Arcane.Potency));   // six times it for a lock of 180

        // Nothing in the type expresses a maximum: max mana is the only cap
        // there is, and it is a cap on what you can carry rather than on depth.
        var deep = new Enchantment(Arcane, Tier: 40);
        Assert.Equal(1200, deep.EffectiveLock);
        Assert.Equal(40 * potency, deep.LevelsFor(Arcane.Potency));
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

        Assert.Equal(0, new Enchantment(broken, Tier: 1).XpToNextTier());   // what the rule exists to refuse
        Assert.Equal(0, new Enchantment(broken, Tier: 9).XpToNextTier());

        var before = GameContent.Current;
        var data = new EnchantmentsData([.. ShippedContent.Enchantments.Enchantments, broken]);
        var ex = Assert.Throws<ContentException>(() => TestContent.Use(enchantments: data));

        Assert.Equal(("voidlock", ContentValidator.RuleEnchantmentLockIsPositive), (ex.EntryId, ex.Rule));
        Assert.Contains(ex.Rule, ex.Message);
        Assert.Same(before, GameContent.Current);   // an abort leaves the running content alone

        // Every shipped entry clears the bar, so the rule costs the catalogue nothing.
        Assert.All(GameContent.Current.Enchantments.All, e => Assert.True(e.Lock >= 1));
    }

    private static (int Tier, int Xp) Tiered(Enchantment e) => (e.Tier, e.Xp);
}
