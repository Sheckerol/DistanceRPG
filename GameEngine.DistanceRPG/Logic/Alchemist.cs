namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// What <see cref="Alchemist.Mix"/> did, for the screen that asked it to log it
/// (Logic never logs): the potion mixed, whose bag it went into and at which slot,
/// and the batch left once its units were spent.
/// </summary>
/// <param name="Potion">The potion mixed: its type, and its quality, the units it spent.</param>
/// <param name="MemberId">The member whose bag it went into.</param>
/// <param name="Slot">The bag slot it went into, 0 being the hand, which never holds one.</param>
/// <param name="BatchLeft">The units the batch holds after it (<see cref="Farm.Batch"/>).</param>
public sealed record Mixed(Potion Potion, string MemberId, int Slot, int BatchLeft);

/// <summary>
/// What <see cref="Alchemist.PourBack"/> did, for the screen that asked it to log
/// it, as <see cref="Mixed"/> is a mix's: the potion poured back, whose bag and which
/// slot it left, and the batch it went back into.
/// </summary>
/// <param name="Potion">The potion poured back: its quality is the units that came back.</param>
/// <param name="MemberId">The member whose bag it left.</param>
/// <param name="Slot">The bag slot it left.</param>
/// <param name="BatchLeft">The units the batch holds after it (<see cref="Farm.Batch"/>).</param>
public sealed record PouredBack(Potion Potion, string MemberId, int Slot, int BatchLeft);

/// <summary>
/// The alchemist (§3.4, §6.6). His arithmetic: what a potion restores, worked out
/// when it is drunk from the drinker's maximum at that moment, so a potion rides
/// the ladders the member is climbing rather than decaying into irrelevance as a
/// flat number would. Engine-free and stateless; the numbers are
/// <c>tuning.json</c>'s (<see cref="Tuning.HealthPotionPercent"/>,
/// <see cref="Tuning.ManaPotionPercent"/>).
/// <para>
/// <strong>And his doors</strong> (Phase 6b.4): between runs, once he has arrived
/// (<see cref="Farm.AlchemistHasArrived"/>), a potion is mixed from the farm's
/// batch into a member's bag (<see cref="Mix"/>) — a quality of <c>q</c> spends
/// <c>q</c> units, taking no runs and no place in a queue (§3.4: no second queue
/// beside the enchanter's) — and any potion in a bag is
/// poured back, its units back in the batch exactly (<see cref="PourBack"/>, PHASE
/// 6b DECISION 9). Neither keeps a number of its own: the batch is the last run's
/// yield less what the bags hold (<see cref="Farm.Batch"/>), so a mix writes only a
/// potion into a bag, which the save keeps.
/// </para>
/// <para>
/// <strong>A share, rounded down, of the pool's maximum</strong> (PHASE 6b
/// DECISION 3): health of <see cref="ActorState.MaxHp"/>; mana of
/// <see cref="ActorState.MaxMana"/>, the pool the member has earned, never of
/// what its locks leave spendable — what a drink can actually take is the
/// mana applier's to cap (<see cref="TurnSystem.TryDrink"/>). The share has no
/// minimum: quality 1 on a pool of 4 offers nothing.
/// </para>
/// <para>
/// <strong>No quality is refused.</strong> Past the quality that restores a whole
/// pool (<see cref="QualityForAll"/>) more quality is overshoot, which only
/// Overheal keeps (§6.6) — so a share is never capped, and is worked out wide
/// enough that no quality and no maximum a save can hold wraps it: it saturates
/// at the largest whole number instead.
/// </para>
/// </summary>
public static class Alchemist
{
    /// <summary>A percentage's whole.</summary>
    private const int FullPercent = 100;

    /// <summary>
    /// What a potion of <paramref name="type"/> mixed at <paramref name="quality"/>
    /// offers a pool whose maximum is <paramref name="max"/>:
    /// <c>floor(percent × quality × max / 100)</c>, held at the largest whole
    /// number. At 20 percent, quality 1 on 33 is 6 (6.6 rounded down), quality 3
    /// on 33 is 19, quality 5 on 33 is all 33, and quality 7 on 30 is 42 — a whole
    /// pool and 12 over.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A quality below 1, or a negative maximum.</exception>
    public static int Share(PotionType type, int quality, int max) => ShareAt(Percent(type), quality, max);

    /// <summary>
    /// <see cref="Share(PotionType, int, int)"/>'s arithmetic at <paramref name="percent"/>
    /// a unit: worked out wide, so no percent, quality or maximum wraps it, and
    /// held at the largest whole number.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A percent or a quality below 1, or a negative maximum.</exception>
    internal static int ShareAt(int percent, int quality, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        Int128 share = (Int128)percent * quality * max / FullPercent;
        return share >= int.MaxValue ? int.MaxValue : (int)share;
    }

    /// <summary>
    /// What <paramref name="potion"/> offers <paramref name="drinker"/> now: a
    /// share of its max HP for a health potion, of its max mana — the earned pool
    /// — for a mana potion (<see cref="Share(PotionType, int, int)"/>).
    /// </summary>
    public static int Share(Potion potion, ActorState drinker)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(drinker);
        return Share(potion.Type, potion.Quality, MaxOf(potion.Type, drinker));
    }

    /// <summary>
    /// The pool's maximum a potion of <paramref name="type"/> is a share of:
    /// <see cref="ActorState.MaxHp"/>, or <see cref="ActorState.MaxMana"/> — the
    /// earned pool, which the readouts beside the share name.
    /// </summary>
    public static int MaxOf(PotionType type, ActorState drinker)
    {
        ArgumentNullException.ThrowIfNull(drinker);
        return type == PotionType.Health ? drinker.MaxHp : drinker.MaxMana;
    }

    /// <summary>
    /// The quality that restores a whole pool from nothing:
    /// <c>ceil(100 / percent)</c> — 5 at 20 percent, the ceiling §6.6 names, "a
    /// potion that heals you completely from 1 HP". Rounding down never weakens
    /// it: at this quality the share is at least the whole maximum, at every
    /// percent from 1 to 100. Past it, more quality is overshoot.
    /// </summary>
    public static int QualityForAll(PotionType type) => QualityForAllAt(Percent(type));

    /// <summary><see cref="QualityForAll"/>'s arithmetic at <paramref name="percent"/> a unit: <c>ceil(100 / percent)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A percent below 1.</exception>
    internal static int QualityForAllAt(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        return (FullPercent + percent - 1) / percent;
    }

    // ── His doors (§6.6, Phase 6b.4) ─────────────────────────────────────────

    /// <summary>
    /// Why the alchemist would not mix a potion of <paramref name="quality"/> into
    /// <paramref name="memberId"/>'s bag now, or null when he would — asked in this
    /// order, and the first that refuses says why:
    /// <list type="number">
    /// <item>he has not arrived (<see cref="Farm.AlchemistHasArrived"/>):
    /// <see cref="NotYet"/>;</item>
    /// <item>a visit is held: no hub asks during one, and this throws;</item>
    /// <item>the batch is 0 (<see cref="Farm.Batch"/>): <see cref="Spent"/> when
    /// the last run yielded some and all of it is mixed, and otherwise why there is
    /// none (<see cref="NoBatch"/>);</item>
    /// <item>the quality wants more units than are left: <see cref="Short"/>;</item>
    /// <item>the bag has no empty slot — the hand keeps a weapon:
    /// <see cref="NoRoom"/>.</item>
    /// </list>
    /// <strong>No quality is capped</strong> (§6.6): past the quality that restores a
    /// whole pool (<see cref="QualityForAll"/>) more is overshoot, which only
    /// Overheal keeps, and the batch is the only ceiling.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality"/> is below 1: one unit makes an ordinary potion, and less is none.</exception>
    /// <exception cref="ArgumentException">No member of the party has the id.</exception>
    /// <exception cref="InvalidOperationException">A visit is in progress, and he has arrived.</exception>
    public static string? MixRefusal(CampaignState campaign, string memberId, int quality)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        var member = campaign.Party.FirstOrDefault(m => m.Id == memberId)
            ?? throw new ArgumentException($"'{memberId}' is no member of the party.", nameof(memberId));

        if (!Farm.AlchemistHasArrived(campaign))
            return NotYet(GameContent.Current.Dungeons.Tutorial);
        if (campaign.Visit is { } visit)
            throw new InvalidOperationException($"A visit to '{visit.Def.Id}' is in progress: the alchemist mixes between runs only.");
        int batch = Farm.Batch(campaign);
        if (batch == 0)
            return campaign.LastResult is { FarmYield: > 0 } ? Spent : NoBatch(campaign);
        if (quality > batch)
            return Short(quality, batch);
        if (!member.EmptyBagSlots.Any())
            return NoRoom(memberId);
        return null;
    }

    /// <summary>
    /// Mix a potion of <paramref name="type"/> at <paramref name="quality"/> into
    /// <paramref name="memberId"/>'s bag, between runs (§6.6): its units spent from
    /// the batch, and the potion laid in the bag's first empty slot from 1, never the
    /// hand (<see cref="CampaignState.Stow"/>). The gates are asked first
    /// (<see cref="MixRefusal"/>), and a refusal throws: the screen never asks for a
    /// mix it would be refused. It takes no runs and rolls nothing.
    /// </summary>
    /// <returns>The potion, where it went, and the batch left, for the screen's log.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress, or the alchemist refuses the mix — the reason in the message.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality"/> is below 1, or <paramref name="type"/> is no potion.</exception>
    /// <exception cref="ArgumentException">No member of the party has the id.</exception>
    public static Mixed Mix(CampaignState campaign, string memberId, PotionType type, int quality)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (MixRefusal(campaign, memberId, quality) is { } refused)
            throw new InvalidOperationException($"The alchemist refuses a {type} potion of quality {quality} for '{memberId}': {refused}");
        var potion = new Potion(type, quality);
        int slot = campaign.Stow(memberId, potion);
        return new Mixed(potion, memberId, slot, Farm.Batch(campaign));
    }

    /// <summary>
    /// Pour the potion in bag slot <paramref name="slot"/> of
    /// <paramref name="memberId"/>'s bag back, between runs (PHASE 6b DECISION 9):
    /// it leaves the bag (<see cref="CampaignState.Unstow"/>), and its units are back
    /// in the batch exactly, which is worked out from the bags — so a mix put in the
    /// wrong bag, or at the wrong strength, costs nothing before the descent. After
    /// it nothing can be poured back: the potions carried spoil as the run ends.
    /// </summary>
    /// <returns>The potion, where it was, and the batch it went back into, for the screen's log.</returns>
    /// <exception cref="InvalidOperationException">A visit is in progress.</exception>
    /// <exception cref="ArgumentException">No member of the party has the id, or the slot holds no potion.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is the hand, or not one the member carries.</exception>
    public static PouredBack PourBack(CampaignState campaign, string memberId, int slot)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var potion = campaign.Unstow(memberId, slot);
        return new PouredBack(potion, memberId, slot, Farm.Batch(campaign));
    }

    /// <summary>
    /// Why the ALCHEMIST page is shut, and why H and M mix nothing there, before he
    /// has arrived (§6.6, PHASE 6b DECISION 10), naming the tutorial as the
    /// enchanter's gate does (<see cref="Enchanter.Closed"/>): <c>THE ALCHEMIST
    /// ARRIVES WITH THE FIRST CLEAR BEYOND THE TUTORIAL</c>.
    /// </summary>
    public static string NotYet(DungeonDef tutorial)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        return $"THE ALCHEMIST ARRIVES WITH THE FIRST CLEAR BEYOND {tutorial.Name.ToUpperInvariant()}";
    }

    /// <summary>A mix with the whole batch mixed already: a potion poured back gives its units back.</summary>
    public const string Spent = "THE BATCH IS SPENT - POUR ONE BACK TO MIX ANOTHER";

    /// <summary>A quality past the units left: <c>QUALITY 3 NEEDS 3 UNITS - 2 ARE LEFT</c>, <c>... - 1 IS LEFT</c>.</summary>
    public static string Short(int quality, int left)
        => $"QUALITY {quality} NEEDS {UnitsSaid(quality)} - {left} {(left == 1 ? "IS" : "ARE")} LEFT";

    /// <summary>A bag with no empty slot: <c>[A] HAS NO ROOM IN THE BAG - THE HAND KEEPS A WEAPON</c>.</summary>
    public static string NoRoom(string memberId) => $"[{memberId}] HAS NO ROOM IN THE BAG - THE HAND KEEPS A WEAPON";

    /// <summary>
    /// Why there is no batch to mix from, the last run having yielded nothing — what
    /// the ALCHEMIST page says in the batch's place, and what H and M say then:
    /// <c>NO BATCH - THE LAST RUN DID NOT COUNT</c>; <c>NO BATCH - THE FARM YIELDS
    /// NOTHING</c> under a <see cref="Tuning.FarmYieldPerClear"/> of 0; and
    /// <c>NO BATCH YET - THE NEXT RUN THAT COUNTS BRINGS ONE</c> after a run that
    /// counted and yielded nothing — a save from before the farm (PHASE 6a's) — or
    /// before any run has ended.
    /// </summary>
    public static string NoBatch(CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.LastResult is { ServiceTick: false })
            return "NO BATCH - THE LAST RUN DID NOT COUNT";
        if (Farm.Yield(Farm.Clears(campaign), counted: true) == 0)
            return "NO BATCH - THE FARM YIELDS NOTHING";
        return "NO BATCH YET - THE NEXT RUN THAT COUNTS BRINGS ONE";
    }

    /// <summary>A number of units as the screen says it: <c>1 UNIT</c>, <c>3 UNITS</c>.</summary>
    private static string UnitsSaid(int n) => $"{n} {(n == 1 ? "UNIT" : "UNITS")}";

    /// <summary>The share a unit of quality restores, as a percentage: the tuning's, for the type.</summary>
    private static int Percent(PotionType type)
    {
        var tuning = GameContent.Current.Tuning;
        return type switch
        {
            PotionType.Health => tuning.HealthPotionPercent,
            PotionType.Mana => tuning.ManaPotionPercent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "There are two potions, health and mana."),
        };
    }
}
