# Phase 1 — Code impact

> §1.7. Stack rules in [phase-1-modifiers](phase-1-modifiers.md); index in [ROADMAP.md](../../ROADMAP.md).

## 1.7 Code impact

### The modifier model replaces `WeaponAbility`

Today a weapon holds `IReadOnlyList<WeaponAbility>` where
`WeaponAbility(AbilityType, int Value)` carries the value inline, and lookup is
`GetAbility(type)` returning at most one (`Weapons.cs:21,33`). Stacking makes
the value **derived**, so the pair becomes:

```csharp
enum ModifierType { Brace, Block, CritWindow, CritMultiplier, Cleave, Charges,
                    Longshot, Light, Riposte, Push, Drag, Splitting, Overwatch,
                    Opportunist, Rout, Pin, Softening, CritWeaken, CritSunder,
                    BlockWeaken, OnHitPoison, Resonant,
                    Momentum }                             // Momentum: enchantment-only

sealed class ModifierSet                 // ModifierType → stack count
{
    int Stacks(ModifierType t);          // 0 when absent
    int Value(ModifierType t);           // ModifierRules.Resolve(t, Stacks(t))
    ModifierSet With(ModifierType t, int n, ModifierSet forged);   // clamped merge
}

[Flags] enum WeaponKind { Melee = 1, Ranged = 2, Caster = 4, Any = 7 }

static class ModifierRules               // the §1.1 table, one place only
{
    const int AcquiredHeadroom = 5;      // per modifier type, independently
    static int PerStack(ModifierType t);
    static int Offset(ModifierType t);   // CritMultiplier 2, Charges 1, else 0
    static int MaxForged(ModifierType t);// Light 1, Resonant 1, else 3 — §1.1

    // Declared one direction only; the symmetric closure is built at static
    // init, so a pair is one line and can never be half-declared.
    static readonly IReadOnlyDictionary<ModifierType, ModifierType[]> Excludes =
        Symmetric(new()
        {
            [Brace] = [Opportunist, Overwatch],   // one threat zone per weapon
            [Opportunist] = [Overwatch],
            [Push]  = [Drag, Rout],               // one displacement direction
            [Drag]  = [Rout],
            [Riposte] = [BlockWeaken],            // one payoff per block
            [CritWeaken] = [CritSunder],          // one rider per crit
            [Light] = [Resonant],                 // one currency per weapon
        });

    // Deepenable if already present, never grantable from zero.
    static readonly IReadOnlySet<ModifierType> ForgedOnly = new HashSet<ModifierType>
    {
        Charges,    // it is a cap, not a bonus: grafting one would make a bow worse
    };

    // Cannot exist WITHOUT these. Not symmetric — a dependency, not a pair.
    static readonly IReadOnlyDictionary<ModifierType, ModifierType[]> Requires =
        new()
        {
            [Riposte]     = [Block],    // nothing to counter off
            [BlockWeaken] = [Block],    // nothing to succeed at
            [Rout]        = [Cleave],   // without a cleave it is just a worse Push
        };

    // Absent → WeaponKind.Any
    static readonly IReadOnlyDictionary<ModifierType, WeaponKind> RequiresKind =
        new()
        {
            [Brace] = Melee, [Opportunist] = Melee,
            [Overwatch] = Ranged,
            // Resonant is NOT caster-only: it discounts enchantment triggers too
        };

    static int Cap(ModifierType t, int forgedStacks)
        => forgedStacks + AcquiredHeadroom;    // no clamp: nothing is capped
    static int Resolve(ModifierType t, int stacks)
        => Offset(t) + PerStack(t) * stacks;

    static bool Allowed(ModifierType t, WeaponKind kind, ModifierSet present)
        => RequiresKind.GetValueOrDefault(t, Any).HasFlag(kind)
        && (!ForgedOnly.Contains(t) || present.Stacks(t) > 0)
        && !Excludes.GetValueOrDefault(t, []).Any(x => present.Stacks(x) > 0)
        &&  Requires.GetValueOrDefault(t, []).All(x => present.Stacks(x) > 0);
}
```

`Cap` has no second term on purpose — **there is no flat cap in the system**
(§1.1), so anything that looks like one appearing here later is a design
regression rather than a tuning change.

`Offset` is what re-prices a modifier granting something before its first stack:
`CritMultiplier`'s base ×2, `Charges`'s first throw. `MaxForged` is the *other*
tool, and it is checked when a weapon is **built** — variant tables, unique
tables, boss theming, all of which are data — rather than in `With`, since it
constrains the forge and never acquisition. Two values: the currency pair
(`Light`, `Resonant`) at 1, everything else at 3. They share the limit because
they share a shape and exclude each other (§1.1); capping one alone would make
the other the better pick by a stack.

**`Excludes` is a table rather than a group enum**, and that is deliberate. A
`Group(t)` returning `reaction | displacement | none` forces every exclusion to
be transitive and to earn a name, which is fine for the two that exist and wrong
the first time a pair needs to exclude without a third joining them. A
`ModifierType → ModifierType[]` map expresses any relation, adds a rule in one
line, and keeps the whole thing readable as data.

The cost is that a hand-written map can be **half-declared** — `Brace` excluding
`Opportunist` while `Opportunist` forgets `Brace` — and the bug that produces is
order-dependent and horrible. So the table is declared **one direction only** and
`Symmetric()` builds the closure at static init. Adding an exclusion is then a
single entry that cannot be got wrong, which is the reason to prefer the
dictionary in the first place rather than a reason to be careful with it.

**`Requires` is the third relation and is deliberately *not* symmetric.**
`Excludes` says two modifiers cannot coexist; `Requires` says one cannot exist
alone. A `Riposte` with no `Block` has nothing to counter off, a `BlockWeaken`
has nothing to succeed at, and a `Rout` without a `Cleave` is a strictly worse
`Push` that also occupies the displacement slot. All three would be dead stacks,
which §1.1 forbids — so they are illegal rather than merely bad.

Two consequences worth having on purpose:

- **Grafts become order-dependent, which is a feature.** A dagger can never be
  offered `Riposte` — but a dagger that has already grafted `Block` can, later.
  Over enough services a weapon can reach spreads no single roll could hand it,
  and the game tells a small story getting there. Nothing is ever removed from a
  weapon, so a satisfied prerequisite stays satisfied.
- **A themed boss can unlock a prerequisite.** The golem forges `Block` onto its
  own drop (§4.3), so that weapon becomes eligible for `Riposte` and
  `BlockWeaken` at the enchanter — as does any other weapon from that dungeon
  that happened to *roll* the theme. A Block-themed spear that later learns to
  riposte is a weapon the drop tables cannot produce and nobody designed, which
  is the best thing grafting does. Note the ordering this implies: the theme
  roll has to resolve before the graft roll asks what the weapon can hold.

`Allowed` folds all three rules into one predicate, so there is exactly one
definition of "this weapon cannot hold that" and every caller asks the same
question. It gates *offers* rather than rejecting after the fact:

| Caller | Uses it to |
| --- | --- |
| Weapon data validation | Assert no forged spread is illegal (§1.2) |
| Enchanter graft roll (§6.4) | Filter the candidate list before rolling |
| Themed boss drop table (§4.3) | Drop whole **classes** that cannot take the theme |

The third is the odd one, because it is a *class*-level question — "could any
weapon of this class hold `Brace`?" — rather than a weapon-level one. It works
today because every conflict comes from a class **baseline**, which all four
variants share, so checking the baseline answers for the class. That stops being
true if a theme is ever drawn from something other than a class signature: a
`Rout`-themed boss would conflict with the Routing Axe alone and would need to
exclude a *variant* rather than a class. Worth knowing before broadening themes.

A test should assert `MaxForged` over the whole unique table, since that limit
is what every per-stack value in §1.1 is priced against and it is enforced by
convention in data rather than by the type system. The same test should walk all
32 weapons plus the uniques and assert no forged spread violates `Allowed` —
cheap, and it catches the case where a future class baseline quietly gives
someone two reactions. A third assertion is nearly free and worth having:
`Excludes` is symmetric after construction, which is the invariant
`Symmetric()` exists to guarantee.

The same test should check the **derivation rule** (§1.5): every unique must
match some variant's spread with exactly one modifier raised to `×3` and nothing
else changed. That is cheap to verify and it is the constraint most likely to be
broken by a future hand-authored unique that seemed like a good idea.

**A weapon carries two sets, not one.** `Weapon.Forged` is the identity spread —
class baseline, variant role, unique spread, dungeon theme — fixed at drop and
never written again. `Weapon.Modifiers` is the live total, forged plus acquired,
and is what every resolution site reads. `Forged` exists purely to compute the
ceiling, so it must be immutable and must travel with the weapon through drops,
saves (§5.1) and enchanter services alike.

`Resolve` no longer clamps: clamping moves to `With`, which is the only path by
which a stack is ever added, so a set cannot be constructed out of bounds in the
first place. That keeps the invariant at the door rather than at every read.

Every call site that reads `GetAbility(x)?.Value ?? 0` becomes
`weapon.Modifiers.Value(x)` — the cap and the per-stack maths never leak out of
`ModifierRules`. `With` being additive *and clamping* is what makes variants,
uniques, farm depth and grafts the same operation: a stack landing on an
already-capped modifier is a no-op rather than a special case. (Enchantments
are a separate system with their own dials — §3.3.)

`Resolve` returns a raw number; whether that number is *absolute or
proportional* is the modifier's own business. `Light` resolves to a percentage
applied to the weapon's cost, so `Weapon.ResolvedCost` is
`Cost * (100 - Modifiers.Value(Light)) / 100`, computed once and cached rather
than recomputed per swing — the HUD and the movement gate must agree on one
number.

**Migration must preserve the shipped values.** Today's dagger carries
`CritRange 4`, and `CombatRules` resolves the crit threshold as `20 - value`
(`CombatRules.cs:35`), so it crits on **16+** — 25% of swings, pinned by
`CombatRulesTests.cs:28`. The sword blocks 3, the spear braces once.

One parity break is deliberate: `CharStartingWeaponIdx` is `{0,1,2,2}` and
becomes dagger/sword/axe/staff (§2.1), so
`CombatRulesTests.StartingWeapons_MatchPrototype` needs its second assertion
updated. The first — Dagger, Sword, Spear at indices 0–2 — still holds, since
new classes append after them.

Sword, spear and staff map to `×1` directly, preserving their shipped
*signature* values.

**More parity breaks come with the second baseline modifier** (§1.2). Every
class gains one, and three of them change behaviour rather than numbers:

| Class | Gains | Breaks |
| --- | --- | --- |
| Sword | `Push ×1` | Sword hits now displace — new behaviour, not a number |
| Spear | `Longshot ×1` | Spear reach moves 130 → **128**, so four tiles is exact |
| Axe | `Opportunist ×1` | A whole new reaction on the exit side of a threat zone |
| Ranged | `CritWindow ×1` | Bows crit on 19–20 rather than 20 |
| Throwing | `CritMultiplier ×1` | Crits multiply ×3 rather than ×2 |
| Staff, wand | **A rolled enchantment**, not a stack | Casters gain no second `ModifierType` at all; their second axis lives in the enchantment list (§3.1) |

The spear's range change is the one to watch: 130 is a shipped value and may be
asserted directly. It is not golden-test data — `TestData/distancerpg-golden.json`
pins map generation, fog and pathing, not weapon statlines — but any
`Weapons.cs` parity assertion moves with it.

These should land in their own commit with a test sweep rather than riding along
with the stacking migration, since together they change what several classes
*do* and not merely what they roll.

**The dagger is a deliberate exception.** It becomes `CritWindow ×1` — crit on
19–20, not the prototype's 16+ (§1.2). Two assertions in
`CombatRulesTests.cs:28` change with it. Narrowing the baseline is what keeps a
crit a 10–20% event for the whole ordinary game (§1.2) and leaves the dagger
seven stacks of headroom instead of one.

### Everything else

New `StatusEffectType` members: `Ward`, `Poison`, `Mire`, `Sundered`,
`Weakened`, and **`Searing`** — the lingering element (§1.5), one member
carrying a `DamageType` rather than a member per element. Shocking and Acidic
need nothing new at all: they apply `Mire` and `Poison`, which the staves
already bring.

`Searing` is also the first status whose **magnitude is not stored on it**. It
ticks for the source enchantment's damage, so the instance holds a reference to
that enchantment rather than a number — which means levelling the element
changes what already-applied ticks do, and means the effect has to handle its
source having been transferred away mid-fight (§1.5). Worth deciding whether it
snapshots on application or resolves live; live is the smaller data model and
the stranger behaviour.

`Enchantment` gains a `bool Unique`. It gates two things and nothing else: the
enchanter's catalogue skips it (§6.4), and `Tier` is pinned at 1 (§3.3). Both
are checks rather than mechanisms, which is the point — a unique enchantment is
an ordinary enchantment with two doors closed.

`Weapon` gains `WeaponClass` (the eight above), `Forged` (§1.1) and
`AreaShape?`. There is **no `CastEffect` field** — a staff's effect is its innate
enchantment (§1.3), so it lives in the enchantment list with everything else and
`Resonant` discounts the mana cost rather than scaling it. Phase 3's drop tables key
off `WeaponClass`.

`Resonant` therefore resolves like `Light`, against `Weapon.ManaCost` instead of
`Weapon.Cost`, and both want the same treatment: resolve once, display the
resolved number, never show the player a percentage mid-turn.

`ActorState` gains its own `ModifierSet` for **innate** modifiers (§4.3), and
every resolution site reads weapon **plus** innate rather than weapon alone.
Doing this in Phase 1 rather than retrofitting it in Phase 4 is much cheaper —
the same call sites are already being rewritten for stacking.

**`Innate` and `Forged` are different things and must not be conflated.**
`ActorState.Innate` belongs to the *actor* — the golem's Block whatever it is
holding — and is added at resolution time. `Weapon.Forged` belongs to the
*weapon* and exists only to compute that weapon's ceiling. An actor's innate
modifiers never raise any weapon's cap; a boss's theme only does so on the
weapons it **drops**, because those drop forged with it (§4.3).

`CombatRules.RollAttack` hardcodes `weapon.Damage * 2` on a crit
(`CombatRules.cs:39`) — that becomes `Damage * Modifiers.Value(CritMultiplier)`,
the `×2` base now living in that modifier's `Offset`, and damage becomes a
function of distance for Longshot.

`ResolveAttack` must also **skip the Block step entirely when the roll crit**
(§1.6), and skip it before the minimum-1 clamp rather than after — a crit that
absorbed down to 1 and was then let through is a different number. The same
branch is what suppresses the `Riposte` and `BlockWeaken` hooks, so all three
should read off one `blocked` flag rather than re-testing the crit separately.

`TurnSystem` gains: cleave and area target selection, a riposte hook, push/drag
displacement, per-turn charge tracking, an overwatch reaction during the enemy
phase, and the five new effects in the now-universal `TickStatusEffects`. Mire
reduces `EffectiveMax` in `PartyMemberState.StartTurn` and the enemy budget in
`StartEnemyAction`. Sundered and Weakened are read by `CombatRules`, so
`ResolveAttack` needs the attacker passed in — today it only takes the two
weapons (`CombatRules.cs:49`).

**Displacement must not teleport.** `Push`, `Drag` and `Rout` move an actor
tile by tile through `NotifyCharacterMoved`, exactly as a voluntary walk does,
so threat zones fire on the way (§1.2). Setting the position directly would
silently drop every brace and overwatch the shove should have triggered, and
would do so invisibly — the combo simply would not happen and nothing would look
broken. It also has to stop early on a wall or an occupied tile rather than
overlapping actors, reusing the anti-stacking mask `EnemyPlacer` and the pathing
already share.

**That path therefore needs to know whether the move was chosen.** `Brace` fires
on entry regardless, but `Opportunist` fires on exit *only* for voluntary
movement (§1.2), so `NotifyCharacterMoved` gains a flag distinguishing a walk
from a shove. It is one parameter, and getting it wrong is invisible in exactly
the same way — a Routing Axe would quietly grant itself an opportunity attack
per target and read as a damage bug rather than a rules bug.

`Opportunist` is otherwise a straight mirror of the brace machinery: the same
per-turn use pool, the same threat-zone lookup, the same resolver. The only new
concept is watching the *exit* edge of a zone rather than the entry edge, so
`TryBracesAgainst` generalises into one function taking which edge it cares
about rather than being copied.

The recursion guard is the existing per-turn brace budget rather than a depth
limit: a brace fired by a displacement spends a use like any other, so a
displacement chain terminates when the pool empties. Worth an explicit test,
since it is the one cascade in the design.

**HUD.** The regen badge is party-only today. Riders land on enemies too, so
enemy nameplates need effect badges, and floating combat text needs a
`SUNDERED!` / `WEAKENED!` beat distinct from the damage number.

**Enemy AI needs a pass.** `EnemyAi.PlanMove` closes to weapon range. Ranged
and wand enemies want the opposite — hold distance and kite — and wands
additionally need the placement scorer from §1.4. That is real work, not a
parameter, and it is why ranged/wand enemies are an open question below.