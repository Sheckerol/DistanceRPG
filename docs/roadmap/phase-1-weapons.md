# Phase 1 — The martial and caster classes

> §1.2–1.5. Stack rules in [phase-1-modifiers](phase-1-modifiers.md); index in [ROADMAP.md](../../ROADMAP.md).

## 1.2 The martial classes

Written as stack operations on the base weapon. Every class follows the same
four rules; only the wildcard differs.

A class has a **baseline** — the modifiers every weapon of that class carries —
and each of its four weapons adds **exactly one more**, drawn from four fixed
roles:

| Role | Adds | Reads as |
| --- | --- | --- |
| **Efficiency** | `Light ×1` | Standard, but quick |
| **Purity** | Another stack of the class's signature | More of what the class *is* |
| **Control** | Something that degrades the enemy | The defensive-ish option |
| **Support** | Something that helps the rest of the party | The interesting one |

Two things this fixes over a rigid "crit-specced" slot. A class whose identity
*is* crit has nothing to gain from one — that belongs in its baseline instead.
And every class gets a **support** weapon, so each one has a reason to exist in
a party that already has damage covered.

### Every class has a second baseline modifier

**No weapon in the game is forged with fewer than two modifiers.** Every class
baseline is its signature *plus* a second, so the four roles resolve to one of
exactly two shapes:

| Role | Forged spread | Shape |
| --- | --- | --- |
| Efficiency | `signature ×1, second ×1, Light ×1` | three at `×1` |
| **Purity** | `signature ×2, second ×1` | one `×2`, one `×1` |
| Control | `signature ×1, second ×1, control ×1` | three at `×1` |
| Support | `signature ×1, second ×1, support ×1` | three at `×1` |

Three forged stacks either way, never on fewer than two modifiers.

**The mechanical reason is the farm.** A repeat-kill farm grants up to 10 stacks
and §1.1 caps each modifier at 5 acquired (§3.2), so a single-modifier weapon
could only ever absorb half of what farming offers. A Purity variant would have
been the one weapon in the game you could not fully invest in — precisely
backwards, since it is the one that most wants depth.

The second modifier is **class-specific**, and each one says something the
signature alone does not:

| Class | Baseline | The second says |
| --- | --- | --- |
| Dagger | `CritWindow ×1, CritMultiplier ×1` | Crit is two dials, and the dagger owns both |
| Sword & Shield | `Block ×1, Push ×1` | **A shield bash.** You stop them, then you move them |
| Spear | `Brace ×1, Longshot ×1` | Full extension is where a polearm wants to be |
| Axe | `Cleave ×1, Opportunist ×1` | **Nobody walks away from an axe** |
| Ranged | `Longshot ×1, CritWindow ×1` | An aimed shot finds the gap |
| Throwing | `Charges ×1, CritMultiplier ×1` | Throw enough and one lands perfectly |
| Staff / Wand | `Resonant ×1` + **one rolled enchantment** | The caster's second axis is not a stack at all |

Four of these change what the class *does*, not just its numbers:

- **`Push` gives the sword a positioning tool it completely lacked.** Every
  other martial class could move an enemy or refuse to be moved; the shield
  could only absorb. A hit that shoves means a sword-bearer can open a lane, peel
  something off a caster, or shove a target back into a spear's threat zone. It
  is also the exact fantasy — a shield is a thing you hit people with.
- **`Longshot` makes the spear want to fight at exactly its reach**, which is
  where its `Brace` threat zone already lives. The two reinforce: stand at full
  extension, threaten everything that closes, and hit hardest doing it. A spear
  that steps in loses both at once.
- **`CritWindow` gives the bow crit *frequency*** where the dagger has it too,
  which is what makes DEX read as the precision stat rather than merely the fast
  one. It is the one place another class shares the dagger's dial.
- **`Opportunist` answers the axe's actual weakness.** `Cleave` rewards being
  surrounded, but nothing stopped an enemy simply walking out of a 60-unit reach
  that costs 60 to swing. Now leaving is what gets you hit.

`CritMultiplier` is the fallback where no better answer exists — throwing — and
it is not a filler pick. §1.6 makes crits bypass `Block` entirely, so a natural
20 is a way through armour, and at `×3` rather than `×2` that answer actually
lands. The axe does not need it: `Splitting` is already its answer to armour
(§1.2), which is why it can afford something stranger.

### The casters' second axis is an enchantment

Staves and wands satisfy the two-axis rule without a second modifier: **every
caster weapon drops carrying an enchantment** (§3.1), and farming can put its
ten points into `Resonant` stacks or into that enchantment's levelling (§3.2). The
allowance has somewhere to go, which is all the rule ever asked for.

The two classes hold it differently, and the difference is each class's
identity kept in the field that suits it:

| | The enchantment is | Rolled? |
| --- | --- | --- |
| **Staff** | The effect it casts — Poison, Ward, Mire, Regeneration (§1.3) | **No.** Fixed by variant |
| **Wand** | Its damage type — Flaming, Shocking, Acidic, Cold (§1.4) | **Yes.** Uniform among four |

A staff *is* its effect, so rolling it would make a Staff of Blight stop being
one. A wand is its **shape**, which leaves the damage free to vary — so the four
geometries stay stable and the element is what makes them feel new. And because
the type chart (§1.4) makes elements situational rather than ranked, a rolled
one is never a bad wand, only a wand for a different dungeon.

`CritMultiplier` was the previous answer here and it was the wrong one for a
class that does not attack. A staff *applies an effect*; multiplying it on a
natural 20 is a rounding event on 5% of casts, not an identity. The
enchantment is the identity — it is the only slot in the game where what a
weapon does is rolled rather than fixed by its class, which is exactly right for
the classes whose four variants already differ by *effect* rather than by role.

It also makes the caster the class the whole enchantment system was built for
(§3.3) rather than merely its best user. A wizard's staff arrives with a soul
already in it; every other class has to earn one at the enchanter.

The trade is downtime. Service time is enchantment count + 1 (§6.2), so a
caster's first visit costs two runs where a fighter's costs one. Casters are
pre-loaded and pay for it in time — which is also the only cost in the game
that scales with nothing you can farm.

**The caveat §1.2 carries still applies.** Caster variants differ by effect and
shape rather than by adding a stack, so a caster carries one forged stack and
one forged enchantment rather than three stacks.

**Cost columns below are base costs.** An Efficiency weapon shows the same cost
as its base row — `Light ×1` resolves it down by 10% (§1.1). The statline is
what the weapon is; modifiers are what happens to it.

### Attack cost tracks weight

| Class | Cost | |
| --- | --- | --- |
| Throwing | 15 | A flick of the wrist, and hard-capped by Charges |
| Dagger | 30 | Light, fast |
| Ranged | 30 | Drawing a bow is not heavy work |
| Sword & Shield | 50 | A shield is most of that number |
| Spear | 55 | A long polearm is slow to bring to bear |
| Axe | 60 | Heaviest thing anyone swings |

Spear at 55 against bow at 30 is a **correction**: the spear was 40 and the bow
45, which had a heavy polearm swinging faster than an archer looses. Weight
should read in the movement cost, since movement is what the game is about.

The spear's damage is 7 and it now costs 55, so it swings twice a turn for 14 —
which is correct rather than broken. **A spear is not a weapon you attack
with, it is a weapon you threaten with.** Its value is `Brace` firing for free
on the enemy turn and 128 units — **exactly four tiles** — of reach; the swing
is the fallback. High cost makes that identity explicit instead of leaving the
spear a cheap poking stick that happens to brace.

The reach is pinned at 128 rather than the prototype's 130 so that "four tiles"
is exact rather than approximate. `Longshot` keys off tile counts (§1.1) and the
spear's baseline now carries it, so the difference between 4.0 and 4.06 tiles is
the difference between a rule and a rounding artefact.

Throwing at 15 undercuts the bow despite a javelin outweighing an arrow — a
throw is a quicker action than nocking, drawing and aiming, and the Charges cap
is what keeps it honest. Uncapped, that price would be indefensible.

### Dagger (DEX) — baseline `CritWindow ×1, CritMultiplier ×1`

Crit is the class, so both of its dimensions live in the baseline: 19–20 to
crit, and ×3 when it lands.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Flensing Knife | 40 | 15 | 30 | `Light ×1` |
| Purity | Assassin's Fang | 40 | 15 | 30 | `CritWindow ×1` → 18–20 |
| Control | Disarming Kris | 40 | 15 | 30 | `CritWeaken ×1` |
| Support | Weakspot Stiletto | 40 | 15 | 30 | `CritSunder ×1` |

- **Disarming Kris** — crits apply `Weakened`, cutting what the target deals.
  Defensive: you crit to stop being hit back.
- **Weakspot Stiletto** — crits apply `Sundered`, so *everyone* hits that
  target harder. The dagger stops being a damage weapon and becomes a setup
  weapon; A crits to open a target and C's axe cashes it in across the cleave.

**The Kris and the Stiletto are exclusive** (§1.1, crit-rider group), so neither
can ever become the other. That is the same treatment the sword's `Riposte` and
`BlockWeaken` get, and for the same reason: one trigger forked by who collects
the payoff, which stops meaning anything if one weapon can hold both. A dagger
picks whether its crits protect *it* or set up the *party*, once, at the forge.

**The baseline is `CritWindow ×1` — crit on 19–20**, a deliberate break from
the prototype's 16+ (`CombatRulesTests.cs:28`).

### Crit frequency is narrow in practice, not by rule

`CritWindow` scales like everything else — `forged + 5`, no cap (§1.1). The full
range it can reach:

| Weapon | Forged | Crits on | Rate |
| --- | --- | --- | --- |
| Sword, spear, axe, throwing, casters | — | 20 | 5% |
| Dagger or bow baseline | `×1` | 19–20 | 10% |
| Assassin's Fang, Longbow | `×2` | 18–20 | 15% |
| Widowmaker, Stormcrow | `×3` | 17–20 | 20% |
| Assassin's Fang, fully worked | `×6` | 14+ | 35% |
| **Widowmaker, fully worked** | `×8` | 12+ | **45%** |

`CritWindow` is forged on **daggers and bows only** — the two DEX classes — so
crit frequency is what DEX buys. Everything else crits on a natural 20 and no
amount of farming or service changes that, since the roll only deepens modifiers
a weapon already carries (§3.2) and a graft caps at `×5` (§6.4).

**The band that matters is the top half of that table, and almost nobody sees
it.** Every stack past the forged spread has to be won one at a time from the
enchanter: a low chance per service, scaled by wear, landing on `CritWindow`
rather than on the two or three other modifiers a crit weapon carries. Five of
those in a row, on the same weapon, kept in rotation across dozens of runs.

So the 45% dagger is not a build you pick — it is an **artefact of a campaign**,
and one weapon's worth of one. For essentially the whole game a crit is a 10–20%
event and the d20 keeps mattering exactly as intended. Capping the window would
have bought that guarantee at the price of telling a player who ground for forty
runs that the thing they were grinding toward was not allowed to exist.

The investment axis is still `CritMultiplier`, which scales the same way. A
weapon forged `CritMultiplier ×1` ceilings at a ×8 multiplier; only a unique
that raised `CritMultiplier` instead of its own signature reaches ×10.
**Frequency and magnitude are therefore separate uniques**, and no single weapon
is forged deep in both.

Note the asymmetry that creates. `CritMultiplier` is forged on the dagger and
throwing baselines and reachable by graft everywhere else, so any class *can* be
taken deep on crit damage — but only at the graft's shallow `×5`, and only if
the rare service roll lands there. `CritWindow` is narrower still, forged on
daggers and bows alone, so only DEX can be taken deep on crit *frequency*. An
axe worked to a ×8 multiplier still only crits on a 20; it just removes a room
when it does.

So a crit build is not "crit constantly" but **"crit rarely and
catastrophically"** — and the two dagger specialists split exactly along that
line — Assassin's Fang buys frequency, the baseline `CritMultiplier` carries
magnitude.

Starting at `×1` rather than the prototype's `×4` also leaves the dagger seven
stacks of headroom for depth, grafts and services to work with, instead of one.

### Sword & Shield (max STR/DEX) — baseline `Block ×1, Push ×1`

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Arming Sword | 80 | 10 | 50 | `Light ×1` |
| Purity | Tower Guard | 80 | 10 | 50 | `Block ×1` → absorbs 6 |
| Control | Riposte Blade | 80 | 10 | 50 | `Riposte ×1` |
| Support | Warden's Shield | 80 | 10 | 50 | `BlockWeaken ×1` |

Both trigger off the same event — a successful block — and split cleanly on who
collects:

- **Riposte** — the block grants a free counter-swing. Turns mitigation into a
  threat, and the payoff is yours.
- **BlockWeaken** — the block applies `Weakened` to the attacker, so it deals
  less to *everyone* afterwards. The same effect the dagger's `CritWeaken`
  applies, on a different trigger.

**Why that is support and the dagger's is control.** A shield-bearer already
mitigates incoming damage with Block, so weakening their attacker barely
improves their own position — it improves the position of whoever gets hit next
without a shield. On a dagger, with no Block behind it, weakening is
self-defence. Same effect, opposite role, decided by who carries it.

It is also the one weapon whose added modifier depends on the class baseline:
`BlockWeaken` cannot fire without a `Block` to succeed at — which §1.1 now
enforces as a prerequisite rather than leaving to convention.

**And a weapon can never hold both.** They are one exclusion group (§1.1), so a
Riposte Blade can never be serviced into a Warden's Shield and no drop combines
them. That is the point: the fork above is only a fork if you have to choose.

**Both have the same blind spot.** Crits bypass Block entirely (§1.6), so a crit
is not a successful block and neither `Riposte` nor `BlockWeaken` triggers on
one. A crit-heavy attacker beats the whole class rather than just its
mitigation, which is the price the sword pays for scaling to `Block ×8`.

### The shield bash is the class's second half

Baseline `Push ×1`: **a sword hit shoves the target back a tile per stack.**

Before this the shield could only ever say no. Every other martial class had a
way to change where an enemy stood — the spear pushes and pins, the axe routs, a
thrower drags — and the one class built to stand in front of things could not
move them at all. `Push` makes the sword an active line rather than a wall:
shove a target off a caster, open a lane through a doorway, or drive something
back into a spear's threat zone so the brace fires on the way in.

It also pairs with `Block` in the obvious way. You absorb the hit, then you
answer it — and unlike `Riposte` and `BlockWeaken`, `Push` does not need the
block to have *succeeded*, so it is the one part of the sword's kit that still
works against a crit-heavy attacker.

### Spear (STR) — baseline `Brace ×1, Longshot ×1`

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Skirmisher's Pike | 128 | 7 | 55 | `Light ×1` |
| Purity | Phalanx Spear | 128 | 7 | 55 | `Brace ×1` → 2 retaliations |
| Control | Halberd | 128 | 7 | 55 | `Push ×1` |
| Support | Pinning Lance | 128 | 7 | 55 | `Pin ×1` |

The spear is the class that decides **where enemies are**, and its two
non-baseline tools are exact opposites:

- **Push** — a braced hit shoves the target back out of its own reach. Breaks
  contact. `TryBracesAgainst` already resolves braces mid-walk, so the hook
  exists.
- **Pin** — a braced hit applies `Mire`, draining the target's movement budget.
  Refuses to let contact break. Catching an enemy on the point and leaving it
  stuck there is what lets the rest of the party disengage and kite, which is
  why this is the spear's *support* weapon even though it reads as control.

Pin applies the existing `Mire` effect (§1.3) rather than inventing its own, so
it reuses machinery already being built.

### `Longshot` on a four-tile weapon is a positioning rule, not a curve

Baseline `Longshot ×1`, and the spear's reach is exactly four tiles. `Longshot`
pays `+1` per stack per tile beyond three, so on a spear **only the final tile
qualifies**: fight at full extension and every stack pays, step in and none of
them do.

That is a feature rather than a degenerate case. On a bow `Longshot` is a curve
you optimise across a whole room; on a spear it collapses into a single binary
question — *am I at reach?* — which is exactly the question the class already
asks. `Brace` threatens the tile an enemy must cross; `Longshot` pays you for
standing where that threat lives. **A spear that closes loses both at once**,
and a spear that holds its distance is doing the two things it is for
simultaneously.

The numbers stay modest at first — `+1` on a 7-damage poke — but the farm
compounds it hard. A spear worked to `Longshot ×6` deals 13 at reach and 7 in
melee, which turns a rounding error into the whole reason to play the class
carefully.

Note this makes the spear the only weapon whose two baseline modifiers want the
*same* position. Most classes trade between their dials; the spear's reinforce,
which is what makes it the least flexible and most decisive martial class.

### Displacement triggers threat zones

**Yes — shoving an enemy into a spear's reach gives the spear a free attack.**

A threat zone does not care *why* a body entered it. `Brace` and `Overwatch`
fire on a target entering a threatened tile, and forced movement from `Push`,
`Drag` or `Rout` is movement. Mechanically it routes through the same
`NotifyCharacterMoved` path a voluntary walk does, tile by tile, so every zone
crossed fires exactly as §4.4 already describes for enemies running past a
spear line.

The precise rule, since the edges matter:

| Situation | `Brace` fires? |
| --- | --- |
| Displaced **into** a threat zone it was outside | **Yes** |
| Displaced **through** two zones on a multi-tile shove | Yes, both |
| Displaced **further away** while already inside a zone | No — it is leaving, not entering |
| Displaced by an **ally** of the bracer | No — threat zones only face the other side |

`Opportunist` takes the opposite convention and fires on **exit, only when
chosen** — forced movement never provokes it. The reasoning is in the axe
section below; the short version is that `Brace` is about dangerous ground and
`Opportunist` is about turning your back, and only one of those is a decision.

No weapon holds both, and none holds an `Overwatch` alongside either: the three
are one exclusion group, and `Brace`/`Opportunist` are melee-only while
`Overwatch` is ranged-only (§1.1). **One weapon, one threat zone** — which is
what stops a front line turning the enemy phase into a second player phase.
`Riposte` sits outside that group deliberately, since it answers being hit
rather than being approached.

That third row is what keeps `Push` coherent. The Halberd's whole purpose is
shoving a target *out* of its own reach to break contact (§1.2), and if leaving
a zone fired braces, the spear's own control weapon would punish the spear for
using it.

**This is the best party-composition combo in the game**, and it falls out of
rules that already existed rather than needing new ones:

- **Sword shoves, spear pokes.** A sword-bearer hits something on open ground
  and drives it four tiles back into the party's spear line. The spear gets a
  free attack it did not spend movement on, and it happens on *your* turn rather
  than waiting for the enemy to walk in.
- **Drag is the inverse and better for it.** A thrower's `Drag` pulls a target
  *toward* the party — which is toward your spears. A ranged class setting up a
  melee class's free attacks is exactly the kind of reason a party wants both.
- **Rout is the crowd version.** A Routing Axe displaces everything its cleave
  caught, so one swing beside a spear line can fire several braces at once.

It runs both ways, of course. An enemy sword-dummy can shove a character into an
enemy spear-dummy's reach, which is the same lesson §2.1 wants the tutorial to
teach from the receiving end.

**Nothing here can loop**, because the existing per-turn brace budget bounds it:
each character's `Brace` value is uses per turn (§1.2), spent whether the
trigger was a walk or a shove. A brace that itself displaces — a Halberd
retaliating — can chain into another zone, but every link consumes a use from a
finite pool, so the chain terminates on its own. That is worth stating rather
than discovering: it is the only place in the design where one action can cause
an unbounded-looking cascade, and the thing that stops it is a budget that was
put there for another reason entirely.

### Axe (STR) — baseline `Cleave ×1, Opportunist ×1`

One swing hits every valid target in range, paying the movement cost once.
Rooms already hold 0–4 dummies and nothing today rewards being surrounded.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Hatchet | 60 | 18 | 60 | `Light ×1` |
| Purity | Great Axe | 60 | 18 | 60 | `Cleave ×1` → 2 extra targets |
| Control | Reaver | 60 | 18 | 60 | `Splitting ×1` |
| Support | Routing Axe | 60 | 18 | 60 | `Rout ×1` |

- **Splitting** — ignores 3 of the target's Block per stack. Axes split
  shields, and it is measured stack for stack against the thing it counters
  rather than against an arbitrary number — though the two no longer share a
  ceiling now that caps scale with the forged spread (see open questions). This
  is the weapon the tutorial dungeon exists to teach you to want (§4.3).
- **Rout** — everything caught by the cleave is pushed back a tile per stack.
  One swing that resets a whole crowd's position buys the entire party room.

### `Opportunist` — nobody walks away from an axe

Baseline `Opportunist ×1`: **a free attack when a target leaves your reach**,
one per stack per turn. It is the exact mirror of the spear's `Brace`, which
fires on *entry*, and it is what the axe was missing.

Read the axe's statline back and the hole is obvious. Reach 60 — under two
tiles, the shortest of any martial weapon. Cost 60, the highest in the game, so
two swings is a whole turn and there is nothing left to chase with. `Cleave`
rewards being surrounded, but an enemy who simply *walked away* paid nothing at
all, and the axe could not follow. The class that most wants a crowd had no way
to keep one.

Now it does, and the pairing with the spear is the good part:

| | Punishes | Reach |
| --- | --- | --- |
| Spear — `Brace` | Coming **in** | Four tiles |
| Axe — `Opportunist` | Going **out** | Under two tiles |

A spear and an axe standing together is a **cage**: wide threat on approach,
tight threat on escape. Getting to the axe costs you, and leaving costs you
again. Neither class had to be redesigned for that — it is just the two halves
of one idea handed to the two classes that wanted them.

**It fires on voluntary movement only.** An enemy shoved out of reach by a
`Push`, `Drag` or `Rout` has not disengaged; it has been moved. That is the
opposite convention to `Brace`, deliberately:

> `Brace` fires on **entry, however caused** — reach is dangerous ground and it
> does not care why you crossed it.
> `Opportunist` fires on **exit, only if chosen** — it punishes turning your
> back, and a body that was thrown made no such decision.

Three things fall out of that, all good:

- **The Routing Axe does not detonate itself.** `Rout` shoves the whole cleave
  out of reach, and if forced movement provoked, one swing would trigger the
  wielder's own opportunity attacks on everything it just displaced. Instead the
  two pull against each other honestly: a Routing Axe wants space, a baseline
  axe wants them stuck, and you pick.
- **Displacement becomes the safe way to break contact.** Walking away from an
  axe costs you a hit; being pushed away does not. So the spear's Halberd and
  the thrower's Harpoon are now *answers* to sticky melee, which is a use for
  displacement nobody designed in on purpose.
- **It is a convention players already know** from tabletop, so the asymmetry
  reads as familiar rather than fiddly.

And it runs both ways. An axe dummy makes disengaging expensive, which is the
first real cost the party has ever paid for kiting — currently free (§1.2). A
party that wants out of an axe's reach has to spend a `Push` or take the hit.

### Ranged (DEX) — baseline `Longshot ×1, CritWindow ×1`

**Longshot**: damage rises with distance to the target — `+1` per tile beyond
3 tiles, per stack. A bow in the front rank is nearly useless; the same bow
across a room is devastating. Most on-theme ability in the game, and it makes
the marching formation a genuine trade-off.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Hunting Bow | 320 | 5 | 30 | `Light ×1` |
| Purity | Longbow | 320 | 5 | 30 | `Longshot ×1` → +2 per tile |
| Control | Pinning Bow | 320 | 5 | 30 | `Pin ×1` |
| Support | Crossbow | 320 | 5 | 30 | `Overwatch ×1` |

- **Pin** — the same `Mire` application as the spear's, delivered by a hit
  rather than a brace. A bow that stops an enemy closing is a bow that never
  has to stop shooting, and it is the other half of the kiting pair.
- **Overwatch** — bank the shot instead of firing; if an enemy enters line of
  sight during the enemy turn, it fires for free. A ranged mirror of Brace,
  reusing the threat-zone machinery Phase 0 unified, and it covers the party's
  approach rather than your own.

**Damage 5 is meant to look low.** A bow held at knife range should be bad —
that is what `Longshot` *is*. The answer to an enemy in your face is not a
stronger bow, it is a different weapon, so the baseline stays weak and the
range curve does the work.

### Baseline `CritWindow ×1` — the aimed shot

The bow is the only class besides the dagger forged with crit *frequency*, and
that is what makes DEX the precision stat rather than merely the fast one. A
drawn bow is aimed in a way a swung axe is not.

Mechanically it compounds with `Longshot` rather than sitting beside it. Crit
damage multiplies the *resolved* number (§1.6), and the resolved number is
whatever distance has already made it — so a 19–20 at the far end of a room is
the biggest single hit an ordinary weapon produces. A Longbow at ten tiles deals
`5 + 14 = 19`, and a crit takes it to 38 with `Block` ignored entirely.

That is the ranged class's whole answer to armour, and it is a positional one:
the archer who backed up is also the archer who crits for meaningful damage. It
is worth watching in play — see the open questions, since `Longshot` and
`CritWindow` deepening together is the sharpest compounding pair in the game
outside the dagger's two crit dials.

### Every stat owns a close option and a ranged one

That answer only holds because swapping never costs you *progression*. Weapon
XP is per class per character but scales off the class's governing stat (§2.2),
so a character swapping inside their own stat levels both weapons at the same
rate:

| Stat | Close | Mid | Far |
| --- | --- | --- | --- |
| DEX | Dagger 40 | *(Sword 80)* | Ranged 320 |
| STR | Axe 60 | Spear 128 | Throwing 190 |
| INT | Enchanted dagger | Staff 100, Wand ~160 | Enchanted bow |

**INT solves range by borrowing, not by owning.** Enchantments are
class-agnostic and their potency scales with INT rather than with weapon
proficiency (§3.3) — so a high-INT character picks up a dagger or a bow, hangs
an elemental enchantment on it, and plinks away at a level their dagger
proficiency could never justify. The weapon is a delivery system; the damage is
theirs.

That is why INT has no martial classes of its own and does not need any. It is
the same mechanism as the close-range wizard build, pointed at the range
problem instead of the damage one.

The 6-slot inventory (§4.4) exists partly for this: a loadout, not a weapon.

### Swapping costs movement once you are in a fight

Free swapping would make every weapon's downside optional — not just the bow's
range curve, but the spear's 55 cost and the axe's 60 reach. Nothing you chose
would ever have to be lived with.

So a swap costs **20 movement**, more than a throw and less than a dagger
swing, once combat has begun. Out of combat it is **free**: the game already
tracks `AnyLiveEnemySeenThisTurn` (`TurnSystem.cs:52`), the same signal that
grants marching, so loadout management between fights stays frictionless while
mid-fight swapping is a real decision.

The cost compounds properly. A bow user caught at knife range pays 20 to swap
and 30 to swing — 50 for the first hit — which is exactly the punishment for
having been caught.

**Consumables cost movement too**, on the same principle: anything that changes
your situation mid-turn should be paid for out of the same budget as moving and
swinging. See §3.4.

### Throwing (STR) — baseline `Charges ×1, CritMultiplier ×1`

**Charges is a cap, and the cap is the point.** A throw costs only 15, so the
movement budget alone would allow ten a turn — absurd. Charges is what bounds
that: it grants **one throw, plus one per stack**, so `×1` permits 2. Raising it
is genuinely the class feature, because the cap rather than the budget is what
binds.

Getting this backwards is easy and worth stating plainly: at a *normal* attack
cost the budget already caps you at four or five swings, so a "throws per turn"
limit would be a restriction rather than a feature. Cheap throws are what make
the cap the interesting number.

| Charges | Throws | Movement spent | Damage | Movement left |
| --- | --- | --- | --- | --- |
| ×1 | 2 | 30 | 18 | 130 |
| ×2 | 3 | 45 | 27 | 115 |
| ×5 | 6 | 90 | 54 | 70 |
| ×7 — a Bandolier at its ceiling | 8 | 120 | 72 | 40 |
| ×8 — Feathered Death at its ceiling, the deepest `Charges` in the game | 9 | 135 | 81 | 25 |

**The per-stack value is chosen so the cap stays inside the budget at every
reachable stack count** (§1.1). At the old `+2` per stack, the deepest spread
would have permitted 16 throws for 240 movement against a budget of 160 — five
of them unthrowable, which would have made the last three stacks of the class's
own signature worthless. `+1` keeps the shipped baseline of 2 throws and lands
the deepest weapon in the game at 9 throws for 135, just inside.

That is deliberate rather than lucky: `Charges` is the one modifier measured
directly against the movement budget, so if the budget or the throw cost ever
moves, this number moves with them.

The class identity is **attack and still have movement left** — the only weapon
that does not force a choice between fighting and repositioning. That gives
STR characters a way to land many hits per turn the way DEX characters do, and
it is what a full kiting party is built around.

Leftover movement is not dead weight here. Half of it banks into next turn
(`EndTurnSaveMovement`) and it regenerates mana
(`RegenManaFromUnusedMovement`), so a low-Charges thrower **fights while
fuelling mana** — feeding enchantment triggers (§3.3) and the mana XP pool
(§2.2).

Which makes the stack count a real build decision rather than a straight
upgrade: low Charges is an economy weapon that keeps your enchantments firing,
high Charges converts that economy into raw damage. It is the only class where
stacking the signature changes what the weapon is *for*.

| Role | Name | Range | Dmg | Cost | Adds |
| --- | --- | --- | --- | --- | --- |
| Efficiency | Darts | 190 | 9 | 15 | `Light ×1` |
| Purity | Bandolier | 190 | 9 | 15 | `Charges ×1` → 3 throws |
| Control | Harpoon | 190 | 9 | 15 | `Drag ×1` |
| Support | Softening Javelins | 190 | 9 | 15 | `Softening ×1` |

- **Drag** — a hit pulls the target *toward* the thrower, the exact inverse of
  the halberd's Push, setting up your own axe and sword line.
- **Softening** — a hit strips 3 of the target's Block per stack for a turn.
  Deliberately distinct from the axe's `Splitting`: Splitting lets *your*
  weapon through, Softening opens the target up for **everyone**. Same
  ceiling, opposite beneficiary — the Control/Support line exactly.

### Caster classes do not take this frame

Staff and wand both carry baseline `Resonant ×1` plus a rolled enchantment (§1.2),
but their four weapons differ by **effect** and **shape** rather than by role
(§1.3, §1.4). Forcing them into
Efficiency / Purity / Control / Support would be redundant — a debuff staff is
already control and a buff staff already support, and "more of the signature"
means nothing when the signature *is* which effect you cast.

## 1.3 Staff — four effects, half buffs and half debuffs (INT)

Each staff casts a *different* effect. All share Range 100 and Cost 40; they
differ in what they apply and what it costs in mana.

| Variant | Name | Target | Mana | Applies |
| --- | --- | --- | --- | --- |
| Buff | Staff of Renewal | Ally | 15 | **Regeneration** — heals per turn, decaying |
| Buff | Staff of Warding | Ally | 20 | **Ward** — a pool absorbing 1 per point, decaying 1 a turn (§3.3) |
| Debuff | Staff of Blight | Enemy | 20 | **Poison** — damage per turn, decaying |
| Debuff | Staff of Mire | Enemy | 25 | **Mire** — cuts the target's movement budget |

Staff of Renewal is today's shipped Staff with its numbers unchanged, so
parity holds.

### `Resonant` is efficiency, not magnitude

**`Resonant ×n` discounts every point of mana the weapon spends by 10% a stack** —
its own cast cost *and* its enchantments' trigger costs — proportional exactly
as `Light` is on movement (§1.1). It is not an effect multiplier and never was a
good one: with tier now carrying the effect's power (§3.3), having `Resonant` do
magnitude too would be two dials on one number.

Because it covers triggers, `Resonant` is **not caster-only**. A dagger carrying
Arcane spends mana every hit, and a stack that makes those hits cheaper is
exactly as meaningful there as on a staff — so the wizard's dagger (§3.3) can be
grafted toward mana efficiency, at a graft's shallow `×5`. It stays *forged* on
casters alone.

**It excludes `Light`** (§1.1). Mana comes back from movement left unspent, so
cheap movement is already cheap mana by the long route; a weapon with both would
compound one discount with itself. You pick which currency your weapon is cheap
in — and a mana-efficient dagger and a light dagger are two different weapons.

The division is clean, and the two halves come from different places:

| | Sets | Earned by |
| --- | --- | --- |
| **Enchantment tier** | How hard the effect lands | Casting — mana spent (§3.3) |
| **`Resonant` stacks** | What the cast costs | Forged, farmed, grafted (§1.1) |

A Staff of Mire at `Resonant ×5` costs 12 mana instead of 25. Same Mire. At its
`×6` ceiling it costs 10 — and `×6` is the ceiling, because `Resonant` is
forge-limited to `×1` exactly as `Light` is (§1.1), so five acquired stacks is
all the headroom there is.

### An efficient staff is harder to level, and that is the point

The two dials pull against each other, and the reason is the **mana loop**, not
a threshold. Mana comes back only from movement left unspent at end of turn
(`PartyMemberState.RegenManaFromUnusedMovement`), and every enchantment trigger
costs mana (§3.3), so over any sustained fight:

```
mana spent per turn  =  mana regained per turn  =  r × (160 − 40k)
```

where `k` is how many times you cast and `r` is `1 / MovementUnitsPerMana`
(below). Read that line
carefully, because it settles the question on its own: **the more you cast, the
less mana you can spend.** Casting consumes the movement that pays for casting.

`Resonant` raises the `k` you can afford. It therefore *lowers* the right-hand
side — and the right-hand side is the enchantment's XP (§3.3).

| | `Resonant ×1` | `Resonant ×6` |
| --- | --- | --- |
| Mana per cast (staff, base 20) | 18 | 8 |
| Sustainable casts a turn, at `MovementUnitsPerMana 10` | 0 | **1** |
| **Rounds of full 4-cast output from a 160 pool** | ~1.5 | ~3.5 |

**An efficient casting tool is genuinely harder to level up, and gives you more
for every point you spend.** That is a real trade rather than a strictly-better
modifier, and it is the first place in the design where a modifier has a
downside written into it rather than merely an opportunity cost.

**There is no crossover to tune.** An earlier draft framed this as a caster
switching from mana-bound to movement-bound at some stack count, which made the
whole trade hostage to a threshold nobody had placed. The steady state above is
**monotone**: at every stack count, more efficiency means more casts and less
mana through the enchantment. `MovementUnitsPerMana` decides *where* the sustainable
`k` lands, never whether the trade exists.

It survives the lazy case too. A player who ignores the extra capacity and casts
the same `k` as before simply spends less mana for the same actions — less XP
again, by the other route. There is no way to hold `Resonant` and level at the
old rate.

Two things had to be true for that to hold, and both are now rules rather than
happy accidents:

- **Every trigger costs mana** (§3.3). One free-firing enchantment would level
  on hits rather than on mana, and this whole argument would have a hole in it
  shaped exactly like that enchantment.
- **`Resonant` is forge-capped at `×1`, ceiling `×6`** (§1.1). The discount tops
  out at 60%, so the sustainable `k` climbs by about one cast rather than
  running away — the trade stays a trade instead of becoming a wall at the deep
  end.

It also splits the caster into two coherent builds:

- **The cheap staff** casts constantly, holds the fight together, and grows a
  large *pool* — its mana XP still feeds max mana (§2.2), which is the ladder
  efficiency does help. Its effect stays shallow for a long time.
- **The expensive staff** casts less and each cast is an event, but every one of
  them banks the full 18 toward tier. Its effect gets deep fast, and it spends
  more of its turns walking.

Neither is the upgrade. And the wrinkle that keeps it from being a simple pick
is that the pool you grow is also the pool your locks eat (§3.3) — so the cheap
staff builds the capacity to *carry* deep enchantments at the same rate it fails
to level them.

### The regen rate is a divisor, and it is derived rather than guessed

The constant is **`MovementUnitsPerMana`** — how much banked movement buys one
mana — expressed as an integer divisor rather than a fractional rate, so the
file reads correctly and nothing drifts:

```
manaRegained = unspentMovement / MovementUnitsPerMana
```

An earlier draft assumed roughly *one mana per unit*, which is a
units-versus-tiles error of the exact kind the standing constraints warn about:
movement is in logic units at **32 per tile**, so "1 per point" means 32 mana a
tile and a fully banked turn paying 160. That is not a trickle, it is a second
income.

**The value comes from one calibration target**, which is worth stating as the
rule rather than the answer, so it re-derives when anything it depends on moves:

> A caster at **`Resonant ×6`** carrying a **tier-1 enchantment** should sustain
> **one cast a round indefinitely** while standing still.

```
MovementUnitsPerMana = (movementBudget − castCost) / (castMana + triggerMana, at ×6)
                     = (160 − 40) / (8 + 3)
                     = 120 / 11  ≈ 10.9   →   10
```

**`MovementUnitsPerMana = 10.`** A fully banked turn is 16 mana; a turn spent on
one cast banks 120 movement and pays 12, against 11 spent. The fully-resonant
caster runs a hair above break-even, forever, which is exactly the target.

### The cliff at `×6` is the point, not an artefact

Run the same sum at shallower `Resonant` and nothing else sustains:

| `Resonant` | Cast + trigger | Regen while casting once | Sustains? |
| --- | --- | --- | --- |
| `×1` | 18 + 7 = 25 | 12 | No |
| `×3` | 14 + 6 = 20 | 12 | No |
| `×5` | 10 + 4 = 14 | 12 | **Nearly** |
| **`×6`** | **8 + 3 = 11** | **12** | **Yes** |

So **sustained casting is the reward for maxing `Resonant`**, and `×6` needs
five acquired stacks — a campaign's worth of services (§6.4). Everything below
it buys *sprint length* rather than sustain, which is still worth having and is
a different thing to want.

That is a sharp edge rather than a gradient, and deliberately so. "The staff
that never stops" is a legible endgame achievement in a way "the staff that
stops 18% later" is not.

### Casters are sprinters

This is the shape the whole caster economy is built to produce, and it is worth
naming because every constant above serves it:

| | Burst | Sustained | Reload |
| --- | --- | --- | --- |
| **Caster** | 4 casts a round while the pool lasts | 1 a round at `×6`, none below | **~10+ turns** to refill |
| **Martial** | Whatever the movement budget allows | The same, every round | **1 turn** — movement refreshes |

A caster **unloads**, then walks. A martial class fights at one rate forever.
Neither is stronger; they are shaped differently, and the caster's shape is the
one that has to be *timed*.

Three consequences fall out, none of which needed a rule:

- **`Resonant` buys sprint length before it buys sustain.** At `×1` a 160-mana
  pool is about 1.5 rounds of full output; at `×6` it is nearer 3.5. The
  fully-resonant caster both sprints longer *and* never fully stops.
- **Arriving full is the caster's preparation.** Mana banks from unspent
  movement, so the walk to the fight *is* the reload — and marching (§4.1),
  which the party does when no enemy is visible, is when it happens. A caster
  dragged into a fight straight after another one is a caster with nothing.
- **It sharpens the `Resonant` trade rather than softening it.** Efficiency
  extends the sprint and lowers the mana moved per round, so the staff that
  fights longest is still the staff that levels its enchantment slowest (§3.3).
  The sprinter shape does not rescue an efficient caster from that; it just
  makes the sprint the thing they are buying.

**The pool is the other half of this and is not yet fixed.** How long a sprint
lasts is `maxMana / spendPerRound`, so the starting pool decides whether a
caster gets one dramatic round or four. `MovementUnitsPerMana` only sets the
*floor* they fall back to.

### The effect *is* the staff's enchantment

**Which** effect a staff casts is not a field and not a modifier — it is the
weapon's innate enchantment (§3.1), fixed by variant rather than rolled. Staff
of Blight always carries Poison; Staff of Renewal always carries Regeneration.

That is worth more than it first looks:

- **The staff levels itself.** Enchantment tier is earned by mana spent through
  it (§3.3), and casting a staff is exactly that. Every cast makes the effect it
  applies land harder, without touching the staff's stats or its proficiency. A
  staff you use is a staff that grows.
- **`Resonant` and tier are two different dials on the same number, and they come
  from different places.** `Resonant` stacks are forged, farmed and grafted — the
  weapon getting better. Tier is cast for — the *effect* getting better. Which
  is why the pairing is not redundant: one is what you found, the other is what
  you did with it.
- **The effect can leave the staff.** Enchantments transfer (§6.5), so a Staff
  of Mire's Poison — earned to tier 5 over a campaign — can be moved onto a
  dagger, and the wizard who does it is precisely the build §3.3 exists for.
  The staff is where the effect *comes from*, not where it has to stay.

The last one is the reason to prefer this over a plain field. A field is a
property of a weapon class; an enchantment is a thing you own, and can carry
somewhere else, which turns eight staves into a **source of effects** rather
than eight fixed loadout choices.

### A cast is a hit

Moving a staff's effect onto a dagger raises the obvious question — a dagger has
no cast for an on-cast enchantment to fire on. The answer is that there is no
on-cast condition: **the condition is `Hit`, and a staff's cast is its hit.**
`TryCast` is a staff's attack, so it resolves through the same path everything
else does (Phase 0's unified resolver) and triggers the same way.

That collapses two lists into one. `Poison` in §3.3's catalogue — lock 20,
trigger 5, applied on hit — **is** the Staff of Blight's enchantment, seen from
the other end. The staff is where you find it; a dagger is somewhere you can put
it. There is one catalogue of enchantments, and a staff variant is a guaranteed
source of one entry rather than a separate system that happens to look
similar.

It also means the wizard-with-a-dagger build (§3.3) is assembled entirely from
parts the caster classes hand you. You do not find a Poison enchantment in the
abstract — you find a Staff of Blight, cast with it until the rot is deep, and
then move it onto the knife.

**Mire is the signature debuff** for this game specifically: in a system where
movement is the only real currency, taxing an enemy's budget is a more
meaningful attack than damage. It also gives INT characters something to do
against enemies that a healer alone cannot.

Debuff staves need `TryCast` to accept an **enemy** target — today `CanCast`
and `TryCast` only take `PartyMemberState ally` (`TurnSystem.cs:178,191`).
Phase 0's `ActorState` makes that a signature change rather than a rewrite.

## 1.4 Wand — four area shapes (INT)

Wands deal damage in an area. All share Damage 8, Cost 45, Mana 20; they differ
only in geometry. Shapes are in logic units.

| Variant | Name | Shape |
| --- | --- | --- |
| Blast | Wand of the Blast | Circle, radius 48, centred on a target point within 160 |
| Cone | Wand of the Cone | 90° cone, length 128, from the caster |
| Beam | Wand of the Beam | Line, width 32, length 224, from the caster |
| Nova | Wand of the Nova | Circle, radius 96, centred on the caster |

All shapes stop at walls — `LineOfSight` already does segment-vs-wall tests and
extends naturally to per-target checks inside a shape.

Wands carry `Resonant ×1` like staves, and it means the same thing — **20% off the
20 mana a cast costs**, not more damage (§1.3). The shape is a field; wand
damage comes from weapon level (§2.2) and from the damage type's tier below.

The efficiency trade lands harder on wands than on staves, because a wand's
proficiency XP is *total damage across every target in the shape* (§2.2). A
cheap wand fires more often into more bodies and levels the weapon fast, while
levelling its element slowly. An expensive one is the reverse.

### The damage type is the wand's enchantment, and it is rolled

Where a staff's enchantment is its effect and fixed, a **wand's is its damage
type and it is rolled** — uniformly among four, at tier 1, on every wand that
drops (§3.1).

| Type | Opposed by | The fiction |
| --- | --- | --- |
| **Flaming** | Cold | Thermal shock, the intuitive pair |
| **Cold** | Flaming | |
| **Shocking** | Acidic | A conductive place diffuses a shock; acid eats the metal doing the conducting |
| **Acidic** | Shocking | |

So a wand is **a shape you chose crossed with an element you did not**. The four
geometries stay the wand's identity and the element is what makes the same four
weapons play differently from one campaign to the next. It also gives the class
the one thing its variant table lacked: a reason to want a *second* wand of a
shape you already have.

### The type chart cancels

Every enemy may carry an **attunement**, and damage resolves against it:

| Incoming type vs the target's attunement | Damage |
| --- | --- |
| **Same** | **Halved** — it cancels |
| **Opposed** | **×1.5** |
| Unrelated, or the target is unattuned | Unchanged |

Four types in two opposed pairs, not a matrix. Every relation in the table is
readable off one line — *what is this weak to* — which is the whole reason to
keep it at four rather than the eight a richer chart would want.

**Attunement is the dungeon's, not the individual enemy's.** A themed floor
(§4.3) attunes what it spawns, so the chart reads as *this place resists fire*
rather than as a per-enemy stat block the player has to memorise. The theme is
already announced, so the counter is knowable before you descend rather than
discovered by wasting a cast.

Three things this buys:

- **A reason to carry a second wand.** The stable (§6.2) already exists because
  weapons sit in service; the chart gives it a second, sharper reason — the wand
  that clears a Flaming floor is the wrong tool for the next dungeon, and
  rotation is preparation rather than a consolation for downtime.
- **It makes a bad roll into a trade rather than a loss.** A Cold wand in a
  Cold dungeon is halved; the same wand is the best thing you own one dungeon
  later. The roll decides *when* a wand is good, never *whether*.
- **The counter is transferable.** The damage type is an enchantment, so §6.5
  moves it — onto another wand, or onto a sword. A fighter carrying Cold into a
  Flaming dungeon is a real answer built out of parts that already existed, and
  the only one available to a party with no caster.

**Halved rather than nullified**, because a wand hits an area and a shape full
of resistant enemies should be a poor cast rather than an illegal one — nothing
in this game has a zero, and a resisted Nova that still softens a room keeps the
class playable in its worst matchup.

Tier scales the type's contribution along with everything else, so a tier-5
Flaming is both more damage and more of what the chart multiplies. That is the
axis a caster who commits to one element is building, and the chart is the cost
of committing.

**Opposed types cannot share a weapon** (§3.3). Flaming and Cold exclude each
other exactly as `Push` and `Drag` do, and for the same reason — one hit cannot
be two contradictory things. Non-opposing types stack freely, so a
Flaming-and-Shocking wand is legal and is what a player builds when they want to
be wrong in fewer dungeons.

### Friendly fire is on, at half damage, both sides

A shape hits **every actor inside it except the caster** — allies included, on
either side of the fight. Allies take **half** damage.

Full-strength friendly fire is the sharper mechanic, but it demands a
placement-scoring AI be *good immediately*, or wand enemies spend the game
detonating their own side and the fight reads as broken. Half damage makes the
AI's job forgiving: a mediocre scorer that clips one ally is merely
inefficient, not suicidal. It buys the right to ship a simple version and
refine it, instead of needing the finished thing on day one.

Why keep it at all:

- **It prices positioning in movement**, which is the game's actual currency.
  Spreading to dodge a cone costs movement; regrouping afterwards costs more.
  That is the central resource doing what it already does everywhere else,
  not a tax bolted on beside it.
- **It is most fun when the enemy suffers it** — baiting a wand caster into
  its own line is a real play. That only reads as skill if avoiding it is the
  AI's normal behaviour and being baited is the exception, which is exactly
  what the scorer below provides.
- **The marching rules already cover the worst case.** A single-file marching
  line is what a Nova would delete, but marching is revoked the moment a live
  enemy is sighted, so the formation dissolves on contact. What remains is that
  a party can still arrive *clustered*, and the opening blast punishes that —
  a good lesson rather than a cheap shot.

### The placement scorer

Smaller than it sounds, and mostly code the player's targeting needs anyway:

1. Enumerate candidate placements — target points for Blast, directions for
   Cone and Beam, fixed at self for Nova.
2. Score each as `enemies hit − (allies hit × 1.5)`.
3. Cast only if the best score is positive; otherwise fall back to moving.

Structurally this is `EnemyAi.SelectTarget` one level up: the same
score-and-pick over a small candidate set, with the shape geometry shared with
the player's own casting. It scores **enemy** casts only — the player decides
their own placements.

### The scoring weight is a policy, not the damage fraction

Allies take **half** damage but are scored at **1.5×**. The two numbers are
deliberately different and must not be reconciled.

Half is the mechanical reality. Scoring at half would make the AI an expected-
value calculator, and an EV calculator fires on a 1:1 trade — full damage to one
of yours against half to one of mine is, strictly, a marginal gain. It is also
what makes baiting worthless: if even trades are acceptable, you can force a
self-clip just by standing near their group, and the play stops being a play.

At 1.5× the policy reads "hit at least twice as many of them as of us":

| Hits | Score | Casts? |
| --- | --- | --- |
| 1 enemy, 1 ally | −0.5 | No |
| 2 enemies, 1 ally | +0.5 | Yes |
| 3 enemies, 2 allies | 0 | No — ties lose |
| 3 enemies, 1 ally | +1.5 | Yes |

So the AI's *default* behaviour is to refuse anything short of clearly
favourable, and forcing a bad cast takes genuine positioning — which is exactly
what makes it feel like the player's doing rather than the AI's failure.

Half-damage friendly fire then backstops the cases the scorer gets *wrong* —
mispredicted movement, an ally stepping into a shape after it was scored —
rather than licensing sloppiness.

**Sequencing.** Wands ship in Phase 1 with the friendly-fire constant **off**.
It flips on once the scorer exists, alongside the kiting AI that ranged and
wand enemies need regardless.

## 1.5 Uniques

**A unique is a variant with one of its modifiers raised to `×3`, carrying an
enchantment that exists nowhere else.**

Take one of the thirty-two weapons above, pick one modifier it is already forged
with, take it to three — and give it a **soul no other weapon can have** (§3.3).
Everything else is unchanged.

| Unique | Class | Built from | Modifiers | Enchantment | Reads as |
| --- | --- | --- | --- | --- | --- |
| The Bulwark | Sword | Tower Guard | `Block ×3, Push ×1` | **Sturdy** | Absorbs 9, shoves what it stops, and refuses to let you die |
| Feathered Death | Throwing | Bandolier | `Charges ×3, CritMultiplier ×1` | **Weightless** | Four throws that barely cost anything to make |
| Shieldbreaker | Axe | Reaver | `Cleave ×1, Opportunist ×1, Splitting ×3` | **Momentum** | Ignores 9 Block across the swing, and every kill pays for the next |
| Widowmaker | Dagger | Assassin's Fang | `CritWindow ×3, CritMultiplier ×1` | **Siphon** | Finds the gap on 17+, and every kill funds the enchantments doing it |
| Hoplite's Wall | Spear | Phalanx Spear | `Brace ×3, Longshot ×1` | *Immovable* | Three retaliations at full reach, from a line that cannot be moved |
| Stormcrow | Ranged | Longbow | `Longshot ×3, CritWindow ×1` | *Piercing* | +3 a tile, and the shot does not stop at the first body |
| *(unnamed)* | Dagger | Flensing Knife | `CritWindow ×3, Light ×1` | **`Vampiric` + `Overheal`** | Stabs itself a shield — the only martial weapon that runs a combo |

**The four named enchantments each answer their weapon's own logic**, which is
the test a pairing has to pass. `Sturdy` on the shield that already refuses
damage. `Weightless` on the class hard-capped by throws-per-turn, where cheaper
throws are the one thing `Charges` cannot give it. `Momentum` on the axe that
kills several things at once, so the refund fires several times. `Siphon` on the
dagger, because the enchanted dagger (§3.3) is the build that runs out of mana
and this is the weapon that does not.

**`Immovable` and `Piercing` are proposals**, not settled — the spear and the
bow need one each and the four in §3.3 were spoken for. `Immovable` fires when
something tries to displace you and negates it, which is the phalanx fantasy and
a real counter to `Push`/`Drag`/`Rout`. `Piercing` fires on a hit and carries
the shot to the next target in line. Both obey the trigger-cost rule; neither
has numbers yet.

**Why a unique enchantment rather than a second `×3`.** Depth was already
available — the derivation rule could simply have allowed two — and it would
have made uniques *more of the same weapon* when the interesting thing is that
they are the same weapon **plus something the game does not otherwise contain**.
A `Block ×3` sword is a Tower Guard that stops more. A `Block ×3` sword that
will not let you die is a story.

It also unifies the two halves of §1.5 that were drifting apart. A caster unique
cannot raise a modifier at all, since `Resonant` is forge-limited to `×1` (§1.1)
— so its whole advantage was already the enchantment. Now that is not a caster
exception; it is **the rule**, and martial uniques simply also get the deeper
body.

### Why derive them rather than author them freely

**It bounds the forge without a second rule.** "One modifier at `×3`" already
implies "nothing forged past `×3`", which is what makes `×8` the universal
ceiling that every per-stack value in §1.1 is priced against. One authoring
constraint, not two.

**It keeps uniques legible.** A unique built off a variant is *a weapon you
already know, more so* — Widowmaker is a Fang that crits harder, Shieldbreaker
is a Reaver that actually splits shields. The player recognises what they picked
up. A freely-authored spread like `Brace ×3, Pin ×2` reads as a stat block; a
derived one reads as a weapon.

**It stops "more special" meaning "more modifiers."** The temptation with
uniques is always to add another line. Deriving them means the only axis
available is depth on something the weapon already had, so a unique is
recognisably of its class and its role rather than a grab bag.

### Four shapes, one per variant

Because each class has four variants, each class has four possible uniques, and
they are not interchangeable:

| Derived from | The raised modifier | Result |
| --- | --- | --- |
| **Purity** | The class signature (already `×2`) | One deep modifier and nothing else — the purest expression the class has |
| **Control** | Either the signature or the control modifier | Signature-deep with a rider, or shallow with a *brutal* rider |
| **Support** | Same, on the support modifier | The party-facing version of the above |
| **Efficiency** | The signature — **never `Light`** | Deep *and* cheap, and paid a **second enchantment** for the axis it spends on `Light` |

Efficiency uniques cannot raise their own added modifier, because `Light` is
forge-limited to `×1` (§1.1). An Efficiency unique is therefore forced onto the
class signature and comes out as `signature ×3, Light ×1` — the only weapons in
the game that are both deep and cheap to swing.

The Control and Support derivations are the richest, since they keep a second
modifier. A Purity unique collapses to a single number, which is right for The
Bulwark but would be dull four times over.

### A unique carrying a currency puts its uniqueness in the soul

`Light` and `Resonant` are the currency group (§1.1) and both are forge-limited
to `×1`, which has a consequence for artifacts that is worth stating once rather
than discovering twice: **a currency modifier can never be the thing that makes
a unique unique.** There is no `×3` version of it to reach for, and the `×1` the
artifact carries is the same `×1` the common variant it derives from already
had.

That is a sharper problem for Efficiency than for any other shape. Every other
derivation keeps a *distinct* second modifier — The Bulwark's `Push`,
Widowmaker's `CritMultiplier` — so the pair reads as an artifact. An Efficiency
unique's second axis is `Light ×1`, which is on the Flensing Knife, the Hatchet
and the Hunting Bow too. Strip the enchantment away and it is a variant with a
deeper signature.

**So a `Light`-forged artifact's identity lives in its enchantments**, and it is
given enough of them to hold one. Where every other martial unique is
`signature ×3` plus a distinct second modifier plus one soul, an Efficiency
unique spends that second axis on `Light` and is paid for it in souls:

| | Forged | Enchantments |
| --- | --- | --- |
| **Every other martial unique** | `signature ×3`, a distinct modifier `×1` | One |
| **An Efficiency unique** | `signature ×3`, `Light ×1` | **Two — one of them unique, or at tier 3** |

That is not a new shape. It is the caster menu (above) handed to a martial
weapon: a second enchantment and an elevated one are the two things §1.5 already
lets an artifact do that no drop can, and the Efficiency unique is the one
martial derivation with a spare axis to pay for them.

### Why `Light` specifically can afford two souls

Because it is the only martial modifier that *generates mana*. Every enchantment
trigger costs mana (§3.3) and mana comes back from movement left unspent (§1.3)
— which is exactly what `Light` produces, and precisely why `Light` and
`Resonant` exclude each other in the first place (§1.1). A cheap weapon banks
the movement that pays for its own triggers.

So the second soul is not compensation bolted on to make a dull shape
interesting. **A `Light` weapon is the only martial platform that can sustain
two triggers**, and giving it two is the design following the mana rather than
decorating the weapon. The cheapness and the enchantments are the same fact.

### Which makes the Efficiency unique the only martial weapon that runs a combo

One soul on a martial weapon has to stand alone. Two can *feed each other*, and
that is a thing no other martial artifact in the game can do:

> **The Efficiency dagger** — from the Flensing Knife — carries
> `CritWindow ×3, Light ×1`, **`Vampiric`**, and **`Overheal`**. It crits on
> 17+, `Vampiric` heals it for the damage dealt (§3.3), and once that healing
> runs past full `Overheal` banks the surplus as `Ward`. A dagger that stabs
> itself a shield.

Neither half works alone. `Overheal` on a weapon with no healing source is
inert, and `Vampiric` at full health is the resource §3.3 calls the one the game
throws away. **The gimmick requires two slots, so it can only exist on the shape
that has two** — which is the cleanest possible answer to what an Efficiency
unique is *for*, and it arrives without inventing an enchantment.

That also settles where `Overheal` lives. It wanted a weapon that produces
surplus healing without being a healer, and a crit-heavy lifesteal dagger is a
better home for it than the staff it was originally proposed on — a staff does
not need help finding value in healing.

### Caster uniques raise the enchantment instead

A caster's two forged axes are `Resonant ×1` and a rolled enchantment (§1.2), so the
derivation rule has two ways to land — and only one of them is interesting.
`Resonant ×3` is a staff that costs less mana, which is a fine modifier and a
terrible legend. So:

A caster unique gets the enchantment half of the rule and **not** the `×3` half.
That is the currency rule above, applied to the other half of the group — and on
a caster it applies *completely*, because `Resonant` is the only modifier a
caster has. An Efficiency unique still has a signature to deepen; a caster has
nothing else at all, so the soul is not merely where the identity lives, it is
the entire artifact.

That leaves three shapes, and all three read as an artifact rather than a good
drop:

| Shape | What it is | What no ordinary weapon can do |
| --- | --- | --- |
| **A unique enchantment** | One that exists nowhere else — the same treatment martial uniques get | Hold it at all |
| **A high tier** | A catalogue enchantment arriving at tier 3 | Drop above tier 1 |
| **Two enchantments** | Two non-opposing catalogue entries, both at tier 1 | Drop with more than one |

| Unique | Class | Built from | Carries | Reads as |
| --- | --- | --- | --- | --- |
| Rotwood | Staff | Staff of Blight | `Resonant ×1`, **Poison tier 3** | A rot that starts where an ordinary staff's ends |
| The Long Candle | Wand | Wand of the Beam | `Resonant ×1`, **Shocking + Flaming** | A beam of plasma |
| *(unnamed)* | Wand | Wand of the Nova | `Resonant ×1`, `Flaming`, **Burning** | A circle that keeps burning after it lands |

### A wand unique's own shape: the element lingers

Where a martial unique's enchantment bends a rule, **a wand's makes its element
persist.** The shot does not stop when the shape does — and each element
persists as the thing that element *does*, which turns out not to be four
damage-over-times:

| Element | It leaves | Which is |
| --- | --- | --- |
| **Flaming** | **Burning** | Damage per turn, decaying |
| **Cold** | **Frostbite** | Damage per turn, decaying |
| **Shocking** | **Mire** | The movement cut — paralysis, mechanically the staff's own effect |
| **Acidic** | **Poison** | The existing status, unchanged |

Two of these reuse statuses the game already has, which is most of the argument
for them. A lingering *shock* never read as a damage tick — paralysis is what
electricity actually does to a body — and acid eating away at something is
`Poison` by another name. Only Flaming and Cold needed anything new, and they
share one `StatusEffectType` carrying a `DamageType`, so a burn and a frostbite
are one mechanism with two names.

**The wand unique is therefore the staff's effect delivered over an area.** A
Staff of Mire cuts one target's movement; a Shocking Nova unique cuts
everything in the circle. That is the cleanest statement of what the two caster
classes are for, and it arrives without inventing a single new effect for two of
the four.

### Levels, not a number, and every conversion is a constant

**Applying a lingering element grants levels derived from the element
enchantment's tier.** How many, what each does, and how fast they burn off are
**three separate named constants**, because they control three different things
and baking any of them at `1` would hide a decision:

```
levelsApplied  = SearLevelsPerTier  × elementTier
damagePerTurn  = SearDamagePerLevel × currentLevels
levelsLost     = SearDecayPerTurn                    // each turn
```

| Constant | Controls | Bounded by |
| --- | --- | --- |
| `SearLevelsPerTier` | How sharply the effect scales with tier — it is **inside the square** | Nothing; this is the free one |
| `SearDamagePerLevel` | Magnitude only, linearly | Enemy HP — the totals below scale straight with it |
| `SearDecayPerTurn` | **Duration**, and therefore the total | Fight length. See below |

Everything else follows the rider rules §1.6 already sets, and deliberately so:
levels **accumulate** on re-application rather than refreshing, and there is no
cap. So `Poison`, `Searing`, `Sundered` and `Weakened` are **one family with one
model** — a level count that accumulates, ticks, and decays. A player learns the
rule once, the HUD needs one presentation, and only the constants differ.

### Why decay is the interesting dial

Levels tick and *then* decay, so the total is a triangular-ish sum and **decay
is what bounds it**. Compare a tier-6 `Flaming` under the two obvious settings,
with `LevelsPerTier` and `DamagePerLevel` both at `1`:

| `DecayPerTurn` | Ticks | Total | Duration |
| --- | --- | --- | --- |
| **1** | 6, 5, 4, 3, 2, 1 | **21** | 6 turns |
| **2** | 6, 4, 2 | **12** | 3 turns |
| **3** | 6, 3 | **9** | 2 turns |

**Decay is not merely a magnitude knob — it decides whether this is a
damage-over-time effect or a delayed burst.** At 1 the room burns while the
party repositions, which is the whole wand fantasy. At 3 it is a second hit
arriving late. Somewhere around 2 is where it stays recognisably a burn while
staying a number you can look at.

The obvious constraint on it is *duration must not outlast the fight* — the
deepest levels would tick against a corpse and a player who invested in tier got
nothing, a dead stack in everything but name. That is right as far as it goes,
but it is stated against a number that does not exist.

**There is no such thing as "the fight length."** Two things set it and they
move in opposite directions:

| Pushes fights *shorter* | Pushes fights *longer* |
| --- | --- |
| Weapon proficiency — `+floor(L/2)` damage (§2.2) | Revival scaling — `+5` HP a cycle (§3.2) |
| Weapon depth — farmed and grafted stacks | Deeper `DefeatCount` on what you chose to farm |

So a party at proficiency one-shots an unfarmed dummy, and the same party
against something it has killed fifteen times is in a long fight. Both happen in
the same run, minutes apart. Tuning a decay constant against the average of
those two would produce a number correct for neither.

**The honest version of the constraint is narrower: duration should match the
fights a *wand* is in.** A tier-6 `Flaming` is a late-campaign item — it has
been levelled by hundreds of points of mana (§3.3) and its owner is fighting
things worth that investment. It is not the weapon in the one-shot case, and a
one-shot is not a fight it was going to matter in.

Which leaves the short fight needing its own reward rather than needing the DoT
to shrink, and §3.2 now gives it one: **a clean kill advances `DefeatCount` by
2.** The trivial fight pays in farm progress instead of in burn damage. Two
playstyles, two currencies, and neither constant has to compromise for the other
— which is the actual resolution, rather than picking a decay that is wrong in
half the situations.

**Starting point: `LevelsPerTier 1`, `DamagePerLevel 1`, `DecayPerTurn 2`** — 12
damage over 3 turns at tier 6. That is a real effect, it reads as burning, and
it is roughly half what decay-1 produced. All three want play rather than
argument, which is why they are constants rather than prose.

### The totals are quadratic in tier, whatever the constants

Worth seeing plainly, because it is the only quadratic term in the design. At
the starting point above:

| Element tier | 1 | 2 | 4 | 6 | 8 |
| --- | --- | --- | --- | --- | --- |
| Levels | 1 | 2 | 4 | 6 | 8 |
| **Total damage** | 1 | 2 | 6 | **12** | **20** |

And it lands on *every* target in the shape, so a Nova catching six enemies at
tier 6 deals 72 over three turns. That is the number to watch, and it is the
strongest argument for the tier-1 cap on the unique enchantment (§3.3) — if the
enchantment scaled as well, the two would multiply.

`SearLevelsPerTier` is the dial that changes the *shape* rather than the height,
because it sits inside the square: halving it quarters the total. That makes it
the right correction if deep wands prove too strong at the top while shallow
ones feel fine, and `SearDamagePerLevel` the right one if the whole curve is
simply too high.

### That resolves the tier-1 cap without breaking it

A unique enchantment cannot level (§3.3), and this is why it does not need to:
**the rule and the magnitude live in different places.**

| | Is | Scales? |
| --- | --- | --- |
| The unique enchantment | "The element lingers" — a rule | **No.** Tier 1, always |
| How deep it lingers | The element's tier, in levels | **Yes**, with the element |

Level your `Flaming` and the Searing it leaves goes deeper with it. What never
intensifies is the *statement* — the element lingers, it does not linger harder.

**It also makes the element a prerequisite.** Searing is nothing without
`Flaming` on the same weapon to say how many levels, which is `Requires` (§1.1)
arriving in the enchantment system by necessity rather than design. Transfer the
`Flaming` off and the burn goes quiet.

**The Long Candle is the third shape, and it is the neatest of the three.** A
plasma beam is fire and lightning at once, so it carries `Shocking` and
`Flaming` — legal because they do not oppose each other (§3.3; `Shocking`
opposes `Acidic`, `Flaming` opposes `Cold`), and impossible to find any other
way, because **a drop rolls exactly one enchantment** (§3.1). It is also the
first weapon in the game that answers *two* attunements: halved in a Flaming
dungeon on one element while the other still lands.

The last two shapes are the same idea on the two different axes, which is why
both are worth keeping:

| | Somebody else already paid for | Which normally costs |
| --- | --- | --- |
| **Rotwood** — tier 3 | Depth | Mana, spent casting (§3.3) |
| **The Long Candle** — two enchantments | Breadth | Runs of downtime (§6.2) |

Neither bends a rule the way `Sturdy` or `Siphon` does. They are artifacts
because of the *work already in them*, not because they do something the game
otherwise forbids — and not every artifact should break the frame. "Somebody
carried this for a very long time" is a legitimate thing for an item to be.

There is a cost buried in the second one worth knowing about: service time is
enchantment count + 1 (§6.2), so The Long Candle's next enchantment costs
**three runs** where an ordinary wand's second costs two. An artifact that
arrives ahead is also an artifact that is slower to extend.

**A tier-3 artifact is levelling somebody else already did.** Tier is earned by
casting (§3.3), so a staff that drops deep has a history — it was carried by
someone, for a long time, before it ended up down there. That is a better story
than a bigger number and it costs nothing to tell, since the mechanism is the
one every other enchantment uses.

**A unique enchantment is the scarcer shape**, and it needs one rule to stay
scarce: it can be **transferred but never catalogued.** §6.5 moves it, because
the body is replaceable and the soul is the thing you built — that metaphor
should not stop working on the best item in the game. But §6.2's "choose from
what you have seen" cannot offer it, because the enchanter cannot make a second
one. There is exactly one, and moving it is moving *it*.

That distinction is worth being precise about, since it is the only place the
catalogue and an actual enchantment come apart: **the catalogue is a list of
things the enchanter can copy.** A unique enchantment is a thing that exists.

The two classes get there differently, which follows from §1.3 and §1.4. A
staff's enchantment is already fixed by variant, so its unique simply starts
deep. A **wand's is rolled**, so its unique does both — fixes the element and
starts it deep — which makes a wand unique the only weapon in the game whose
damage type is not a roll. That is what makes it recognisable in the way
`Block ×3` makes The Bulwark recognisable: you know what it is the moment it
drops.

It also inherits the right ceiling by accident. Tier is uncapped and earned by
casting (§3.3), so a unique's tier 3 is not near any limit — it is three tiers
of *levelling you did not have to do*, on a climb with no top. A caster unique
is the deepest project in the game for exactly the same reason The Bulwark is:
it starts high on the axis that keeps going.

Note what a caster unique is **not**: it is not a bigger number on the weapon.
`Resonant ×1` is unchanged, the statline is unchanged, and every point of its
advantage sits in a thing that can be transferred off it (§6.5). A caster unique
is a *soul that arrives grown*, and the body it came in is ordinary.

### Uniques are the deepest ceilings in the game

A unique's whole spread is **forged** (§1.1), so its raised modifier caps at
`×8` with a full five-stack acquired budget still unspent above it. A unique is
therefore not just the best weapon you can find — it is the best weapon you can
*keep building*, which is what earns it the deep end of the repeat-kill ladder
(§3.2).

Because they are just stacks, a unique that turns out overtuned is a data edit,
not a code change. And since ceilings scale with the forged spread rather than
sitting at a flat cap, the balance envelope is held by the derivation rule plus
hand-authoring: no roll, graft or service can reach a spread nobody wrote down.