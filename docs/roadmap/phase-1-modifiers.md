# Phase 1 — Weapon classes and variants

**Eight classes, four variants each: 32 weapons.**

The classes split into two groups with *different variant axes*. Martial
classes vary by stat profile; caster classes vary by what the cast actually
does. Forcing casters into the martial shape would produce four staves that all
do the same thing at different prices, which is not interesting.

Every class baseline is its signature **plus a class-specific second modifier**
(§1.2), so no weapon in the game is forged with fewer than two modifiers:
`Push` on the sword, `Longshot` on the spear, `Opportunist` on the axe,
`CritWindow` on the bow, `CritMultiplier` on throwing and the casters.

| Class | Stat | Class feature | Variant axis |
| --- | --- | --- | --- |
| Dagger | DEX | CritWindow | Martial |
| Sword & Shield | **max(STR, DEX)** | Block | Martial |
| Spear | STR | Brace | Martial |
| Axe | STR | Cleave | Martial |
| Ranged | DEX | Longshot | Martial |
| Throwing | STR | Charges | Martial |
| Staff | INT | Single-target buff/debuff | **Four effects** |
| Wand | INT | Area of effect | **Four shapes** |

CON governs no weapon — it is the health stat alone.

## 1.1 Modifiers are stacks, not values

**This is the load-bearing decision of Phase 1.** A weapon is a base statline
plus a **multiset of modifiers**. There is no `Brace 3` — there is `Brace ×3`,
the same modifier applied three times. Stack count is the only dial.

That makes every variant, every unique, every dungeon theme, every farmed tier
and every service result the same operation — *add stacks* — so nothing below
needs a bespoke mechanism:

| Concept | Expressed as |
| --- | --- |
| Class baseline | The one or two modifiers every weapon of that class carries |
| Efficiency weapon | `+ Light ×1` |
| Purity weapon | `+` one more stack of the class's signature (→ ×2) |
| Control weapon | `+` one stack of something that degrades the enemy |
| Support weapon | `+` one stack of something that helps the party |
| **Unique** | Arbitrary stacks — signature ×3, or two modifiers at ×2 |
| Dungeon theme (Phase 4) | `+` one stack of the boss's modifier, on everything it drops — §4.3 |
| Repeat-kill depth (Phase 3) | `+1` or `+2` stacks of the class signature — §3.2 |
| Enchanter service (Phase 6) | `+1` stack, deepening or grafting — §6.4 |
| Enchantment (Phase 3) | Its own system entirely — §3.3 |

A unique is therefore *not a new kind of thing*. "A halberd that braces three
times" is `Brace ×3, Push ×1` and needs no code beyond what the base system
already does.

### Forged and acquired: where a modifier caps

Each modifier declares what one stack is worth and where it caps. This is the
only place a modifier's maths exists.

A weapon's stacks come from two places, and the split is what sets its ceiling:

| | Means | Sources |
| --- | --- | --- |
| **Forged** | What the weapon's identity entitles it to | Class baseline, variant role, unique spread, and the theme of the dungeon it dropped in (§4.3) |
| **Acquired** | Everything piled on afterwards | Repeat-kill depth (§3.2), enchanter grafts and improvements (§6.4) |

**A modifier caps at its forged stacks plus five.**

```
cap(t) = forged(t) + 5
```

The five is **one shared budget per modifier type**, and every acquired source
draws on it. Farming spends it up front; the enchanter spends it slowly across
many runs. Every ceiling in the game falls out of that one line.

Forged is *not* "what the weapon dropped with." A repeat-killed dummy hands you
a weapon already carrying acquired stacks, and those count against the budget
exactly as an enchanter's would. The line is **chosen identity versus
accumulation**: the dungeon you chose to run and the weapon you chose to keep
decide how deep it can ever go — grinding and servicing decide how much of that
depth you have actually filled.

### Why the cap is not a flat five

A flat cap erases the **Purity** role. Its entire distinguishing feature is a
second stack of the class signature, which is the first thing a shared ceiling
absorbs. At `×5` an Assassin's Fang is `CritWindow ×5, CritMultiplier ×5` and a
Flensing Knife is that *plus* `Light ×5` — the purity weapon ends up strictly
worse than every sibling, because its variant stack was the only one that never
bought it a new modifier type.

The other roles survive a flat cap, because §6.4 deepens modifiers a weapon
already carries: a Kris grows `CritWeaken`, a Hatchet grows `Light`, and they
stay distinct by *which* modifiers rather than by how many. Purity is the one
role with nothing of its own to grow into.

`forged + 5` fixes that at the root, and pays out twice more:

- **Uniques and boss drops become better projects, not merely better starts.**
  Widowmaker's `CritWindow ×3` is a ceiling of 8 with five stacks of headroom
  above it — the deepest forge in the game *and* the most room left above it.
- **It makes grafting safe.** A modifier the weapon was never forged with has
  `forged = 0` and so caps at 5, which means a grafted `Block` on a dagger is
  always shallower than that dagger's own crit line. Breadth can be handed out
  freely (§6.4) without any weapon losing its shape.

The cap is on the *stack count*, not the resolved value, and it is enforced in
one place — `ModifierSet.With` clamps per type against the weapon's forged set.

**Per-stack value is still the only balance dial**, but it is now chosen as
`intended ceiling ÷ deepest reachable stack count` rather than `÷ 5`. No unique
may be forged past `×3` (§1.5), so the deepest anything reaches is **`×8`**, and
that is the number to divide by.

### Nothing is capped at the ceiling

**There is no flat cap anywhere in this system.** Every modifier resolves at
`forged + 5`, and every stack in that range does something. A capped modifier
would contain dead stacks, and a dead stack is a farm cycle or a service roll
that bought the player nothing — the one outcome this whole design is built to
avoid.

What *is* bounded is the **forge**: what the game is willing to hand you,
authored in data. Acquisition is not bounded at all, only slowed. So the shape
of the rule is:

> The game caps what it *gives* you. It never caps what you can build toward.

That leaves two tools for a modifier that would misbehave deep, and both act
before the player ever touches the weapon.

**1. Re-price the stack.** Choose a per-stack value such that the deepest
reachable spread still lands inside whatever bounds it. Nothing is capped,
nothing is dead, and the modifier scales like everything else. Always try this
first.

`Charges` is the case. At `+2` throws per stack it overshoots — a unique's
forged `×3` ceilings at `×8`, and 16 throws × 15 is 240 against a 160 budget.
Re-priced to **one throw plus one per stack** it lands at 9 throws for 135, just
inside the budget at the deepest spread in the game, with the shipped baseline
of 2 throws at `×1` unchanged. `CritMultiplier` already carries an offset like
this (base ×2, +1 per stack), so the shape has precedent.

**2. Limit what can be forged.** Leave the `+5` acquired headroom alone and
bound the *forged* contribution instead. The ceiling comes down without any
stack becoming dead, and the modifier stays fully reachable through play.

Two forge limits exist, and they are the only hard numbers in the system:

| Limit | Applies to | Effect |
| --- | --- | --- |
| **A unique is a variant with one modifier raised to `×3`** | Every unique (§1.5) | Nothing is forged past `×3`, so `×8` is the universal ceiling |
| **No weapon may be forged past `Light ×1`** | Everything, uniques included | `Light` ceilings at `×6`, a 60% discount — dagger at 12, axe at 24 |

### What a weapon may hold: three relations

Three constraints act on *which* modifiers can share a weapon at all, rather
than on how deep they go. All bind every source — the forge, a farm roll, a
graft, and a boss theme alike — and all are tables rather than special cases.

| Relation | Says | Example |
| --- | --- | --- |
| **Excludes** | These two cannot coexist | `Brace` and `Opportunist` |
| **Requires** | This cannot exist without that | `Riposte` needs `Block` |
| **Kind** | Only on this sort of weapon | `Overwatch` is ranged only |

**Excludes — a weapon may carry at most one modifier from each group:**

| Group | Members | Why they exclude |
| --- | --- | --- |
| **Threat zone** | `Brace`, `Opportunist`, `Overwatch` | One weapon, one zone it watches |
| **Displacement** | `Push`, `Drag`, `Rout` | A weapon that both shoves and pulls has no answer to "which way?" |
| **Block response** | `Riposte`, `BlockWeaken` | One block, one payoff |
| **Efficiency** | `Light`, `Cast` | One currency per weapon |

A group exists for one of two reasons, and it is worth being able to tell which:

- **Holding both would be incoherent.** Displacement is the case — one hit
  cannot move a target two directions.
- **Holding both would collapse a distinction the design deliberately drew.**
  Threat zones and block responses are both this. §1.2 spends a section arguing
  that `Riposte` and `BlockWeaken` are *the same trigger split by who collects* —
  the counter-swing is yours, the debuff is the party's. A shield carrying both
  gets two payoffs from one defensive event and the Control/Support fork stops
  meaning anything.

The efficiency group is a **third** reason, and the most subtle: holding both
would be *double-dipping on one axis while looking like two*.

**`Light` and `Cast` are the same discount reached two ways.** Mana regenerates
only from movement left unspent at end of turn
(`PartyMemberState.RegenManaFromUnusedMovement`), so cheaper *movement* is
already cheaper *mana* — it just arrives by the long route. `Cast` takes the
short one. A weapon carrying both compounds a discount with itself: it spends
less mana per action, and the movement it saves comes back as more mana to
spend.

So the group forces a real choice rather than forbidding a silly one:

| | Buys you | The route |
| --- | --- | --- |
| **`Light`** | More actions per turn | Movement saved becomes mana at end of turn |
| **`Cast`** | More mana per action | The mana simply costs less |

**A mana-efficient dagger and a light dagger are now different weapons**, and
the enchanted-dagger build (§3.3) has to pick one. `Cast` is the better answer
for a wizard leaning on expensive triggers; `Light` is better for one who wants
to swing four times and let the regen carry them. Neither is available on an
Efficiency variant carrying the other, and neither can be grafted onto a caster,
since every caster is forged `Cast ×1`.

There is a levelling consequence too, and it points the same way. Both modifiers
reduce **mana moved**, which is enchantment XP (§3.3), so a weapon carrying both
would be the cheapest thing in the game to operate *and* the slowest to grow —
an extreme at both ends rather than a position on the trade §1.3 describes.

**`Riposte` is still outside the *threat-zone* group**, which is the distinction
the two block modifiers make clear. All four fire on the enemy turn, but `Brace`,
`Opportunist` and `Overwatch` answer *being approached* while `Riposte` and
`BlockWeaken` answer *being hit*. Grouping by when something resolves would put
all five together; grouping by what it responds to gives the two groups above,
and that is the useful cut.

**Crit riders are *not* a group — provisionally.** `CritWeaken` and `CritSunder`
share a trigger too, so the pattern above would suggest excluding them, and they
appear to fail both tests: they do not contradict (a crit can rattle a swing
*and* open a target up), and they cost no enemy-turn economy, since they ride an
attack already paid for on your own turn.

The reason to be unsure anyway is that the dagger's Control and Support variants
are exactly `CritWeaken` and `CritSunder`, so this is the *same* shape as the
sword's block responses — one trigger forked by who collects — and that shape got
a group. The tests above say leave it; the symmetry with the sword says group it.
Left open rather than resolved; see open questions.

**Requires — some modifiers depend on another being present:**

| Modifier | Needs | Without it |
| --- | --- | --- |
| `Riposte` | `Block` | Nothing to counter off |
| `BlockWeaken` | `Block` | Nothing to succeed at |
| `Rout` | `Cleave` | A strictly worse `Push` that also takes the displacement slot |

Each of those would otherwise be a **dead stack**, which §1.1 forbids — so they
are illegal rather than merely bad. Unlike `Excludes`, this relation is one-way:
`Block` needs nothing.

It also makes grafting **order-dependent**, which is worth having. A dagger can
never be offered `Riposte` — but a dagger that already grafted `Block` can, and
nothing is ever removed from a weapon, so a satisfied prerequisite stays
satisfied. Over enough services a weapon reaches spreads no single roll could
hand it.

**And three modifiers are restricted by weapon type:**

| Modifier | Allowed on | Because |
| --- | --- | --- |
| `Brace`, `Opportunist` | **Melee only** — dagger, sword, spear, axe | A threat zone is a weapon's physical reach; you cannot menace a tile with a bow |
| `Overwatch` | **Ranged only** — bow, throwing | Holding a shot is what a nocked arrow does; a spear cannot wait for a target to appear |

`Cast` is deliberately **not** on that list. It was caster-only while it scaled
an effect level, because a weapon with no effect had nothing to scale. Now that
it discounts *every* point of mana a weapon spends — its own cast cost and its
enchantments' trigger costs alike — any weapon carrying an enchantment has
something for it to work on, and the mana-efficient dagger is a build rather
than a category error. It stays **forged** on casters only, so on anything else
it is a graft: `forged = 0`, ceiling `×5`, −50% at the very deepest.

It is inert, not harmful, on a weapon with no enchantments at all — which §1.1
tolerates. `Charges` remains the one modifier that can actively make a weapon
worse; see open questions.

Together these give a rule worth stating on its own:

> **Every weapon holds exactly one reaction, and melee and ranged hold different
> ones.** A spear braces, an axe punishes leaving, a bow watches — and no weapon
> in the game does two of those.

That is what keeps the enemy turn readable. Reactions all resolve during the
opposing side's movement, so a front line able to stack three of them would turn
the enemy phase into a second player phase. Bounding it at the *weapon* rather
than with a per-character reaction budget means the limit is visible on the item
you chose, and needs no new resource to track.

The displacement group is the same idea pointed at coherence rather than budget:
`Rout` is `Push` applied to everything a cleave caught, so a weapon carrying
both is asking one hit to move a target two directions at once.

**A graft that would conflict is never offered.** For the enchanter (§6.4) that
means the graft roll picks only among modifiers the weapon can legally hold — a
spear is never offered `Opportunist`, a bow is never offered `Brace`.

For a **boss theme** it goes further, because a themed drop is a guarantee
rather than a roll: the boss's drop table simply **excludes the classes that
cannot carry its theme** (§4.3). A `Brace`-themed boss does not drop axes at
all, rather than dropping ungrafted ones. Every weapon a themed boss hands you
carries the theme, without exception — which is the promise §4.3 makes, and it
should not have a footnote.

`Light` needs its own limit because it is the one modifier measured against the
weapon's own cost, and a cost heading toward zero is a different problem from a
number merely getting large. A weapon *not* forged Light still reaches `×5` for
−50%; the limit only stops the discount stacking on top of a head start.

`Block` needed neither, and `CritWindow` no longer does — see §1.6 for the
first, where crits bypass Block entirely, and §1.2 for the second.

Values below are shown at `×5`, which is the reference point rather than the
ceiling — a forged spread reaches further.

| Modifier | Per stack | At ×5 | Notes |
| --- | --- | --- | --- |
| `Brace` | +1 retaliation | 5 | **Melee only**; threat-zone group |
| `Block` | +3 absorbed | 15 | Never reduces below 1 taken; **crits ignore it entirely** — §1.6 |
| `CritWindow` | **+1 to the window** | crit on 15+ | ×1 = 19–20, ×2 = 18–20 … ×8 = 12+ |
| `CritMultiplier` | +1 to the multiplier | ×7 | Base is ×2 with no stacks |
| `Cleave` | +1 extra target | 5 | |
| `Charges` | +1 throw per turn, on top of 1 | 6 | A **cap**, not a grant — see §1.2 |
| `Longshot` | +1 damage per tile | 5 | Beyond 3 tiles |
| `Light` | **−10% of the weapon's cost** | −50% | Additive, not compounding; **max forged ×1**, so it ceilings at ×6 |
| `Riposte` | +1 counter per turn | 5 | Requires `Block`; block-response group |
| `Push` / `Drag` / `Rout` | +1 tile displaced | 5 | Displacement group — one direction per weapon. Fires on any hit, however delivered |
| `Splitting` | +3 of the target's Block ignored | 15 | Matches `Block` stack for stack, but not ceiling for ceiling — open question |
| `Softening` | +3 of the target's Block stripped for a turn | 15 | Same per-stack value, but for everyone |
| `Pin` | +1 `Mire` level on the target | 5 | |
| `Overwatch` | +1 held shot | 5 | **Ranged only**; threat-zone group |
| `Opportunist` | +1 free attack when a target **leaves** your reach | 5 | **Melee only**; threat-zone group. Voluntary movement only — the mirror of `Brace`, §1.2 |
| `CritWeaken` | +1 `Weakened` level on a crit | 5 | Levels **accumulate**, uncapped — §1.6 |
| `CritSunder` | +1 `Sundered` level on a crit | 5 | Levels **accumulate**, uncapped — §1.6 |
| `BlockWeaken` | +1 `Weakened` level on a successful block | 5 | Requires `Block`; block-response group. Levels **accumulate**, uncapped — §1.6 |
| `Cast` | **−10% of all mana the weapon spends** per stack, proportional | 5 | Cast costs *and* enchantment triggers. Efficiency, not magnitude — §1.3. **Excludes `Light`** |

### `Light` has to be proportional, not flat

Attack costs span 20–60 across the classes, so a flat discount does not mean
the same thing twice. At a flat −15 per stack, a 30-cost dagger reaches zero at
×2 — three of its five stacks would do nothing, and a floored 10-cost dagger
against a 160 budget is sixteen swings a turn. Flat also converges every class
on the same floor, so a maxed dagger and a maxed axe would cost the same to
swing, erasing the cost structure that tells them apart.

At −10% per stack the discount scales with what it is discounting. A maxed
`Light ×5` halves any weapon: dagger 30 → 15, sword 50 → 25, axe 60 → 30. The
classes keep their relative footing at every stack count.

Percentages are harder to count with mid-turn, which is a real cost in this
game — so the **resolved** cost is computed once and displayed on the weapon.
The player reads `Cost 15`, never `30 −50%`.

**The general rule: a modifier acting on a value that varies across classes has
to be proportional.** `Light` and `Cast` are the two that do — `Light` on the
movement cost every class has, `Cast` on the mana cost only casters have (15–25
across the variants, so the same argument applies in miniature). The two are the
same modifier pointed at the game's two currencies, which is why they take the
same shape and the same −10%.

`Block` is flat by deliberate exception: absorbing 3 is meant to blunt
many small hits more than one large one, and the minimum-1 rule already stops
it running away.

The per-stack numbers above are first-pass targets, not tuned values.

### If it needs a cap, it is an enchantment

**A weapon modifier must be safe at its ceiling by construction** — and since
the ceiling is `forged + 5` and nothing is forged past `×3`, that means safe at
`×8`. Anything that would need a bespoke cap to stay sane does not belong in
this table at all; it belongs in the enchantment system (§3.3), which is a
separate mechanism with its own dials, bounded by a mana budget rather than by
stack counts.

That is a real dividing line, not a style preference. Every modifier above is
bounded by *something structural*: a per-turn count, a triggering condition, or
another modifier it is measured against (`Splitting` against `Block`). None of
them feed their own resource back into themselves. The two tools above are for
modifiers that are bounded, but by a quantity that runs out before the eighth
stack does — re-price the stack, or limit the forge. Never cap the ceiling.

A refund does. `Momentum` — movement returned per enemy killed — pays back into
the budget that bought the swing, so more kills buy more swings. No per-stack
value fixes that shape; capping it per swing would just be the bespoke ceiling
this rule exists to avoid. As an enchantment it self-limits: each trigger costs
mana, mana regenerates only from *unspent* movement, so a refund loop starves
itself. See Phase 3.

