# Open questions

- **Ranged and wand enemies.** `EnemyAi.PlanMove` only knows how to close.
  Those classes need kiting — hold range, back off when approached — plus the
  placement scorer for wands (§1.4). Until both exist, `EnemyPlacer` should
  roll only the six close-range classes, and the friendly-fire constant stays
  off. Half-damage friendly fire is what lets the first version of that scorer
  be merely adequate rather than finished.
- **STR has four classes to DEX's three.** STR covers axe, spear, throwing and
  sword; DEX covers dagger, bow and sword. Both have close and far answers, so
  nothing is *broken*, but STR simply has more room to move. Fixing it means
  either a ninth class on DEX, or moving throwing to DEX (thrown knives are
  plausibly dexterous, though it costs the STR-throws-heavy-things read), or
  accepting that STR is the martial-breadth stat and DEX the precision one.
- **What are consumables, actually?** §3.4 fixes that they cost movement and
  occupy slots; their contents, where they come from (dungeon drops or the
  hub), and whether they are craftable at the enchanter are all open.
- **Is half the right fraction?** Half damage is the forgiveness knob that
  makes a simple scorer shippable; once the scorer is good, full friendly fire
  may be the better game. Worth revisiting rather than treating as final. Note
  the 1.5× scoring weight is a separate dial and does not have to move with it.
- **Should the ally weight vary per target?** A flat 1.5× ignores that clipping
  a nearly-dead ally, or a healer, costs more than clipping a fresh dummy.
  Weighting by remaining HP or by role would be more accurate — and more
  expensive, and harder to predict when baiting. Flat first.
- **Should `CritWeaken` and `CritSunder` be an exclusion group?** §1.1 currently
  says no, on the grounds that they neither contradict nor cost enemy-turn
  economy. But they are the dagger's Control and Support variants, which makes
  them structurally identical to the sword's `Riposte`/`BlockWeaken` — one
  trigger forked by who collects — and that pair *did* get a group. Either the
  fork argument applies to both or to neither, and the current answer picks
  differently for two things of the same shape. The counter-argument is that a
  crit is a rare event a player built toward, where a block is routine, so
  doubling up on a crit is a payoff rather than an economy exploit. Needs
  deciding before Phase 1 codes grafting, since it changes what a deep dagger
  can become.
- **`Charges` may be actively harmful in the wrong place, not merely useless.**
  It is a *cap* on attacks per turn (§1.2), and a weapon without it has no cap
  at all beyond the movement budget. So a `Charges ×1` grafted onto a bow would
  hold it to 2 shots where 30-cost attacks already allowed 5 — a stack that
  makes the weapon worse. Every other modifier is at worst inert. Either
  `Charges` needs a throwing-only restriction (which `WeaponKind` cannot express,
  since throwing and ranged are both `Ranged`), or it needs redefining so it
  only ever raises a cap that already exists. Worth checking the whole table for
  others of this shape before Phase 1 codes grafting.
- **Does a shallow boss pay the same as a deep one?** The boss floor rolls 5–10
  on entry and a win ticks service once either way, so a floor-5 dungeon is
  strictly cheaper than a floor-10 one for the same reward. Not disclosing the
  roll stops players re-entering to scum for a shallow dungeon — you cannot
  tell without descending — but if a hint ever surfaces the depth, ticks should
  scale with boss depth instead.
- **The golem's statline.** Themed drops are settled (§4.3) but the fight is
  not: HP, movement budget, whether it rolls a weapon like a dummy does, and
  how many `Block` stacks it carries innately. A slow, heavily armoured
  construct is the obvious shape, but slow enemies are trivially kited once
  ranged weapons exist (§1.2) — worth checking the tutorial boss does not
  become a joke in Phase 1. `Block ×3` also now means a tutorial dagger crit
  lands for 45 where its normal swing lands for 6; that is the lesson working,
  but a first-time player needs the floating combat text to *say* the armour was
  ignored, or it reads as a bug.
- **What does the hub look like?** Dungeon selection now definitely exists
  (§4.3): a list of themed dungeons, the tutorial present until beaten. Nothing
  else about the hub is specified — how dungeons are discovered, whether the
  list grows, whether themes repeat. Phase 6 scope.
- **Which debuff staff does D carry?** Blight (Poison) is the more legible
  demonstration; Mire (movement tax) is the more on-theme one for this game.
- **Can you re-enter after killing the boss?** Leaving resets the dungeon
  (§4.1), which re-rolls the boss floor and revives everything. So a cleared
  dungeon cannot be returned to — clearing it is worth doing only for what you
  can carry out in that visit.
- **Is 24 the right carry limit?** The structure is settled — unified with the
  loadout, no haul bag — but the number is a guess. Too high and the extraction
  stops forcing choices; too low and a deep farm is mostly wasted. Needs play.
- **Does wear cap?** If it accumulates without limit, a long-serving weapon
  eventually has enough for any enchantment forever and wear stops being a
  gate. A ceiling — or wear being fully consumed per attachment — needs
  deciding.
- **`Splitting` versus `Block` is coupled again, but only at the unique tier.**
  Both ceiling at `×8` — 24 ignored against 24 absorbed — so Shieldbreaker
  exactly answers The Bulwark, which is a satisfying pairing of two uniques
  derived from the same rule. But the ordinary *Reaver* is forged `Splitting ×1`
  and ceilings at `×6`, 18 against 24, so the anti-armour axe the tutorial
  teaches you to want (§4.3) only fully arrives as a unique. That is probably
  right, since crits bypass Block regardless (§1.6) — but it is worth deciding
  whether the Reaver should be forged `Splitting ×2` so the lesson pays off
  before the endgame.
- **How rare is a graft, and can a weapon collect them without limit?** §6.4
  makes grafts safe in *depth* (capped at 5) but says nothing about *breadth*.
  A weapon serviced twenty times could plausibly end up carrying eight modifiers
  at ×5, which is not overpowered but is unreadable, and it would blur classes
  by accumulation rather than by depth. A cap on distinct modifier types per
  weapon — or simply a low enough graft rate — needs deciding before Phase 6
  codes it.
- **The fully worked crit dagger is the game's end state and nobody has played
  it.** Deriving uniques from variants (§1.5) split this in two, which helps: a
  Widowmaker ceilings at `CritWindow ×8` (45%) but only `CritMultiplier ×6`
  (×8), for 120 from a 15-damage dagger, ignoring armour, on nearly half of
  roughly five swings a turn. The ×10 multiplier now needs a *different* unique
  — a Kris or Stiletto that raised `CritMultiplier` — which ceilings at only
  `CritWindow ×6`, 35%. **No single weapon is forged deep in both dials any
  more**, and that is most of the answer to "is this too strong." What remains
  is whether ~120 on 45% is fine at the end of a campaign. Probably yes, given
  the grind, but it wants watching rather than assuming.
- **What is the revival step, actually?** `+5` in §3.2 is an explicit
  placeholder. Against a party dealing 10–18 a swing, `+5` HP is small and `+5`
  damage is large, and a flat number means far more on a dagger dummy (9 → 14)
  than an axe one (18 → 23). Candidates: split the step (`+3` damage / `+8` HP),
  weight the roll toward health, or scale the step proportionally to the
  dummy's own weapon rather than flat. The three-way roll shape is settled; the
  numbers are not.
- **Where should the logistic's hot zone sit?** §3.2 puts `Midpoint` at
  `DefeatCount 20` with a 50% ceiling, so the odds go interesting somewhere
  around 12–25. Whether that is reachable depends entirely on how survivable a
  twenty-times-revived dummy is, which is unknown until revival scaling has
  numbers. If `DefeatCount 20` turns out to be suicide, the midpoint has to come
  down to meet it; if it turns out trivial, both the midpoint and the revival
  step are too soft.
- **Is a flat 10-stack farm allowance right, or should the per-modifier cap do
  all the work?** §3.2 caps a farm at 10 stacks total. Removing that number
  entirely would let §1.1's per-modifier 5 be the only bound, so a
  three-modifier Kris could absorb 15 and a two-modifier Fang 10 — broader
  weapons rewarded for breadth, with no extra rule. The argument for keeping 10
  is that it leaves a three-modifier weapon something for the enchanter to do;
  the argument against is that it is exactly the kind of bespoke ceiling §1.1
  spent a section rejecting.
- **Does `CritMultiplier ×1` on the remaining baselines make crits too swingy?**
  Dagger and throwing crit for `×3` instead of `×2`, and crits ignore
  `Block` entirely (§1.6), and enemy crits do the same to the party. It is what
  makes the natural-20 answer to armour real, but it raises tail variance by 50%
  for those classes, which no other decision here has done so bluntly. Narrowing
  the baseline from four classes to two has taken some heat out of this, at the
  price of leaving the other six reliant on a `×2` crit for the same job.
- **`Longshot` and `CritWindow` compound, but the dungeon is the counterweight.**
  A bow's crit multiplies the *distance-inflated* damage, so a Longbow at ten
  tiles deals 19 and crits for 38 with `Block` ignored, and a farm can deepen
  both dials at once (§3.2). What keeps it honest is the map: this is a dungeon
  of rooms, corridors and blind corners, and most contact happens within a few
  tiles where `Longshot` pays nothing at all. The archer gets the long hall and
  the open room and is a 5-damage weapon everywhere else — which is the trade
  §1.2 already argues for. Still worth watching once the kiting AI exists, since
  that is what decides whether an archer can *choose* the long shot or merely
  take the ones the map hands them.
- **Does a two-weapon loadout dodge the one-reaction rule?** §1.1 bounds
  reactions at the *weapon*, which is clean while a character is holding one.
  But the inventory holds six slots and swapping costs 20 movement in combat
  (§1.2), so nothing currently stops a character ending their turn on a spear
  after spending it on an axe. Whether a held reaction survives a swap — or
  whether only the equipped weapon's reaction is ever live — needs a ruling
  before Phase 1 codes the enemy phase.
- **Does the sword's `Push` fight its own class?** A shield-bearer is the party's
  front line, and shoving a target back can pull it *out* of the axe's cleave as
  easily as into a spear's threat zone (§1.2). `Push` on a Halberd is chosen —
  you swing it when you want displacement — but on the sword it fires on every
  hit, whether or not the party wanted the target moved. Worth checking whether
  it needs to be opt-in, which no other modifier is.
- **Should the 50% stack roll be flat?** A flat rate makes early farming feel
  reliable and the allowance land around `DefeatCount 20`, matching the unique
  curve's midpoint. A decaying rate would stretch the stack curve to match the
  logistic's shape rather than just its endpoint, at the cost of a second
  formula in the same mechanic.
- **Does anything scale a boss the same way?** The boss cannot revive, so it
  never accumulates — which is right, but it means a heavily farmed floor can
  contain dummies more dangerous than the boss guarding it. Either the boss
  scales with the deepest `DefeatCount` on its floor, or that inversion is
  accepted as the price of farming.
- **How deep does an accumulated `Sundered` actually get in play?** The level
  cap is gone (§1.6) and re-application adds levels, bounded only by one-per-turn
  decay and by the target dying. Against a dummy that is self-limiting; against
  a boss with a large HP pool and a party critting every turn it could ramp a
  long way. The per-level `+1` is the dial if it does, but the shape needs
  playing before anyone reaches for it.
- **`Charges` is priced against three numbers that could all move.** `+1` throw
  per stack works because 9 throws × 15 lands on 135 against a 160 budget. The
  throw cost, the budget, and the deepest forged `Charges` spread are all
  tuning targets, and this per-stack value has to be re-derived if any of them
  changes rather than left at `+1`. Worth a test that asserts the deepest
  reachable spread still fits the budget, so the invariant fails loudly.
- **Does `Light ×6` interact badly with `Charges`?** A thrower forged Light and
  worked to `×6` throws at cost 6, so 9 throws costs 54 of 160. That is the
  intended shape — `Charges` is the binding cap, exactly as §1.2 argues — but it
  is also the cheapest damage in the game by some margin, and the combination
  has never been priced together.
- **Two enchanter slots, or a strict last-in-only queue?** Phase 6 specs the
  two-newest rule as the legible middle ground, but a hard slot count is
  simpler and a pure last-in rule is harsher.
- **Should a natural 1 have a rider too?** `RollOutcome.Weak` already exists
  and only halves damage. The symmetric move is a fumble applying **Weakened**
  to *yourself* — but crits and fumbles both firing riders may be too much
  status churn per turn. Deliberately not specced.

- **Does a caster still want to crit at all?** Losing `CritMultiplier ×1`
  leaves staves and wands crit-indifferent — a natural 20 doubles a wand's
  damage and does very little for a staff's applied effect (§1.6). That may be
  correct for the classes whose whole pitch is a resource loop rather than a
  damage roll, or it may leave the d20 feeling inert on two of eight classes.
  The cheap fix if it does is a caster-specific crit meaning — a free cast, a
  refunded mana cost — rather than putting the multiplier back.
- **Is one enchantment on every caster drop too generous?** It is a guaranteed
  effect on a guaranteed drop, where every other class rolls rarely for the same
  thing. The arguments that it is fine are that it is capped at tier 1, that it
  costs an extra run of service time forever after (§6.2), and that a low-INT
  character cannot afford its lock at all (§3.1). The argument that it is not is
  that a wizard's first staff is strictly better than a fighter's first sword in
  a way no other class comparison is.
- **Does the uncapped tier self-limit fast enough?** The brake is that a higher
  tier locks more of the pool it levels from (§3.3), which is elegant but has
  never been simulated. If max mana grows faster than locks do — and §2.2 grows
  it from the *same* mana spending that feeds the tier — the loop may accelerate
  rather than converge. The dial if it does is superlinear lock scaling, not a
  cap.
- **Mana spent now pays twice.** It grows max mana (§2.2) *and* levels the
  enchantment that moved it. That is a deliberate double credit and it makes
  casting the most rewarding thing an INT character can do, but it is the only
  place in the game one action feeds two ladders. Worth checking it does not
  make the caster the obvious pick for every party slot.
- **Does choosing from what you have seen make late innate rolls inert?** By the
  time the catalogue is full, a rolled innate is only ever a free tier-1 copy of
  something you could already ask for. That is fine — it is still free, and it
  is still a body — but the *discovery* half of drops has a natural end, and
  nothing currently replaces it.
- **Should unattuned floors exist?** Attunement sits on the themed floor (§1.4),
  so a floor with no theme has no chart and every element is identical there. If
  most floors are unthemed, the type chart is a boss-fight mechanic wearing a
  system's clothes; if none are, every dungeon has a right answer and carrying
  the wrong wand is punishment rather than a trade.
- **Four types may be too few to make a rotation.** Two opposed pairs means any
  wand is correct against exactly one theme and wrong against exactly one, so a
  two-wand stable covers everything. That is either pleasingly tight or it is
  the whole mechanic solved by owning two items — the fix is more types, which
  costs the one-line readability the four were chosen for.
- **Does `Block` or the type chart apply first?** §3.5 flags it; the numbers
  differ and the answer is a decision, not a derivation. Crits already bypass
  `Block` entirely (§1.6), so the interesting case is a resisted crit.
- **Where exactly does the mana-bound / movement-bound crossover sit?** The
  whole `Cast` trade (§1.3) depends on there being a crossover at all — if a
  caster is movement-bound from `Cast ×1`, efficiency is pure downside on the
  levelling axis, and if they never stop being mana-bound even at `×8`, it is
  pure upside. The interesting shape needs the crossover somewhere in the middle
  of the stack range, which is a joint constraint on cast cost (40), mana cost
  (15–25), the movement budget (160) and the regen rate. None of those four were
  chosen with this in mind, so at least one probably has to move.
- **Does `Cast` on a wand mean something different in practice?** Wand
  proficiency XP is total damage across the shape (§2.2), so a cheap wand levels
  the *weapon* fast while levelling its *element* slowly. A staff has no such
  second ladder — its proficiency comes from effect level applied, which tier
  drives, so efficiency slows both of a staff's ladders and only one of a wand's.
  That may make `Cast` straightforwardly better on wands than on staves.
- **Is there anything left for a caster Efficiency variant?** `Light` discounts
  movement and `Cast` now discounts mana, so a caster already has an efficiency
  signature. §1.2 exempts casters from the four-role frame, which sidesteps it —
  but `Cast ×2` as a Purity variant and `Light ×1` as Efficiency are now two
  discounts on one weapon, and it is worth checking they do not stack into a
  caster that acts nearly for free.
