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
- **Overwatch and enemy-turn reactions.** Overwatch fires during the enemy
  phase, as braces already do. Whether a character can hold *both* an overwatch
  shot and a brace in the same turn needs a ruling before Phase 1 codes it.
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
- **Does a universal `CritMultiplier ×1` make crits too swingy game-wide?**
  Every weapon now crits for `×3` instead of `×2`, and crits ignore `Block`
  entirely (§1.6) — so an axe's natural 20 goes from 36 to 54 through any
  armour, and enemy crits do the same to the party. The change is what makes the
  universal answer to armour real, but it raises the variance of every fight in
  the game by 50% on the tail, which no other decision here has done so bluntly.
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

