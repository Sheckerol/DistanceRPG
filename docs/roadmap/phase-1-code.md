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
                    Rout, Pin, Softening, CritWeaken, CritSunder, BlockWeaken,
                    OnHitPoison, Cast,
                    Momentum }                             // Momentum: enchantment-only

sealed class ModifierSet                 // ModifierType → stack count
{
    int Stacks(ModifierType t);          // 0 when absent
    int Value(ModifierType t);           // ModifierRules.Resolve(t, Stacks(t))
    ModifierSet With(ModifierType t, int n, ModifierSet forged);   // clamped merge
}

static class ModifierRules               // the §1.1 table, one place only
{
    const int AcquiredHeadroom = 5;      // per modifier type, independently
    static int PerStack(ModifierType t);
    static int Offset(ModifierType t);   // CritMultiplier 2, Charges 1, else 0
    static int MaxForged(ModifierType t);// Light 1, else 3  — §1.1

    static int Cap(ModifierType t, int forgedStacks)
        => forgedStacks + AcquiredHeadroom;    // no clamp: nothing is capped
    static int Resolve(ModifierType t, int stacks)
        => Offset(t) + PerStack(t) * stacks;
}
```

`Cap` has no second term on purpose — **there is no flat cap in the system**
(§1.1), so anything that looks like one appearing here later is a design
regression rather than a tuning change.

`Offset` is what re-prices a modifier granting something before its first stack:
`CritMultiplier`'s base ×2, `Charges`'s first throw. `MaxForged` is the *other*
tool, and it is checked when a weapon is **built** — variant tables, unique
tables, boss theming, all of which are data — rather than in `With`, since it
constrains the forge and never acquisition. Two values: `Light` at 1, everything
else at 3.

A test should assert `MaxForged` over the whole unique table, since that limit
is what every per-stack value in §1.1 is priced against and it is enforced by
convention in data rather than by the type system.

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

Sword, spear and staff map to `×1` directly, preserving their shipped values.

**The dagger is a deliberate exception.** It becomes `CritWindow ×1` — crit on
19–20, not the prototype's 16+ (§1.2). Two assertions in
`CombatRulesTests.cs:28` change with it. Narrowing the baseline is what keeps a
crit a 10–20% event for the whole ordinary game (§1.2) and leaves the dagger
seven stacks of headroom instead of one.

### Everything else

New `StatusEffectType` members: `Ward`, `Poison`, `Mire`, `Sundered`,
`Weakened`.

`Weapon` gains `WeaponClass` (the eight above), `Forged` (§1.1), `AreaShape?`,
and `StatusEffectType? CastEffect` — which effect a staff or wand applies is a
*field*, not a modifier; the modifier only says how hard it lands. Phase 3's
drop tables key off `WeaponClass`.

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

**HUD.** The regen badge is party-only today. Riders land on enemies too, so
enemy nameplates need effect badges, and floating combat text needs a
`SUNDERED!` / `WEAKENED!` beat distinct from the damage number.

**Enemy AI needs a pass.** `EnemyAi.PlanMove` closes to weapon range. Ranged
and wand enemies want the opposite — hold distance and kite — and wands
additionally need the placement scorer from §1.4. That is real work, not a
parameter, and it is why ranged/wand enemies are an open question below.