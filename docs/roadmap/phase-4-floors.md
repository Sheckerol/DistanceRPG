# Phase 4 — Multiple floors

## 4.1 Persistent while you are in the dungeon; reset when you leave

Floors are revisitable via stairs, and every visited floor keeps its enemy
state and explored fog for as long as the party remains in the dungeon.
**Leaving the dungeon discards all of it** — re-entering regenerates every
floor fresh.

That keeps the in-run tactics (retreat upstairs, come back for a half-cleared
room) without the save file growing without bound, and it caps repeat-kill
weapon farming at one dungeon visit.

The reset is also what Phase 6 is built on: the dungeon is the renewable half
of the game and your gear is the permanent half.

## 4.2 Stairs must not perturb the golden seed

`MapGenerator` cannot gain stair placement inline — that would reorder its RNG
calls and break `MapGeneratorTests`. Stairs get placed by a **post-pass on its
own stream**, the same pattern `EnemyPlacer` established:

```
Logic/StairPlacer.cs      seed = mapSeed ^ StairSalt ^ floorIndex
```

Down-stairs go in the room furthest from `PlayerStart` (by A* cost, using the
existing `Pathfinder`); up-stairs at the arrival tile.

Per-floor seed: `floorSeed(n) = mapSeed ^ (FloorSalt * n)`, with `n = 0`
resolving to exactly today's seed so floor 0 stays golden.

## 4.3 The boss floor

Every dungeon has exactly one boss, on a floor rolled **between 5 and 10 on
entry**. The boss floor is the dungeon's bottom, so dungeon depth varies per
visit — a short, sharp descent or a long grind, decided before you take the
first step.

Own RNG stream, same pattern as everything else:

```
bossFloor = 5 + new Mulberry32(mapSeed ^ BossSalt).NextInt(0, 5)
```

**The roll is not disclosed.** It is fixed at entry but unknown to the player,
who learns the depth only by reaching it. That is what keeps the depth-scaled
payout below from being scummable: you cannot re-enter until you see a floor
you like, because a cleared dungeon is locked and a failed one re-rolls.

### Killing the boss stops resurrection

Dummies revive after 10 turns today (`TurnSystem.cs:532`), and sooner the more
often they have died — down to 3 (§3.2). Once the boss is down, they stop: the
dungeon becomes **finite and clearable** for the rest of the visit.

That is the reward, and it also creates the run's central tension. The
repeat-kill ladder runs *on* resurrection — `DefeatCount` only advances because
dummies come back (§3.2). So:

- **Before the boss**, the dungeon is an infinite farm — and an *accelerating*
  one, since every cycle makes a dummy stronger and quicker to return (§3.2).
  Deeper drops and better unique odds, as long as you have the turns and the
  health to keep cycling.
- **After the boss**, it is a finite clear. Whatever is left, you take once.

Killing the boss is what makes a run *successful* (§6.3), and it is also what
ends your farming. Deciding when you have farmed enough is the run's real
decision, and it is entirely the player's to make.

**It is also the only thing that makes leaving cheap.** Retreat without the boss
and the climb out runs back through every floor you farmed, against dummies that
are stronger for having died and returning in three turns rather than ten. The
boss is not the last obstacle between the party and the exit — **it is what
makes an exit exist.** A player who farms hard and then loses their nerve has
built the gauntlet they now have to walk.

### A boss is a dungeon's theme, and the theme is a modifier

Each dungeon is themed, its boss embodies that theme, and the theme is
**one guaranteed modifier**:

- The boss carries that modifier **innately**, regardless of what it wields.
- **The boss's own drop is forged with it**, on top of whatever the weapon's own
  class modifier is.
- **Everything else that drops in the dungeon rolls for it** — the theme joins
  that weapon's graft pool rather than being handed to it.

**This needs no mechanism of its own.** Grafting an off-class modifier onto a
weapon is exactly what the enchanter does (§6.4); the boss simply *guarantees
which one you get*. Same operation, different source — and the theme is not a
special case so much as the one place in the game where a graft is chosen
rather than rolled.

The ceiling rule (§1.1) then does the rest for free, because the boss's graft
arrives **forged** and the enchanter's arrives acquired:

| Source | The off-class modifier | Ceiling |
| --- | --- | --- |
| **Boss's own drop** — chosen, guaranteed, one weapon a run | Forged | 6 |
| **Any other drop in that dungeon** — the theme is in its roll pool | Acquired | 5 |
| Enchanter graft — rolled from the general pool, rare | Acquired | 5 |

So the golem's own spear is `Brace ×1, Block ×1` and can be worked up to
`Block ×6`; a spear that rolls Block off a dummy in the same dungeon, or in
service later, tops out at `×5`. The boss's article is strictly the better one,
and nothing had to be special-cased to make it so.

**The theme is one weapon a run, not a coat of paint on the floor.** Handing it
to everything would mean a single Block run re-forging your whole stable, which
is both too fast and too flat — every weapon you own converging on the same
off-class modifier because you happened to like one dungeon. Guaranteeing it on
the boss and *weighting* it everywhere else gets the good half without the bad:
you always come home with the thing you went for, and the rest of the haul leans
that way without all arriving identical.

The middle row is the part worth stating plainly. **A dungeon does not change
what drops; it changes what the drops are likely to grow.** An ordinary weapon
picked up in a Block dungeon rolls its graft from the usual pool *plus* `Block`,
so the theme shows up often without ever being certain — and when it does it
arrives acquired, shallower than the boss's, exactly as §1.1 wants.

That is also the honest version of what dungeon selection is *for*. The point
was never that off-class modifiers are unobtainable elsewhere — it is that the
boss is the only way to get the one you **chose**, at full depth, guaranteed.
Choice and depth, not scarcity.

### A deeper boss pays more

The boss floor rolls 5–10, and **the drop carries one acquired stack per floor
past 4**:

| Boss floor | 5 | 6 | 7 | 8 | 9 | 10 |
| --- | --- | --- | --- | --- | --- | --- |
| Stacks on the drop | 1 | 2 | 3 | 4 | 5 | **6** |

Distributed exactly as a farm's are (§3.2) — rolled among the modifiers the
weapon is already forged with, the theme graft included, skipping any at its
ceiling. What is *not* like a farm is that they are **guaranteed rather than
rolled for**. A floor-10 boss hands you six stacks; twenty defeat cycles hand
you an expected ten and might hand you four.

That is the texture difference between the two ladders, and it is worth keeping
sharp:

| | Gives | Costs |
| --- | --- | --- |
| **Boss** | Certainty — a chosen theme, a known count | A descent, and the dungeon afterwards (below) |
| **Farm** | Volume, and the unique lottery | Turns, and an enemy that gets stronger every cycle (§3.2) |

**Six is deliberately short of ten.** A boss can never hand you as much as a
well-farmed weapon, because **entering a dungeon is a choice and farming is
not** — you pick the theme, you know what the drop will be forged with, and
every weapon on the floor carries it. Certainty is already most of the reward,
so it does not also get to be the largest one. The farm stays the only route to
a *finished* weapon.

It also settles what depth is worth without disclosing it. The floor roll stays
hidden, so a deep dungeon is a harder run with a better payout **discovered**
rather than chosen — you cannot scum for a shallow one, and finding out you are
on floor 9 is now good news rather than merely a longer walk.

**Service ticks do not scale with it.** A win still ticks once at any depth
(§6.3). The tick rewards the *achievement*, which is binary — you killed a boss
or you did not — and the drop rewards the *depth*. Splitting them keeps a
floor-5 clear from feeling like a failed run while still making a floor-10 one
the better prize.

### A cleared dungeon goes on cooldown

**Beating a dungeon's boss locks that dungeon for a number of runs equal to the
floor you beat it on.** A floor-5 clear locks it for 5 runs, a floor-10 clear
for 10, counted by the same events that tick the enchanter (§6.3) so there is no
second clock to track.

**A dungeon is powered, and killing its boss unpowers it.** That is the same
fact the tutorial rests on (below): depth is a mana budget, so a ten-floor
dungeon represents more of the BBEG's pool than a five-floor one and takes
proportionally longer to restore. The cooldown is not an arbitrary lockout — it
is somebody rebuilding what you broke, and the bigger the thing you broke, the
longer it takes.

The depth that paid you is the depth that takes it away, which is what stops
"deeper is better" from being the whole story. A deep dungeon is a bigger haul
and a longer absence; a shallow one is a modest haul you can go back to soon.
Neither dominates, and the run you actually got is the one you plan around.

Three things this fixes, none of which a flat lockout would:

- **Themes stay meaningful.** Without a cooldown the correct play is to run the
  dungeon whose theme you want until every weapon you own carries it. Themes
  would be a menu you visit once. With it, your stable ends up carrying the
  themes of the dungeons you have *been able* to run, which is a campaign
  history rather than a shopping list.
- **The named unique stays rare.** Each themed dungeon can drop exactly one
  unique (§1.5, and the Purity coincidence in Settled). A cooldown is the only
  thing standing between that and farming the same boss for it.
- **The stable gets a second job.** §6.2 builds a stable because weapons sit in
  service. Now the *dungeons* rotate too, and a party geared for one theme has
  to fight in another — which is where a deep bench stops being insurance and
  becomes the point.

**Failing does not lock anything.** The cooldown starts on a *clear*, so a
dungeon that beat you is available immediately and as many times as you like.
Losing costs you the run; it must not also cost you the option.

**The tutorial does not come back at all.** It is not on a cooldown — beating it
removes it from the hub **permanently**, and the reason is lore rather than
economy: the tutorial dungeon is a place the story closes behind you, not a
resource you exhaust. The mechanical version of that is simply the strongest
possible cooldown, so it needs no separate rule, only a flag.

It lines up with the economy anyway, which is the tell that the lore is pulling
in the right direction. The tutorial's boss sits on a fixed floor 2 (below), so
the depth formula would price it at one stack and a two-run lock — the least
rewarding dungeon in the game, kept alive forever as a cheap service-tick farm.
Removing it is both the better story and the better rule.

The obvious worry — locking a new party out of the only content they know — does
not arise, because the tutorial is the thing that *unlocks* the hub. You cannot
beat it before you have somewhere else to go.

### A themed boss cannot drop a class that will not take its theme

The boss's drop is a *guarantee*, so its drop table bends to it rather than the
other way round. Some modifiers are exclusive with others or restricted by
weapon type (§1.1), and where a class cannot legally carry the theme, **the boss
simply does not drop that class.**

This applies to the boss's own drop and nothing else. Ordinary drops need no
such rule: the theme is only in their *roll pool*, and `Allowed` (§1.7) already
skips a modifier a weapon cannot hold. An axe picked up in a `Brace` dungeon
simply never rolls `Brace` and rolls something else instead — no table to bend,
no class to exclude.

A `Brace`-themed boss is the worked example. `Brace` is melee-only and sits in
the reaction group with `Opportunist`, so:

| Class | Can it take `Brace`? | |
| --- | --- | --- |
| Dagger, Sword & Shield | Yes | No reaction forged, and melee |
| Spear | Yes | It is the theme's own class — the graft deepens it |
| **Axe** | **No** | Forged `Opportunist`; same reaction group |
| **Ranged, Throwing** | **No** | `Brace` is melee-only |
| **Staff, Wand** | **No** | Also not melee |

So that dungeon drops daggers, swords and spears, and nothing else.

The alternative — dropping ungrafted axes — was rejected because it puts a
footnote on the one thing the theme promises. A player who runs the Brace
dungeon and comes out with a plain Reaver has been handed the exception rather
than the rule, and no amount of tooltip explains that well.

**Theme breadth therefore varies by theme, and that is a feature.** `Block` and
`Cleave` graft onto anything, so the golem's table is all eight classes.
`Brace` reaches three. A narrow dungeon is a *specialised* dungeon — you run the
Brace dungeon to kit a melee line, and you go elsewhere for a bow. The unique
each one gates is unaffected, since a theme's unique always comes from its own
class (§1.5), which can always carry it by definition.

It settles boss drops versus repeat-kill uniques too. They are different axes,
and all three should exist:

| Source | Gives you |
| --- | --- |
| Repeat-kill depth (§3.2) | Depth in the weapon's **own** class signature, spent out of its acquired budget |
| Boss drop | A **chosen** off-class modifier, forged, so it runs a stack deeper |
| Enchanter graft (§6.4) | An off-class modifier you did not pick, shallow by construction |

You run the Block dungeon because you want Block on something that has no
business having it, and you want it deep enough to matter.

### A boss never rolls a unique — but its graft can produce one

**There is no unique roll on a boss kill.** The repeat-kill ladder (§3.2) is the
only place uniques are *rolled*, and the two sources stay separate.

But the theme graft can *arithmetically* produce one, and this needs no rule
either. A boss drop is the rolled variant plus one stack of the theme. A unique
is a variant with one modifier at `×3` (§1.5). So when the roll lands on the
variant already forged `×2` in the theme's own modifier — the **Purity** variant
of the theme modifier's class — the graft takes it to `×3` and the result *is*
that class's unique:

```
Tower Guard  (Block ×2)  +  golem's Block graft  →  Block ×3  =  The Bulwark
```

Nothing special-cased it. The two rules met.

That is the whole exception, and it is a narrow one. Any other roll produces an
ordinary themed drop: a Riposte Blade becomes `Block ×2, Riposte ×1`, which is
excellent and is not a unique. A spear becomes `Brace ×1, Block ×1`, an
off-class hybrid and still not a unique.

Three consequences worth stating plainly:

- **Each themed dungeon can drop exactly one unique**, and everyone knows which.
  The golem yields The Bulwark or nothing. That is a far sharper reason to pick
  a dungeon than "Block on stuff" — you run it to hunt one named weapon.
- **It is rare without being rare by decree.** The drop has to roll the right
  class *and* the right variant within it. Nothing was tuned to make that
  unlikely; it just is.
- **Themes are drawn from class signatures.** `Block`, `Brace`, `Cleave`,
  `Longshot`, `Charges`, `CritWindow`, `CritMultiplier`, `Resonant` — the modifiers
  a Purity variant doubles. A theme on a Control or Support modifier like `Pin`
  could never reach `×3`, since no variant is forged `×2` in one, so such a
  dungeon would be the only one with no unique to hunt. `Light` is excluded for
  the same reason from the other direction: it is forge-limited to `×1` (§1.1),
  so a Light theme could not graft at all.

### The tutorial dungeon: the stone golem

The dungeon shipped today is the **tutorial**, and its theme is **Block**. Its
boss is a stone golem: innate Block whatever it happens to be holding, and the
weapon it drops comes away forged with Block. Everything else killed down there
rolls for Block on top of its usual graft pool.

### The enchanter is running it

**The tutorial dungeon is the enchanter's.** He built it, he is powering the
golems, and he is standing there operating the whole thing while the party
learns to fight in it — training them on what they are about to face by
building a small, safe copy of it.

He is not the villain, and the difference is measured in mana. The BBEG powers
dungeons five to ten floors deep and holds them indefinitely; the enchanter can
sustain two floors, once, and it costs him everything he has while it runs.

That premise is doing an unusual amount of work, because **three mechanics that
were each justified separately now come from one fact**:

| Mechanic | Follows because |
| --- | --- |
| **Fixed at two floors** (below) | It is the most his pool will hold. Depth is a mana budget, and his is the smallest in the game |
| **Gone once beaten** (below) | He stops powering it and goes back to his workshop. Nothing is consumed — he simply has a job to return to |
| **No service while it runs** (§6.3) | Powering golems *is* his mana. He cannot enchant and be a dungeon at once |

The last one is the real gain, because it replaces a shrug with a reason.
Previously the tutorial had no mercy tick and the defence was that a first-run
party has nothing in service anyway — true, but incidental. Now **the enchanter
is unavailable for the whole tutorial**, because he is busy being the dungeon,
and there is no service clock to advance because there is no service. The
workshop opens when the training ends.

Which makes the handoff in the pacing note below exact rather than lucky: you
beat the golem, he stops spending mana on it, and the first thing he can do is
take your weapon. Finishing the tutorial does not merely *earn* the first
enchantment — it is what **frees the man who performs it**.

It also quietly sets the stakes. The first thing the game teaches you is a
scaled-down imitation of the real thing, built by someone who could only afford
two floors of it. Everything after is the version made by someone who was not
short of mana.

### Two floors deep

The tutorial ignores the 5–10 roll: its boss sits on **floor 2**, fixed — as
much as the enchanter's pool will hold. It is the one dungeon in the game with a
hardcoded depth, and the special case earns itself twice over.

It teaches the complete loop in miniature — descend once, fight the boss,
extract back through a single floor — so every dungeon mechanic is demonstrated
in the shortest run that can contain them all. And since the dungeon is
consumed by beating it, a short one means a fast turnaround into the hub rather
than a long commitment a first-time player cannot yet evaluate.

Two floors is also enough to seed the **first stable**. Phase 6's rotation
needs several weapons to work at all, and a couple of floors of dummies farmed
lightly is where they come from.

The pacing lines up on its own: one successful tutorial run ticks the enchanter
once (§6.3), and attaching the first enchantment costs exactly one run (§6.2).
Finishing the tutorial buys your first enchantment, immediately. Wear from two
floors of fighting should be tuned to cover it — the tutorial ought to end with
the player able to use the enchanter, not merely able to look at it.

### The tutorial is consumed by beating it

Unlike every other dungeon, the tutorial is **available exactly once**. Kill
the golem and it is gone from the hub for good; fail, and it is still there.
Every other dungeon is picked from the hub and comes back after its cooldown.

The lore carries the asymmetry without a rule: the enchanter powers his dungeon
down and returns to work, so it is *retired* rather than locked. A real dungeon
is somebody else's and merely needs **re-powering**, which is why it comes back
at all — and why deeper ones take longer to. The tutorial is the only one whose
operator had somewhere better to be.

That makes it the one place where **rushing the boss permanently costs you
something**. Farm the tutorial and you leave with Block-themed weapons no other
run in the game will hand you cheaply; dive straight to the golem and that
opportunity closes behind you.

Which is exactly the decision the whole game is built on (§4.4), delivered once
in miniature where it is cheap to learn. The tutorial should **say so plainly**
— this is the one dungeon the game is allowed to warn you about, and a
first-time player who loses the theme without knowing the rule has been cheated
rather than taught.

Block must therefore remain obtainable elsewhere. A rushed tutorial should cost
a good head start, never a permanently closed build.

The golem is a good first boss because it teaches the one thing flat Block
makes true. Block absorbs a *flat* amount and never reduces a hit below 1
(§1.1, a deliberate exception to the proportional rule), so many small hits are
terrible against it and few large ones are fine.

The starting party spans that range deliberately — **axe 18, dagger 15, sword
10**, plus a healer. Give the golem `Block ×3` innately — 9 absorbed — and the
maths does the teaching by itself: the sword's 10 lands for 1, the dagger's 15
for 6, the axe's 18 for 9. The player discovers that by swinging, not by
reading a tooltip.

Then the dagger crits and lands **45**, because crits ignore Block entirely
(§1.6). One roll in ten, out of nowhere, the armour stops existing.

Two lessons for the price of one fight, and the second is the more valuable:
**damage per swing beats swings per turn against armour**, and **there is always
a way through** — you may just have to wait for it. Picking the right party
member for the target is the whole game, and the golem teaches both halves of
what "right" means.

The pairing runs deeper than the tutorial. The axe's wildcard modifier is
`Splitting`, which ignores Block outright (§1.2) — so the tutorial dungeon is
themed on the exact defence the axe class exists to break. A player who takes
that lesson and hunts a Reaver has understood the game.

### Innate modifiers

"Block regardless of its weapon" means modifiers must be able to live on an
**actor**, not only on a weapon. `ActorState` (Phase 0) gains its own
`ModifierSet`, and resolution reads weapon *plus* innate.

That is generally useful rather than a boss special case — it is also how
armour, monster traits, and any future innate would work, on either side of the
fight. `CombatRules.ResolveAttack` currently reads Block off the defender's
weapon alone (`CombatRules.cs:55`); it needs the defender's actor too, which
Phase 1 already changes the signature for (Sundered/Weakened).

**Innate is not forged.** The golem's own `Block ×3` lives on the actor and is
added at resolution time; it raises no weapon's ceiling, including the golem's
own. What the theme forges into the weapons it **drops** is a separate
application of the same modifier, and only that one counts toward those weapons'
caps (§1.1). Two ideas, two fields, deliberately different words.

## 4.4 Fighting your way out

**The boss does not end the run.** Killing it turns the party around: you climb
back out through every floor you descended, killing the monsters one last
time — and that final kill is when you collect their drops.

### `DefeatCount` sets the quality; the last kill collects it

This resolves when loot actually materialises. Repeat-killing a dummy during
the descent does not hand you anything — it raises that dummy's `DefeatCount`,
which is the *quality* of the drop it is carrying. Because the boss stopped
resurrection, killing it on the way out is permanent, and permanent death is
what yields the goods.

Farm a dummy twenty times on the way down and it is holding a fully deepened
drop with a coin-flip's worth of unique chance on it (§3.2). You bank the
quality going in and harvest it coming out — and the unique roll itself resolves
on that final kill, so the extraction is where you learn what the farm was
worth.

### The risk curve inverts

You are strongest descending and weakest climbing out — resources spent, HP
gone, movement banked away — and that is precisely when the entire payout sits
on the board. Every extra farming cycle makes the drop better *and* the
extraction harder, with the same currency paying for both.

**And the enemies you farmed are the strongest things left.** Each revival adds
damage or health (§3.2), so the dummy carrying your deepest drop is also the one
that has come back the most times and hits accordingly. The weapon you most want
to collect is guarded by the version of itself you made.

The gauntlet does thin in *number* as you climb, since nothing revives any more
— but it does not thin in quality. Ordinary enemies are a diminishing fight;
your farm targets are the opposite, and they are precisely the ones you have to
go back for.

Navigation is not the challenge: floors persist within a visit (§4.1), so the
map and the fog are already known. The way out is a combat problem.

### You cannot carry it all

Inventory becomes **6 slots per character, 24 party-wide** — replacing today's
3, which was a testing remnant rather than a design (`PartyMemberState.cs:27`).
Exact number is a tuning target; the structure is the decision.

Crucially there is **no separate haul bag**. Loot goes in the same slots as
your weapons, so every drop you pick up climbing out costs you a weapon you
could have been swinging. A greedy hauler is a worse fighter, which puts the
carry limit exactly where the tension belongs — on the extraction itself,
scaling with how much you are trying to leave with.

A deep farm banks far more than 24 drops, so the ascent is **targeted, not
exhaustive**. You do not clear the dungeon on the way out; you revisit the
dummies you invested in and leave the rest standing. Since `DefeatCount` is
visible on the nameplate (§4.5), that choice is informed.

### Skipping floors is allowed

You can run a floor rather than fight it. This needs no gate — three systems
already price it:

- **Moving through a threat is not free.** Brace fires from every zone crossed
  (`TurnSystem.NotifyCharacterMoved`), and you are running it at low HP with a
  spent movement budget.
- **Skipping forfeits the drops.** Abandoning a floor abandons everything you
  banked there, so the decision regulates itself: fight through what you
  farmed, run past what you did not.
- **Wear guards against rushing.** A party could dive a floor-5 boss and skip
  straight out for a free service tick — but they would surface with almost no
  wear, and wear is what the enchanter consumes (§6.1). Ticks bought without
  fighting buy nothing.

A percentage-of-enemies gate was considered and rejected: it would force you to
kill dummies whose drops you do not want, which is busywork, and it overrides
the self-regulation above rather than adding to it.

### Wear charges on the way out

Wear accrues from swinging (§6.1), so the extraction is also what charges your
weapons for the enchanter — including the improvement roll, which scales with
wear brought in (§6.4). The fight out pays twice.

## 4.5 Code impact

New `Logic/DungeonState.cs` holding `Floor[]`, each with its map, actor list,
and `FogState`, plus `BossFloor` and a `BossDefeated` flag. `DungeonScene`
currently builds one map in its constructor and holds the party, enemies and
fog directly — that becomes a swap of the active floor, tearing down and
rebuilding geometry on transit.

The resurrection block in `TurnSystem.StartPlayerTurn` (`TurnSystem.cs:530`)
gets gated on `!BossDefeated`, which is the same flag that turns `EnemyDefeated`
into a drop rather than just a `DefeatCount` increment.

**Cooldowns are campaign state, not dungeon state.** A cleared dungeon outlives
the `DungeonState` that was cleared, so `CampaignState` (§6.7) holds
`Dictionary<DungeonId, int> LockedUntilRun` against its run counter — the same
counter §6.3 already advances for service. A dungeon is selectable when
`runCounter >= LockedUntilRun[id]`, absent means available, and the tutorial is
simply never written to it.

The hub's dungeon list therefore needs a **locked presentation with a countdown**
rather than hiding entries. "Available in 4 runs" is information the player
plans around; a dungeon that silently vanishes reads as a bug and makes the
rotation impossible to think about.

`BossFloor` has to survive into the drop resolution, since it sets both the
stack count and the cooldown length. It is already on `DungeonState`; what is
new is that leaving has to carry two numbers out to `CampaignState` rather than
discarding everything.

**The HUD must show `DefeatCount` on the enemy nameplate.** Farming with no
visible reward until the extraction would read as broken otherwise — the player
needs to see the quality building on each dummy to make the stop-farming call
deliberately, and to pick targets on the way out.

It has to read as a **threat** as well, since the same counter drives the
dummy's accumulated damage and health (§3.2). A bare `7` communicates the reward
half and hides the half that kills you; the nameplate wants the earned statline
visible next to it, and a beat when a revival rolls its bonus so the player
connects the two.

The count is **unbounded**, so it cannot render as `n/5` or any other fraction.
That is the honest presentation — there is no target to reach, only a curve to
judge — but it does mean the HUD has to make "further is worse" legible without
a denominator to lean on.

`PartyMemberState.Inventory` goes from 3 slots to 6 (`PartyMemberState.cs:27`),
and `DungeonHud`'s inventory panel has to grow with it. Slot 0 stays the
equipped weapon; the swap keys currently hardcode slots 2 and 3, so the input
handling generalises.

`Logic/FogState.cs` becomes per-floor rather than per-scene.

