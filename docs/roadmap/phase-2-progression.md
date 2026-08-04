# Phase 2 — Progression

## 2.1 Innate stats are fixed at creation

Each party member gets a permanent spread of **STR / DEX / CON / INT**. Stats
never rise. They are pure *rate multipliers*: you level what you use, at the
speed your nature allows. This is what makes party composition matter for the
whole run — D will always be the better caster, no matter how long C swings a
staff.

Starting spreads (20 points each, tuning targets):

| Member | STR | DEX | CON | INT | Starts with | Teaches |
| --- | --- | --- | --- | --- | --- | --- |
| A | 4 | 8 | 4 | 4 | Dagger | Crit windows |
| B | 7 | 5 | 5 | 3 | Sword & shield | Block |
| C | 7 | 4 | 6 | 3 | **Axe** | Cleave, and beating armour |
| D | 3 | 4 | 5 | 8 | **Staff of Renewal** | Buffs, mana |

The axe goes to C, the only member whose STR 7 suits it. D gives up the second
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

Rate curve, applied to every XP gain below:

```
rate(stat) = 0.5 + 0.1 * stat        # stat 1..10  →  0.6× .. 1.5×
```

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
weaponXp[member][class] += damageDealt * rate(governingStat)
xpToNext(L) = 100 * L                      # triangular, tune later
```

Level effects: `+floor(L / 2)` damage, and `-1` movement cost per 3 levels
(floored so a weapon never becomes free).

Staves deal no damage, so their proficiency is fed by **effect level applied**
rather than damage — and wands by total damage dealt across every target in the
shape, which makes wand levelling reward good shape placement.

### Health XP

```
hpXp[member] += hpRestored * rate(CON)
```

`TickStatusEffects` already caps healing at missing HP (`TurnSystem.cs:589`),
so overheal grants nothing. The emergent rule is neat: **constitution grows by
getting hurt and then healed** — a party that never takes a scratch never gains
HP.

### Mana XP

```
manaXp[member] += manaSpent * rate(INT)
```

Every cast and every enchantment trigger feeds it. This closes a loop with
Phase 3: enchantments lock max mana but spend mana when they fire, so running
them actively grows the pool that pays for them.

## 2.3 Code impact

New `Logic/InnateStats.cs` (the four values plus `RateFor`) and
`Logic/Progression.cs` (pools and curves). `PartyMemberState` gains `Stats`,
`WeaponXp`, `HpXp`, `ManaXp`; `MaxHp` and `MaxMana` become computed rather than
the constants they are today (`PartyMemberState.cs:16,21`).

`TurnSystem` credits XP at existing sites: the unified attack resolver
(damage), `TickStatusEffects` (healing), and `TryCast` (mana spent).

HUD gains per-class level and XP bars in the inventory panel.

