# Phase 2 — Progression

## 2.1 Innate stats are fixed at creation

Each party member gets a permanent spread of **STR / DEX / CON / INT**. Stats
never rise. They are pure *rate multipliers*: you level what you use, at the
speed your nature allows. This is what makes party composition matter for the
whole run — D will always be the better caster, no matter how long C swings a
staff.

### Stats run 1–4, and every character is a permutation of them

**No character is better endowed than another; they are differently arranged.**
Each party member's four stats are the numbers 1, 2, 3 and 4 in some order — so
every spread sums to 10, everyone is excellent at exactly one thing and hopeless
at exactly one thing, and the differences between members are entirely about
*which*.

That is a much smaller range than the tabletop instinct reaches for, and
deliberately: these are rate divisors (below), so a 4 against a 1 is already a
**fourfold** difference in how fast a pool grows. A fighter gains health four
times faster than a wizard, which is as wide a gap as any progression system
needs and wider than most survive.

| Member | STR | DEX | CON | INT | Starts with | Teaches |
| --- | --- | --- | --- | --- | --- | --- |
| A | 1 | **4** | 2 | 3 | Dagger | Crit windows |
| B | 3 | 1 | **4** | 2 | Sword & shield | Block |
| C | **4** | 2 | 3 | 1 | **Axe** | Cleave, and beating armour |
| D | 2 | 3 | 1 | **4** | **Staff of Renewal** | Buffs, mana |

**The four spreads fall into two pairs, and the pairs split opposite halves of
the stat list:**

| | Top two, swapped | Bottom two, swapped |
| --- | --- | --- |
| **Front line** — B and C | CON 4 / STR 3, and STR 4 / CON 3 | INT 2 / DEX 1, and DEX 2 / INT 1 |
| **Back line** — A and D | DEX 4 / INT 3, and INT 4 / DEX 3 | CON 2 / STR 1, and STR 2 / CON 1 |

So B is the party's body and C is its arm — the difference between the one who
holds a line and the one who breaks it — while A and D divide speed and mind the
same way. Each pair is a mirror of the other's ranking, which means no stat is
anyone's 4 twice and every stat is somebody's best.

**D is the only CON 1 in the party**, which makes the caster the single thing
that must not be reached — not a shared frailty but a specific one.

**Expect to move these after playing.** The permutation rule is the part worth
keeping; which member gets which permutation is a first guess.

The axe goes to C, the only member whose STR 4 suits it. D gives up the second
spear for a staff — at STR 3 they would never level a martial weapon well
anyway, and a party with no caster never discovers half the game.

**D carries a debuff staff in the bag.** Renewal equipped, Blight or Mire in an
inventory slot, so the player meets both halves of casting and learns the swap
mechanic getting to the second one.

### Brace is taught from the receiving end

Dropping the spear costs the party its `Brace` demonstration — but spear dummies
already brace against the party (`TurnSystem.NotifyCharacterMoved`), so walking
into one's reach still costs a free poke. Learning a threat zone by being
punished by it, then finding a spear and turning it around, is a better first
lesson than owning one from the start. Spears remain an early, common drop.

### Stats are divisors, and every pool starts at 25

**Max HP, max mana and a weapon's wear capacity all start at 25**, and all three
grow the same way: *a full bar's worth of XP, divided by the stat that governs
it.*

```
xpToNextPoint = currentMax / governingStat
```

| Pool | Governing stat | At 25, a point costs |
| --- | --- | --- |
| **Max HP** | CON | 6 at CON 4, **25 at CON 1** |
| **Max mana** | INT | 6 at INT 4, **25 at INT 1** |
| **Wear capacity** | **None — a flat 1** | 25, always. A weapon has no stats |

Three things this gets right at once:

- **The stat is a rate, not a bonus.** Nobody starts with more of anything; the
  spreads decide who *becomes* what. A CON 1 wizard and a CON 4 fighter open the
  game with the same 25 HP and diverge from there, which is the whole thesis of
  §2.1 arriving as arithmetic instead of a claim.
- **It self-slows.** The threshold is the *current* bar, so each point costs
  more than the last and no pool runs away. Growth is fast while you are fragile
  and glacial once you are not.
- **A weapon divides by 1 because it has no nature to divide by.** It needs a
  whole bar for every step, which is why `WearCapacity` climbs on an authored
  ladder (§6.1) rather than a curve — one full pool cashed, one step up.

## 2.2 Three XP pools, each fed by *doing the thing*

| Pool | Fed by | Governing stat |
| --- | --- | --- |
| Weapon proficiency | Damage dealt with that weapon class | Per class (§1) |
| Max HP | HP actually restored to you | CON |
| Max mana | Mana actually spent | INT |

### Weapon XP is per class, per character

Character A holds a **Dagger proficiency**; any dagger they pick up wields at
that level. Loot never resets progress, so Phase 3 cannot fight Phase 2.

```
weaponXp[member][class] += damageDealt            # AFTER mitigation — §1.6
xpToNext(L) = 100 * L / governingStat             # triangular, tune later
```

**`damageDealt` is the mitigated number, not the rolled one**, and that is a
decision rather than a detail. It is `Dealt` from §1.6's pipeline: after the
defender's `Block`, before their `Ward`. Three things follow:

- **A crit fast-forwards proficiency**, twice over. It multiplies the damage
  *and* skips `Block` entirely (§1.6), so a natural 20 into an armoured target
  can be worth several ordinary swings of XP. Crit builds level fastest, which
  is a distinction the classes did not otherwise have.
- **Armour slows you down.** Hitting a heavily-blocking target for 12 of a
  rolled 20 credits 12. You learn from the damage you actually did, which is
  the same principle every other pool in the game runs on.
- **`Ward` does not reduce it**, because `Ward` is temporary health rather than
  mitigation (§3.3). Damage that lands on a shield was still dealt.

This is also the requirement that shapes §1.7's handler contract: the attacker
cannot credit XP until the defender's handlers have run and **handed the number
back**. Damage is a value that returns, not one that is written.

Same shape as every other pool (§2.1): the XP is raw, and the **stat divides the
threshold**. One mechanism, four applications, and nothing anywhere multiplies a
gain by a fractional rate.

Level effects: `+floor(L / 2)` damage, and `-1` movement cost per 3 levels
(floored so a weapon never becomes free).

Staves deal no damage, so their proficiency is fed by **effect level applied**
rather than damage — and wands by total damage dealt across every target in the
shape, which makes wand levelling reward good shape placement.

### Health XP

```
hpXp[member]  += hpRestored
xpToNextPoint  = currentMaxHp / CON
```

`TickStatusEffects` already caps healing at missing HP (`TurnSystem.cs:589`),
so overheal grants nothing. The emergent rule is neat: **constitution grows by
getting hurt and then healed** — a party that never takes a scratch never gains
HP.

### Mana XP

```
manaXp[member] += manaSpent
xpToNextPoint   = currentMaxMana / INT
```

Every cast and every enchantment trigger feeds it. This closes a loop with
Phase 3: enchantments lock max mana but spend mana when they fire, so running
them actively grows the pool that pays for them.

## 2.3 Code impact

New `Logic/InnateStats.cs` (the four values) and `Logic/Progression.cs` (pools
and thresholds). `PartyMemberState` gains `Stats`, `WeaponXp`, `HpXp`, `ManaXp`;
`MaxHp` and `MaxMana` become computed rather than the constants they are today
(`PartyMemberState.cs:16,21`).

**There is no rate multiplier anywhere.** XP is credited raw and the stat
divides the *threshold* (§2.1), so `Progression` exposes one function and every
pool calls it:

```csharp
static int XpToNext(int currentMax, int stat) => currentMax / stat;   // stat 1..4
```

That keeps the four pools on one mechanism, keeps every XP credit an integer,
and means a stat can never introduce a fractional gain that has to be rounded
somewhere. `InnateStats` should **validate that a spread is a permutation of
1–4** at load (§2.1) — it is the kind of invariant that is free to check and
silently wrong if it drifts.

`TurnSystem` credits XP at existing sites: the unified attack resolver
(damage), `TickStatusEffects` (healing), and `TryCast` (mana spent).

HUD gains per-class level and XP bars in the inventory panel.

