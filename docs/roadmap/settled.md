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
- **A caster's crit and fumble move mana cost, not just effect level.** On top
  of §1.6's doubled effect level, a natural 20 halves that cast's mana cost
  (floor) and a natural 1 doubles it. This gives the d20 a second, sharper
  consequence for staves and wands specifically — a cheap efficient burst on a
  crit, a real punish for fishing on a fumble — without reopening
  `CritMultiplier`, which stays a martial-only stat.
- **STR keeping four weapon classes to DEX's three is accepted, not fixed.**
  STR is the martial-breadth stat (axe, spear, throwing, sword); DEX is the
  precision one (dagger, bow, sword). No class moves, no ninth class is added.
- **A held weapon's reaction-on-swap question is moot by the existing turn
  rule.** Reactions all resolve during the opposing side's movement (§1.1),
  and a weapon swap only happens on the player's own turn, so there is never a
  window where a swap changes which reaction is live mid-resolution. Whatever
  is equipped when the enemy phase begins is what reacts.
- **A shape's enchantment trigger costs one payment per cast, not one per
  target caught.** A six-target Nova pays `DotManaPerDamage × expectedTotal`
  once, the same as a single-target Blast at the same tier, while still
  dealing its full total to every target hit. Area damage ends up strictly
  cheaper per point of damage than single-target — the shape's reward for
  catching more than one thing, not a tax on it. (§1.5, `DotManaPerDamage`.)
- **Efficiency uniques keep the costed-soul design as the standard shape.** A
  second soul — mana cost every trigger, starts at tier 1 — beats a plain free
  second modifier for this unique tier, even though it is slower to pay off;
  the dagger's `Serrated → Vampiric → Overheal` is the template every other
  Efficiency unique should aim for, not the exception. Authoring the remaining
  three classes' pairs (§1.5's open item) is content work, not a design
  question, and can trail Phase 1 code rather than block it — a class can ship
  with a placeholder single-modifier unique until its pair is found.
- **`WearCapacity` is already the answer to "does wear cap."** §6.1's
  authored ladder gives wear a ceiling that *rises* with every full repair, and
  depositing already spends the whole pool (§6.1's "no 0% to explain," "a
  deposit's value is the wear you brought") — so wear is both fully consumed
  per attachment and capped, exactly the combination asked for. Ship the
  existing three-step ladder; add a fourth step later if playtesting shows
  weapons plateauing at 40, per §6.1's own note that the ladder is authored
  precisely so it can grow. (Superseded in its ladder by the user's ruling, 2026-09-25 — that capacity caps wear, and a deposit spends it whole, stands: R4, below.)
- **A batch of balance questions are accepted as designed, to be watched
  during playtest rather than pre-solved on paper** — each is a case where the
  doc's own reasoning already leans toward "probably fine" and no amount of
  further analysis substitutes for actually fighting it:
  - `CritMultiplier ×1` on dagger/throwing (crits for `×3`) stays as designed.
  - `Longshot`×`CritWindow` compounding on a deep bow is accepted; revisit once
    the kiting AI exists and archers can actually choose their range.
  - The 50%-per-defeat stack roll stays flat, not decaying.
  - A boss does not scale with the deepest `DefeatCount` farmed on its floor —
    a farmed floor being more dangerous than its own boss is accepted as the
    price of farming, not patched with boss scaling.
  - Accumulated `Sundered` depth in a long fight is accepted; the per-level
    `+1` is the dial if it ever proves too much.
  - `Light ×6` stacking with `Charges` (54 of 160 mana for 9 throws) is
    accepted as the intended shape — `Charges` is already the binding cap.
  - The enchanter keeps its two-newest-slots rule as the legible middle
    ground between a hard slot count and a pure last-in queue.
  - One guaranteed tier-1 enchantment on every caster drop stays as designed —
    it costs a permanent extra run of service time and a lock a low-INT
    character can't afford, which is enough of a tax.
  - Whether the uncapped enchantment tier self-limits fast enough against
    max-mana growth is accepted as unsimulated; a superlinear lock-scaling
    dial is the fix if play shows it runs away, not a cap.
  - Mana spending feeding both max mana and enchantment tier at once (§2.2,
    §3.3) stays a deliberate double credit.
  - A rolled innate becoming a free tier-1 copy of something already in the
    catalogue late in a campaign is accepted — it's still free, and campaigns
    end.
  - `Resonant` reading as straightforwardly stronger on wands than staves
    (asymmetric ladders, §1.4) is observed and accepted, not adjusted for.
  - The `Light`/`Resonant` exclusion forcing a wizard's dagger into a
    non-Efficiency variant is accepted as the intended trade.
  - `resurrectTurns = max(3, 10 − DefeatCount)`'s front-loaded slope is kept
    as designed; `10 − n/2` is the fallback if the front half of a farm feels
    too samey.
  - The retreat gauntlet hitting hardest at whoever farmed deepest and then
    balked is accepted as the intended lesson, not a trap to soften.
  - A clean kill on an area weapon (a Nova advancing every target's
    `DefeatCount`) keeps no per-swing cap — it's the area weapon's reward for
    being one, matching the "feature not bug" read already settled for a
    single clean kill (§3.2).
  - `UniqueChanceMidpoint` stays at `DefeatCount 20` pending real numbers from
    `ReviveStepPercent` play — if a twenty-times-revived dummy proves
    unsurvivable or trivial, the midpoint moves to match, not the other way
    round.
  - The fully-worked crit dagger's endgame ceiling (~120 damage on a 45% crit
    chance, armour ignored) is accepted; no single weapon is forged deep in
    both crit dials any more, which is most of the argument that it's fine.
  - Whether the four 1–4 innate-stat permutations (§2.1) read as four distinct
    characters, and whether `currentMax / stat` as the HP/mana growth
    threshold reads as natural divergence rather than one character never
    growing, are both play questions with no action to take before playing.
- **`EnemyPlacer` rolls only the six close-range classes until kiting AI and
  the wand placement scorer both exist** (§1.2, §1.4); friendly fire stays off
  until then. This was already the working interim rule, now formalised as
  settled rather than sitting as an open question.
- **A short presentation/UX backlog, not blocking Phase 1–5 code:** surface
  the accidental clean-kill (a crit jumping `DefeatCount` by 2) as a connected
  beat rather than a silent number change; show the enchanter mid-ritual and
  unavailable during the tutorial rather than simply absent; surface the
  boss-floor lockout/re-roll trade-off in the encounter prompt before a player
  commits (§4.3); how much of the enchanter's stake is spoken aloud is a
  narrative-pass question, not a mechanics one — only load-bearing lore
  belongs in these docs. What the hub actually looks like (layout, discovery,
  repeat themes) is explicitly Phase 6 scope, not decided here.
- **A batch of provisional numbers are picked now so nothing blocks
  implementation, each tunable via `tuning.json` (§5.3) and expected to move
  after real play:**
  - `HealthPotionPercent` / `ManaPotionPercent` = 20%, equal to each other.
  - Consumables stay **one potion per slot, no stacking**; if that proves too
    harsh in practice the fix is more inventory slots, not stacking.
  - Potions are **self-only**, not ally-targeted — simpler, and keeps the
    movement cost landing on whoever benefits.
  - Friendly-fire damage to allies stays **half**, and the AI's ally-weight
    scoring stays a **flat ×1.5** regardless of the ally's role or remaining
    HP — both are the "flat first" version the docs already argued for.
  - Party member D carries **Mire**, not Blight — the on-theme choice for a
    game where movement is the resource, over the more legible-but-generic
    Poison demonstration.
  - The party carry limit stays **24** (6 slots × 4 characters).
  - A caster's starting **max mana is 160**.
  - `DotManaPerDamage` = **⅓**, the worked example already in §1.5. Stored as
    its integer reciprocal, `DotDamagePerMana = 3` — the `Tuning` property and
    the `tuning.json` key — because §5.3 makes every ratio an integer divisor
    named by its units; there is no `DotManaPerDamage` key.
  - `ApplyPercent` per element: **Flaming 20%, Cold 15%, Shocking 25%, Acidic
    15%** — spread within the doc's own 10–25% band rather than one value for
    all four, so the elements don't all produce identically-paced burns.
  - `OverhealPerWard` = **5**.
- **A fumble does not get a self-rider.** `RollOutcome.Weak` stays exactly
  what it is — halved damage, nothing more. A symmetric self-`Weakened` on a
  natural 1 was considered and rejected: crit riders, block riders and a
  fumble rider firing in the same turn is too much status churn to track and
  telegraph for the value it adds.
- **The enchanter refuses a transfer that would strand a unique's lingering
  effect.** Rather than letting a wand unique's element move off and leave the
  effect quietly inert, the service simply cannot be requested — consistent
  with how a lock or a mana cost the party can't pay never fails silently
  elsewhere in this design (§3.3, §6.5). No new UI needed to explain a trap
  that no longer exists.
- **Theme is a dungeon-level property, not a per-floor one.** A themed dungeon
  attunes every floor it has; an unthemed dungeon attunes none of them. There
  is no floor-by-floor variation within one dungeon to track. Four wand types
  in two opposed pairs is accepted as tight-but-sufficient for now — worth
  revisiting only if playtesting shows a two-wand stable trivialising every
  matchup. (§1.4, §4.3.)
- **The type-chart multiplier applies before `Block`, not after.** It happens
  in the same pipeline slot as `Sundered`/`Weakened` (§1.6, steps 1–3) —
  building the weapon's own damage — before `Block` absorbs at step 5. A
  resisted hit into heavy armour can land for very little; a resisted crit
  skips `Block` per the existing crit rule and still lands its halved damage
  in full. (§1.4, §1.6.)
- **The Reaver forges `Splitting ×2`, not `×1`**, so the anti-armour axe the
  tutorial teaches you to want (§4.3) starts stronger against `Block` from the
  moment it drops (ceiling `×7`, 21 ignored of 24) rather than only fully
  answering armour once reforged into the unique Shieldbreaker (`×8`, 24). Both
  values need updating anywhere the old `×1`/`×6`/18 numbers were quoted.
- **The dummy's revival step scales proportionally, not flatly.**
  `ReviveStepPercent` (§5.3, tunable) adds a percentage of the dummy's own
  current damage/HP per revival rather than a flat number, so a dagger dummy
  and an axe dummy grow at the same relative rate instead of the flat `+5`
  landing 56% on one and 28% on the other. Compounds geometrically; the
  three-way roll (damage / health / both) is unchanged. (§3.2.)
- **The hub ships with 6 non-tutorial dungeons for the first playable
  version.** Small enough to author quickly, large enough that one dungeon's
  boss lockout (5–10 runs) doesn't dominate a rotation the way it would at 4,
  and not so large it needs adjusting the existing `bossFloor`-based lockout
  formula the way 10 would invite. `FarmYieldPerClear` (§6.6) is defined in
  terms of the count so a 7th dungeon added later does not silently inflate
  the system.
- **A run has three outcomes: boss kill, retreat, death** — not two. Boss kill
  collects the run's loot and keeps its XP. A fighting retreat (leaving alive
  without the boss, including declining a found boss floor) keeps the run's
  XP but forfeits loot/`DefeatCount` progress. Death forfeits loot too, and
  additionally claws back `DeathXpLossPercent` (§5.3, tunable) of the run's XP
  gain, rolled back from a snapshot taken at dungeon entry. Retreat and death
  already shared the mercy tick (§6.3, floor 3+); they no longer share an XP
  outcome — surviving the climb out is what earns the difference. (§4.4, §5.1.)
- **Finding a dungeon's boss floor does not commit you to fighting it.**
  Declining and retreating instead is a legitimate choice — it re-rolls the
  depth for next visit, accepted as part of the risk/reward rather than an
  exploit, since it still costs the full gauntlet back out. (§4.3.)
- **A farmed weapon's stack total has no bespoke ceiling — only the
  per-modifier `forged + 5` cap from §1.1.** The old flat 10-stack-per-weapon
  limit is removed. A two-modifier weapon still finishes at 10 total around
  `DefeatCount 20`; a three-modifier weapon can finish all three at 15 total,
  taking roughly 1.5× the cycles. Breadth is rewarded for being breadth, with
  no extra rule — the same argument §1.1 already made against bespoke
  ceilings, now applied to the farm. (§3.2.)
- **Graft breadth has no explicit cap — `GraftBaseChance` (3%) is the only
  defence.** A weapon can in principle collect many distinct grafted modifiers,
  but at 3% (doubled at most by a full wear bar, §6.4) it should stay rare in
  practice without an extra rule. Revisit with an explicit per-weapon
  distinct-modifier cap only if playtesting shows heavily-serviced weapons
  actually accumulating breadth this way. (Superseded by the user's ruling,
  2026-09-25, in its parenthesis: refining's multiplier grows with the wear a
  weapon can hold, and capacity has no top now, so a full bar more than doubles
  the 3% once a weapon has grown — 7% at capacity 40, 9% at 60, which is 1,335
  wear cashed from new. It climbs slowly enough that the rest stands — R4, the
  Phase 5 pass below.)
- **Sword's `Push` stays always-on.** It fires on every hit, same as the rest
  of the sword's kit; occasionally shoving a target out of an axe's cleave or a
  spear's threat zone is accepted as a small tactical cost of mixing a sword
  into the front line, not a bug to design out. `Push` stays opt-in only on the
  Halberd, where it is the class's whole pitch rather than a side effect.
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
- **The tutorial is two floors deep**, boss on floor 2 — the one dungeon with a
  fixed rather than rolled depth (`dungeons.json` pins both bounds to 2, §5.8).
  Shortest run that demonstrates every dungeon mechanic, and a fast turnaround
  into the hub since it cannot be replayed.
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
  never hot-reloaded, the built-in file's number for every missing key, scalars
  only. Nothing the golden tests pin may ever appear in it, enforced by a test
  that asserts the schema is disjoint from the parity-critical names. Ratios are
  **integer divisors with the units in the name** (`MovementUnitsPerMana: 10`),
  because §1.3 already made the tiles-versus-units error once and it was a
  factor of 32. Saves record a hash of the values they were played under and
  warn on mismatch rather than refusing. (Superseded by the user's ruling,
  2026-09-25, in one clause: a missing key had a "compiled fallback"; the
  shipped `tuning.json` is now the master copy, built into the game, and the
  code keeps no number of its own — the Phase 5 pass below.)
- **Content lives in three validated files** (§5.4–5.8) — `restricted.json`,
  `weapons.json`, `dungeons.json` — beside the code catalogues, loading in the
  order the catalogues, tuning, relations, weapons, dungeons, the starting
  party, since relations validate everything, weapons name enchantment ids, and
  dungeons name modifiers. Invalid content **aborts startup** naming the entry
  and the rule, rather than clamping as `tuning.json` does, because a broken
  invariant is not a bad balance number. (Superseded by the user's ruling,
  2026-09-25: there were four files, `enchantments.json` among them; the
  enchantment catalogue and the starting party are code — the Phase 5 pass
  below.)
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
- **The point of it is that mechanics become additive.** Behaviour and the
  reference data it is addressed by stay in code, relations are
  `restricted.json`, values are `tuning.json`, and which weapons and dungeons
  *use* them is content — so adding a modifier is: implement it, price it,
  declare its relations, and it is then usable on any weapon in any spread
  without touching code again. That is §1.1's uniformity finally paying out in
  the workflow rather than only in the design. (Superseded by the user's
  ruling, 2026-09-25: this said "behaviour and relations stay in code", against
  the entry above; the relations are `restricted.json`'s alone, and the
  modifiers, the enchantment catalogue and the starting party are the reference
  data code keeps — the Phase 5 pass below.)
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
  the pool — not the clock — is what limits it. (Superseded — already by §6.4,
  whose two services differ by what the smith works on and not by a remainder,
  and by the user's ruling, 2026-09-25: a deposit spends the whole pool whatever
  it buys, and attaching enchantment n needs a deposit of at least
  `30 + 5(n − 1)` wear, so no remainder is left over to pay for a roll.
  Refining's odds are `GraftBaseChance × (1 + wear ÷ StartingPool)` on the whole
  deposit, and the three consequences follow from that instead — R4, the Phase
  5 pass below.)
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
  `×2` buys three, and three is the most any weapon in the game *arrives*
  carrying. **Five remains the ceiling** (§6.2) — the trade buys a head start on
  the service climb, not a higher cap. (Qualified by the user's ruling,
  2026-09-25: a head start in the souls the weapon holds, never on the climb to
  five, which costs every weapon the same 840 wear and costs one that arrives
  with souls more downtime — R4's fifth consequence, the Phase 5 pass below.)
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
- **Max HP, max mana and wear capacity all start at 30**, and all grow by
  `currentMax / governingStat` XP per point — CON, INT, and a flat 1 for
  weapons, which have no nature to divide by. The stat is a **rate, not a
  bonus**: everyone opens the game identical and diverges, which turns §2.1's
  thesis into arithmetic. It self-slows, because the threshold is the current
  bar.
- **`WearCapacity` is an authored ladder: 30 → 40 → 55**, one step per full bar
  cashed, and **no fourth step until play asks for one**. Authored rather than
  computed precisely so a fourth can be appended. (Superseded by the user's
  ruling, 2026-09-25: capacity is computed after all, by the entry above's own
  rule with a weapon's flat 1 — a point for every full bar's worth of wear
  cashed, the bar being the capacity at the time and the progress carrying, with
  no top, so there is no step to append — R4, the Phase 5 pass below.)
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
  is an entry someone adds: a relation or a weapon in the data files, an
  enchantment in the catalogue — never a rebalance of a constant, which is a
  recompile and an argument. (Superseded by the user's ruling, 2026-09-25, in
  its example: the enchantment catalogue is code, so a new enchantment is a
  rebuild — the Phase 5 pass below — and a rebalance of a number is a
  `tuning.json` edit rather than a recompile; what makes it the worse tool is
  that it reaches everyone silently.)

## Implementation pass — Phase 2 (innate stats and the three pools)

Three rulings the branch made and the record did not carry. Each is written down
here so the next reader meets the ruling rather than the arithmetic that assumes
it.

- **Max mana starts at 25 like every other pool, and the 160 is a movement
  budget** [SUPERSEDES "A caster's starting **max mana is 160**"]. Two figures
  were both live — that one, and "Max HP, max mana and wear capacity all start
  at 25" — and the evidence between them is arithmetic rather than preference.
  The §1.3 derivations that survive are `(160 − 40) / (8 + 3)` and
  `r × (160 − 40k)`, where `40` is a staff's **movement** cost and `160` the
  **movement** budget: the expression reads "budget less the cast's movement,
  over the mana one cast moves", and a mana *pool* of 160 has no place in it.
  `MovementUnitsPerMana` is derived *from* that identity, so reading the 160 as
  mana would re-derive a shipped divisor nothing else asks to move. The 25 is
  also the later pass — the one that fixed the 1–4 permutations and the wear
  ladder — and §5.3, the file that will carry the number, names 25. So
  `StartingPool` is the one key for HP, mana and wear capacity alike, and the
  caster's 160 is `MovementBudget` beside it.
- **The knock-on is accepted and watched, with one knob.** A fresh D opens able
  to cast Renewal once, or Mire exactly once, from a full pool, refilled by
  unspent movement; at 25 HP an arming sword's 10 kills a member in three. No
  weapon and no enemy is rescaled, because §2.1 asks precisely for the flat
  start — "a CON 1 wizard and a CON 4 fighter open the game with the same 25
  HP" is the whole thesis arriving as arithmetic — and the provisional numbers
  are expected to move. If play says otherwise the single edit is
  `Tuning.StartingPool`. An actor with no progression — every enemy — still
  carries the flat 100 it always did.
- **`Serrated`'s magnitude is a percentage of the weapon's share of what was
  dealt** [SUPERSEDES "`Serrated`'s magnitude comes from the weapon and the
  wielder, not its tier: `BleedPercent × (base damage + proficiency level)`"].
  The superseding pass already said it — "`Serrated` reads the weapon's share
  too — the wound is as deep as the blow that made it" — and that is the
  entry the code follows; this records which of the two wins, because the
  earlier formula was still being quoted in the source. The later reading keeps
  `Serrated` on the §1.6 line that every other attribution question is answered
  on, and it does not lose what the earlier formula was for: the share carries
  the wielder, because §2.2's `+floor(L / 2)` is part of the weapon's own damage
  before anything else touches it, and it is still self-limiting on transfer,
  because the share is whatever the soul's new weapon deals in hands that have
  not trained it. Two consequences the earlier formula did not have, accepted:
  a crit deepens the bleed, and a blocking target shallows it. Both are the
  same sentence — the wound is as deep as the blow — read forwards.
- **Element damage's INT scaling is §3.3's, not §2.2's.** "These ladders are
  partly a character stat — element damage carries INT scaling" is a
  *multiplier*, and §2.1's rule is that a stat divides a threshold and never
  multiplies a value: there is no rate multiplier anywhere in Phase 2, which is
  what lets one function serve four pools. The INT term belongs with the tier
  that prices it and with unique potency, which are the same paragraph. Phase 2
  ships the two seams it needs and nothing else — `InnateStats` on the member,
  reachable wherever a wielder is, and `ActorState.WeaponLevel(weapon)` — so
  §3.3 adds a term rather than a plumbing pass.

- **Every pool starts at 30 rather than 25**, because the pool *is* the
  enchantment lock budget (§3.3: the sum of equipped locks may not exceed max
  mana). At 25 a fresh caster could hold one elemental enchantment (lock 20) or
  a single Regeneration (lock 15) and nothing else; 30 admits two Regeneration
  locks, or exactly one Arcane, which is the starting breadth the design wants
  to be reachable. Max HP and wear capacity move with it rather than splitting
  the constant, since §2.1's thesis is that every pool opens identical and
  diverges only through play. `Tuning.StartingPool` is the one key.
- **The `WearCapacity` ladder shifts with the starting bar, to 30 → 40 → 55.**
  The first rung *is* the shared starting pool, so leaving the ladder at
  25 → 30 → 40 would have made a weapon's first service raise its capacity from
  30 to 30 and buy nothing. The rungs keep widening, so reaching the third step
  is now 70 hits on one weapon rather than 55 — a longer arc, deliberately, and
  still three steps with no fourth until play asks for one. (Superseded by the
  user's ruling, 2026-09-25: there is no ladder to shift. What this kept stands —
  a weapon's capacity opens on the shared starting pool, 30, `StartingPool` —
  and from there it rises a point a full bar cashed, 30 → 31 → 32 and on, with
  no top — R4, the Phase 5 pass below.)

## Implementation pass — Phase 4 (floors, the boss, the theme, the way out)

Rulings the Phase 4 branch made where the phase doc was silent, or where two
passages met and one had to be read in the light of the other. Each is written
down here so the next reader meets the ruling rather than the code that assumes
it. Four of them bend a rule above or a line of the phase doc rather than fill
a silence; those four are marked **ruled by the user (2026-09-24)**: each shipped
provisionally, open-questions.md set out the alternatives, and the user kept all
four as shipped.

- **The campaign lives outside the dungeon, and holds the visit.**
  `CampaignState` is §6.7's campaign: the party, what it has met, the run
  counter, the locks, the cleared and retired dungeons — and the visit in
  progress (`DungeonState`), which is null between runs. What the campaign has
  met is campaign-wide: it outlives every floor and every visit, and §4.1's reset
  never touches it, which closes the old question of where `CampaignState`
  lives. The game owns one campaign for the process, the scenes are views of it,
  and the visit never holds the campaign.
- **A floor number is 1-based; a floor index is 0-based.** Prose, the HUD,
  `BossFloor` (5–10, the tutorial's 2) and `MercyFloor` (3) are numbers; the
  seeds' `n` and `floorIndex` are indices, so the boss floor's index is
  `BossFloor − 1`. Floor index 0 of a map seed is that seed's dungeon exactly
  (`floorSeed(0) == mapSeed`), which keeps the golden dungeon the first floor of
  the shipped seed. The HUD says `FLOOR 3`, never "of 7".
- **One clock for the whole visit, and the turn travels with the party.** Every
  floor counts turns on the visit's one clock, so a dummy's defeat turn means the
  same on every floor. A floor nobody is standing on is frozen except for
  resurrection: on arrival, every dummy whose wait ran out while the party was
  elsewhere gets up, cut off at the turn the boss died. So the corridor behind a
  retreat is armed (§6.3: "every dummy you killed on the way down is up again"),
  and a stair hop is not a free respawn. Taking the stairs keeps the turn, all of
  it: the movement left, the attacks and reactions already spent (a thrower's
  `Charges`), and the enemies already seen. A down-and-up hop buys nothing.
- **The party gathers to take the stairs.** `E` works with the active member on
  the stair and every living member within `StairGatherTiles` (3) tiles of it.
  The fallen are carried, so nobody is ever on another floor. No enemy count
  gates it (§4.4). The first floor's up-stairs are the way out, and floors are
  adjacent only.
- **The boss wields what an enemy can field and drops from its theme's whole
  table — ruled by the user (2026-09-24): kept.** The class it holds is drawn over the
  theme's table narrowed to the placer's close-range classes (the `EnemyPlacer`
  entry above), because a staff in an enemy's hands flees and a wand has no
  enemy AI. The class it drops is drawn over the whole table, all eight for
  Block as §4.3 says of the golem, so the Long Gallery drops bows no enemy
  swings. That departs from §3.1's "an enemy drops a weapon of its own class"
  for the one enemy whose table bends to the guarantee. Both class draws redraw
  over a compiled list rather than step to the next legal class, so the drop is
  uniform over its table and no class is favoured by the gaps.
- **The golem stands behind its own Block ×3 plus whatever its weapon forges —
  ruled by the user (2026-09-24): kept.** Resolution reads weapon plus innate (§4.3), and
  the golem holds a class an enemy fields, drawn like any boss's. Four of the
  five forge no Block, and against those it stands behind 9 and teaches §4.3's
  numbers exactly: the sword's 10 lands for 1, the dagger's 15 for 6, the axe's
  18 for 9. About one tutorial in five its hands draw a sword, and every sword
  forges Block: 12 behind an Arming Sword, a Riposte Blade or a Warden's Shield
  (hits of 1, 3 and 6), 15 behind a Tower Guard (1, 1 and 3). The crit still
  lands 45, and the golem's plate shows the Block it stands behind. That bends
  §4.3's worked numbers in those runs rather than filling a silence.
- **A boss drop becomes its class's unique only at ×3.** The graft makes the drop
  the unique when it takes the theme to its forge limit (×3) and the forged
  spread then equals the unique's. The ×3 is load-bearing: a Flensing Knife
  grafted with CritWindow reaches the Nameless Knife's forged spread at ×2, and
  without the condition a CritWindow dungeon would hold two uniques. Each
  dungeon has exactly one.
- **The themes that can carry a dungeon are the class signatures with a unique
  to hunt.** Block, Brace, Longshot, CritWindow and Charges ship. Cleave waits
  for a Great Axe unique; CritMultiplier and Resonant are refused under the
  shipped content, since neither can reach the Purity coincidence a unique needs;
  Pin and Light never can. Validation refuses a theme whose own class falls out
  of its table, the accident that would make the unique unobtainable.
- **Attunement is its own field, and "themed" in the entry above means
  element-themed — ruled by the user (2026-09-24): kept.** "A themed dungeon attunes every
  floor it has" sits with §1.4's wand chart, and §5.8 makes attunement a field
  of its own that may be none. So a dungeon's modifier theme attunes nothing;
  `DungeonDef.Attunement` attunes every enemy the dungeon spawns, and none of
  the shipped five sets it. Attuning one is a one-field content edit. The other
  reading would attune every dungeon.
- **The tutorial's "one stack" is the forged graft itself.** A boss drop carries
  `max(0, BossFloor − 4)` guaranteed acquired stacks — 1 at floor 5, 6 at floor
  10 — so the tutorial's floor 2 adds none, and the one stack §4.3 prices it at
  is the forged Block. The count is fixed at entry, with the depth, so a tuning
  change during the visit moves neither. "Never raise the boss cap to 10" is
  held at load: a tuning, or a dungeon pinning a deeper range of its own, whose
  deepest boss would pay as many stacks as a well-farmed two-modifier weapon
  banks (twice `AcquiredHeadroom`) is refused.
- **Two dummies far enough apart may share a roll, and that is accepted.** Every
  floor's drop and revival streams are Phase 3's, keyed by that floor's seed —
  §4.2's `mapSeed ^ (FloorSalt × n)` verbatim, so floor 1 of a seed is that
  seed's dungeon. Those mixes combine by XOR, so their terms can cancel: over
  every spawn index and revival cycle to 64, no two streams meet on the first
  21 floors, more than twice the deepest a shipped dungeon goes; but floor 22
  meets floor 20, and floors 4 and 10 meet once spawn indices and cycles pass
  64. A meeting is two dummies on different floors rolling the same revival:
  nothing breaks and nothing can be exploited, so depth is not capped for it,
  and no floor term is added to the mixes, which would renumber every Phase 3
  drop.
- **The lock is written after the clearing run's tick.** A clear ticks the run
  counter first, then locks the dungeon until
  `RunCounter + BossFloor × CooldownRunsPerFloor`. A floor-5 clear is back after
  exactly five more runs that count, "available in n runs" is the lock less the
  counter, and a dungeon can be picked once the counter reaches its lock (absent
  reads available). The tutorial is never locked: it retires, a flag, and the
  hub opens when it does.
- **The cleared set is its own campaign field**, never read back off the locks.
  Every boss kill adds its dungeon, the tutorial's included, and nothing takes
  one out.
- **A wipe forfeits spoils, not gear.** The spoils are what the party picked up
  this run and still carries. Gear carried in is never taken, and a weapon put
  down in the dungeon is lost with it either way.
- **Four dungeons ship beside the tutorial, not the six above — provisional.** The
  missing two need content that does not exist yet (a Great Axe unique, or a
  repeated theme), and Phase 6b completes the roster. The four's names, bosses
  and statlines are provisional, and so is `DeathXpLossPercent` at 50: the entry
  above leaves it unfixed, and §4.4 asks only that it be data rather than a
  hardcoded 100%.
- **The hub's dungeon list is the screen between runs.** The game opens on it.
  Before the tutorial is beaten it offers the tutorial alone and says under it,
  before the run is committed to, that beating the golem closes the dungeon and
  its Block farm for good: the one dungeon the game is allowed to warn you about
  (§4.3). The boss floor's arrival banner says so again. After, it lists every
  dungeon left, each named with its theme and never its depth, a locked one
  counting down in runs rather than vanishing (§4.5).
- **With every dungeon locked, the one whose lock lifts soonest is offered early
  — ruled by the user (2026-09-24): kept.** With four dungeons and locks of five runs or
  more, four straight clears reach a dead end where no run can tick and no lock
  can lift. Entering the early offer sets its lock to the run counter and moves
  nothing else. This bends the entry above ("clearing a dungeon locks it for
  `bossFloor` runs") in that one corner. The alternatives weighed were a
  second clock (ruled out above), shortening every lock while all are locked,
  and making a sixth dungeon a Phase 4 requirement; the early offer stands.

## Implementation pass — Phase 5 (the numbers, the content files, save and load)

Rulings the Phase 5 branch made where the phase doc was silent, or where two
passages met and one had to be read in the light of the other. Each is written
down here so the next reader meets the ruling rather than the code that assumes
it. Six of them bent a line of the phase doc rather than fill a silence; those
are marked **bends the phase doc**, each with why it needs no ruling, or that it
is the user's. Four rulings were the user's to make — the dice on a reload, the
save model, where content's truth lives, and the class baselines, which was also
one of the six bends — and each shipped provisionally. **Three are ruled**
(superseded by the user's rulings, 2026-09-25, below): content's truth is the
JSON, and the class baselines are a section of `weapons.json`, which withdraws
that bend; and the dice stay the visit's own, seeded and saved. **The save
model**, the fourth, the user ruled the same day, and this pass records it as it
is built: until then its entry below says what ships, and open-questions.md the
alternatives it shipped beside.

### The user's ruling on content (2026-09-25)

**Reference data is code; everything else is JSON, and the JSON is the master
copy.** The user ruled: "behaviour, modifiers, and enchantments living in the
code due to them being reference data, while weapon types, weapon modifiers,
rolled weapons and numbers driving tuning are fully in json" — and on why no
value may keep a second home in code, "if it lives in the code then changing the
json loses all purpose": a number or a relation with a copy in code is one whose
file edit may not be the one that plays. Phase 5 had shipped six files generated
from a compiled copy of all of it (`ContentDefaults`), pinned byte for byte; the
ruling ends that, and supersedes the entries below where they differ.

- **In code**: behaviour (the event table, the modifier and enchantment
  behaviours), and as reference data the modifiers — the `ModifierType`
  definitions and what each is; the enchantment catalogue and its unique souls
  (`Logic/Enchantments/EnchantmentDefinitions.cs`), so `enchantments.json` is
  gone; and the starting party, its roster and its loadouts
  (`Logic/Content/StartingParty.cs`), so `party.json` is gone. Each is addressed
  everywhere by a stable string id, and each is validated at load against the
  JSON, a failure naming the entry, the rule and the file to fix: a relation
  naming an enchantment the catalogue lacks stops the game on `restricted.json`,
  a weapon naming an innate that does not exist on `weapons.json`, and a
  `weapons.json` that renames a weapon only the party names on `weapons.json`,
  naming the member. A copy of either retired file left beside the exe is named
  on screen as no longer read.
- **In JSON, the master copy**: `restricted.json` — the relations (`excludes`,
  `requires`, `kind`, `forgedOnly`, `neverRolled`) live there alone; `weapons.json`
  — the weapon types as a section of their own, every variant, the uniques, and
  each weapon's forged modifiers and innate enchantment references;
  `dungeons.json`; and `tuning.json`, whose `Tuning` record keeps its shape, its
  key names, its clamps and its never-contain rules and holds no value. The
  shipped files in `GameEngine.DistanceRPG/Data` are hand-kept, the build embeds
  them, and each is the fallback for its copy beside the exe, **per file and,
  for the tuning, per key**: a copy missing or unreadable plays the built-in one
  and says so, as before, and a key left out plays the built-in number. A broken
  built-in copy stops the game on its screen marked `(BUILT-IN)` — and, when no
  file beside the exe can have broken it, names the master to fix and rebuild.
- **The save** keeps the weapons the party owns in `campaign.json`, as before. It
  stores ids and enum names, every id kept its name, and the tuning's hash
  covers the same values, so a save written before the ruling loads and resumes
  unchanged — pinned by two saves the pre-rework code wrote — and the save
  format needs no new version.
- **Phase 5 USER DECISIONS 3 and 4 are ruled**: content's truth is the JSON, and
  the weapon types are a section of `weapons.json` — the class-baselines bend is
  withdrawn, and §5.6 holds literally. **Decisions 1 and 2** — the dice across a
  reload, the save model — **stay open**, and so does whether a content file's
  numbers have bounds; what a deposit costs stays Phase 6a's. (Superseded by the
  user's rulings of 2026-09-25, below: the user ruled those four the same day —
  the dice kept, the save model rebuilt, every number of a content file given a
  range, and what a deposit costs, which Phase 6a builds.)

**The exceptions, named so that none reads as a surviving copy.** Code still
names some weapon ids and keeps one coexistence rule, each for a reason of its
own:

- The starting party's loadouts — reference data, by the ruling.
- `arming_sword`, what a dummy holds (`EnemyState.DefaultWeaponId`), and
  `staff_of_renewal`, a crowded room's healer (`EnemyPlacer.HealerWeaponId`) —
  named by behaviour, and each checked at load by `RuleCodeNamedWeaponsExist`,
  which also wants a variant of every class the placer draws.
- `ContentValidator.CurrencyGroup` (`Light`, `Resonant`), the one coexistence
  rule the code keeps. Which modifiers can pay for a second soul (§1.5's combo
  platform) is what those two modifiers *do*, so it gates
  `RuleDependencyOnCurrency` from code; every relation *between* ids is
  `restricted.json`'s alone.

(Superseded by the rework's audit, 2026-09-25: a third exception stood here,
`LootTable.CasterVariants`, a C# list of eight `weapons.json` variant ids — which
staves and wands a caster drop rolls between, and in what order. It is the
`dropOrder` of the Staff and Wand rows now, below.)

**The rework's audit (2026-09-25)** found three copies of a master value the
ruling leaves no room for, and closed them the ruling's way:

- **The caster drop order is `weapons.json`'s**: each caster class's row names
  its `dropOrder`, the variants a drop of it rolls between, in the order the
  draw indexes them. The draw is uniform over the order's length (four a class,
  as ever, so no saved drop moved), and never over the file's order of its
  variants, so reordering `variants` re-rolls nobody (§3.5). A caster variant the
  order leaves out never drops, and one appended to it drops with no code edit.
  The load checks it (`RuleCasterVariantPool`): a caster class that fields
  variants names an order, every id one of its own variants and none twice, and
  a martial class names none, since its drop draws a role. A copy of
  `weapons.json` beside the exe from before has no order, and stops the game
  saying to delete it or run `--write-data <exe folder>`, as the flat format's
  copy does.
- **`neverRolled` is `restricted.json`'s alone**: the load no longer holds it to
  exactly the catalogue's unique souls, which made any edit to the list alone
  stop the game. The `unique` flag pins a soul's tier and nothing else; the load
  asks only that `neverRolled` names enchantments of the catalogue
  (`RuleNeverRolledNamesEnchantments`: a modifier there is a relation nothing
  reads). A soul taken out of the list may be copied and still drops nowhere —
  the drop pool (`LootTable.DropPool`, the code's list of the catalogue's
  entries a drop may carry) names none — and an entry put in stops dropping.
- **A caster's baseline is its row's**: the load asks it what it asks any
  baseline — the forge allows it — plus one modifier at least (a caster's two
  axes are a modifier and its innate) and no signature, and holds every staff
  and wand to it (`RuleCasterForged`); it no longer pins both caster rows to
  exactly `Resonant ×1`. That value is the rows' design (§1.3, §1.4), pinned by
  the tests as a design doc's value (REWORK DECISION 3), never by a load rule.

It also made two failures of a file say so readably: a JSON reader's sentence is
one short line (a word it does not know, `nul` or `tru`, had it quote the rest
of the file into the screen that stops the game), and a blank `tuning.json`
beside the exe plays the built-in numbers with the notice `TUNING.JSON: THE FILE
IS EMPTY - THE BUILT-IN NUMBERS ARE PLAYING`, where it had played them silently.
Whether the gameplay numbers still compiled — an enemy's hit points, mana and
movement, the crowded-room healer's odds, the chart's ×1.5, the proficiency
steps, the room cap — are tuning numbers the ruling moves into `tuning.json` is
the user's to rule: open-questions.md sets it out, and they stay compiled until
then.

**The rework's own choices, shipped provisionally** — each labelled REWORK
DECISION, never USER DECISION, which names Phase 5's list; open-questions.md
sets out each alternative, and this pass records the ruling once the user makes
it:

- **REWORK DECISION 1 — a variant may name its own statline, field by field.**
  A class row carries the statline its variants share; any of `range`, `damage`,
  `cost` and `manaCost` given on a variant plays instead of its class's. The
  shipped file uses it only for the Staff of Renewal's 15 mana and the Staff of
  Mire's 25.
- **REWORK DECISION 2 — any edit to the caster drop order plays.** The order
  is `weapons.json`'s (the audit, above), and a copy beside the exe may reorder,
  shorten or lengthen it; each moves what a saved, not-yet-collected caster
  dummy of that class drops, as a fifth variant moves the placement (§5.6: "a
  deliberate, rare change rather than an ordinary edit"). (Superseded by the
  rework's audit, 2026-09-25: this decision was that the order stays in code, as
  `LootTable.CasterVariants`; the ruling left no room for that copy.)
- **REWORK DECISION 3 — the suite keeps as literals only the values a design
  doc fixes** — `MovementBudget` 160 and the `MovementUnitsPerMana` derived from
  it, §1.2–§1.4's class rows, statlines, spreads and wand shapes, §1.5's unique
  table, §5.8's tutorial row, and the friendly-fire switch — so an edit to one
  fails by name and its doc moves with it. Every other test reads the master or
  stands on numbers of its own (`Premise`), and no other master edit fails the
  suite.

### The user's rulings of 2026-09-25: the dice, the save model, raw JSON, and the deposit

The same day as the ruling on content, the user ruled on four things more. Each
supersedes the phase docs, the entries above and the rest of this pass wherever
they differ: **R1**, what the d20 does across a reload; **R2**, the save model —
a title screen offering CONTINUE, NEW CAMPAIGN, LOAD GAME and QUIT, one autosave
slot, and manual slots whose count is a `tuning.json` key; **R3**, raw JSON —
"since we are storing in raw json we should probably not trust the fields", so
every number of a content file gets a range, and a save's fields are checked
against each other; and **R4**, what a deposit costs. R1 and R4 change no rule
of play — R1 keeps what shipped, and R4 is Phase 6a's to build, its numbers
declared as `tuning.json` keys ahead of it — so both are written here whole. R3
and R2 change the game, and each is written here as it is built. R3 is, both
halves: every number of a content file has a range (R3(a), below), and every
count of a save has its ceiling and its fields are checked against each other
(R3(b), below). R2 is built in three steps, and its first two are written
below: the save folder's slots, a save's time, the slot count, and what a write
never costs (R2, the folder); and the title screen, with CONTINUE, NEW CAMPAIGN,
LOAD GAME and QUIT (R2, the title). Saving by hand is still to come, and until it
is, the entry below that R2 supersedes — the save model — says what else the
player meets.

- **R1 — the dice are the visit's own, seeded and saved: kept.** What shipped
  provisionally as Phase 5 USER DECISION 1 stands, ruled (the entry under "The
  save", below): each d20 is a draw from a sequence the visit seeds, and how
  many the visit has rolled is saved, so a reload rolls on exactly where play
  left off and never rerolls. The user's reason: "just to protect players from
  themselves" — a bad roll is never reloaded away. The price stated with the
  decision is accepted as it stands: a turn replayed after the process is killed
  past its autosave knows its next draws, and keying each draw by the turn, the
  actor and the attack's ordinal instead stays not taken. The question leaves
  open-questions.md.
- **R4 — what a deposit costs.** Recorded for Phase 6a, which builds the
  enchanter: nothing of it is played yet — its numbers are `tuning.json` keys
  (below), which no rule reads until then. The user's rule:
  - A weapon's `WearCapacity` starts at **30**: `StartingPool`, the one bar
    every pool opens on (the Phase 2 pass above), which the graft formula spells
    `StartingWearCapacity`.
  - **It rises by 1 for every full bar's worth of wear cashed in**, a bar being
    the capacity at the time. The progress accumulates across deposits: a
    partial deposit counts, and what a deposit cashes past a full bar carries
    toward the next point. Whatever the deposit bought, the growth is the same —
    "the wear bar increases by one per full bar value cashed in". The pool
    itself still empties at every deposit (§6.1): what carries is the capacity's
    progress, never the wear.
  - **Enchantment slots open every 5 capacity**: slot n at `30 + 5(n − 1)` —
    30, 35, 40, 45, and the fifth and last at 50, §6.2's five.
  - **Attaching enchantment #n needs a free slot and a deposit of at least
    `30 + 5(n − 1)` wear**, and a deposit still spends the whole pool (§6.1).
    Capacity above the price is breathing room: at capacity 35 with no
    enchantment, cashing 30 buys the first.
  - **Worked**, a fresh weapon cashing full bars: #1 at 30 hits, capacity 30 →
    31; bars of 31, 32, 33 and 34 take capacity to 35, at 160 hits in all; and
    #2 at 195. On by the same rule: #3 at 385 (bars of 36 to 39 between), #4 at
    600 (41 to 44) and #5 at 840 (46 to 49) — twenty-one deposits from a fresh
    weapon, five of them attaching.
- **R4 agrees with §2.1, and replaces the ladder.** Phase 2's
  `xpToNextPoint = currentMax / governingStat`, with a weapon's flat 1 — the
  superseding pass's "all grow by `currentMax / governingStat` … a flat 1 for
  weapons" — *is* a point per full bar, the bar the current capacity, the
  progress carrying: capacity climbs the curve HP and mana climb, with a stat
  of 1. The authored 30 → 40 → 55 ladder was the one passage that disagreed,
  and it goes, and with it `n × EnchantmentWearCost` (§6.2) and §6.4's
  service-dependent growth — "`+` a little" when enchanting, "`+` more" when
  refining. Each passage that stated them is marked where it stands. Above: the
  entry that answered "does wear cap" with the ladder (its answer stands: the
  capacity caps wear, and a deposit spends it whole), the graft's "doubled at
  most", the two rates as one mechanism, the ladder and its shift to 30. Below:
  the Enchanter's hand-off. In the phase docs: §6.1, §6.2, §6.4 and §6.7, §5.3's
  rows and §2.1's line on a weapon. The question leaves open-questions.md.
- **R4's consequences are the rule's own**, each superseding a passage of the
  phase docs:
  1. **Refinement grows the pool no faster than enchanting.** §6.1, §6.2 and
     §6.4 had refinement buy "a bigger pool *forever*" and grow capacity
     fastest; every deposit now grows it alike, a point a full bar. What sets
     the two services apart is what they buy — a chosen soul, or the graft roll
     at refinement's odds (§6.4) — and between one slot and the next there is no
     soul to buy, so the bars that open a slot are refinements.
  2. **The graft odds climb without a top, slowly.** §6.4's "the odds cannot run
     away" stood on a short authored ladder. Capacity has no top now, so
     refinement's multiplier `1 + wear ÷ StartingPool` climbs a point of
     capacity per full bar — 6% on a fresh full bar, 7% at capacity 40, and 9%
     at 60, which is 1,335 wear cashed from new.
  3. **A fifth enchantment also waits on the bars between slots.** §6.2's
     "fifteen runs to a fully enchanted weapon" counted the attaching services
     alone. From new the climb is 840 wear and twenty-one deposits at the least,
     since slot 5 opens only at capacity 50, and every deposit, refinements
     included, costs `count + 1` runs (§6.4). Attaching each enchantment as its
     slot opens, the sixteen deposits between the slots are refinements and the
     climb takes 71 runs of downtime. Refining first and attaching all five on
     the last five deposits, at capacities 46 to 50, takes the least there is:
     31 runs, for a weapon that carries no soul until then.
  4. **"A free slot" is read literally.** What a weapon arrives with fills its
     slots from the first: a staff's innate fills slot 1, so a fresh staff
     attaches its second enchantment only once capacity opens slot 2, at 35; and
     a unique that arrives with three souls attaches a fourth only once capacity
     opens slot 4, at 45. At the ruled numbers the deposit implies the slot — a
     deposit of `30 + 5(n − 1)` needs a capacity at least that high, which is
     where slot n opens — so the two conditions part only if their numbers are
     tuned apart.
  5. **What a weapon arrives with is a head start in the souls it holds, never
     on the climb to five.** It saves no wear: slot 5 opens at capacity 50
     whatever the weapon arrived with, so every weapon cashes the same 840 to
     fill it. And it costs downtime, because every soul a weapon arrives with
     is in the count each later deposit pays `count + 1` runs on, refinements
     included. Attaching each enchantment as its slot opens, an innate staff's
     climb takes 72 runs, a unique that arrives with two souls 78 and one that
     arrives with three 89, against a fresh weapon's 71; in the order that
     takes the least (3), they take 48, 66 and 85 against 31. The phase docs
     had a head start shorten the climb by what the weapon was handed (§1.5,
     §3.1, §6.2), and each of those passages is qualified where it stands, as
     is the entry above on a `Light` artifact's souls.
- **Every number of R4 but its start, which is `StartingPool`'s already, is a
  `tuning.json` key of the Enchanter group**, by the user's standing preference
  that a number the user may revisit lives in the file: `WearCapacityPerBar`
  (the capacity a full bar cashed adds), `FirstSlotCapacity` and
  `SlotCapacityStep` (slot n opens at `FirstSlotCapacity + SlotCapacityStep ×
  (n − 1)`), `EnchantmentSlots` (the most enchantments a weapon holds), and
  `FirstEnchantmentWear` and `EnchantmentWearStep` (attaching #n needs a deposit
  of at least `FirstEnchantmentWear + EnchantmentWearStep × (n − 1)`). They
  replace the `WearCapacityLadder` and `EnchantmentWearCost` placeholders: a file
  naming either is told it is no key and pointed at the key that took its place.
  `WearPerHit` and `GraftBaseChance` are not R4's numbers, and wait with their
  Phase 6a readers as before.
  - **Each number its own key — how they split is provisional, the user's to
    rule.** Apart, any one number moves alone, and a weapon's starting capacity
    stays `StartingPool`, the Phase 2 pass's one key for every pool. The
    alternatives: tie them — slot n opening at, and enchantment #n costing,
    `StartingPool + SlotCapacityStep × (n − 1)`, three keys, so the slots and the
    price can drift neither apart nor from the start — or split the start into a
    key of its own, un-ruling the Phase 2 pass. Apart, the slots and the price
    agree at the ruled numbers and part only if tuned apart (consequence 4,
    above). It goes to open-questions.md with the save model's own choices, once
    that is built.
  - **The clamps**, each the whole check a file's number gets:
    `WearCapacityPerBar` 0–1000 — 0 freezes capacity, a dial rather than an
    error, and 1000 is where the file's magnitudes clamp; `FirstSlotCapacity`
    1–10000, a capacity, so `StartingPool`'s range; `SlotCapacityStep` 0–10000,
    0 opening every slot at once; `EnchantmentSlots` 3–10 — never fewer than a
    weapon already arrives with, the floor read off
    `ContentValidator.MostArrivingEnchantments` (the Nameless Knife's three) and
    never written as 3, so no `tuning.json` can declare a weapon over its own
    slot count, and at most twice the docs' five until Phase 6a sees a weapon
    carry more; `FirstEnchantmentWear` 1–10000 — never free, since a weapon
    never fought with is never enchanted (§6.1); `EnchantmentWearStep` 0–10000,
    0 pricing every enchantment as the first.
  - **Keys ahead of their reader: validated, and outside the hash until they
    play.** Each is `TuningKey.Played = false`, and is read exactly as every
    key: the built-in copy must name it with a whole number, a copy beside the
    exe merges it, clamps it and warns, a doc's spelling is pointed at it, and
    the log names it among the numbers a file changed. But the save's tuning
    hash walks the played keys alone. A save's hash answers "are these the
    numbers it was played under?", and a number nothing plays is none it was
    played under — so turning one never says THE NUMBERS HAVE CHANGED on a
    resume, and adding the six moved no save's hash: the saves written before
    the rework still hash as the tuning they were played under — their frozen
    tuning file, read over the master, lacks these six keys and no other. No
    `ContentValidator.ValidateTuning` rule: it holds what a reader
    needs of a `Tuning` built in code, and nothing reads these. Phase 6a flips
    each to played as it reads it, with the save's version 2.
  - **The docs' five is two numbers, kept apart on purpose.** `EnchantmentSlots`
    is how many enchantments the enchanter lets a weapon hold; the Light shape's
    five — `ContentValidator.LightShapeSlots`, a rule constant: `signature ×n`
    buys `5 − n` souls (§1.5) — decides what a unique *arrives* with. They agree
    at five by the docs and mean different things: moving the key moves the
    enchanter's ceiling and never the unique table's shape, and the key's floor,
    `MostArrivingEnchantments` — the Light shape at its floor, `LightShapeSlots −
    LightShapeFloor` — keeps the two from contradicting.
- **R3(a) — every number of a content file has a range.** The user's reason:
  "since we are storing in raw json we should probably not trust the fields".
  Every number `weapons.json` and `dungeons.json` hold has a row of
  `ContentBounds` — the range the game plays it in, and why — and one outside it
  stops the game under `RuleNumberInRange`, naming the file, the entry, the field,
  the number and the whole range: `cost 0 is outside 1 to 10000 - movement is
  charged a unit at least, …`. Never clamped, since content is validated (§5.4):
  a copy beside the exe out of range never falls back to the built-in copy, and a
  built-in copy out of range is the master's own — a range is the game's, never
  another file's to break — so the screen names the master to fix and rebuild.
  Every top is read once off a `tuning.json` clamp's ceiling or a rule of the
  game, never a number of a master, and every shipped and doc-fixed number lies
  inside its row:

  | Number | Range | Why |
  | --- | --- | --- |
  | a class row's `range`, and a variant's own | 1 – `MovementBudget`'s ceiling (10000) | a reach of 0 hits nothing, and the farthest the tuning measures is past any map |
  | a class row's `damage`, and a variant's own | 0 – `PerStack`'s ceiling (1000) | a staff hits for 0, its cast being its innate; the most a magnitude can be |
  | a class row's `cost`, and a variant's own | `Progression.MinimumMovementCost` (1) – 10000 | movement is charged a unit at least, so a smaller cost names one never charged, which the HUD's COST would show |
  | a class row's `manaCost`, and a variant's own | 0 – 1000 | a martial class casts for 0; below 0 the cast gate opens on an empty pool |
  | a class row's `baseline` stack; a weapon's spread as composed, a unique's `raises` among it | 1 – the modifier's tuned `MaxForged` (3 at most) | `RuleForgedStackCount` and `RuleMaxForged`, both ends, as before |
  | a variant's `adds` stack | 1 – `MaxForged`'s ceiling (3) | an add adds a stack at least, and none is deeper than any forge goes; what it composes to stays the forge's rules' |
  | an enchantment's `tier` | `DropTier` (1) – `ArtifactTier` (3) | a weapon arrives at tier 1 and an artifact at 3 at most; tier grows in play from there, uncapped (§3.3), and a saved weapon's tier is the save's to bound. Below 1 plays as 1, a tier the game never plays |
  | a wand's `radius`, `length`, `width`, `targetReach` its kind measures | 1 – 10000 | a measured dimension of 0 covers nothing (a left-out one read as 0); the farthest the tuning measures |
  | a Cone's `angleDegrees` | 1 – a full turn (360) | a cone of nothing, or wider than all around, is no cone |
  | a pinned `bossFloorMin` or `bossFloorMax` | 1 – `BossFloorMax`'s ceiling (99) | a floor number starts at 1; no tuning names a deeper floor, and the fit can always serve 99 |
  | a boss's `maxHp` | 1 – `StartingPool`'s ceiling (10000) | a hit point at least; no pool opens on a bigger bar |
  | a boss's `movementBudget` | 1 – 10000 | a unit of movement at least; the farthest the tuning measures |
  | a boss's `innate` stack | 1 – `MaxForged`'s ceiling plus `AcquiredHeadroom`'s (53) | innate is no forge's — "a golem deeper than any forge is a golem" — but no stack grows deeper |

  - **The rules that bounded a number before keep the end they own**, and the
    range rule is asked after them, so each refuses what it always refused, its
    detail now in the range rule's form, naming the field and the whole range:
    `RuleBossStatline` (`boss.maxHp 0 is outside 1 to 10000 - …`, and
    `boss.movementBudget` alike; a boss that is missing keeps `names no boss`),
    `RuleBossFloorRange` (`bossFloorMin 0 is outside 1 to 99 - …`, and a
    `bossFloorMax` below 1 alike; an inverted range `bossFloorMax 3 is below
    bossFloorMin 5`, an end the dungeon leaves to the tuning named as its key,
    `tuning.json BossFloorMin 5` — asked only once both ends lie in range, so an
    inversion is never how a floor out of range is named),
    `RuleBossInnateStackable` (`boss.innate.Brace 0 is outside 1 to 53 - …`; a
    member that never stacks breaks no range, and keeps its detail), and the
    forge's two, which keep both ends of a forged count: a class row's
    `baseline.CritMultiplier 0 is outside 1 to 3 - …`, a weapon's
    `forged.CritWindow 4 is outside 1 to 3 - …`, the top the tuned `MaxForged` of
    the modifier. A refusal that names a relation keeps its words
    (`baseline Brace x1 on a Ranged weapon beside …`): it breaks no range.
  - **Where the range rule is asked.** A class row's statline once the row's own
    checks pass, so a fault is named on the class, where it is written, and never
    on the first variant that plays it; a weapon's statline as composed, its
    shape's measured dimensions and its enchantments' tiers once its row's
    checks pass; a boss's numbers after its own rules; and a pinned boss floor
    **once `RuleBossFloorRange` has kept the 1 of each end, and before it asks
    whether the range inverts or the retiring rule and the farm's read the
    floor**, so a floor out of range is named as one, with the whole range,
    whatever the tuning or the other end: a dungeon pinned to floor 500 is the
    range rule's, where it had been `RuleBossPaysLessThanTheFarm`'s; a
    `bossFloorMin` of 150 beside the tuning's deeper end is the range rule's too,
    never `tuning.json BossFloorMax 10 is below bossFloorMin 150`; a
    `bossFloorMax` of 0 is `bossFloorMax 0 is outside 1 to 99 - …`, never below
    the other end; and `TuningFit` raises no offset for a floor past the top and
    says nothing about it. What a variant `adds` the reader asks
    as it reads it, before summing it onto the class's baseline: summed, a raw
    `-1` would read as a composed stack one short, a `0` on a baseline modifier
    would add nothing in silence, and the largest whole number would wrap — so the
    refusal names the number written, at its path (`weapons.json:
    variants[0].adds.Light: 0 is outside 1 to 3 - …`). A unique's `raises` is set,
    not summed, so the composed spread's rules name its raw number.
  - **The boss floor's top is the save's too.** `ContentBounds.DeepestBossFloor`,
    `BossFloorMax`'s clamp ceiling, is one accessor that the content's pinned
    floors and the save's boss floor both read (`SaveMapper`), so the two never
    disagree about how deep a boss goes; the save had summed a farm's banks to
    198 while a pinned floor had no top. And at `AcquiredHeadroom`'s clamp floor a
    farm banks 2, so a boss on floor 99 needs an offset of 98, inside
    `BossStackOffset`'s clamp: the fit can always serve the deepest floor a
    dungeon may pin, which the suite asserts off the schema.
  - **What holds no number**: `restricted.json`'s relations are ids and groups of
    ids, and a caster's `dropOrder` is ids, its length and membership bound by
    `RuleCasterVariantPool`. A dimension a shape's kind does not measure is 0 or
    refused by the reader, as before.
  - **A unique soul's tier is held to the same 1 – 3, and plays at its pin.** A
    narrower row, 1 to 1, was planned — a soul written above 1 names a tier the
    game never plays (`Enchantment.Tier` pins it) — and is not built: the suite
    loads a soul written at 3 and holds the pin on it
    (`UniqueContentTests.UniqueSouls_AreNeverRolled_AndTierPinned`), a test this
    step had no licence to rewrite. Narrowing it is that row and that test.
  - **The enchantment catalogue, the other half of the old question, is settled
    as code.** Under the ruling on content it is code (`EnchantmentDefinitions`),
    reviewed and tested like every rule, and R3's reason does not reach it: no
    player edits it. Its load checks stand (`RuleEnchantmentLockIsPositive`,
    `RuleZeroTrigger`, `RuleMagnitudeNamesAPotency`), and the dialled numbers laid
    over it (`ApplyPercent`, `ArcanePotency`) are `tuning.json`'s, clamped 0–1000.
    Nothing of it is a user decision, and the question — whether a content file's
    numbers have bounds — leaves open-questions.md whole.
- **R3(b) — a save is raw JSON too: every count has its ceiling, and the fields
  are checked against each other.** The user's reason is R3's: "since we are
  storing in raw json we should probably not trust the fields". The save's walk
  (`SaveMapper`) already refused an id the content lacks, a count below its
  floor, a list that is not one and two answers to one question; it now also
  refuses every count whose larger value would wrap the arithmetic that reads it
  — at the field's own path, beside its floor, before anything else is asked of
  it — and, once every field has been read on its own, `SaveConsistency.Check`
  asks the fields of each other, so every refusal of a single field keeps its
  path. A save that fails either is refused whole as one that cannot be read:
  set aside as `campaign.unreadable-N.json`, never deleted, the backup tried, and
  never a crash (the resume catches every exception). The notice shows the field
  and the first clause of what is wrong there — `CAMPAIGN.JSON COULD NOT BE READ
  (CAMPAIGN.RETIRED[0]: TUTORIAL IS RETIRED AND WAS NEVER CLEARED)` — so every
  problem is written in the HUD's font, an id named bare. Every ceiling and every
  check holds by construction in play, on both saves the pre-rework code wrote
  and on the Flagship, and `SaveConsistencyTests` plays a campaign through every
  outcome to show it.

  | Count (a weapon's wherever it is: a bag, a floor, a spoil, a row, a pinned drop, a summary) | Refused when | Read off | What it would break |
  | --- | --- | --- | --- |
  | a forged stack | above 3 | `MaxForged`'s clamp ceiling (`ContentBounds.DeepestForge`): nothing forges past it | a spread past any forge, which the cap then lifts further |
  | a live stack | above its forged stack + 50 | `AcquiredHeadroom`'s clamp ceiling (`MostAcquired`): acquisition stops at the forged count plus the headroom, and a lowered headroom never takes a stack away | a Block two billion deep, whose absorb overflows |
  | a member's innate stack | above 53 | the deepest a stack grows (`DeepestStack`), a boss innate's top in `dungeons.json` too | a member's Block past any weapon's |
  | an enchantment's tier | above 2147 | `DeepestTier`: the largest whole number ÷ (`ApplyPercent`'s clamp ceiling 1000 × `ArcanePotency`'s 1000, the largest potency) | an entry's lock (lock × tier) and its levels (percentage × potency or source × tier) past an int |
  | a weapon's enchantments | more than 10 | `EnchantmentSlots`' clamp ceiling (`MostEnchantments`); none arrives with more than 3 | `Weapon.TotalLock` summing its locks past an int |
  | the run counter, and the last run's copy of it | 2,147,473,846 or more | `MostRuns`: the largest whole number less `BossFloorMax`'s ceiling 99 × `CooldownRunsPerFloor`'s 99 | a run's end adds one, and a clear locks until the counter plus up to 9,801 runs: both would wrap negative |
  | the visit's boss floor | above 99 | `DeepestBossFloor`, R3(a)'s | a dungeon no content made, every floor of which a load generates |
  | a member's hit points; a row's | above 10000 plus its HP XP; above 10000 plus its revival gains — each summed as a long | `StartingPool`'s clamp ceiling (`Bar`): a maximum is its bar plus a point for each HP XP at most, or its base plus what its revivals added | a member standing on 99999 HP |

  - **The tier's ceiling is a sanity bound, and is recorded as one.** Tier is
    uncapped by design (§3.3, "the pool is the cap"), and 2147 is only where the
    arithmetic of an entry stops being whole. Play never nears it: an entry
    climbs only while awake, its lock inside its wielder's max mana, so tier 2147
    under the catalogue's smallest lock (15) wants 32,190 mana — some 117 million
    mana XP — and a drop banks at most `AcquiredHeadroom` tiers onto an
    artifact's 3.
  - **Every ceiling is a clamp's ceiling or a rule, never the tuning in hand**,
    read once in `ContentBounds` beside R3(a)'s, so no tuning in range and no
    content that loads refuses a save play wrote (the run counter's own edge
    aside, below), and the content and the save never disagree about how deep a
    number goes. Beside the three the save alone needs (`DeepestTier`,
    `MostRuns`, `MostEnchantments`), a live stack's reads `MostAcquired`, which
    `DeepestStack` is now built from.
  - **A lock keeps its floor alone** — the campaign's and the last run's. The
    first draft held both to `MostRuns` as well, and the review of Phase 5.S4
    found that play could write a lock the next load refused: a clear locks until
    the counter plus up to 9,801 runs — `MostRuns` or more whenever the counter
    is within that of it. And a lock needs no ceiling, since nothing reads one but
    against the counter: entering and the list compare the two (`IsSelectable`,
    `EarlyOffer`), and the two countdowns take the counter from a lock above it —
    the list's, only for a dungeon not yet selectable, and the summary's, whose
    lock check 7 holds at or above its own counter — so no lock wraps anything.
  - **The cross-checks**, in the order asked: (1) a retired dungeon was cleared —
    it retires on its clear; (2) a locked one was cleared — only a clear writes a
    lock; (4) with no run ended, nothing is counted and nothing cleared; (5) a
    boss kill has the boss down and a retreat has it standing; (6) a run counted
    exactly when its boss fell or its mercy was earned, and never a mercy with the
    boss down; (7) a clear locks or retires its dungeon and never both, a failure
    does neither, and a last run's own lock is never below its own counter; (8)
    the boss floor is told exactly when it was reached — it is the deepest floor,
    never missing with the boss down — and the deepest floor is no deeper than
    99, the ceiling a visit's boss floor has; (9) spoils are lost and XP clawed
    back only by a wipe, and a wipe keeps no spoil; (10) a claw-back takes back,
    once a pool; (11) the last run's counter is the campaign's; (12) a last run's
    lock is the campaign's on that dungeon, unless a visit to it is in progress
    and the lock is the counter — the early offer's rewrite — while a run that
    locks nothing writes none beside an earlier clear's lock, so an unset lock is
    never compared; (13) a clear's dungeon is cleared, and a retiring clear's
    retired; (14) no two members share a colour; (15) a visit's dungeon is neither
    retired nor locked past the counter, which only the early offer's lifted lock
    lets it be entered at; (16) what each pool held at the door is no more than it
    holds now — a visit only adds XP; (17) the boss's drop is promised no more
    stacks than the boss floor; (18) where the bottom floor is saved, its boss
    row stands exactly while the visit has the boss standing — its fall records
    it, and a boss never rises; (19) a drop is taken only from a corpse, once the
    boss is down.
  - **The ruling's own example is legal state — please confirm.** "A lock ending
    before the current run count" is how a lifted lock stays: nothing removes a
    lock (`CampaignState.LockedInOrder`: "a lock that has lifted stays, reading a
    run already reached"), since a lifted lock already reads as open — the
    counter at or past it — and the early offer compares locks lifted or not; and
    the early offer itself writes the counter as a lock. Refusing it would refuse
    every campaign that played a cooldown out. The form of it no play writes is
    check 7's — a last run's own lock below its own counter — and that is refused.
  - **Check 3, a lock on a retired dungeon, is withdrawn.** `dungeons.json`
    beside the exe may move `retiresOnClear` onto a dungeon already cleared and
    locked (`RuleOneRetiringDungeon` asks only that one dungeon carry it, and a
    retiring dungeon pins its floor), and its next clear retires it and leaves
    the old lock where it was: play writes the pair. It is inert — the list, the
    early offer and entering all ask whether a dungeon is retired first.
  - **Considered and not refused**, each for its reason: HP and mana against
    their maximum — stored mana reads as its spendable ceiling at most, and the
    maximum is derived at load from today's tuning and content, so a save made
    before either was lowered holds more HP honestly, and play meets it (a heal
    adds nothing past full, a rest sets the maximum); a soul's tier above 1,
    which its pin plays at 1 (a later build may make an entry a soul); a lock
    further ahead of the counter than a clear writes, which only counts down, the
    early offer reaching it before any dead end; the active member naming nobody,
    where the scene falls back; an enchantment carried and never seen; spoils
    lost on a wipe whose boss stood, which the summary alone reads; a row's farm
    counters against each other, and the boss row's defeat turn against the
    visit's — tests fell rows by hand, and only a revival's timing reads them,
    which tolerates it; a member's `alive` against its HP, written apart; and
    bags of unequal length, each member keeping the bag it was built with.
  - **The walk's known edge.** Every other count — the XP pools and an entry's
    XP, the turn and the dice rolled, a row's farm counters and revival gains,
    held shots, a status's levels — keeps its floor alone, since no clamp or rule
    gives an honest ceiling short of the largest whole number: a pool grows with
    play, a soul banks its spend for ever, revival gains compound. So a
    hand-edited count within one credit of the largest whole number can still
    wrap on that credit. And the run counter is the one count play itself carries
    up to its ceiling: a counter a hand edit leaves just short of `MostRuns`
    loads, and the ticking runs played on from it take it to `MostRuns`, a save
    the next load sets aside — and played on past that in the same session, a
    clear's lock can wrap. No ceiling on a count play adds to can admit
    everything played on from it; two billion runs are never played, so only a
    hand edit meets it (`SaveConsistencyTests` pins it). A list longer than play
    writes — a floor's rows — costs load time, never a wrong number: every
    duplicate and every row that names nothing is refused already.
- **R2, the folder — the save folder's slots, and a save's time.** The user's
  ruling: a title screen offering CONTINUE, NEW CAMPAIGN, LOAD GAME and QUIT; one
  autosave slot that saves itself where the game autosaves today; and manual
  slots, "the number of saves should live on the tuning.json so that we don't need
  to fix the code every time I change my mind". Its first part is the disk and
  the loader under them, built beneath a launch that plays exactly as before —
  it loads the autosave, and no play writes a manual slot yet:
  - **The save folder is the autosave slot**: `campaign.json`, its backup, its
    markers and its set-aside files, by the names they always had. So a save
    written before the slots is the autosave as it stands — no file moves, and
    there is no migration to fail halfway; both saves the pre-rework code wrote
    load as the autosave, through the listing (`PreReworkSaveTests`). **Manual
    slot n is the folder `slot-n` inside it**, holding the same files for its own
    save: its own backup, markers and set-aside files. The names are built from
    the numbers and the folder is never enumerated, so a folder named any other
    way — `slot-01`, `slot-10`, `slot-x` — is no slot: never read, listed or
    written, and no folder's spelling or order reaches a choice.
  - **The slot count is `tuning.json`'s `SaveSlots`**: 3, clamped 1–9 with the
    usual warning. At least one, so saving by hand always has somewhere to go; at
    most nine, since a digit picks each slot, as the bag's are picked, and the
    number row stops at 9. It sits in a group of its own, `Saves`, the file's
    last, and is `TuningKey.Played = false`: the launcher reads it, never a rule
    of play, so the save's hash leaves it out and turning it never says the
    numbers changed. A count lowered below a slot that holds a save deletes
    nothing: the slot is kept, listed for loading and never saved into, and a
    count raised again makes it an ordinary slot.
  - **Every save carries its time** — `savedAt`, the document's last member: the
    UTC instant as round-trip text, from the edge's clock. `Logic/` never reads a
    clock; the game hands the time in, and a test a fixed one. A save written
    before the member has none, and reads as one with no time. A save of the
    campaign a slot already holds, whatever its time, writes nothing — the two are
    compared with the time left out — so the backup stays the save before it and
    a save keeps the time its state was first written. `savedAt` is raw JSON like
    every field (R3): one that is no round-trip time refuses the save whole,
    `savedAt: yesterday is no time`, set aside as any unreadable save is.
  - **The most recent save** — what CONTINUE is to load — is chosen by that time,
    deterministically: the latest among the slots whose save, or failing that
    whose backup, reads; a save with no time is older than any with one; a time
    more than a day ahead of the clock counts as none, so a hand edit or a clock
    once set wrong can never pin the choice on one slot through every autosave
    after it, while the day's slack admits a save carried from a machine whose
    clock runs a little fast; and a tie goes to the autosave, then the lower
    slot number, by number and never by a folder's order. File times are never
    read — a copy or a sync tool rewrites them, and a test cannot fix them — at
    the price the README states: a clock set back can make an older save look
    newer. A counter would need every slot read at every write.
  - **The listing reads and never moves**: each slot's save and backup are read
    and parsed for a header — when it was saved, the runs counted, and the
    dungeon, floor and turn of a run in progress — and never restored, since a
    run in progress would regenerate its floors; nothing is moved, deleted or
    written. A save that parses and will not restore — content gone, a
    contradiction — is listed as reading, and its load sets it aside.
  - **A load is per slot, and never makes a campaign**: a slot's resume marker,
    its save, then its backup, each set aside in the slot's own folder when it
    cannot be read or restored, as the one launch's resume always did. The words
    are the autosave's as ever — `CAMPAIGN RESUMED`, `... FROM ITS BACKUP, ONE SAVE
    OLDER` — and a manual slot's `CAMPAIGN RESUMED FROM SLOT 2` and `CAMPAIGN
    RESUMED FROM THE BACKUP OF SLOT 2, ONE SAVE OLDER`, green like them. A slot
    that brings nothing back hands the choice to its caller: the launch begins a
    campaign, saying `A NEW CAMPAIGN BEGINS` ahead of whatever was set aside.
    (Superseded by the title, below: the launch no longer loads, and a load that
    brings nothing back goes back to the title, saying so.)
  - **No write costs a file.** Until the slots, every launch set an unreadable,
    crashed or newer-build `campaign.json` aside before anything wrote there. A
    slot can now be written without being loaded — the autosave after a manual
    load, a save by hand into any slot — and the replace would rotate such a file
    into the backup and delete it at the next write: a newer build's save written
    over, which the codec promises never happens, and a readable backup deleted
    under an unreadable save. So the one door every write goes through checks the
    files first: a `campaign.json` whose text the codec refuses is set aside as
    `unreadable`, and the write moves into place with the backup untouched; while
    a save stands for the replace to rotate, a backup the codec refuses is set
    aside too. Only the codec's refusal counts — a file the file system cannot
    read right now is left to the write, which fails and says so, so a passing
    lock never refuses a session's writes — and a file this session wrote there
    or read whole is not read again. What it sets aside is said like a failed
    save, at once over the floor and on the next screen, and never taken back: it
    is a fact about a file. A file that will not move refuses the slot's writes,
    as a load's does, and its notice is the failure.
  - **A write clears its slot's resume marker.** The marker guards the file a
    load was resuming; once a write has replaced that file, a marker left standing
    would set the new save aside as crashed at the next load and resume the
    backup — the very save that crashed. No write reaches a slot while its own
    load is in flight: the autosaves a loaded screen asks for wait until it is up
    and its marker cleared, and are written the frame it clears, so a load whose
    first screen crashes never reaches the save it was loading.
  - **A dev-override session never touches a slot**: its store is its scratch
    slot, emptied as ever, and the slots are never made, so no save of the session
    — entry, turn, arrival, a run's end, quit — reaches the autosave or a manual
    slot.
- **R2, the title — CONTINUE, NEW CAMPAIGN, LOAD GAME, QUIT.** The ruling's
  second part: "the game opens on a TITLE SCREEN offering, in this order, CONTINUE
  / NEW CAMPAIGN / LOAD GAME / QUIT". Built on the folder above, in place of the
  launch that resumed the autosave:
  - **The game opens on the title**: `DISTANCE RPG`, the notices under it, the
    four options in the ruling's order, and the keys along the bottom
    (`TitleScene`, `TitleHud`, every string of `TitleText` and `SlotText` tested).
    A dev-override launch has none — it boots straight into its scratch campaign,
    as ever — and `--new-campaign` begins a campaign at launch, straight to the
    screen between runs: both developer tooling. No campaign is in play at the
    title, so a close from it saves nothing. The data files' notices are the
    title's, as the first scene's.
  - **CONTINUE loads the most recent save** — the listing's choice (R2, the
    folder), the autosave or a manual slot — and names it by its own row:
    `CONTINUE - AUTOSAVE - THE SPEAR HALL FLOOR 4 - 3 RUNS - 2026-09-25 14:32`, the
    time in the player's own zone. With nothing to load it is `CONTINUE - NO SAVE
    YET`, dimmed and passed over, and the choice starts on NEW CAMPAIGN; otherwise
    it starts on CONTINUE, so ENTER at launch is one key to where the game stood.
  - **LOAD GAME lists every slot** in the options' place and the notices', so the
    list never contends with the save's word for height: `AUTOSAVE`, `SLOT 1` to
    the count, then any slot beyond it that holds a save. A slot is its own row —
    where its campaign stands (the dungeon's name and floor, or `BETWEEN RUNS`),
    the runs counted and the time, or `EMPTY`, or `CANNOT BE READ` — and, only
    when one applies, a markers' row under it: `FROM ITS BACKUP`, `ITS LAST LOAD
    DID NOT FINISH`, `AHEAD OF THIS CLOCK` or `NO TIME RECORDED`, `BEYOND THE 3
    SLOTS - LOAD ONLY`. One row of both would run past a 1280-pixel window at any
    scale the list can be read at; split, at the list's one scale (1.4), ten
    slots each with its markers' row fit a 720-pixel window. W/S or the arrows
    move over the slots that hold something, a digit takes its slot (0 the
    autosave), and an empty slot says `SLOT 2 IS EMPTY` and loads nothing. A digit
    loads at once, as ENTER does, and never only picks, so the keys line says so:
    `W/S OR ARROWS CHOOSE - ENTER OR 0-9 LOADS - ESC BACK` (S6's review; the plan's
    `W/S OR ARROWS OR 0-9 CHOOSE - ...` read as a digit that only picks). A slot
    whose files none of which reads can be chosen: its load sets them aside and
    says so. Nothing drawn is the file's raw text: a dungeon id the content lacks
    is `AN UNKNOWN DUNGEON`, and a time the player's zone cannot hold is shown in
    UTC.
  - **Every ask is two rows, where the keys stand, and only ENTER says yes**:
    what it does, then `... - ENTER: YES - ESC: NO`. N, Y and every other key say
    no and are spent, so a player pressing N to mean no keeps what the ask named —
    the guarantee the between-runs prompt gave (a key that answers never also
    acts), kept, and its N-then-N yes gone with it.
  - **NEW CAMPAIGN asks only when it sets something aside** — USER DECISION 3,
    shipped provisionally: with the autosave slot empty it begins at once; when it
    holds a save or a backup it asks, `NEW CAMPAIGN SETS THE AUTOSAVE ASIDE - THE
    SPEAR HALL FLOOR 4 - 3 RUNS` / `AS FILES, NEVER DELETED - SLOTS 1 TO 3 ARE
    KEPT - ENTER: YES - ESC: NO`, and on ENTER sets the autosave's backup and save
    aside as `campaign.abandoned-N.json` — `--new-campaign`'s path, its notices
    naming no command line — the manual slots untouched, the new campaign saved
    at once, and the screen between runs saying `A NEW CAMPAIGN BEGINS` and where
    the old files went. The alternative: it asks, then saves over the autosave
    (the old save one save deep as the backup, then gone).
  - **A manual slot loaded over a different autosave asks first, then sets that
    autosave aside — never a silent loss** (USER DECISION 2, shipped
    provisionally). Whatever slot a campaign is loaded from, it autosaves into the
    autosave slot, and the manual slot changes only when saved into by hand. Left
    at that, a different campaign in the autosave slot — for a player who never
    saved by hand, their only copy — would be the backup after the first autosave
    and gone after the second. So a load of a manual slot while the autosave holds
    anything else (another text, time aside, or files that cannot be read) asks —
    `LOADING SLOT 2 SETS THE AUTOSAVE ASIDE - ...` / `AS FILES, NEVER DELETED -
    ENTER: YES - ESC: NO` — from the list or from CONTINUE alike, and once the
    slot's load has brought its campaign back (never before, so a load that
    restores nothing leaves the autosave as it was) and before anything is
    written, the autosave's backup and then its save are set aside
    (`CampaignBoot.MakeRoom`): the one its resume marker guards as `crashed`, one
    the codec refuses as `unreadable`, anything else as `replaced` —
    `CAMPAIGN.JSON MADE WAY FOR SLOT 2 - SET ASIDE AS ...` — said on the loaded
    screen after the load's own word; then the marker is cleared. A file that will
    not move refuses the autosave's writes, as every stuck file does, and nothing
    is autosaved that session. A slot whose text is the autosave's own (a hand save,
    then a quit that changed nothing) asks nothing: continuing it is continuing
    the autosave. The title's ask and the host's load read one question, off the
    one listing the title showed. A slot listed `CANNOT BE READ` is asked too, in
    words that promise no set-aside — `SLOT 2 CANNOT BE READ - IF IT LOADS, THE
    AUTOSAVE IS SET ASIDE - ...` — since its load most likely sets its own files
    aside and brings nothing back, which leaves the autosave as it was (S6's
    review: the plain words promised a set-aside that would not happen, and could
    scare a player off trying the slot). It is asked, never skipped: a file
    another program held while the list was read can load a moment later, and a
    load that set the autosave aside unasked would break the ask's promise. The
    alternative: a loaded manual slot autosaves into itself, the autosave slot
    kept for the last campaign begun or continued there — no ask and no file set
    aside, but a manual slot is then written by the game and not by hand alone.
  - **A load that restores nothing goes back to the title** — every file of the
    slot set aside — which says so first, whole: `NOTHING COULD BE LOADED FROM
    SLOT 2` (or `FROM THE AUTOSAVE`), then each file set aside, in the load's
    order; the listing is read again (the slot now empty, or its backup's), and
    nothing else is touched. Nothing begins on its own any more: the launch's `A
    NEW CAMPAIGN BEGINS` over what was set aside is gone with the launch's resume.
  - **A refusal is the session's, and a campaign that will not be autosaved says
    so on its first screen** (S6's review). A file that can be neither read nor
    moved refuses its slot's writes for as long as the game runs (the slot's one
    store, R2 above) — even once another program lets go of it — so nothing
    written that session takes its place. Before the title there was one load, at
    launch, and the refusal came with it; the title lets the player try again,
    and a try that loads once the file is free, or a NEW CAMPAIGN after a stuck
    one, plays a campaign the autosave will not take. So that campaign's first
    screen says it, after the load's word or the new campaign's: `NOTHING IS
    AUTOSAVED THIS SESSION: EARLIER, CAMPAIGN.JSON COULD NOT BE READ (ANOTHER
    PROGRAM HAS IT OPEN) AND COULD NOT BE SET ASIDE (ANOTHER PROGRAM HAS IT
    OPEN) - LAUNCH THE GAME AGAIN TO SAVE`, rather than leave it to the first
    autosave to fail. A relaunch is the retry. A refusal the load or the new
    campaign met itself is said by its own stuck notice, once. The alternative,
    not taken: let a retry clear the refusal once the file reads again — which
    would change the refusal's contract, and is the user's to rule.
  - **The unsaved-quit marker is the title's to say**: it is the autosave's (a
    quit saves there), and the title takes it when it opens, before anything is
    loaded, in words true of the window's close and of leaving play for the title
    alike, and of a manual slot's campaign that a quit was writing into the
    autosave: `THE AUTOSAVE COULD NOT BE WRITTEN WHEN YOU LAST LEFT PLAY - WHAT IT
    HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT IT`. A load says what resumed,
    and nothing of the marker.
  - **What went with the title** (R2(e)): the launch's resume — CONTINUE is its
    replacement — and the screen between runs' NEW CAMPAIGN, N then N, whose keys
    line now reads `W/S OR ARROWS CHOOSE - ENTER DESCENDS - ESC SAVES AND QUITS`.
    ESC between runs still saves and quits, and the pause menu's `2  SAVE AND
    QUIT` still closes the game, until saving by hand gives both screens their
    menu. The developer tooling stays as it was: `DISTANCERPG_DUNGEON`,
    `DISTANCERPG_SEED` and their scratch slot, `--new-campaign`, `--write-data`.
  - **The save's word can now name three files** (the entry on the save's word,
    below): a manual slot's save set aside as its backup resumes, and the
    autosave's two making room. The common case — a slot's campaign making room
    for itself — takes about seven rows in the player's own save folder; the
    rarest — the slot resumed from its backup under other numbers, its save set
    aside with a long reason, the autosave's crashed save and its backup set aside
    — about fourteen, the ceiling itself, past which the rows are cut and counted
    and the log has every word. S6's review measured that rarest word at about
    fifteen with a longer user name in the path — a row past the ceiling, cut and
    counted like any other. Left as recorded: raising the ceiling a row puts a
    resumed floor's banner exactly at the two-thirds line it keeps above, and
    shortening the notices (the folder named once) is a change to every
    set-aside notice; both are left for the step that brings saving by hand,
    which measures the word again.

### The numbers: `tuning.json`

- **A key is the `Tuning` property's name, verbatim, and `MovementBudget` is the
  one new key.** One naming source: `DotDamagePerMana` (the entry above that
  made it an integer reciprocal), and never an alias — §5.3's spellings are
  unknown keys whose warning points at the key. `MovementBudget` is the 160 the
  Phase 2 pass named, in logic units, and the party's refill, its bank's cap and
  the enemies' cost scale read it. (Superseded by the user's ruling of
  2026-09-25 on the deposit, in its count: six more keys came with it, the
  Enchanter's — R4's keys, above.)
- **`OverhealPerWard` is the key, and `OverhealPerWardLevel` is the other
  spelling of it.** The Overheal entry above calls its rate
  `OverhealPerWardLevel`; §5.3 and the provisional-numbers batch call the same
  constant, at 5, `OverhealPerWard`, which is the `Tuning` property's name and
  so the key. `OverhealPerWardLevel` in `tuning.json` is an unknown key: it is
  ignored, and its warning points at `OverhealPerWard`, as the warnings for
  §5.3's own spellings point at theirs. The graft formula's
  `StartingWearCapacity` points at `StartingPool` the same way, the one key the
  Phase 2 pass made it.
- **`MovementUnitsPerMana` is re-derived by hand, never computed from the
  budget.** It is derived — `(MovementBudget − 40) / (8 + 3)`, the budget less a
  staff's 40 of movement, over the mana one cast moves at Resonant: 10.9, so 10
  — and its comment in the file carries that formula and says the divisor does
  not follow the budget. Computed, a budget edit would silently re-price every
  cast; a designer who turns the budget re-derives the divisor on purpose.
- **The file clamps; a tuning built in code is still refused.** A missing key
  keeps the built-in file's number, a table merges per entry, an unknown key or a
  value that is not an integer warns and is ignored, and a value out of range
  clamps and warns — a typo costs a line on screen and in the log, never a
  session. `ContentValidator.ValidateTuning` keeps refusing a `Tuning` built in
  code — now also a table that does not name every member of its kind
  (`RuleTableNamesEveryMember`) — so every rule still has its refusal, and every
  tuning a file can make passes it. (Superseded by the user's ruling,
  2026-09-25, in its first clause: a missing key kept "its compiled default"
  until the ruling took every number out of the code.)
- **Where tuning meets content, the tuning bends — never the content, and never
  with an abort.** Three keys are measured against the content as well as
  against themselves, and an in-range value can still break shipped content: a
  forge limit below a shipped spread, a carry too small for the roster's
  loadout, a boss offset that lets a pinned boss floor pay the farm. So
  `TuningFit` raises `MaxForged`, `PartyCarrySlots` and `BossStackOffset` to what
  the loaded content needs, within their own ranges, with one warning naming the
  entry that needed it; content no in-range value can serve is content's own
  error. Every tuning a file can make loads with the shipped content, and a test
  holds that.
- **The save records the hash of the effective values**: the fitted tuning,
  every table expanded over every member it can be asked for, as sorted
  `Key=value` lines, so formatting, order and comments never move it and any
  value — a number the master copy ships, changed by a new build, included —
  does. (Superseded by the user's ruling, 2026-09-25, in its example: this read
  "a compiled default a new build changed". The hash covers the same values it
  did, so a save written before the ruling still hashes as its tuning. And by
  the user's ruling of the same day on the deposit, in its reach: the values
  hashed are those of every key a rule of play reads. A key declared ahead of
  its reader — the Enchanter's six, until Phase 6a — is left out, so turning one
  never says the numbers changed, and declaring them moved no save's hash: R4's
  keys, above. And by the ruling of the same day on the save model, `SaveSlots`,
  which the launcher reads and no rule of play does, is left out the same way:
  R2, the folder, above.)
- **The `…Percent` key names — bends the phase doc** (§5.3 fixes
  `UniqueChanceCeiling` and `UniqueChanceK`; the keys are
  `UniqueChanceCeilingPercent` and `UniqueChanceKPercent`). Needs no ruling:
  §5.3 itself makes every ratio an integer with its units in its name, so the
  doc's `K = 0.26` is written 26 and must say percent, and the entry that made
  `DotDamagePerMana` an integer ruled the identical case.
- **The `Tuning` record, not `GameConstants` — bends the phase doc** (§5.3:
  "read once at startup into `GameConstants`"). Needs no ruling: a `const`
  cannot be overridden at runtime, §5.3 keeps the parity constants compiled
  "permanently and without exception", so tunables and parity constants must not
  share a home, and the record has been §5.3's home since Phase 1.
  `GameConstants` keeps only what never tunes.
- **The Enchanter and Consumables keys wait for their readers — bends the phase
  doc** (§5.3's starting set lists them). Needs no ruling: a key with no reader
  is a dial wired to nothing, and `EnchantmentWearCost` sits on the open
  question of what a deposit costs, which nothing here may resolve by accident.
  `tuning.json` still shows both groups as headings naming their keys, so §5.3's
  one screen stays whole. **The hand-off**: Phase 6a adds `WearPerHit`,
  `WearCapacityLadder` (30/40/55), `EnchantmentWearCost` and `GraftBaseChance`
  with their readers; Phase 6b adds `PotionUseCost`, `HealthPotionPercent`,
  `ManaPotionPercent` and `FarmYieldPerClear` with theirs. (Superseded by the
  user's ruling, 2026-09-25, in the Enchanter's half: what a deposit costs is
  ruled — R4, above — so no open question holds `EnchantmentWearCost`, and there
  is no ladder for `WearCapacityLadder` to hold. The ruling's six numbers have
  taken the two placeholders' place as keys of the Enchanter group, in the file
  ahead of their reader: the one exception to this entry's rule, since the user
  keeps a number they may revisit in the file, and a safe one, since nothing
  plays them and the save's hash leaves them out (R4's keys, above). A file
  naming either placeholder is pointed at the key that took its place.
  `WearPerHit` and `GraftBaseChance` still wait with their Phase 6a readers.)

### The content files

- **Content is strict where tuning is lenient.** Three files —
  `restricted.json`, `weapons.json` and `dungeons.json` — each a top-level object,
  every entry with a stable string id, field names the records' camel-cased and
  the doc's where it fixes them, enum values by their names, comments and
  trailing commas allowed. An unknown member is refused (`RuleFileShape`): a
  mistyped field in content is a rule that silently does not apply. Invalid
  content stops the game on a screen naming the file, the entry, the rule and
  its detail — and so does a number outside its range: every number of the files
  has one (R3(a), above), and the refusal names the field, the number and the
  range. (Superseded by the user's ruling, 2026-09-25: there were five
  files, the contract covering `enchantments.json` and `party.json` too, until
  the catalogue and the party became code.)
- **The staged load's order is a stated rule.** Every present file is parsed
  first, each shape failure recorded, then the stages run in §5.4's order — the
  enchantment catalogue on its own, the tuning, the relations and the catalogue
  against them, the weapons, the dungeons, the starting party — each first
  reporting its own file's shape failure: so a syntax error in a later file never
  pre-empts a semantic error in an earlier one. (Superseded by the user's ruling,
  2026-09-25: the one order a reader might not expect — `enchantments.json`'s
  shape reported before `restricted.json`'s own rules — went with the file.)
- **An absent content file plays the built-in copy, and says so.** The doc
  gives a fallback for missing tuning keys and is silent on a missing content
  file; the built-in copy is the shipped file itself, the master copy the game
  is built with, so there is nothing to abort for, and a notice keeps a renamed
  or deleted file from passing unseen. A file there but unreadable does the
  same, and says why. (Superseded by the user's ruling, 2026-09-25: it played
  "the compiled record", the copy in code the ruling removed.)
- **One owner for a dialled number: the dial.** `ApplyPercent`'s five ids and
  `arcane`'s potency are `tuning.json`'s (§5.3, and §5.4's "what number? —
  `tuning.json`"). The code catalogue carries no number of theirs — each row
  marks its dialled field as set by tuning — so there is no second number for an
  edit to disagree with, and a built-in `tuning.json` that leaves one out stops
  the game (`RuleTuningOwnsTheNumber`). (Superseded by the user's ruling,
  2026-09-25: the row in `enchantments.json` kept its field as the base, the
  file marked each dialled one, and an edit there was a notice naming both
  files; the notice went with the file.)
- **The content checks the files brought, none a new invariant**: a file's
  shape (`RuleFileShape`) and the weapons the code names existing
  (`RuleCodeNamedWeaponsExist`, now also a variant of each class the placer
  draws). With the ruling came the checks between the code and the JSON: each
  class of `weapons.json` having one row (`RuleEveryClassHasARow`), casting
  exactly when the code casts with it (`RuleCasterClass`), levelling on STR, DEX
  or INT (`RuleClassStats`) and signing alone (`RuleClassSignatureDistinct`) —
  each a code-names-it-so-the-JSON-must-fit check; a number the catalogue leaves
  to the tuning named there (`RuleTuningOwnsTheNumber`) and a tuning table built
  in code naming every member (`RuleTableNamesEveryMember`) — each a
  no-number-under-it check; and the game carrying every data file it plays built
  in (`RuleBuiltInCopy`) — a the-build-carries-it check. `kind` and `forgedOnly`
  name modifiers alone, since only the modifier rules read them: an enchantment
  named in either would silently not apply, so it is refused under the existing
  relation-id rule; and `neverRolled` names enchantments alone, the mirror of it
  (`RuleNeverRolledNamesEnchantments`, since the rework's audit, which retired the
  check holding `neverRolled` to the catalogue's unique souls). A caster class
  orders its drop variants on its row (`RuleCasterVariantPool`, which checked a
  list in code until the audit). (Superseded by the user's ruling, 2026-09-25:
  there were three, the third an effect name resolving to a kind —
  `RuleUnknownEffect` — which went with `enchantments.json`, since an entry's
  effect is now typed in code. And the user's ruling of the same day on raw JSON
  brought one new invariant: every number of a content file in its range,
  `RuleNumberInRange` — R3(a), above.)
- **"Naming both" is an explanation, not a check.** A weapon a relations edit
  makes illegal names the relation that refused it, read off the file in hand,
  asking the rules' own clauses in their order — for modifiers and enchantments
  alike. It is there exactly when the rule refuses.
- **Where content's truth lives: the JSON — ruled by the user (2026-09-25).**
  The shipped files are the master copy, hand-kept and built into the game as
  the fallback, with no copy of any of their values in code (the ruling above).
  A designer tunes the copies beside the exe and makes a keeper in the master.
  `--write-data <folder>` writes the four built-in copies out — the way back
  from an edited copy beside the exe — and refuses the source `Data` folder.
  (Superseded by the user's ruling, 2026-09-25: shipped provisionally as the
  compiled content, `ContentDefaults`, being the source, `--write-data` writing
  the six files from it, and a test pinning the shipped ones byte for byte.)
- **Class baselines are a section of `weapons.json` — ruled by the user
  (2026-09-25), withdrawing the bend** (§5.6: "the class baselines leave
  `Weapons.cs`" — now for a section of the file). Each class has a row — kind,
  levelling stats, signature (none for a caster), baseline, statline — a
  variant is written as its class and the modifier it adds (nothing, for a
  caster), and a unique as its variant and what it raises; re-basing a class is
  one edit. (Superseded by the user's ruling, 2026-09-25:
  shipped provisionally as derived, never a section — every variant carrying its
  whole forged spread, a class's baseline what its four variants share.)
- **No `condition` field in an enchantment's entry — bends the phase doc** (§5.7,
  and the entry above listing condition among an enchantment's dials). Needs no
  ruling: §5.7's own example puts the condition inside the effect an entry
  names, and Phase 3 resolved the condition vocabulary into the kinds' events; a
  column would be a second answer to "when does it fire". It holds for the code
  catalogue's record as it held for the file (superseded by the user's ruling,
  2026-09-25, in where the entries live: `EnchantmentDefinitions`, not
  `enchantments.json`).
- Whether a content file's numbers have bounds stays open: open-questions.md
  sets out both readings, and Phase 5 adds no new kind of content rule.
  (Superseded by the user's ruling, 2026-09-25: every number of a content file
  has a range, and the enchantment catalogue's numbers are settled as code —
  R3(a), above; the question left open-questions.md.)

### The save

- **A DTO layer, and every piece of live state accounted for.** The save is plain
  records in `Logic/Save/` — ids for content, names for enums, locators for
  references — restored through internal doors, never a public setter, and no
  gameplay type carries an attribute. The campaign is the outer document, the
  visit inside it or none between runs: PHASE6-223, Phase 6's line, shipped in
  Phase 5.4.
  Beyond §5.1's list the save holds the last run's summary; a member's innate
  modifiers and attunement (empty today); the whole a turn's movement cap was cut
  from, which the Mire legend reads; the spoils as locators, a bag slot or a
  floor, since a spoil put down is still a spoil; the current floor's sight this
  turn, which the pick and the march read; an untaken drop lying on a corpse,
  pinned; the visit's die's position; and the scene's view — the active member,
  and whether the turn marches. Every field of every type the save walks, the
  two scenes' included, is listed as saved, derived or not saved with its reason,
  in a test a new field fails. The marching line itself is not saved: a resumed
  march starts a fresh line, as every march does, so a reload is not claimed
  indistinguishable on a marching turn — only for what the turn's Logic calls do.
- **A pending revival follows a changed tuning; a lying drop does not.** A drop
  already lying there may have been seen, so it is pinned and never changes under
  an edit. A revival is a future event: under new numbers a dummy due by them
  gets up as the floor opens — the arrival rule above — and the changed-numbers
  notice promises exactly that the campaign plays on under the new ones.
- **The dice are the visit's own, seeded and saved — ruled by the user
  (2026-09-25): kept.** The d20 is a family of its own (`DiceSalt`, `DiceMix`),
  each draw a stream keyed by the map seed and how many the visit has rolled,
  and the count is saved, so after a reload the dice fall as they would have and
  a reload is never a reroll — "just to protect players from themselves", the
  user's reason: a bad roll is never reloaded away. The price stated with it is
  accepted as it stands: a turn replayed after killing the process knows its
  next draws, and keying each draw by the turn, the actor and the attack's
  ordinal instead is not taken (R1, above). The family is clean against every
  drop, revival and boss-drop stream under the shipped `BossFloorMax`, and
  shares a number with a revival roll only as the Phase 4 pass accepts between
  the floors' own streams: draw 34,430 meets floor index 8's revival roll for
  spawn 50, cycle 2 at the shipped depth, and under a tuning as deep as
  `BossFloorMax`'s clamp allows (99) draw 2801 meets floor index 58's for spawn
  7, cycle 16 — the one meeting of draws 0–4096 on any floor a tuning reaches.
  Both are pinned by a test. (Superseded by the user's ruling, 2026-09-25:
  shipped provisionally as the user's to rule, open-questions.md setting out the
  alternatives — keep it, key each draw by turn, actor and ordinal, or go back to
  `Random.Shared`, so that a reload is a reroll. The user kept it.)
- **A save is taken only at a safe point, and a quit settles on the fixed logic
  clock.** A safe point is between runs, a visit with no floor open, or the
  player phase with nothing in flight; the enemies' turn, its banner and a raise
  in flight never are, which is what lets a save hold none of the enemy phase's
  working state. The turn system runs on a fixed logic clock — whole steps of
  1/120 s, never a frame's own time — so the enemies' turn plays out the same at
  any frame rate, and a quit during it is played out to the next player turn on
  that same step, the scene's sighting included, before anything is written: the
  save is the turn the player would have met. A settle in which the party is
  wiped ends the run, and the campaign is saved between runs.
- **The save model: one campaign, one slot, saved by itself, resumed where it
  stood — provisional, the user's to rule.** Saves live in the player's data
  folder, never beside the exe (the data files do). The game saves at a run's
  entry, every player turn's start, every floor arrival, a run's end, NEW
  CAMPAIGN and every quit, with no manual save. A launch resumes the campaign
  where it stood: between runs, or in the dungeon at the saved floor and turn,
  the turn's sight, active member and march as they were, `RESUMED` over the
  floor banner. A `campaign.resuming` marker guards a resume, NEW CAMPAIGN asks
  twice, `--new-campaign` sets the save and its backup aside and saves the new
  campaign at once, and the dev overrides play in a scratch slot that never
  touches the campaign. The alternatives are in open-questions.md. (Being
  superseded by the user's ruling, 2026-09-25, as it is built: the save folder
  is the autosave slot now, beside manual slots whose count is `tuning.json`'s,
  and every save carries its time — R2, the folder, above; and the game opens on
  a title screen — CONTINUE, NEW CAMPAIGN, LOAD GAME, QUIT — in place of resuming
  at launch and of NEW CAMPAIGN's N-then-N between runs — R2, the title, above.
  Until saving by hand arrives, the rest of this entry says what the player
  meets.)
- **A save that cannot be played is set aside, never deleted or written over** —
  unreadable, crashed on its own resume, or retired by `--new-campaign` — named
  by its full path with how to try it again, and the backup is tried.
  (Superseded by the user's ruling, 2026-09-25, in what cannot be read: a save
  that contradicts itself, or holds a count past what play can reach, is one too,
  set aside the same way — R3(b), above.)
- **A save that fails is said at once, and a quit that cannot save is said at
  the next launch.** A write that fails — a full disk, a folder with no rights,
  another program holding the file — costs nothing, since the save before it is
  kept, but the player is told straight away: over the floor being played, the
  save's word shown whole ahead of whatever is up, and again on the next screen
  between runs — once, however many autosaves failed, and not at all if a later
  save has gone through by then, since the campaign is on the disk again. No
  screen follows a quit, so a quit that cannot save — its write fails, or the
  enemies' turn cannot be played out — leaves `campaign.unsaved` beside the save,
  and the next launch says, under the resumed line, that what resumes is from
  before that quit. A marker that cannot be written either leaves the log to say
  so. (Superseded by the user's ruling, 2026-09-25, in who says it and how: the
  title, when it opens and before anything is loaded, that what the autosave
  holds is from before the last time play was left — R2, the title, above.)
- **The save's word is shown whole, on the screen between runs and over a floor
  a saved run resumes on; the data files' notices keep their three-row cap after
  it.** The cap is for what a broken file can make without bound; what became of
  the campaign is bounded by construction — a handful of notices, and a launch
  sets at most two files aside — and a notice cut off there would hide the one
  thing the player has to act on. That holds on a resumed floor too: a backup
  holding a run resumes in the dungeon, and the file set aside beside it is
  named there. The floor's banner moves down to make room, so no row is drawn
  over the readouts along the top, and the rows stay up longer the more there
  are. A dungeon a dev override boots into and the error screen cap as before,
  the log keeping every word. **The whole rows have a ceiling of their own:
  fourteen.** What construction bounds is how many notices there are, not how
  long a path is, and fourteen rows — with the data files' three and the row
  that counts the rest under them — still leave the dungeon list on a 720-pixel
  window, and a resumed floor's banner in its upper two thirds. The longest case
  in the player's own save folder, a new campaign after the save and its backup
  were both set aside, takes about ten, so only a save folder with an unusually
  long path (`DISTANCERPG_SAVE_DIR` can name any) reaches the ceiling; past it
  the rows are cut and counted like any others. The alternative not taken: a
  shorter notice naming the file and the save folder, the full path in the log
  alone. (Superseded by the user's ruling, 2026-09-25, in how many files: a load
  of a manual slot can set three aside, the title shows the save's word whole
  too, and the rarest such word reaches the ceiling in an ordinary save folder —
  R2, the title, above.)
- **Wear, `WearCapacity` and carried consumables are not in save version 1 —
  bends the phase doc** (§5.1 lists them). Needs no ruling: the state does not
  exist until Phases 6a and 6b, and the save's version and migration steps are
  how the doc asks the format to grow. **The hand-off**: 6a adds `wear` and
  `wearCapacity` to a weapon's row as a version-2 migration step, with the four
  Enchanter keys and their readers; 6b adds a non-weapon inventory slot as a
  migration step, with the four Consumables keys. Every row is a named-field
  object, so each is an addition and a step, never a reshaping. And PHASE6-223,
  the campaign as the save's outer document, shipped in Phase 5.4 (above).
  (Superseded by the user's ruling, 2026-09-25, in the Enchanter's keys: six of
  them, what a deposit costs, are in `tuning.json` already, read by nothing and
  outside the save's hash (R4's keys, above). 6a flips each to played as it
  reads it, adds `WearPerHit` and `GraftBaseChance`, the two still to come, with
  their readers, and saves beside `wear` and `wearCapacity` the capacity's
  progress toward its next point, which carries from one deposit to the next
  (§6.7).)
