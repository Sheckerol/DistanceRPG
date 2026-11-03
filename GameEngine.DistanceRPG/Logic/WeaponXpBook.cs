namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// One member's weapon XP, per class (§2.2): "Character A holds a Dagger
/// proficiency; any dagger they pick up wields at that level." Keyed on
/// <see cref="WeaponClass"/> and never on an item, so loot never resets
/// progress — swapping, dropping or picking a weapon up touches nothing here.
/// <para>
/// Every entry is a plain settable integer and nothing derived is stored beside
/// it: a level is replayed from the number on read (<see cref="Progression.Ladder"/>).
/// That is what makes Phase 4's death snapshot a copy of a few integers and
/// Phase 5's save a list of them, and it is why the setter is public — a
/// rollback and a load both write the raw XP back.
/// </para>
/// </summary>
public sealed class WeaponXpBook
{
    private static readonly int ClassCount = Enum.GetValues<WeaponClass>().Length;

    /// <summary>Cumulative XP indexed by enum ordinal, which gives enum order for free.</summary>
    private readonly int[] _xp = new int[ClassCount];

    /// <summary>Cumulative raw XP in <paramref name="cls"/>; 0 for a class this member has never used.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cls"/> is not a <see cref="WeaponClass"/>, or the value set is negative.</exception>
    public int this[WeaponClass cls]
    {
        get => _xp[Index(cls)];
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Cumulative XP is never negative: a ladder is only ever credited.");
            _xp[Index(cls)] = value;
        }
    }

    /// <summary>
    /// The classes this member has earned XP in, with their totals, in
    /// <see cref="WeaponClass"/> order — deterministic for the HUD and for a
    /// save, the <see cref="ModifierSet.Entries"/> precedent. A class at zero is
    /// absent: nothing has happened there to record.
    /// </summary>
    public IEnumerable<(WeaponClass Class, int Xp)> Entries
    {
        get
        {
            for (int i = 0; i < _xp.Length; i++)
                if (_xp[i] > 0)
                    yield return ((WeaponClass)i, _xp[i]);
        }
    }

    /// <summary>Every class's XP together: what the member has done with a weapon in their hands, whatever they were holding.</summary>
    public int Total
    {
        get
        {
            int total = 0;
            foreach (int xp in _xp)
                total += xp;
            return total;
        }
    }

    private static int Index(WeaponClass cls)
    {
        int i = (int)cls;
        if ((uint)i >= (uint)ClassCount)
            throw new ArgumentOutOfRangeException(nameof(cls), cls, "Not a WeaponClass.");
        return i;
    }
}
