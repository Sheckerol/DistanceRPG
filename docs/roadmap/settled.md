# Settled

- Weapon XP is **per class, per character**, not per item.
- Status effects are **universal** — every actor can carry them (Phase 0).
- Innate stats are **fixed at creation** and never rise.
- Enchantment mana locks apply **while equipped** only.
- Floors persist **within a dungeon visit** and reset on leaving.
- **Crit riders accumulate and have no level cap.** Re-applying `Sundered` or
  `Weakened` adds levels rather than refreshing a timer, so a `CritSunder ×4`
  weapon that crits three times leaves the target taking `+12`. Investment
  should read as a bigger number, not the same number lasting longer — and a cap
  would have meant the last four stacks of a deepened rider weapon applying
  nothing, the exact dead-stack failure §1.1 forbids. Per level is `+1`, now the
  effect's only dial; one-level-per-turn decay, the fight ending, and
  `Weakened`'s floor of 1 are what bound it instead.
- Crits apply a **class-flavoured rider** on top of the damage spike, both
  ways; casts crit for double effect level.
- Modifiers are **stacks, not values**. A Purity weapon is a second stack of
  the class signature; uniques are arbitrary stacks. One mechanism.
- **Enchantments are a separate system**, not modifier stacks — own dials
  (lock, trigger cost, condition, potency, tier), potency scaling with INT, and
  max mana as the budget. That is what lets a wizard enchant a dagger into
  close-range damage without touching their dagger proficiency.
- **The dungeon supplies bodies and one seed soul; the hub supplies the rest.**
  A drop rolls an enchantment — always on staves and wands, rarely on everything
  else — but only ever one, and only ever tier 1. Breadth and depth past that
  are the enchanter's alone.
- **Enchantment tier is uncapped and earned by use** — mana *spent* by an
  enchantment is its XP, so it levels by being cast with, never bought. The
  brake is built into the fuel: the tier that spending buys locks more of the
  pool the spending came from. Farming may bank five tiers' worth as a head
  start; the rest is yours to cast for.
- **The enchanter sells breadth, use buys depth.** Service time prices how many
  enchantments a weapon carries and never how deep any of them runs. You cannot
  grind to breadth or pay for depth, and the two ambitions want the weapon in
  different places — in the shop, or in your hand.
- **What the enchanter attaches is chosen, not rolled**, from everything the
  campaign has seen. It is the one lever in itemisation the player operates
  directly, which is what makes every other roll bearable — and random drops
  feed the catalogue rather than competing with it.
- **A lock you cannot afford leaves the enchantment dormant**, never the weapon
  unequippable. Same non-event as a trigger you cannot pay for.
- **The casters' second forged axis is that enchantment, not `CritMultiplier`.**
  Multiplying an applied effect on a natural 20 is a rounding event on 5% of
  casts. Caster uniques follow, arriving at tier 3.
- **`Resonant` is efficiency, not magnitude** — −10% per stack off *every* point of
  mana the weapon spends, cast costs and enchantment triggers alike. Tier sets
  how hard an effect lands; `Resonant` sets what it costs. Forged on casters only,
  graftable onto anything carrying an enchantment.
- **Every enchantment trigger costs mana**, whatever fires it. No zero-cost
  triggers, ever — Siphon now pays 5 to restore more. A free trigger is a
  passive that dodges the budget balancing everything else, it could never level
  (tier is earned from mana spent), and `Resonant` would be inert on the build it
  exists for. It also removed the "mana *moved*" special case: XP is mana spent,
  the same number §2.2 already credits.
- **`Resonant` is forge-capped at `×1`, ceiling `×6`** — the same limit `Light`
  carries, because they are one modifier pointed at two currencies. Capping one
  alone would have made the mana half the stronger pick by a stack. It also
  forces caster uniques onto their enchantment, exactly as an Efficiency unique
  is forced off `Light` and onto the class signature.
- **The `Resonant` trade has no crossover to tune.** In steady state
  `mana spent per turn = r × (160 − 40k)`, so casting consumes the movement that
  pays for casting: more efficiency buys more casts and therefore *less* mana
  through the enchantment, monotonically. `MovementUnitsPerMana` sets where the
  sustainable cast count lands, never whether the trade exists.
- **Every unique carries an enchantment that exists nowhere else.** Martial
  uniques get that *and* one modifier at `×3`; caster uniques get only the
  enchantment, since `Resonant` cannot be forged past `×1`. So the caster case
  stopped being an exception and became the rule. `Siphon`, `Weightless`,
  `Sturdy`, `Momentum` and `Overheal` are unique-level and out of the
  catalogue.
- **The test for unique-level is "does it break the frame", not "is it strong".**
  All four bend something the game is built on — Siphon breaks mana's dependence
  on unspent movement, Weightless and Momentum refund the game's currency,
  Sturdy denies death, Overheal un-wastes surplus healing. An enchantment that
  merely deals more damage is a
  catalogue entry however much damage it deals.
- **A unique enchantment is transferable but never catalogued.** The enchanter
  copies what you have seen; there is nothing to copy here, only the one that
  exists.
- **Unique enchantments never leave tier 1.** The design's first real cap, and
  it holds because these are the first things that are not numbers: a catalogue
  enchantment scales a *magnitude*, a unique one scales a *rule*, and there is
  no safe multiple of "deny death". A magnitude is re-priced; a rule is limited
  at the forge — the same reason `Light` and `Resonant` cap at `×1`. Not a dead
  stack, since mana spent through one still feeds max mana (§2.2): you grow the
  character instead of the item. The *enchantment* is as good the day you find
  it as it will ever be — the **weapon** around it is the deepest project in the
  game, capping at `×8` with headroom unspent. The body accumulates; the
  identity does not.
- **A wand unique's enchantment makes its element linger** — Flaming leaves
  **Burning**, Cold leaves **Frostbite**, Shocking leaves **Mire** (paralysis),
  Acidic leaves **Poison**. Two reuse existing statuses; only the first two
  needed anything new, and they share one `StatusEffectType` carrying a
  `DamageType`. So a wand unique is *the staff's effect delivered over an area*,
  which is the cleanest statement of what the two caster classes are for.
- **A clean kill — a killing blow dealing at least the target's *max* HP —
  advances `DefeatCount` by 2.** The test is the swing against their
  constitution, not against whatever is left of them, which makes it uncheesable
  (softening a target never brings a clean kill closer) and reduces it to one
  comparison with no tracked state. Short fights were paying the same as long
  ones; now the trivial fight is worth exactly what it looks like. It needs no
  counterweight because `DefeatCount` is already both reward and threat, so it
  buys two cycles of drop quality *and* hands the dummy two cycles of statline.
  And it puts itself out of business by the most direct route: the bar *is* max
  HP, which revival scaling raises. It also makes **burst the farming build**, a
  distinction the classes did not previously have.
- **The accidental clean kill is a feature, not a leak.** Crits bypass `Block`
  (§1.6), so a natural 20 can clear a bar the same weapon's ordinary swing
  cannot — landing two cycles of statline on a dummy that was already winning,
  returning in three turns instead of eight. The usual objection to RNG driving
  a progression rate does not apply when the thing luck advances is also the
  thing that kills you: there is no free-value version, only a get-further-into-
  trouble-faster version. It is the one reward in the design nobody chose to
  pursue, which is exactly what makes it memorable.
- **Caster uniques take one of three shapes** — a unique enchantment, a
  catalogue one at tier 3 (the only place a drop starts above tier 1), or **two
  non-opposing catalogue entries** (the only place a drop carries more than one).
  The Long Candle is the third: a plasma beam carrying `Shocking + Flaming`. The
  last two are the same idea on the two axes — somebody else already paid the
  mana for depth, or the downtime for breadth — and neither bends a rule, which
  is deliberate. Not every artifact should break the frame.
- **`Light` and `Resonant` exclude each other** — the **currency** group. Mana
  regenerates from movement left unspent, so cheap movement is already cheap
  mana by the long route; a weapon with both compounds one discount with itself.
  One currency per weapon, so a mana-efficient dagger and a light dagger are
  different weapons.
- **The modifier is named `Resonant`, not `Cast`.** It no longer has anything to
  do with casting — it discounts mana on any weapon that spends it — and an
  adjective matches its opposite number `Light`. `Attunement` was unavailable
  (damage types took it) and `Efficiency` would have collided with the variant
  role whose modifier is `Light ×1`.
- **An efficient caster is harder to level.** Mana moved is enchantment XP, so
  once `Resonant` stacks make a caster movement-bound rather than mana-bound, every
  further stack moves less mana for the same casts. More bang per point spent,
  and a slower climb — the first modifier in the design with a real downside
  written into it rather than an opportunity cost.
- **A staff's effect and a wand's damage type *are* their enchantments.** The
  staff's is fixed by variant, because a staff is its effect; the wand's is
  rolled, because a wand is its shape. So a staff levels the effect it casts by
  casting it, and that effect can be transferred onto a dagger.
- **Four damage types, two opposed pairs** — Flaming/Cold, Shocking/Acidic. All
  four are ordinary catalogue enchantments. Against a target's attunement: same
  halved, opposed ×1.5, otherwise unchanged. Attunement belongs to the **themed
  floor**, not to individual enemies, so the counter is knowable before
  descending. Halved rather than nullified, because nothing in this game has a
  zero.
- **`Arcane` (was `Arcane Edge`) is elementless**, so the chart never touches it
  — the safe damage enchantment against four situational ones. Renamed because
  it goes on wands, which have no edge.
- **Only opposed enchantments exclude each other.** Any non-opposing combination
  may share a weapon; the lock budget is what actually limits breadth, so a
  second rule would be a cap by another name.
- **Wear is a resource, not damage.** Nothing breaks; using a weapon is what
  makes it enchantable.
- **Service costs runs, not gold** — `enchantments + 1` runs per attachment, so
  power and availability trade off directly.
- **Entering does not tick service.** A successful run ticks; **failing at floor
  3 or deeper** ticks once as mercy — however it ended, death or retreat — so a
  losing streak cannot freeze the workshop. The mercy tick pays for *failing
  deep*, never for leaving early, or a two-floor exit would be a strategy.
- **A revival is faster as well as stronger.** `resurrectTurns = max(3, 10 −
  DefeatCount)`. Speed front-loads and the statline back-loads, so the early
  farm gets busy and the late farm gets dangerous. Three is a floor because a
  two-turn revival is a treadmill rather than a fight.
- **Farming arms the corridor behind you.** Resurrection stops only when the
  boss dies, so retreating without one means climbing back through everything
  you farmed, stronger and returning in three turns. The boss is not the last
  obstacle before the exit — it is what makes an exit exist.
- **A run is successful when you kill the boss.** One boss per dungeon, on a
  floor rolled 5–10 at entry and not disclosed; the boss floor is the bottom.
- **A deeper boss pays more, and costs the dungeon for longer.** The drop
  carries `bossFloor − 4` acquired stacks — 1 at floor 5, 6 at floor 10 —
  distributed as a farm's are but **guaranteed** rather than rolled for. Six is
  deliberately short of the farm's ten: entering a dungeon is a choice and
  farming is not, so certainty is already most of the reward and does not also
  get to be the largest. Service ticks stay at one per win, because the tick
  rewards the achievement and the drop rewards the depth.
- **Clearing a dungeon locks it for `bossFloor` runs.** The depth that paid you
  is the depth that takes it away, so deep and shallow runs trade rather than
  rank. Counted by the same events that tick the enchanter, so there is no
  second clock. **Failing locks nothing** — a dungeon that beat you is available
  immediately. **The tutorial does not come back at all** — beating it removes
  it from the hub permanently, for lore reasons rather than economic ones. It is
  also the thing that unlocks the hub, so no party is ever stranded by it.
- **`Charges` is the only negative graft.** The sweep is done; nothing else in
  the table takes something away at `×1`. Still worth a test asserting every
  modifier's first stack is a non-decrease, so a future modifier cannot
  reintroduce the shape unnoticed.
- **A boss never rolls a unique, but its graft can produce one.** There is no
  unique roll on a boss kill — the repeat-kill ladder is the only place uniques
  are rolled. When the drop happens to roll the **Purity** variant of the theme
  modifier's own class, though, the theme graft takes it from `×2` to `×3` and
  the result *is* that class's unique: Tower Guard + the golem's Block = The
  Bulwark. Nothing special-cased it; §1.5's derivation rule and §4.3's graft met.
  So each themed dungeon can drop exactly one named unique, and themes are drawn
  from class signatures for that reason. Only the **boss's own** drop can do
  this: an ordinary drop that merely *rolls* the theme gains it acquired, and
  §1.5 defines a unique by its forged spread, so a `Block ×2 + acquired` Tower
  Guard is a good weapon rather than The Bulwark.
- **Each dungeon is themed on one modifier.** Its boss carries that modifier
  innately whatever it wields, and **its own drop** is *forged* with it while
  every other drop in the dungeon merely **rolls** for it (acquired, so
  shallower).
  That is a guaranteed graft rather than a mechanism of its own, and the reason
  to choose one dungeon over another is that it is the only **chosen** off-class
  modifier — and the only one that runs a stack deeper than a rolled graft.
- **The shipped dungeon is the tutorial**, themed on Block, with a stone golem
  for a boss.
- **The enchanter runs the tutorial dungeon.** He built it and powers the golems
  to train the party on what they are about to face; the BBEG holds dungeons of
  5–10 floors, and the enchanter can manage two, once. That single premise
  supplies three mechanics that were each argued separately: the fixed depth of
  two (his pool is the smallest in the game), the permanent retirement on
  completion (he powers it down and returns to work, rather than it being
  consumed), and **no enchanting service for the whole tutorial** — powering
  golems *is* his mana. The workshop opens when the training ends, which is why
  one tutorial run lands exactly on the one-run cost of a first enchantment.
- **A class has a baseline plus four role weapons** — Efficiency (`Light`),
  Purity (more of the signature), Control (degrade the enemy), Support (help
  the party). The old universal "crit-specced" slot is gone; a crit class puts
  crit in its baseline instead.
- **Crit riders are per-weapon modifiers**, `CritWeaken` and `CritSunder`, not
  a per-class field — so a stack stays a count rather than a payload.
- **The dagger's baseline is `CritWindow ×1, CritMultiplier ×1`**, with its
  four weapons adding `Light`, another `CritWindow`, `CritWeaken`, and
  `CritSunder`.
- **All six martial classes are specced** — sword (`Riposte`/`BlockWeaken`), spear
  (`Push`/`Pin`), axe (`Splitting`/`Rout`), ranged (`Pin`/`Overwatch`),
  throwing (`Drag`/`Softening`). Caster classes keep their own frame: baseline
  `Resonant ×1` plus four effects or four shapes, since role decomposition means
  nothing when the signature *is* which effect you cast.
- **`Pin` appears on both spear and ranged**, delivered by a brace and by a hit
  respectively — the two halves of a kiting pair.
- **Throws are cheap (15) and hard-capped by `Charges`**, not free. The cap is
  the binding constraint rather than the budget, so the class fights *and*
  keeps movement — which banks and regenerates mana, making low-Charges an
  economy weapon and high-Charges a damage one.
- **Attack cost tracks weight**: throwing 15, dagger and bow 30, sword 50,
  spear 55, axe 60. The spear was cheaper than the bow, which had a polearm
  swinging faster than an archer looses.
- **The bow is deliberately weak up close.** Damage 5 stays; the answer to an
  enemy in your face is swapping weapons, not a stronger baseline. Every stat
  owns both a close and a ranged answer, so swapping never costs progression
  rate.
- **INT solves range by borrowing.** No martial classes of its own — a high-INT
  character enchants a dagger or a bow and delivers INT-scaled damage through
  it, since enchantment potency ignores weapon proficiency.
- **Swapping costs 20 movement in combat, free out of it**, gated on the
  existing `AnyLiveEnemySeenThisTurn` signal. Free swapping would make every
  weapon's downside optional rather than only the bow's.
- **Consumables cost movement and occupy inventory slots**, making the 24-slot
  budget a three-way trade between weapons, potions and haul.
- **The starting party is dagger, sword, axe, staff** — both spears go, one to
  an axe on C and one to a staff on D, with a debuff staff in D's bag. Brace is
  taught by enemy spear dummies instead of a party spear.
- **The tutorial is consumed by beating it**; every other dungeon is picked
  from the hub and repeatable. Rushing its boss permanently forfeits its themed
  drops, which is the game's central decision delivered once in miniature.
- **The tutorial is two floors deep**, boss on floor 2 — the only hardcoded
  depth in the game. Shortest run that demonstrates every dungeon mechanic, and
  a fast turnaround into the hub since it cannot be replayed.
- **Modifiers can live on an actor, not just a weapon.** `ActorState` carries
  its own `ModifierSet`; resolution reads weapon plus innate.
- **Killing the boss stops resurrection**, turning the dungeon from an infinite
  farm into a finite clear — so winning ends your farming, and choosing when to
  stop farming is the run's central decision.
- **You fight your way out.** The boss turns the party around; you climb back
  through every floor killing everything a final time.
- **`DefeatCount` is the drop's quality, the last kill collects it.** Farming
  banks value into a dummy; you harvest it on the way out. Die in the
  stairwell and you keep the service tick but none of the spoils.
- **Carry limit is unified with the loadout** — 6 slots per character, 24
  party-wide, no separate haul bag. Loot competes with weapons, so hauling
  deep makes you weaker for the fight you are hauling through.
- **Floors can be skipped on the way out.** No gate is needed: brace taxes
  running through, skipping forfeits that floor's drops, and wear gating means
  a rushed run earns service time it cannot spend.
- **Service has a low chance of adding a modifier stack**, scaled by wear
  brought in and bounded by the §1.1 caps — this is how a weapon improves
  across runs rather than merely accumulating attachments. It can **deepen**
  what the weapon carries or, rarely, **graft** something it never had; the
  graft is safe because `forged = 0` caps it at 5, so it is permanently the
  shallowest thing on the weapon.
- **What comes back is stronger than what died.** Every revival rolls `+5`
  damage, `+5` max HP, or both — one third each — accumulating on the actor and
  never on the weapon it drops. `DefeatCount` therefore means two things at
  once, deliberately: the quality of the drop and how dangerous the dummy has
  become. You cannot bank value into a dummy without arming it, and the
  extraction goes back for exactly the ones you armed. The scaling never stops,
  and `+5` is a placeholder for the step rather than a settled number.
- **The repeat-kill ladder has no top; the danger curve is the top.** An earlier
  draft capped it at `DefeatCount 5` because farming's only cost was turns, and
  turns are cheap. Revival scaling makes that obsolete — every cycle is a harder
  fight — so the ladder runs forever and prices itself. Stacks accrue on the
  the weapon by **two rolls per defeat** — 50% for a stack at all, then
  uniformly for which forged modifier receives it. A farm grants at most **10
  stacks**, and §1.1's per-modifier cap of 5 is what forces those across at
  least two modifiers rather than needing a rule of its own. Depth only, never
  breadth — the roll can only pick a modifier the weapon already carries, and
  breadth stays the enchanter's product. A two-modifier weapon farmed to the end
  is *complete*, both dials capped; a three-modifier one comes out good at three
  things with room left for the enchanter.
- **Four relations govern what a weapon may hold**, all tables rather than
  special cases: **Excludes** (cannot coexist), **Requires** (cannot exist
  without), **Kind** (melee / ranged / caster only), and **ForgedOnly** (can be
  deepened, never added).
  - Excludes: four groups — **threat zone** (`Brace`/`Opportunist`/
    `Overwatch`), **displacement** (`Push`/`Drag`/`Rout`), **block
    response** (`Riposte`/`BlockWeaken`), **crit rider**
    (`CritWeaken`/`CritSunder`), and **currency** (`Light`/`Resonant`). A group
    exists either because holding
    both would be incoherent (one hit cannot move a target two ways) or because
    it would collapse a distinction the design drew on purpose — §1.2 argues
    `Riposte` and `BlockWeaken` are the same trigger split by who collects, and
    a shield holding both erases that. One threat zone per weapon also stops a
    front line turning the enemy phase into a second player phase, bounded at
    the weapon where the player can see it rather than by a new per-character
    resource.
  - Crit riders **are** a group, decided on symmetry rather than on the two
    tests above — they pass both, but they are the dagger's Control and Support
    variants and so the same shape as the sword's block responses, and answering
    the same question two ways on two classes is no rule at all. Revisit by
    ungrouping if they play underwhelming, not by raising the per-level `+1`.
  - ForgedOnly: `Charges` alone. It is a **cap, not a bonus**, so a graft would
    make a bow *worse* — 2 shots where it already had 5. Every other modifier is
    at worst inert on the wrong weapon. The rule sharpens §1.1's to *no stack
    may be dead, and no graft may be negative*, and it sidesteps needing a
    throwing-only `WeaponKind` that the melee/ranged/caster flags cannot express.
  - Requires: `Riposte` and `BlockWeaken` need `Block`, `Rout` needs `Cleave`.
    Each would otherwise be a dead stack, so it is illegal rather than bad. This
    makes grafting **order-dependent** — a dagger cannot be offered `Riposte`
    until it has grafted `Block` — and since nothing is ever removed, a
    satisfied prerequisite stays satisfied.
  - Kind: `Brace`/`Opportunist` melee, `Overwatch` ranged. `Resonant` is *not*
    kind-restricted — it discounts enchantment triggers, so any weapon carrying
    an enchantment wants it; it is merely forged on casters alone.
- **`Riposte` is outside the threat-zone group on purpose.** It fires on the
  enemy turn like the other three, but it answers *being hit* rather than *being
  approached* — no zone, no movement, nothing the enemy could have walked
  around. Grouping it there would be grouping by when it resolves rather than by
  what it responds to.
- **A themed boss cannot drop a class that will not take its theme.** The drop
  table bends to the guarantee rather than the other way round: a `Brace`-themed
  boss drops daggers, swords and spears and nothing else, because axes hold
  `Opportunist` and the rest are not melee. Dropping *ungrafted* axes was
  rejected — it puts a footnote on the one thing the theme promises. Theme
  breadth therefore varies (`Block` reaches all eight classes, `Brace` three),
  which makes narrow themes specialised dungeons rather than broken ones.
- **`Opportunist` is the axe's second baseline** — a free attack when a target
  *leaves* your reach, the exact mirror of `Brace`. It answers the hole in the
  class: reach under two tiles, the game's highest attack cost, no way to chase,
  and `Cleave` rewarding a crowd that could simply walk off. Spear and axe now
  form a cage — wide threat coming in, tight threat going out. It fires on
  **voluntary movement only**, unlike `Brace`, so a `Rout` cannot detonate the
  wielder's own opportunity attacks and displacement becomes the safe way to
  break contact.
- **Forced movement triggers threat zones.** Shoving an enemy into a spear's
  reach gives the spear a free attack — `Push`, `Drag` and `Rout` route through
  the same per-tile movement path a walk does, so `Brace` and `Overwatch` fire
  on every zone *entered*. Leaving a zone does not fire, which is what keeps the
  Halberd's break-contact purpose coherent, and an ally's shove never triggers a
  bracer on its own side. The existing per-turn brace budget is the recursion
  guard, so displacement chains terminate on their own. This is the game's best
  party-composition combo and it needed no new rules: sword shoves into the
  spear line, a thrower's `Drag` pulls toward it, an axe's `Rout` fires several
  at once.
- **Every class baseline carries a class-specific second modifier**, so no
  weapon is forged with fewer than two and every forged spread is either three
  at `×1` or one `×2` and one `×1`. Mechanically this stops a Purity variant
  being the one weapon that could not absorb a full farm (§3.2). The picks:
  `Push` on sword & shield — a shield bash, and the positioning tool the class
  completely lacked; `Longshot` on spear, whose reach is pinned to exactly four
  tiles so it pays only at full extension, reinforcing the `Brace` threat zone;
  `CritWindow` on ranged, making DEX the precision stat and the bow the second
  crit-frequency class; `CritMultiplier` elsewhere, which is not filler — crits
  bypass `Block` (§1.6), so a natural 20 at `×3` is a real answer to armour.
- **Unique chance accelerates toward an asymptote, never to certainty.** A
  logistic on `DefeatCount`: ~1% at 5, 11% at 15, 25% at 20, flattening toward a
  50% ceiling. A flat per-cycle rate made deep farming a novelty; the S-curve
  gives it a hot zone that lands on exactly the cycles revival scaling has made
  dangerous, and the ceiling keeps it a gamble rather than a long safe purchase.
  Rolled once, on the permanent kill.
- **The unique roll is the only thing that undoes farming's cost.** Ordinary
  tiers spend the acquired budget the enchanter would have filled, so a
  fully farmed drop can never be improved on its signature again. A unique's
  spread is forged, so winning the roll lands a deeper base with the budget
  untouched. Lose it and you carry out the most finished weapon in the game; win
  it and you carry out the best project in the game.
- **A modifier caps at `forged + 5`**, per type, with types independent — no
  shared budget across a weapon. Forged is the weapon's identity spread (class
  baseline, variant, unique, dungeon theme); the five is one acquired budget
  that farming and the enchanter both draw on. A flat cap was rejected because
  it erases the Purity role, whose only distinguishing feature is the stack a
  shared ceiling absorbs first.
- **Forged means chosen identity, not "what it dropped with."** Repeat-kill
  stacks arrive on the body and still count as acquired; the boss's theme
  arrives on the body and counts as forged, because you chose the dungeon.
- **Nothing is capped at the ceiling. The game caps what it *gives* you, never
  what you can build toward.** A capped modifier contains dead stacks, and a
  dead stack is a farm cycle or a service roll that bought nothing. Two tools
  handle a modifier that would misbehave deep, and both act on the forge:
  **re-price the stack** (`Charges` grants 1 throw plus 1 per stack, so the
  deepest spread still fits the movement budget) or **limit the forge**.
- **A unique is a variant with exactly one modifier raised to `×3`** — derived,
  never freely authored. That bounds the forge without a second rule (`×8`
  becomes the universal ceiling every per-stack value is priced against), keeps
  a unique legible as "a weapon you know, more so", and stops "more special"
  turning into "more modifiers". Each class therefore has four possible uniques,
  one per variant, and the Control and Support derivations are the richest since
  they keep a second modifier.
- **The other forge limit is `Light ×1`**, uniques included, so `Light` ceilings
  at `×6` for −60%. It also means an Efficiency-derived unique cannot raise its
  own added modifier and is forced onto the class signature — coming out
  `signature ×3, Light ×1`, the only shape in the game that is both deep and
  cheap to swing.
- **Crits are not blocked at all.** That is what lets `Block` scale to `×8` like
  everything else instead of being capped — the shield stays excellent and
  armour stays answerable. The dagger is the burst answer, the axe's `Splitting`
  the grind answer, and a natural 20 is everyone's. `Riposte` and `BlockWeaken`
  do not fire on a crit, since no block succeeded.
- **Uniques have the deepest ceilings in the game**, since their whole spread is
  forged. The balance envelope is held by the unique tables being hand-authored
  and by the `×3` forge limit, not by a cap on what play can reach.
- `CritWindow` is **+1 per stack**: ×1 crits on 19–20, ×2 on 18–20, and so on.
- **Crit frequency is narrow in practice rather than by rule.** The dagger's
  baseline is `CritWindow ×1` and its crit specialist `×2`, a deliberate break
  from the prototype's 16+, so a crit is a 10–20% event for essentially the
  whole game. A fully worked Widowmaker reaches 45% on 12+ for a ×8 multiplier,
  and that is allowed to exist: getting there is five separate low-chance
  services landing on the same modifier, so it is an artefact of a campaign
  rather than a build anyone picks. `CritMultiplier` remains the investment
  axis, so crit builds stay rare-and-catastrophic rather than constant.
- **Wand friendly fire is on, at half damage, on both sides.** Half is what
  makes a simple placement scorer shippable — a mediocre wand enemy is
  inefficient rather than suicidal. Ships behind a constant, flipped on when
  the scorer lands.
- **The scorer weights allies at 1.5×, not at the half they actually take.**
  The weight is a policy, not an EV calculation: it refuses even trades, so
  baiting a caster into its own line takes real positioning instead of just
  standing nearby.
- **If it needs a cap, it is an enchantment.** A weapon modifier has to be safe
  at its ceiling by construction — ×8, the deepest anything reaches now that
  caps scale; anything needing a bespoke ceiling goes to the
  enchantment layer, where the mana lock and trigger cost bound it organically.
  `Momentum` moved there for exactly this reason.
- **A modifier on a value that varies across classes is proportional.** `Light`
  is −10% of the weapon's cost per stack, not a flat subtraction — attack costs
  span 20–60, and flat would zero out a dagger while barely touching an axe.
- **Tunable numbers live in `tuning.json`** (§5.3) — read once at startup,
  never hot-reloaded, compiled fallback for every missing key, scalars only.
  Nothing the golden tests pin may ever appear in it, enforced by a test that
  asserts the schema is disjoint from the parity-critical names. Ratios are
  **integer divisors with the units in the name** (`MovementUnitsPerMana: 10`),
  because §1.3 already made the tiles-versus-units error once and it was a
  factor of 32. Saves record a hash of the values they were played under and
  warn on mismatch rather than refusing.
- **Content lives in four validated files** (§5.4–5.8) — `restricted.json`,
  `enchantments.json`, `weapons.json`, `dungeons.json` — loading in that order,
  since relations validate everything, weapons name enchantment ids, and
  dungeons name modifiers. Invalid content **aborts startup** naming the entry
  and the rule, rather than clamping as `tuning.json` does, because a broken
  invariant is not a bad balance number.
- **Relations are data, not code.** `restricted.json` holds `excludes` (as
  *groups*, expanded to directed pairs at load), `requires`, `kind`,
  `forgedOnly` and `neverRolled`, for modifiers and enchantments alike. The
  earlier reasoning — that relations validate content and so cannot be content —
  was wrong in the way that mattered: if declaring a relation needs a recompile,
  then **adding a modifier needs a recompile**, and the additive property the
  split exists to buy is not real. The validation concern is answered by
  validating the relations too (ids resolve, nothing excludes itself, `requires`
  acyclic, nothing both requires and excludes the same id) and by loading them
  **before** content — so a relations edit that invalidates a weapon aborts
  naming *both*, which is better than freezing either.
- **`forgedOnly` and `neverRolled` stay separate.** One is a safety rule
  (`Charges` granted from zero would make a bow worse), the other an economy
  rule (a unique enchantment the enchanter cannot copy). Collapsing them would
  lose the reason either exists.
- **Everything is referenced by stable string id, never index.** Adding a weapon
  otherwise re-rolls every enemy in every existing save; shifting an enchantment
  id silently turns a player's earned catalogue into a set of different
  enchantments.
- **The point of it is that mechanics become additive.** Behaviour and relations
  stay in code, values are `tuning.json`, and which weapons and dungeons *use*
  them is content — so adding a modifier is: implement it, price it, declare its
  relations, and it is then usable on any weapon in any spread without touching
  code again. That is §1.1's uniformity finally paying out in the workflow
  rather than only in the design.
- **A dungeon's drop table is computed, not authored.** §4.3's rule that a
  themed boss cannot drop a class that will not take its theme *is* `Allowed`
  run over the eight classes at load. Authoring it by hand would let the two
  disagree, and the disagreement would look exactly like a drop-rate bug.
- **`Overheal` converts healing above full HP into `Ward`**, lossily, at
  `OverhealPerWardLevel` (start at **5**) — a salvage mechanism rather than a
  second healing pool. Unique-level by the same test as the rest:
  `TickStatusEffects` discards overheal on purpose, and §2.2 rests "constitution
  grows by getting hurt and then healed" on that. The converted overflow grants
  **no HP XP** — it is `Ward`, not healing — so the surplus stops being wasted
  for *survival* while staying wasted for *progression*, and a healer cannot
  farm CON off a full-HP party. Its best use is paired with `Vampiric` on a
  weapon that never heals anyone otherwise.
- **`Ward` is temporary health, not armour**, which is why crits do not bypass
  it. It does not reduce a hit, it takes it — so a crit finding the gap in
  armour makes sense and a crit finding the gap in *being alive* does not. That
  framing also answers on its own why it spends one per HP saved, why it decays,
  and why it stacks with `Block` in that order. The party gets a crit counter it
  was never designed to have, which beats making armour crit-proof and taking
  the natural 20 away again.
- **The Bulwark's enchantment is `Sturdy`**, not `Warding`. That settles the
  one-letter collision with the staff's `Ward` by renaming the *rarer* of the
  two, so the catalogue entry and the `Ward` status it applies both keep the
  obvious name — and `Ward` stays free for the thing `Overheal` pours into.
  One rename instead of two, and `Sturdy` reads better on a shield that refuses
  to die than `Warding` ever did.
- **Party-stacked `Ward` accumulates, deliberately.** An ally's temporary health
  is still temporary health, and making a second caster's contribution vanish
  would be a bespoke exception to the only thing the status does. So a patient
  party can walk into a boss room with an absurd pool and gum an overlevelled
  boss to death — a legitimate strategy, and a funny one. Three existing rules
  bound it without anything being written for the purpose: mana throughput
  prices it in hundreds of turns, **resurrection makes those turns dangerous**
  (you cannot bank in peace before the boss, and after it there is nothing to
  spend on), and decay charges you for the walk. The 1-damage floor keeps it
  from ever being immortality — a big pool buys a *long* fight, not an unlosable
  one.
- **`MovementUnitsPerMana` is 10**, and it is *derived* rather than picked: a
  caster at `Resonant ×6` with a tier-1 enchantment should sustain one cast a
  round while standing still, which is `(160 − 40) / (8 + 3) ≈ 10.9`. Stated as
  the rule rather than the number, so it re-derives when the movement budget or
  cast costs move.
- **Sustained casting is the reward for maxing `Resonant`, and it is a cliff.**
  Nothing below `×6` sustains — `×5` misses by two mana — so shallower stacks
  buy *sprint length* rather than sustain. That sharp edge is deliberate: "the
  staff that never stops" is a legible endgame achievement in a way "the staff
  that stops 18% later" is not, and `×6` costs five acquired stacks.
- **Casters are sprinters.** Four casts a round while the pool lasts, one a
  round sustained at `×6`, and **~10+ turns to reload** against a martial
  class's one. Neither is stronger; the caster's shape is the one that has to be
  *timed*. Two things follow without a rule: the walk to the fight **is** the
  reload, so marching is when a caster refills — and `Resonant` buys sprint
  length before it buys sustain, so an efficient staff fights longest while
  still levelling its enchantment slowest (§3.3).
- **A deposit buys one of two things: an enchantment, or a gamble.** Enchanting
  attaches a chosen soul and leaves a token chance of the weapon improving;
  **refinement** attaches nothing and rolls at much better odds (§6.4). It is
  the same trade the whole design runs on — the guaranteed thing, or the rare
  thing.
- **The two rates are one mechanism.** Attaching consumes wear, and the
  improvement roll is paid out of the remainder, so refinement's better odds are
  literally *the price of the enchantment, unspent*. No second knob, and three
  consequences arrive free: a fresh drop refines for nothing, hoarding wear
  before a deposit is a real decision, and refinement cannot be spammed because
  the pool — not the clock — is what limits it.
- **A deposit empties the weapon's wear**, whichever service it bought. That is
  what makes *when* to deposit a decision rather than a formality.
- **Refinement costs the same downtime as the enchantment it replaced** and does
  **not** advance the service rung. The gamble delays breadth; it never makes
  breadth more expensive. A weapon at five enchantments therefore has refinement
  as its only remaining service, which closes the fork at the top end without a
  rule.
- **Refinement improves whether the roll lands, not what it lands on.** Deepen
  stays common and a graft stays the surprise, so §1.1's ceilings remain the
  thing shaping a weapon rather than a drop table.
- **`WearPerHit = 1`.** Per hit rather than per swing, per turn or scaled by
  damage, so the pool is a plain count of work done — a multi-hit weapon is
  neither rewarded nor punished, and the number stays one a player can hold in
  their head while deciding whether to deposit.
- **Wear is bounded by `WearCapacity`, and that ceiling *is* the durability
  system** — read from the other end. `durability = WearCapacity − wear`; one
  number, and the design keeps the wear direction as the vocabulary so the
  counter only ever goes one way. Service repairs and spends in a single action,
  because those were never two events.
- **A fully worn weapon is not broken.** It swings identically; it simply stops
  banking, so further hits are work done for free. That is the entire penalty,
  and it is the right one — this design has no punishment mechanics, and
  degradation would be a death spiral in a game whose way out of trouble is to
  fight. It also supplies the bound on hoarding that §6.4's deposit-timing
  decision needed: you cannot wait past full.
- **A currency modifier can never make a unique unique.** `Light` and `Resonant`
  are both forge-limited to `×1` (§1.1), so there is no `×3` to reach for and
  the `×1` an artifact carries is the same one its common variant had. **Every
  `Light`-forged artifact's identity lives in its enchantments**, not its
  forged spread. This is one rule covering what §1.5 previously treated as two
  coincidences — the Efficiency unique and the caster unique hit the same wall
  for the same reason.
- **An Efficiency unique carries two enchantments, one of them unique or at
  tier 3.** Every other martial unique is `signature ×3` plus a distinct
  modifier plus one soul; an Efficiency unique spends that second axis on
  `Light` and is paid in souls instead. Not a new shape — it is the caster menu
  handed to a martial weapon.
- **`Light` is the only martial modifier that generates mana**, which is why it
  is the one that can afford two souls. Triggers cost mana (§3.3), mana comes
  from movement left unspent (§1.3), and cheapness is exactly what `Light`
  produces — which is also why it excludes `Resonant` (§1.1). The second
  enchantment follows the mana rather than decorating the weapon.
- **The Efficiency unique is therefore the only martial weapon that runs a
  combo**, because two souls can feed each other where one must stand alone.
  The dagger from the Flensing Knife carries `CritWindow ×3, Light ×1`,
  `Vampiric` and `Overheal`: it crits on 17+, lifesteals, and banks the surplus
  healing as `Ward`. Neither half works without the other, so the gimmick can
  only exist on the shape with two slots. This also settles where `Overheal`
  lives — a crit-heavy lifesteal dagger produces surplus healing without being a
  healer, and a staff never needed help finding value in healing.
- **`Overheal` drops on two weapons: the Efficiency dagger and a Staff of
  Renewal unique.** The dagger pairs it with `Vampiric` — a selfish build
  converting its own crits into its own survival — and the staff pairs it with
  `Regeneration`, a party build turning a healer's routine waste into everyone's
  shield. Not the same enchantment twice; picking one home would discard the
  better half.
- **A unique enchantment may have more than one home**, provided each home can
  actually run it. That is not a loosening of what unique means — `neverRolled`
  still holds, no service grants it, no other drop carries it. **A soul with a
  dependency is defined by the dependency rather than by scarcity**, so it goes
  everywhere the dependency can be met.
- **The currency group is the combo platform.** `Light` generates mana from
  unspent movement, `Resonant` discounts it at the trigger; both are
  forge-limited to `×1` so neither can buy depth, and both are paid in souls
  instead. They are therefore the only two shapes carrying two enchantments, and
  the only place in the design where one enchantment may depend on another
  existing.
- **A `Light` artifact trades forged depth for souls at one stack each**, with a
  floor of `signature ×2` — below that it is no deeper than the variant it
  derives from and stops being an artifact. So `×3` buys two enchantments and
  `×2` buys three, and three is the most any weapon in the game can carry.
- **The Efficiency dagger is `CritWindow ×2, Light ×1` + `Serrated`,
  `Vampiric` tier 3, `Overheal`** — three souls forming a *chain* rather than a
  pair, each link consuming what the last produced: a hit becomes continuous
  damage, damage becomes healing, surplus healing becomes `Ward`. Dropping the
  third `CritWindow` is the point rather than a cost — `Vampiric` on a burst
  weapon heals in spikes that mostly land on a full wielder and are thrown away,
  so the dagger converts its output to a stream *in order to* make its healing
  continuous.
- **`Serrated` applies `Bleeding`**, which joins the level family alongside
  `Poison`, `Searing`, `Sundered` and `Weakened` — the enchantment-and-status
  pairing `Flaming`/`Burning` already uses.
- **`Serrated`'s magnitude comes from the weapon and the wielder, not its
  tier**: `BleedPercent × (base damage + proficiency level)`. It is the first
  enchantment whose power belongs to the character, and it is the right
  exception — a unique is pinned at tier 1 (§3.3), which would otherwise freeze
  a *magnitude* forever and make it a dead stack. The fix is not to unpin it but
  to hang it off a ladder already climbing (§2.2). It also makes `Serrated`
  self-limiting on transfer: the soul moves, the proficiency term resets to the
  new class's skill, and the strength does not follow.
- **`Vampiric` fires on *damage dealt* and heals a flat 1 per tier**, at a
  trigger cost of 2 rather than 10. Both halves of the old entry were wrong
  together: a bleed tick is damage and is not a crit, so the dagger's chain
  could not close; and healing for a *share* of the damage is a percentage of a
  number that grows all campaign.
- **A trigger is priced against how often it fires.** The cost fell from 10 to 2
  because the frequency rose — the enchantment-side version of §1.1's re-price-
  the-stack rule. An every-instance trigger at a crit-shaped price would be
  unpayable.
- **Flat-per-instance makes `Vampiric` a build rather than a bonus.** A greataxe
  swinging once heals `tier`; a dagger with `Serrated` running on three enemies
  heals `4 × tier` without swinging. It rewards weapons that generate
  *instances* — daggers, `Charges` throwers, lingering-element wands — which is
  a new itemisation axis, and the reason the Efficiency dagger trades a
  `CritWindow` stack for `Serrated`.
- **`Bleeding` does not trigger `Vampiric`.** A tick is damage the *status*
  deals, on the enemy's turn, from a wound whose applier may no longer hold the
  weapon — paying lifesteal on it needs a status to remember which character
  left it, plus an ordering rule. Triggers fire on damage the wielder deals on
  their own turn.
- **So the Efficiency dagger is a pair plus a theme, not a three-link chain.**
  `Vampiric` → `Overheal` is the loop; `Serrated` is the offensive soul on a
  weapon whose other two are defensive. **Not every soul on a combo weapon has
  to be in the combo** — over-synergised items only work fully assembled and
  stop being readable. Theme is free and this synergy was not worth its price.
- **`Light` is the engine, and it supplies both halves.** Cheap attacks are the
  instances `Vampiric` converts (a dagger at `×6` costs 12 movement, not 30);
  movement banked at end of turn is the mana those triggers cost. And because
  one budget pays for both, the build governs itself — a turn spent swinging is
  a turn not spent banking, so nobody runs it at maximum for free. No rule was
  written for that.
- **Unique enchantments are pinned at tier 1 permanently**, which §3.3
  previously contradicted by claiming nothing sits at tier 1 forever. A unique
  bends a rule rather than supplying a number, and a rule has no second tier.
  They still accrue mana spent; it simply buys nothing. `Serrated` is the one
  unique that *is* a magnitude, which is precisely why the pin forced it onto
  the weapon's and wielder's ladders instead.
- **`Overheal`'s exchange rate therefore never improves.** What grows is the
  input — a deeper `Vampiric`, a deeper `Regeneration`, more attacks a turn —
  never the conversion. That is what stops a `Ward` engine compounding: every
  term feeding it climbs and the term converting it does not.
- **Every unique enchantment that is a *magnitude* borrows a ladder**, taking a
  percentage of the damage its source deals. That is the general rule and
  `Serrated` was its first instance, not a special case. A unique is pinned at
  tier 1, so a unique that is a *rule* needs no ladder — a rule has no second
  tier — but a unique that is a number would otherwise be frozen at whatever it
  did the day it dropped.
- **The wand DoTs ride their element's damage**: `Burning` off the wand's
  Flaming damage, `Frostbite` off its Cold, `Poison` off its Acidic. So
  `SearDamagePerLevel` — a flat constant, and therefore a ladder the pin cannot
  climb — becomes an `ApplyPercent` on the *level count*. A deep `Flaming` now
  leaves a deep burn. (Superseded in shape by the unified status model below,
  which moved the percentage from damage-per-level onto levels-applied; the
  principle it establishes is unchanged.)
- **`Mire` is the exception, because its magnitude is not damage.** It cuts a
  movement budget in movement units, where a percentage of a damage number means
  nothing, so it keeps a flat cut per level and scales by level count alone.
- **These ladders are partly a character stat**, which is new. Element damage
  carries INT scaling, so a smarter caster burns harder with the same wand —
  the same sentence §2.2 already makes true of a proficient fighter's bleed.
  Unique magnitudes are the only place in the design where an item's power is
  partly the person holding it.
- **A borrowed ladder lifts the price as well as the magnitude.** A unique DoT's
  `triggerCost` is `DotManaPerDamage × the expected total`, not a flat number on
  the catalogue entry. Lifting only the effect would be the tier-1 pin's problem
  pointed the other way — the damage growing all campaign while the mana stayed
  at whatever the entry was written with, making the lingering elements the one
  place in the game where getting deeper made you *cheaper*.
- **`Serrated`'s cost scales too**, at the same exchange rate. The flat 6 would
  have decayed into free by the end of a campaign. It also keeps the Efficiency
  dagger's economy tense at every depth: `Light` funds the triggers and the
  triggers get more expensive at exactly the rate the wielder gets better, so
  the swing-or-bank split is still a real decision at proficiency 20.
- **`DotManaPerDamage` lives in `tuning.json`, and it is the clearest case in
  the file.** One exchange rate governs what the entire DoT layer costs — every
  lingering element and every bleed, on every target in a shape — so it has the
  widest blast radius of any scalar and the least chance of being right first
  time. It cannot be reasoned to a value; it needs a session with a wand and a
  floor of enemies.
- **Charging a shape per target is provisional.** It is what prices the Nova's
  burst, but the fallback — one trigger per cast however many it catches — is a
  one-line change with no new constant, so nothing should be built that assumes
  either answer.
- **Two consumables: a health potion and a mana potion.** Emergency first aid,
  and that is the whole list — buffs, cures and utility all have better homes in
  this design as staves, statuses or modifiers. What it lacked was a way to
  survive the minute after the healer goes down.
- **The movement cost enforces "emergency" without a second rule.** Drinking
  costs a turn's worth of action when you are fine and costs nothing when you
  were about to die anyway, so nobody drinks routinely and everybody drinks at 4
  HP. No cooldown, no per-fight limit, no usage restriction.
- **Potions restore a *share* of max, not a flat amount** — the unique-magnitude
  rule (§1.5) reaching a third system. A number with no ladder decays into
  irrelevance, and a flat potion would be a resurrection on floor 1 and a
  rounding error on floor 9, which is backwards for an item that exists for the
  moment before death.
- **A consumable may break a rule a permanent item may not, because it breaks it
  once.** A mana potion breaks the movement-to-mana dependence outright, which
  is the exact thing that makes `Siphon` unique-level — the difference is that
  `Siphon` does it permanently. That licence is what the category is for, and it
  is why two potions are enough: anything wanting to bend a rule *repeatedly*
  should be an enchantment and pay an enchantment's costs.
- **For a caster a mana potion is the reload, skipped**, which is the emergency
  the sprinter shape (§1.3) creates: a boss arriving before the caster has
  refilled.
- **A potion grants health XP, and needs no guard**, because healing is capped
  at missing HP (`TurnSystem.cs:589`). A full-HP character who drinks restores
  nothing and learns nothing, so `hpXp` can only ever cash in damage actually
  taken — a potion cannot manufacture the wound it heals. That makes it a
  legitimate constitution source on exactly §2.2's terms: get hurt, then get
  healed, and who poured it was never part of the rule.
- **That is also where `Overheal`'s guard belongs.** It grants no health XP
  because it operates *past* the cap — surplus healing is by definition what
  lands on a full-HP target, so it is the one heal the missing-HP rule does not
  already bound. The cap protects ordinary healing; `Overheal` needed a rule
  because it steps outside it.
- **Potions are dungeon drops because the design has no currency.** Nothing is
  priced in money anywhere — the enchanter charges in downtime and wear, farming
  in turns, depth in mana spent. Every cost is something you *do*. Hub purchase
  is unavailable rather than rejected, and inventing an economy to sell two
  items would be a system built for the smallest thing in the game. It also
  keeps a crafting bench out of the enchanter, whose scarce time is spoken for.
- **One potion per inventory slot, no stacking.** Stacking would quietly undo
  the three-way trade §3.4 rests on: carrying four potions has to mean carrying
  four fewer of everything else.
- **Potions come from a farm in the hub, not from drops.** Every first-time
  clear adds a worker; the farm yields per run and the player takes the split
  between health and mana. This pays for the one thing nothing else in the
  design paid for — the design is depth-biased everywhere, and a dungeon's
  *first* clear had no reward attached even though the cooldown (§4.2) was
  already pushing the party toward breadth. The farm attaches a reward to what
  the cooldown forces.
- **It is production, not purchase, so the no-currency invariant survives.**
  Priced in dungeons beaten — the steepest cost in the game — and paid before
  the goods arrive. It also dissolves the question of what a potion drops off:
  nothing does, so the class-locked drop table needs no non-weapon outcome and
  the design needs no containers.
- **Production ticks per run**, the same unit as the enchanter's service clock,
  so the hub advances weapons and stocks the shelf in one event. A hub that
  worked while the player was *idle* would be the first reward here not paid for
  by playing.
- **The batch is a flow, not a stock.** You split the run's yield between health
  and mana at the alchemist, and **what you do not pick up spoils** — it is not
  waiting when you climb back out, and next run's batch is fresh. So the
  loadout decision happens every single run rather than once in the late
  campaign, a cautious player cannot hoard their way out of choosing, and the
  carry limit and the yield become one conversation instead of two.
- **Surplus yield buys quality, so the farm does not saturate.** A potion's
  `quality` is just how many units of the batch went into it, and it multiplies
  the share of max restored — no exchange table, no named tiers. §3.4 makes the
  *slot* the scarce thing rather than the potion, so the way past a full
  inventory is to make each slot worth more: a late party carries the same six
  potions as an early one, each worth five times as much.
- **Quality is linear on purpose**, so it buys slot efficiency rather than raw
  value. Many small potions cover several crises; few large ones cover one
  enormous save and lose granularity. **And a big potion wastes what it
  overshoots** with no rule needed, since healing caps at missing HP — large
  potions are lossiest exactly when they are least needed. A party carrying
  `Overheal` is the exception, and pleasingly so: the overshoot becomes `Ward`,
  so the build that wants enormous potions is the one that solved overhealing.
- **It asymptotes rather than saturating, at a ceiling a player can name**: a
  potion that heals you completely from 1 HP is the best potion there is, and
  past that quality is pure overshoot. At 20% a level that is quality 5, so a
  full loadout of six maximum-value potions is thirty units of yield — many
  first clears deep, with every clear before it buying something felt.
- **The alchemist arrives with the first non-tutorial clear**, and until then
  the whole category does not exist — no potions and no shelf. Same shape §6.3
  uses for the enchanter, one step later: beating the tutorial frees the
  enchanter and opens the workshop; beating a real dungeon brings back the
  people who work the farm.
- **The tutorial does not count toward the farm**, which needs no new rule — it
  is the same exclusion it already gets from mercy ticks, cooldowns and return
  visits, and it cannot be first-cleared twice anyway.
- **So the early campaign has no potions at all**, deliberately. A party learns
  to survive on positioning, the healer's mana and the movement budget before it
  learns to drink, and first aid arrives as something that changes how you fight
  rather than something you always had. The first real clear — already the
  hardest gate in the game — hands over a whole category instead of a number.
- **The farm's target is stated as a total, not a rate**, because the dungeon
  count is an unmade content decision and a per-clear number would inflate
  silently with every dungeon added. The rule: *a campaign that has cleared
  everything produces about one full loadout of maximum-quality potions per
  run*, with `FarmYieldPerClear` derived by dividing by however many dungeons
  end up existing. Same move as `MovementUnitsPerMana` (§1.3) — state the rule
  so it re-derives when its inputs move.
- **`HealthPotionPercent` sets the span rather than the height.** It decides how
  many units a maximum potion costs and therefore how much of a campaign passes
  before the farm stops mattering.
- **`ManaPotionPercent` starts equal to `HealthPotionPercent`**, making the split
  a pure preference. The argument for charging more for mana is that health is
  spent by being attacked while mana is spent by *choosing* to cast, so a caster
  can decline to run dry where a front-liner cannot decline to be hit. The
  counter is that the dry spell is the sprinter shape working as designed
  (§1.3), and taxing it taxes the thing the design built on purpose. Equal is
  the honest start, and the free split is what will reveal the answer: if every
  party mixes the same ratio every run, the prices are wrong.
- **Wear is what makes a weapon workable, and refinement is maintenance — not
  experience.** People grow; things are built and repaired. A weapon does not
  learn, it gets worn, and a worn weapon is one a craftsman can do something
  with: material worked into the places it thinned, somewhere for a new magic
  circle to be cut.
- **That is why wear gates both services and why a soul's price climbs**
  (§6.2). A circle has to ingrain itself before another can be laid beside it
  without erasing it, so a weapon carrying four is harder to add a fifth to — in
  material and in the smith's time. The two prices climbing together stops being
  a balance decision and becomes the same fact stated twice.
- **Refinement on an unworn weapon needs no rule, and needs no metaphor**: a
  pristine blade has nothing to repair. There is no 0% to explain. A craftsman
  handed a weapon never swung has no work to do on it, which every player
  already understands about every craft.
- **The graft stays a roll, because it is the craftsman's eye rather than
  luck.** A craftsman looking at his own work sees the mistakes — every time.
  What comes back changed is *what he happened to notice this pass*. The
  Disarming Kris that returns with `Block` has a cause: he found a flaw in the
  metal and reinforced it well enough that the blade now turns a blow. That
  earns the randomness rather than merely permitting it, and it settles the
  threshold-versus-roll fork — a threshold would mean the weapon is levelling,
  and things do not grow.
- **It also explains deepen-versus-graft with no extra rule.** Deepening is the
  same flaw seen again and worked further; a graft is a *new* one, spotted for
  the first time — which is why grafts are rarer and why they cap shallower
  (§6.4). A fault corrected for twenty services is one he knows; a fault just
  found is one he has only begun on.
- **Tier is the circle burned deeper.** Mana driven through an enchantment cuts
  its figure further in, finding as it goes the path it most naturally follows
  through *that* weapon. Depth is not knowledge the enchantment accumulates; it
  is the mark getting deeper and truer to the thing it is cut into.
- **That is why depth cannot be bought** (§6.2): the enchanter can *cut* a
  circle, but only use can burn one in, and burning happens where the power
  flows — in your hand, not on his bench. It is also why carrying an unfired
  enchantment deepens nothing.
- **And it gives the transfer tier-loss a cause rather than a justification**
  (§6.5). The deepest part of the burn is the part shaped by the old weapon's
  grain, and it has nothing to correspond to in a new body — so what is lost is
  precisely the weapon-specific work, which is the part that took longest to
  earn. **Tier 1 travels intact** because a circle that shallow has not yet
  found any path in particular.

## Superseding pass — the unified status model and the 1–4 stat scale

- **One status model.** A level count decaying 1 at the end of a round, a
  trigger, and an effect per level that is fixed for that status forever. The
  source varies only in **how many levels** it applies (§1.5). This replaced
  three per-status constants with one per-*applier* percentage, and made §1.7's
  "one representation" claim literally true rather than nearly true.
- **A DoT resolves at the end of the target's turn** — damage equal to level,
  then level minus one — so the tick *is* the decay. Levels are therefore
  magnitude and duration at once, and the old `SearDecayPerTurn` duration dial
  is gone. Totals fell from cubic to quadratic and the 216-target Nova became 36:
  the curve was fixed by simplifying the model, not by pricing against it.
- **Effect per level is 1 damage** for `Searing`, `Bleeding` and `Poison`, which
  makes the whole DoT layer supplementary by construction — bounded above by
  about `fightLength² / 2`. Damage over time is a garnish, never a build.
- **`Mire` stopped being an exception.** Its per-level effect is a 10% movement
  cut rather than damage, but its level count comes off source damage like every
  other status. **Past ten levels it paralyses**, released by the same universal
  decay, with no new state and no separate condition.
- **`Ward` is a level count too**, not a second shape. Its trigger is taking
  damage from any source; damage reduces the level by the damage amount and 1
  still gets through. Being hit for 19 is nineteen points of one event, where a
  turn passing is one — same loop, different decrement.
- **Stats run 1–4 and every character is a permutation of them.** Every spread
  sums to 10; everyone is excellent at one thing and hopeless at one thing, and
  the difference between members is entirely *which*. A 4 against a 1 is already
  a fourfold difference in growth rate, which is as wide as any progression
  system needs.
- **Max HP, max mana and wear capacity all start at 25**, and all grow by
  `currentMax / governingStat` XP per point — CON, INT, and a flat 1 for
  weapons, which have no nature to divide by. The stat is a **rate, not a
  bonus**: everyone opens the game identical and diverges, which turns §2.1's
  thesis into arithmetic. It self-slows, because the threshold is the current
  bar.
- **`WearCapacity` is an authored ladder: 25 → 30 → 40**, one step per full bar
  cashed, and **no fourth step until play asks for one**. Authored rather than
  computed precisely so a fourth can be appended.
- **Grafting is 3% base when enchanting**, multiplied by
  `1 + wear ÷ StartingWearCapacity` when refining — so a full fresh bar doubles
  it to 6%. Proportional rather than thresholded, so cashing one point of wear
  is not worth the same as bringing in a practically broken blade. Enchanting's
  3% does not move with wear at all, which sharpens the fork: **bring a worn
  weapon to refine, bring any weapon to enchant.**
- **Behaviour attaches through an event table, and handlers transform and
  return.** A handler is *apply this effect, hand back the result so the next
  one can act on it* — a chain rather than a broadcast, with nothing mutating
  the world mid-chain and the dispatcher applying the settled outcome once. This
  is the code half of the promise the JSON content files make: if adding a
  modifier means editing six switch arms, "content is data" is only half true.
- **Order is a declared priority, and the damage pipeline is the canonical
  one.** Block, Ward and the crit riders all handle the same event, so undefined
  order among them is exactly the side effect the architecture exists to
  prevent. Raised events **queue rather than recurse** (cascades are real:
  Vampiric to healing to Overheal to Ward), with bounded depth treated as a bug
  rather than clamped. The subscriber list is an ordered structure, never a
  Dictionary iterated directly, because dispatch order falls under the
  determinism constraint and would surface as flaky golden tests.
- **The damage pipeline has two outputs.** `Dealt` is after mitigation and
  before absorption; `Taken` is what reached hit points. They differ by whatever
  Ward swallowed, which follows from Ward being temporary health rather than
  armour. Weapon XP, Serrated's levels and the clean-kill test all read `Dealt`;
  death and Sturdy read `Taken`.
- **Weapon XP is credited on mitigated damage.** A crit fast-forwards
  proficiency twice over — it multiplies damage *and* skips Block — so crit
  builds level fastest, a distinction the classes did not otherwise have.
  Armour slows you down, since you learn from the damage you actually did. Ward
  does not reduce the credit. This is also what forces the attack resolver to
  **return** a result rather than write one: the attacker cannot credit XP until
  the defender's handlers have handed the number back.
- **Enchantments fire and pay in attachment order.** First on, first to fire,
  first to be paid for, each taking mana from what the ones before it left. That
  makes order a build decision that comes free with a choice you were already
  making at the enchanter — and one that is *not* free to change, since
  reordering means detaching and re-attaching, which is a transfer: a tier and a
  service. The order of a five-enchantment weapon is a record of the order you
  built it in.
- **Running out of mana mid-list scales the effect rather than cancelling it.**
  The enchantment fires at `manaAvailable / manaWanted` of full strength and
  pays exactly for what it did. A cliff was the alternative and reads as a bug —
  the fifth enchantment being dead weight for most of a fight and then abruptly
  not. A ratio degrades the weapon smoothly as the pool drains, which is what
  running low should feel like.
- **An effect that scales to nothing is a non-event**: it does not fire, pays
  nothing, and passes the remainder down the list, so a cheap enchantment behind
  an expensive one can still fire. Same non-event as an unaffordable lock, and
  it keeps the rule from producing a spend that bought nothing.
- **This makes `Resonant` worth more than its discount suggests.** Cutting
  trigger costs moves *where in the list* the pool runs dry — the difference
  between four enchantments at full strength and two plus a third at 40%.
- **`Weapon.Enchantments` is a list because order is gameplay**, so it must
  never be normalised, sorted or deduped. A migration that rebuilds it in a
  different sequence is a silent balance change.
- **Weapon XP is fed by the weapon's own damage only.** Attached enchantments
  fire on the successful-hit chain and their damage is tracked separately, so a
  wizard holding an `Arcane` dagger does not out-level a real thief at daggers.
  Proficiency measures the weapon; crediting it for what is bolted to the weapon
  would let the party's worst knife-fighter become its best on the strength of a
  stat with nothing to do with knives.
- **Each ladder is fed by the thing it actually is.** The wizard's knife climbs
  dagger proficiency slowly and its `Arcane` quickly, both true at once and
  neither leaking. That is §2.1's thesis pointed the other way: C never becomes
  the better caster however long they swing a staff, and D never becomes the
  better knife-fighter however much damage their knife does.
- **A forged enchantment is part of the weapon and does count.** §3.1 already
  drew that line, and it has to hold or a **wand would have no XP source at
  all**, since a wand's damage *is* its element. The distinction is not
  weapon-versus-magic but **what the weapon is** versus what was attached later.
- **`Serrated` reads the weapon's share too** — the wound is as deep as the blow
  that made it, and an `Arcane` discharge riding the same swing did not cut
  anyone deeper.
- **The clean-kill test still counts everything**, because it asks *could this
  have killed it outright* — a question about output rather than craft. Two
  questions, two numbers, both already in the payload.
- **`Block` comes off the weapon's share first.** Armour stops blows; it is not
  obvious it should stop the lightning riding one, and this keeps `Block`'s
  minimum-1 rule operating on what it was written against. The consequence is
  that enchantment damage is slightly better into armour than a raw swing —
  real, adjustable, one line, with proportional attribution as the alternative.
- **Answer a strong build with a counter, not a nerf.** The third time the
  design has reached for this shape: §1.1 re-prices a modifier rather than
  capping it, §6.4 limits at the forge rather than forbidding a graft, and
  enchantment damage being better into armour (§1.6) is met by **a shielding
  enchantment** rather than by changing how `Block` attributes. Nerfing costs
  one line and reaches everyone silently, including builds that were fine; a
  counter costs a slot, a service, a lock and a trigger, and reaches only the
  fight where someone chose to bring it.
- **It needs no new mechanic**: a catalogue entry whose `EffectPerLevel` absorbs
  a point of *enchantment* damage, sitting beside `Block` as the second armour
  for the second damage source. It works on both sides, so a party leaning on
  `Arcane` eventually meets dummies that shrug it off — a better lesson than the
  number quietly having been smaller all along.
- **This is also what keeps the arms race in the data files** (§5.5). A counter
  is an entry someone adds; a rebalance is a recompile and an argument.
