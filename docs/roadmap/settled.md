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
- **A lingering element applies levels derived from the element's tier**, and
  every conversion is a **named constant** rather than a baked `1`:
  `SearLevelsPerTier`, `SearDamagePerLevel`, `SearDecayPerTurn`. Levels
  **accumulate** on re-application and have no cap — the rider model §1.6
  already sets, so `Poison`, `Searing`, `Sundered` and `Weakened` are one family
  with one model and only the constants differ. That is what makes the tier-1
  cap survivable: the rule ("the element lingers") is fixed while the depth
  rides a catalogue enchantment that levels normally. It also makes the element
  a **prerequisite** — transfer the `Flaming` off and the burn goes quiet.
- **Decay is the dial that matters most**, because it decides whether this is a
  damage-over-time effect or a delayed burst. It is **not** tuned against "the
  fight length", which does not exist — proficiency and weapon depth push fights
  shorter while revival scaling pushes them longer, so the same party has
  one-shot fights and fifteen-cycle fights minutes apart. It is tuned against
  the fights a *wand* is in, which are the long ones by construction.
  Starting point `1 / 1 / 2`: 12 damage over 3 turns at tier 6.
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
- **The total is quadratic in tier**, the only such term in the design, and it
  lands on every target in the shape. `SearLevelsPerTier` sits *inside* the
  square, so halving it quarters the total — that is the correction if deep
  wands are too strong while shallow ones are fine. `SearDamagePerLevel` is the
  correction if the whole curve is too high.
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
  **integer divisors with the units in the name** (`MovementUnitsPerMana: 16`),
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
- **`Ward` is a pool that decays.** It absorbs **one point per HP saved**, never
  reduces a hit below 1 taken (the `Block` floor), accumulates on
  re-application, and loses **one point per turn** at the start of the wielder's
  turn. So 20 damage into 20 `Ward` leaves 1 through and 1 remaining — and that
  last point is gone next turn.
- **Decay of 1 a turn is slow against a fight and fast against a run**, which is
  why it is a flat point rather than a percentage: a shield survives the
  engagement it was raised in and bleeds away on the walk to the next. Nobody
  stockpiles `Ward` in the hub and cashes it on floor 9. Two drains on one pool
  — decay and absorbing — is also what makes `Overheal` self-limiting without
  any ceiling, since the equilibrium moves with how hard you are being hit.
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
