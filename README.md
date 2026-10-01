# DistanceRPG

A turn-based party RPG where movement is the resource: every step, attack, and
saved manoeuvre spends the same per-turn distance budget. Ported from the
original [Phaser prototype](https://github.com/Sheckerol/Game) onto the
custom [GameEngine](https://github.com/Sheckerol/GameEngine), reinterpreted as
a 3D dungeon with an angled top-down camera.

Each run generates a fresh dungeon (the seed is logged as the run is entered and
also determines the enemy layout, so a run can be reproduced — see the dev
overrides under [Saves](#saves)). Every room but the party's starting one holds
0–4 training dummies; a defeated dummy gets back up some turns later, stronger,
and sooner each time it falls, until the dungeon's boss is killed. Dummies stay
passive until spotted and doze back off two turns after losing sight of the
party.

## Layout requirement

The engine is consumed as a **sibling checkout** — clone both repos next to
each other:

```
<parent>/
  GameEngine/    ← https://github.com/Sheckerol/GameEngine
  DistanceRPG/   ← this repo
```

## Build & run

```bash
dotnet build
dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj
dotnet test GameEngine.DistanceRPG.Tests/GameEngine.DistanceRPG.Tests.csproj
```

## Controls

The game opens on the title screen: `DISTANCE RPG`, then `CONTINUE`,
`NEW CAMPAIGN`, `LOAD GAME` and `QUIT`, in that order. CONTINUE names the most
recent save and resumes it where it was left: on the screen between runs, with
`CAMPAIGN RESUMED FROM SLOT 2` (or `FROM THE OLD AUTOSAVE`) under the title, or
back in the dungeon mid-run, with `RESUMED` over the floor banner (see
[Saves](#saves)); with no save yet it reads `CONTINUE - NO SAVE YET`, dimmed, and
is passed over. The screen between runs is two pages, Tab going from one to the
other: DUNGEONS, the dungeon list, with the last run's summary above it once a run
has ended — until the tutorial's golem is beaten it lists the tutorial alone — and
WEAPONS, every member's hand and bag, the stable and the enchanter's shop, where
weapons are moved between them and deposited at the enchanter (below). In play —
in a dungeon or between runs — Esc opens the menu, where the campaign is saved by
hand into its own slot, the one it saves into all the time, and play is left for
the title.

| Title | Action |
| --- | --- |
| W/S / arrows | Choose an option (CONTINUE is passed over while there is nothing to continue) |
| Enter | CONTINUE: load the most recent save. NEW CAMPAIGN: the slot list to begin a campaign in — the starting party (`Logic/Content/StartingParty.cs`), nothing played — which it saves into from then on (below). LOAD GAME: the slot list to load from. QUIT: close the game |
| Esc | Quit (on a slot list, back to the options; on a question, no) |

| Title: LOAD GAME's slot list | Action |
| --- | --- |
| W/S / arrows | Choose a slot (an empty one is skipped) |
| 1–9 | Load that slot (an empty one says `SLOT 2 IS EMPTY`). The campaign it brings back saves into that slot from then on, as the keys line says: `PLAY SAVES INTO THE SLOT YOU LOAD` |
| 0 | Load the `OLD AUTOSAVE`, while the list shows it (the keys line then reads `0-9`); otherwise nothing |
| Enter | Load the chosen slot, the same way (with every slot empty, it says `EVERY SLOT IS EMPTY`) |
| Esc | Back to the options |

| Title: NEW CAMPAIGN's slot list (`NEW CAMPAIGN - CHOOSE A SLOT`) | Action |
| --- | --- |
| W/S / arrows | Choose a slot, `SLOT 1` to `SLOT 3` (it starts on the lowest empty one) |
| 1–9 | Begin the new campaign in that slot, which it saves into from then on: an empty one at once, one that holds anything after asking (a slot above the count in `tuning.json` is never offered, and says `SLOT 5 IS BEYOND THE 3 SLOTS`; 0, the old autosave, does nothing) |
| Enter | Begin in the chosen slot, the same way |
| Esc | Back to the options (on a question, no) |

| Between runs: DUNGEONS | Action |
| --- | --- |
| W/S / arrows | Choose a dungeon (a locked one shows its countdown and is skipped) |
| Enter | Descend into the chosen dungeon |
| Tab | The WEAPONS page |
| Esc | Open the menu, over the hub (below) |

| Between runs: WEAPONS | Action |
| --- | --- |
| W/S / arrows | Choose a weapon, a room — a member's empty hand, the room left in its bag, the stable's end — or a weapon in the enchanter's shop (a heading or a member's name is skipped, and so is a potion in a bag, listed grey under its member as `  BAG   HEALTH POTION Q2`: a potion is never moved here, and its slot is no room — PHASE 6b DECISION 1). The window scrolls with the choice, and the chosen weapon is said whole on the right: its readouts, its wear, and its capacity and how far the wear it has cashed has come toward the next point |
| Enter | Take the chosen weapon up. Nothing moves yet: the keys line reads `CARRYING THE ...` |
| D | Deposit the chosen weapon at the enchanter: the deposit panel (below). Before the tutorial is beaten it says `THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN`, over the keys, and opens nothing |
| T | On a weapon in the shop (`WORKING` or `STALLED`), or a transfer's source held there (`HELD`): ask whether to take it back (below) — a transfer's two weapons together |
| Tab | The DUNGEONS page |
| Esc | Open the menu, over the hub |

| Between runs: the deposit panel (D) | Action |
| --- | --- |
| 1 | `1  ENCHANT - A CHOSEN ENCHANTMENT - 3 IN 100 TO IMPROVE`: the offer list. Dimmed with its reason — `NO FREE SLOT - SLOT 2 OPENS AT CAPACITY 35 - IT IS 31`, `ENCHANTMENT 1 NEEDS A DEPOSIT OF 30 WEAR - IT CARRIES 0`, `ALL 5 SLOTS ARE FILLED`, `[A] WOULD CARRY NO WEAPON - TAKE ANOTHER FROM THE STABLE FIRST` — the key says the reason over the keys, and picks nothing |
| 2 | `2  REFINE - NOTHING ATTACHED - 6.6 IN 100 TO IMPROVE`: the confirm. An unworn weapon's is not refused: it says `NOTHING TO REPAIR - IT WILL COME BACK AS IT WENT` |
| 3 | `3  MOVE AN ENCHANTMENT ONTO IT - ONE TIER LOST - 3 IN 100 TO IMPROVE`: the source list, every soul on the party's other weapons and then the weapon's own. Dimmed with its reason as ENCHANT is, or `NOTHING TO MOVE - NO ENCHANTMENT THE PARTY OWNS CAN GO ON IT` |
| W/S / arrows | On the offer list, choose an enchantment the campaign has met: `FLAMING - LOCK 20`, `REGENERATION - LOCK 15 - NEVER FIRES ON A SWORD`. On the source list, choose a soul, the weapon it is on and whose hand or bag that is, or the stable: `VAMPIRIC T3 ON THE NAMELESS KNIFE IN THE BAG OF [C] - ARRIVES AT T2 - 2 RUNS`, or on a sword carrying Flaming and Vampiric `FLAMING - ITS OWN - MOVES TO THE END - ARRIVES AT T1 - 2 RUNS`; a soul that does nothing yet moves all the same, noted `DOES NOTHING YET`. A refused one is listed dimmed with its reason (`COLD - CANNOT SIT WITH FLAMING`, `FLAMING ON THE NAMELESS NOVA IN THE BAG OF [C] - BURNING NEEDS FLAMING AHEAD OF IT`, `POISON T3 ON THE ROTWOOD IN THE STABLE - THE ROTWOOD WOULD CAST NOTHING`), and chosen a line at a time like the rest, so every line of a long list comes into view. Each list opens on the first that can be chosen and scrolls with the choice |
| Enter | On either list, choose it: the confirm — or, on a refused one, say why over the keys and choose nothing. On the confirm — `DEPOSIT THE ARMING SWORD TO ENCHANT FLAMING - 2 RUNS - ITS 36 WEAR IS SPENT`, `DEPOSIT THE ARMING SWORD TO TAKE VAMPIRIC FROM THE NAMELESS KNIFE - 2 RUNS - ITS 36 WEAR IS SPENT` — deposit it: the weapon goes into the shop, a transfer's source with it, a hand either empties takes up the first weapon in its bag, and the campaign is saved at once |
| Esc | Back a step: from the confirm to the list it came from (or the services), from either list to the services, from the services out of the panel. Nothing is deposited until the confirm's Enter |
| Tab, D, T | Nothing |

| Between runs: the take-back ask (T) | Action |
| --- | --- |
| Enter | `TAKE BACK THE ARMING SWORD - ITS SERVICE IS LOST - ITS 36 WEAR COMES BACK`: the weapon goes to the end of the stable, its wear and its capacity as they were before it went in, and the campaign is saved at once. A transfer's — `TAKE BACK THE ARMING SWORD AND THE NAMELESS KNIFE - THE MOVE IS LOST - ITS 36 WEAR COMES BACK` — returns both, the source's souls and wear as they went in |
| Esc | No: it stays in the shop |
| Tab, D, T | Nothing |

| Between runs: WEAPONS, carrying a weapon | Action |
| --- | --- |
| W/S / arrows | Choose where it goes: a weapon, a room, or the stable's end |
| Enter | Put it down there: onto a weapon it swaps with it — onto a hand, that is equipping it — and into a room where none lies, the stable closing up behind a weapon taken from it; the campaign is saved at once. On its own place, it is put back. A member's last weapon cannot be put away: `EACH MEMBER KEEPS A WEAPON - [A] WOULD CARRY NONE`, over the keys, and it stays taken up |
| Tab | Nothing |
| Esc | Put it back: nothing has moved |

| The menu (Esc, in a dungeon or between runs) | Action |
| --- | --- |
| 1 | `1  CLOSE`: close the menu |
| 2 | `2  SAVE GAME`: save the campaign into its own slot at once, asking nothing (below) — on your own turn; during the enemies' turn it is dimmed and says `SAVE ON YOUR OWN TURN` |
| 3 | `3  QUIT TO TITLE`: the campaign's slot — the one it was loaded from or begun in — is written (the enemies' turn played out first, if it is theirs), and the title opens |
| Esc | Close the menu |

The menu says under its options which slot is the campaign's: `PROGRESS SAVES
TO SLOT 2 AT EVERY TURN`. The one question left — NEW CAMPAIGN over a slot in
use, on the title — takes the next key: **Enter alone says yes**, as the
question says, and every other key, N and Y and the slot's own digit included,
says no. Loading a slot asks nothing, and nor does saving. While the menu is up
every key is its: nothing behind it moves, descends, ends the turn or opens the
bag.

| In a dungeon | Action |
| --- | --- |
| WASD / arrows | Move the active character |
| 1–4 / Tab / click | Switch party member (click their box, the party list, or the member — unless a support staff in hand casts on them instead) |
| Left-click enemy | Attack with the equipped weapon — or cast with it: a debuff staff lands its effect on the enemy, a wand aims its shape at it |
| Left-click ally / floor | A support staff (Renewal, Warding) casts on the ally clicked; a Blast, Cone or Beam wand casts at the floor point clicked |
| N | A Nova wand casts around the active character |
| O | Hold fire: a ranged weapon with Overwatch spends an attack's movement to hold its shots through the enemies' turn, loosed at whatever walks into reach |
| Space / Enter | End turn (banks half your unspent movement) |
| E | Take the stairs the active character stands on: down, up, or out by the first floor's way out. Every living member must be within 3 tiles; the way out asks first, and E or Enter says yes |
| G | Pick up what the active character stands on: a drop, or a weapon the party left there |
| I / B | Inventory: six slots a member, slot 1 in hand (2–6 swap that bag slot's weapon into the hand). A slot can hold a potion instead of a weapon — `3  HEALTH POTION Q2` in green, `4  MANA POTION Q1` in cyan — which fills it (a pickup finds no room there) and is never taken into the hand |
| X (inventory open) | Leave a weapon: then 2–6 puts that bag slot's weapon on the floor here, free — or, on a potion's slot, pours the potion out, free: `POURED OUT THE HEALTH POTION Q2`, and it is gone, nothing left on the floor (PHASE 6b DECISION 1). The keys say so while the bag holds a potion: `X TO LEAVE OR POUR ONE OUT`, then `LEAVE OR POUR OUT WHICH? 2-6 - X CANCELS`. X again cancels; G takes a weapon back |
| Scroll wheel | Zoom |
| Esc | Close the open menu (at the way out's prompt, that is no), otherwise open/close the menu above — `1  CLOSE`, `2  SAVE GAME`, `3  QUIT TO TITLE` — which says the run also saves itself at every turn, and into which slot. Does nothing on a run's summary |
| Enter (run's summary) | Back to the screen between runs |

**Wear** (Phase 6a): every weapon banks wear as it is used — `WearPerHit`
(1) for every hit it lands: each body a swing, a cleave, a shot or a wand's cast
strikes, each counter and each brace, and each landed cast of a staff, a heal or
a curse alike — up to its capacity, 30 on every new weapon (`StartingPool`). The
weapon line at the top of the screen, and each weapon in the bag, reads its wear
over its capacity after the statline, `WEAR 12/30`, in the line's own colour —
each wrapping between its entries onto lines under it where it would run off the
window, as a weapon carrying several enchantments does, the top line to its first
alone while a menu or a notice is up; a
full weapon reads `WEAR 30/30 FULL` in green — on the yellow line of the weapon
in hand as on a white one — banks nothing more, and plays exactly as it did —
wear is a resource the enchanter spends between runs, never damage, and a full
weapon is not broken. Nothing else wears a weapon: a status ticking, a hit on the
dead, the enemies' own weapons. A wipe keeps the party's wear (what counts as a
hit, a staff's cast among it, and the wipe are PHASE 6a DECISION 1 in
`docs/roadmap/open-questions.md`). The log says when a member's weapon fills:
`[Wear] A's Weakspot Stiletto is full: wear 30 of 30`. The enchanter spends it
and grows a weapon's capacity (below).

**The stable** (Phase 6a): the weapons the party owns and is not carrying,
kept with the campaign in the order they were put there, as many as you like. The
WEAPONS page between runs lists every member — `[A]`, its `HAND` and each weapon
in its `BAG`, with `- ROOM FOR 4 MORE -` while its bag has room — then `THE
STABLE - 3 WEAPONS` and its `- ROOM -`, each weapon with its wear (`WEAR 12/30`,
green once `FULL`). A staff or wand that casts nothing — only a hand-edited save
holds one — says `CASTS NOTHING - ITS FIRST ENCHANTMENT IS ITS CAST` in its
detail. Take a weapon up with Enter and put it down elsewhere with Enter: into
another member's hand (equipping it) or bag, into the stable, or back out of it.
Taking up moves nothing — Esc puts it back — and only the put-down changes the
campaign, which is saved at once, into its own slot (PHASE 6a DECISION 10 in
`docs/roadmap/open-questions.md`); a save that fails says so at once under the
title, until a later save goes through. No move leaves a member carrying no
weapon, and a hand a move empties takes up the first weapon in its bag, as it
would before a dungeon (PHASE 6a DECISION 7) — so a hand's weapon put down in its
own bag ahead of every weapon there would come straight back, and is refused as
changing nothing: `AN EMPTY HAND TAKES THE FIRST WEAPON IN THE BAG - [A] WOULD TAKE
IT STRAIGHT BACK`. The log says each move: `[Hub] Moved Staff of Renewal from A's
bag slot 2 to the stable's end`. On screen a name is drawn without its apostrophe,
which the font lacks: `SKIRMISHERS PIKE`.

**The enchanter** (Phase 6a): the WEAPONS page ends with the enchanter's
shop — `THE ENCHANTER OPENS WHEN THE TUTORIAL IS BEATEN` until the tutorial is
beaten, and then `THE ENCHANTER - WORKS ON THE 2 NEWEST` over each weapon in it,
the newest first: `WORKING  ROTWOOD - 0 OF 2 RUNS` in cyan for the two the next
run that counts works (`EnchanterWorksOn`), `STALLED  RIPOSTE BLADE - 0 OF 1 RUN`
in yellow for the rest — or `THE ENCHANTER - NOTHING IN THE SHOP`; chosen, a row
keeps its colour, marked `> <`. A weapon in the
shop is chosen to be read, never taken up; its detail says what its deposit bought
and its runs (`ENCHANT VAMPIRIC - 1 OF 2 RUNS`, `REFINE - 0 OF 1 RUN`), and when it
comes back (`WORKING - BACK AFTER 1 MORE RUN THAT COUNTS`) or what it waits on
(`STALLED - 2 NEWER DEPOSITS ARE AHEAD OF IT`). Every other weapon's detail says
its slots and what its next enchantment costs — `ENCHANTMENTS 1 - SLOTS OPEN 1 OF 5
- SLOT 2 OPENS AT CAPACITY 35`, `THE NEXT NEEDS A DEPOSIT OF 35 WEAR - 2 RUNS IN THE
SHOP` — as the user ruled (R4): slot n opens at capacity 30 + 5(n − 1), what a weapon
arrives with filling its slots from the first, and attaching enchantment n needs a
free slot and a deposit of 30 + 5(n − 1) wear. A deposit cashes the weapon's whole
pool — its capacity growing on it, a point a full bar, whatever it bought — and puts
it in the shop for its enchantment count plus one runs that count (PHASE 6a DECISION
6). D on a weapon deposits it: the panel says the weapon's wear, its enchantments,
the slots its capacity has opened and its runs in the shop — `WEAR 36 OF 36 -
ENCHANTMENTS 1 OF 5 - SLOTS OPEN 2 - 2 RUNS IN THE SHOP` — and offers ENCHANT,
REFINE and MOVE (moving an enchantment, below), each with the odds its return
improves the weapon anyway (below), a refused one dimmed with the enchanter's
reason, and an unworn weapon's refinement noted as coming back as it went; then
the confirm, which names the wear it spends. What an Enchant may buy is any
enchantment the campaign has met, less what `restricted.json` never copies (the unique souls) and what does nothing yet
(`echoing`): one the relations refuse on the weapon (`COLD - CANNOT SIT WITH
FLAMING`, `BURNING - NEEDS FLAMING AHEAD OF IT`), a status onto a wand
(`REGENERATION - A WAND CASTS NO STATUS`) or a status a staff would cast on the
wrong side (`MIRE - THIS STAFF WOULD CAST IT ON AN ALLY`) is refused, and one that
never fires on the weapon attaches all the same, noted `NEVER FIRES ON A SWORD` — on
a staff, anything that rides a strike or a kill, since a staff never strikes (PHASE
6a DECISION 12); with nothing left to choose the Enchant says `NOTHING TO OFFER - NO
ENCHANTMENT MET YET CAN GO ON IT`, and refining is left. ENCHANT lists the offer, a
refused one dimmed with its reason and never hidden. A run that counts works the two
newest a run on, and a service whose last run it works comes back to the end of the
stable, an Enchant with its enchantment attached last at tier 1 (PHASE 6a DECISION
3); a run that does not count works nothing. A service coming back may improve the
weapon anyway: enchanting at `GraftBaseChance` in 100 (3), flat, and refining at
that times 1 + the wear cashed ÷ `StartingPool` — 6 in 100 on a fresh
full bar, 7 at capacity 40, 9 at capacity 60 — and a deposit that cashed no wear
rolls nothing. A roll that lands deepens a modifier the weapon carries by a stack,
three times likelier on its class's signature, or, a quarter of the time, grafts
one it never carried, which caps at 5; each falls through to the other when it has
nothing to take, and the relations bind both, so `Charges` is never grafted (PHASE
6a DECISION 5). The roll is keyed to the run's map seed and the deposit's ordinal,
both saved, so a reload never rerolls it. The run's summary says so after whether
the run counted — `WAND OF THE CONE IS BACK FROM THE ENCHANTER - ATTACHED VAMPIRIC -
NO CHANGE`, `TOWER GUARD IS BACK FROM THE ENCHANTER - REFINED - DEEPENED BLOCK`,
`… - GRAFTED RIPOSTE` on a green line, `THE ENCHANTER WORKED THE ROTWOOD - 1 OF 2 RUNS`,
`STALLED AT THE ENCHANTER: RIPOSTE BLADE`, or `THE ENCHANTER WORKED NOTHING - THE
RUN DID NOT COUNT` — and past three lines of services on the screen between runs
(two over the floor, a transfer's two lines told together),
`AND 2 MORE AT THE ENCHANTER - TAB WEAPONS`. The log says it too:
`[Enchanter] Run counted: worked Wand of the Cone 2/2, Rotwood 1/2; stalled Riposte
Blade 0/1`, then `[Enchanter] Wand of the Cone finished: attached vampiric;
improvement roll 1073 of 3000 against 90: no change` — the draw, out of 100 ×
`StartingPool`, against the odds (`… : grafted Block`, `… : deepened Push`, or `;
no wear, no roll`). A weapon taken back early (T on its row in the shop, and Enter
on the ask) comes back to the end of the stable with its wear and its capacity as they
were before it went in, its service lost (PHASE 6a DECISION 8). A deposit and a
take-back are each saved at once (PHASE 6a DECISION 10), and logged: `[Enchanter]
Deposited Tower Guard (tower_guard) from B's hand: Enchant vampiric, 30 wear cashed
(capacity 30 -> 31), 1 run, deposit #0; B's hand, emptied, took up Staff of Renewal
from the bag` — the deposit's number, with the run's map seed, is what a finished
service's improvement roll is keyed by — and `[Enchanter] Took back Tower Guard
(tower_guard) to the stable's end: Enchant vampiric forfeited, 0 of 1 run worked
lost, 30 wear returned (capacity 31 -> 30), deposit #0`.

**Moving an enchantment** (Phase 6a): 3 on the deposit panel lists every soul on
the party's other weapons out of the shop — each member's, then the stable's — and
last the weapon's own, each with where it lies, the tier it arrives at and its runs.
A move is the Enchant service with the soul brought from a weapon the party owns
rather than from the offer: the weapon needs the free slot and the deposit of
attaching it as its next, its whole pool is cashed, it takes that attachment's runs
and it has enchanting's odds of improving (PHASE 6a DECISION 13). Any soul may move —
a staff's forged effect, a unique's soul — and it comes back a tier down, the new
tier from no XP; one at tier 1, and every unique soul, moves intact with its XP. It
lands last, acquired: the forged mark stays with the weapon forged with it. Both
weapons wait in the shop as one deposit, one of the two the enchanter works on, the
source's wear left in its pool: the WEAPONS page lists the source `HELD     NAMELESS
KNIFE` under `WORKING  RIPOSTE BLADE - 0 OF 1 RUN`, its detail `HELD - GIVING
VAMPIRIC TO THE RIPOSTE BLADE` (a full one's wear `WEAR 40 OF 40 - FULL - THE MOVE
LEAVES IT UNSPENT`), and T on either takes both back. The summary says
`RIPOSTE BLADE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED ON AT T2 - NO CHANGE` and,
under it, `NAMELESS KNIFE IS BACK FROM THE ENCHANTER - VAMPIRIC MOVED OFF`. Moving a
weapon's own soul to its end is a reorder: it takes the weapon's count in runs
rather than the count plus one, and it is allowed with every slot filled — `ALL 5
SLOTS ARE FILLED - A REORDER NEEDS A DEPOSIT OF 50 WEAR` — but not for the entry
already last, so it needs two souls and takes 2 runs at the least. A weapon forged
whole keeps every forged mark through a reorder; one forged in part loses the moved
soul's, since a forged entry cannot stand behind an acquired one (PHASE 6a DECISION
11). The enchanter refuses a move that would leave a member carrying no weapon,
asked with both weapons gone (`[B] WOULD CARRY NO WEAPON - …`); one the relations
refuse on the weapon, as ENCHANT's offer does; one that would strand an entry behind
it on the weapon it leaves (`BURNING NEEDS FLAMING AHEAD OF IT`); and one that would
leave a staff or a wand casting nothing (`THE ROTWOOD WOULD CAST NOTHING`: a staff's
first enchantment is its cast and must apply a status, a wand's must be an element —
so a staff gives its effect away once it has a second status to cast). A staff whose
first enchantment a move changes must be left casting a status a staff casts — never
Serrated, whose Bleeding rides a strike — and landing each status it keeps on the
side it landed it before (`THE STAFF OF RENEWAL WOULD CAST WARD ON AN ENEMY`; PHASE
6a DECISION 12). A lock the new wielder cannot pay is no refusal: the soul sleeps
there. The log names both: `[Enchanter] Deposited Riposte Blade
(riposte_blade) from B's bag slot 3: Transfer vampiric (T3) from Nameless Knife
(flensing_knife_unique) in B's bag slot 4, 30 wear cashed (capacity 30 -> 31), 1
run, deposit #0`, and `[Enchanter] Riposte Blade finished: vampiric moved on at T2
from Nameless Knife; improvement roll …`.

Outside combat the rest of the party marches behind the active character in a
single-file line, the group paces itself to the member with the least movement
left, and the turn cycles automatically once everyone's movement runs out.
Marching is granted only at the start of a turn, and only if no living enemy
is visible and everyone has regrouped within their max movement of the leader
— so a party split by a fight doesn't charge back into formation the moment
line of sight breaks. Sighting the enemy revokes it for the rest of the turn:
everyone stops following, characters body-block each other, and turns end
manually.

## Saves

There is one campaign in play at a time, and the game saves it on its own —
**into the slot it came from**, as old games keep a campaign to its file: the
slot you loaded it from, by LOAD GAME or CONTINUE, or the one you chose for it
on NEW CAMPAIGN. Every campaign is in a **save slot**, a folder of its own in the
save folder, in one of which every new campaign begins; a campaign an earlier
build autosaved into the save folder itself — the **old autosave** — is moved
into a slot of its own when the game launches (the user's rulings of 2026-09-25
on the save model and on the save slots). The game opens on the title screen,
where CONTINUE resumes the most recent save, LOAD GAME any slot, and NEW
CAMPAIGN starts fresh in the slot you choose; the menu's QUIT TO TITLE comes
back to it, and the game quits from it.

- **Where**: `%LOCALAPPDATA%\DistanceRPG\saves\` on Windows
  (`~/.local/share/DistanceRPG/saves/` on Linux), each campaign in a slot folder
  there — `saves\slot-1\campaign.json` — the player's data folder, never beside
  the exe, which a rebuild or `dotnet clean` replaces. Set
  `DISTANCERPG_SAVE_DIR` to a folder to keep the saves there instead. The log
  names the full path of every save written or read, and says at launch where the
  saves are and how many slots there are (`[Save] Saves in ...: 3 slots`).
- **The slots**: slot *n* is the folder `slot-n` in the save folder
  (`saves\slot-1\` to `saves\slot-3\`), holding its save, `campaign.json`, and
  its own backup, its own markers and its own files set aside (below) — and
  written by the game, and by SAVE GAME, for the campaign loaded from it or begun
  in it. How many there are is `tuning.json`'s `SaveSlots` (below); lowering it
  deletes nothing, and a slot above the count keeps its save, loads like any, and
  goes on saving the campaign played from it; NEW CAMPAIGN never offers it.
  (Whether such a slot may be played in place is the user's to rule: SLOT
  DECISION 1 in `docs/roadmap/open-questions.md`.) A folder named any other way
  — `slot-01`, `slot-10`, `slot-x` — is no slot, and is never read.
- **The old autosave**: an earlier build saved its campaign in the save folder
  itself, as the autosave — `campaign.json`, its backup and the markers and
  set-aside files, by the same names. **Every launch moves it into a slot of its
  own**, before the title and without asking: into the lowest empty slot, `SLOT
  1` to `SLOT 3`, or else the lowest empty one above the count, listed `BEYOND
  THE 3 SLOTS` and played like any; the title says `THE AUTOSAVE MOVED INTO SLOT
  4 - IT SAVES THERE FROM NOW ON`, in green, and from then on it is that slot's
  campaign. Only its save, its backup and its two markers move — the save first,
  each under its own name, byte for byte, so a quit it could not save is said as
  the slot's — and nothing is written over: a slot holding any of those names,
  even a stray marker, is passed over and left as it is. Files set aside stay in
  the save folder, and one renamed back to `campaign.json` there moves at the next
  launch. The move is all or nothing: one that stops at a file is undone — the
  slot folder it made taken back with it, while empty — and the next slot tried;
  one a crash cut off is finished at the next launch, before the
  title (`campaign.moving` in the save folder names its slot meanwhile). While a
  launch could not move it, LOAD GAME lists it first as `OLD AUTOSAVE`, marked
  `NOT YET MOVED INTO A SLOT`, and 0 loads it. With slots 1 to 9 all holding
  something it stays and is played in place, saving back there (`PROGRESS SAVES
  TO THE OLD AUTOSAVE AT EVERY TURN`). When its own files would not move —
  another program holding its save, say — or the move's own marker cannot be
  written there, or cannot be cleared once a try is undone or once a marker a
  crash left cannot be followed, the title says `THE AUTOSAVE COULD NOT MOVE
  INTO A SLOT (ANOTHER PROGRAM HAS IT OPEN) - IT STAYS AS THE OLD AUTOSAVE AND
  TAKES NO SAVES THIS SESSION - LAUNCH THE GAME AGAIN TO MOVE IT`, and it loads
  nothing that session, since none of its saves could go through either. When
  the marker a crash left cannot be read, the title says the same and it loads
  nothing too, the marker kept for a launch that can read it, since a move
  afresh could leave the campaign in two slots. A marker that will not clear
  after a move that went through refuses nothing: the title says the move again
  at each launch, until one clears it. A move neither finished nor undone says
  `THE AUTOSAVE IS HALF MOVED INTO SLOT 4 (...) - NEITHER LOADS THIS SESSION -
  LAUNCH THE GAME AGAIN TO FINISH IT`, and the next launch finishes it. NEW
  CAMPAIGN never begins a campaign in the save folder, and with neither file
  there the old autosave is not listed at all.
  (Where it goes is the user's to rule: SLOT DECISION 4 in
  `docs/roadmap/open-questions.md`.)
- **A save's time**: every save records when it was written — `"savedAt"`, the
  UTC time, the last entry of `campaign.json` (a save from an earlier build has
  none). A save of the campaign a slot already holds writes nothing, so the time
  is when that state was first saved — but for `2  SAVE GAME`, which stamps the
  save with the time you saved it, as old games stamp their file, rewriting the
  time alone and keeping the backup as it was. The time is how the most recent
  save is told — what CONTINUE loads: the latest time wins; a save with no time
  counts as older than any with one; a time more than a day ahead of this
  computer's clock counts as none, so a hand edit or a clock once set wrong
  cannot go on winning; and a tie goes to the lowest slot — the old autosave,
  while it is listed, before slot 1. File dates are never read, so a clock set
  back can make an older save look like the newest. A `savedAt` that is no such
  time is refused like any field that cannot be trusted (below): the save is set
  aside, `CAMPAIGN.JSON COULD NOT BE READ (SAVEDAT: YESTERDAY IS NO TIME)`.
- **When**: all the time, on its own, and always into the campaign's own slot.
  Mid-run the game saves when a run is entered, at the start of every one of your
  turns, and every time the party arrives on a floor; it saves at a run's end
  (the summary with it), when NEW CAMPAIGN on the title or `--new-campaign` begins
  one, after every change made on the screen between runs — a weapon moved
  between the bags and the stable, a deposit at the enchanter, a weapon taken
  back from it (PHASE 6a DECISION 10) — and whenever play is left — the menu's
  `3  QUIT TO TITLE`, or the window's close button. A save is never taken while
  the enemies are moving: a quit during their turn (the menu can be opened then,
  and the close button pressed any time) plays their turn out to your next one
  first — at the game's fixed logic step, so exactly as it would have played —
  and saves that. If the enemies wipe the
  party on the way, the run ends and the campaign is saved between runs with its
  summary.
- **Saving by hand**: `2  SAVE GAME` on the menu Esc opens — in a dungeon on
  your own turn, or between runs, where it always can — saves the campaign at
  once into its own slot, the one it saves into all the time, **asking
  nothing**: there is no list of slots to choose from (the user's ruling on the
  save slots: "instead of choosing a save slot everytime you save"). The menu
  names that slot under its options — `PROGRESS SAVES TO SLOT 2 AT EVERY TURN`
  — stays up, and says under its lines how the save went: `SAVED TO SLOT 2` in
  green (and any file set aside first, and where), or `SLOT 2 COULD NOT BE SAVED
  (...) - WHAT IT HELD IS KEPT - SEE THE LOG` in orange, until the next key.
  During the enemies' turn the option is dimmed and 2 says `SAVE ON YOUR OWN
  TURN`, saving nothing, then or later. Since the game saves the slot at every
  turn already, SAVE GAME mostly finds it holding the campaign as it stands:
  then it rewrites the save's time alone, keeping the backup, so the slot you
  just saved is the one CONTINUE names. A file in the slot that cannot be read —
  put there behind the game's back — is set aside first in the slot's own folder
  as `campaign.unreadable-N.json`, never deleted, a backup that reads kept where
  it is. There is no way in the game to save a second copy of a campaign into
  another slot.
- **Leaving play**: `3  QUIT TO TITLE`, on the menu in a dungeon or between
  runs, writes the campaign's own slot as the window's close does — the enemies'
  turn played out first when it is theirs — and opens the title (no key acts
  while the screen fades to it), where CONTINUE resumes the campaign. The game
  quits from the title (QUIT, or Esc) or by the window's close button from
  anywhere, which saves the campaign in play first. Esc between runs opens the
  menu and never quits.
- **Resuming**: CONTINUE on the title resumes the most recent save where it was,
  and LOAD GAME any slot — the `OLD AUTOSAVE` while there is one, `SLOT 1` to
  `SLOT 3`, then any slot above the count that holds a save — each row saying
  where its campaign stands (the dungeon and floor, or `BETWEEN RUNS`), the runs
  counted and when it was saved, and under it what a load of it would do that the
  row cannot say: `FROM ITS BACKUP`, `ITS LAST LOAD DID NOT FINISH`, `AHEAD OF
  THIS CLOCK`, `NO TIME RECORDED`, `BEYOND THE 3 SLOTS`, and on the old
  autosave `NOT YET MOVED INTO A SLOT` — or, in place of the first two, `TAKES NO
  SAVES THIS SESSION`, for a slot whose load brings nothing back until the game
  is launched again (below); an empty slot says `EMPTY`. Between runs
  it is the screen between runs, with `CAMPAIGN RESUMED FROM SLOT 2` (or `FROM
  THE OLD AUTOSAVE`) under the title and the last run's summary above the list.
  Mid-run it is the dungeon: the same floor, turn and positions, HP, mana,
  statuses and movement left, the corpses lying on the very weapons they lay on,
  the weapons left on the floor, the explored fog and what the party could
  already see this turn, the same member active and the party marching or not as
  it was — with `RESUMED` over the floor banner. The dice fall as they would have
  without the quit: the d20 is the run's own, seeded by the run and saved, so
  reloading never rerolls a turn — on purpose, to protect players from
  themselves: a bad roll is never reloaded away. A campaign saved under other
  numbers (`tuning.json` edited since) plays on under the new ones and says `THE
  NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS ON UNDER THE NEW
  ONES`, once: the next save records the new numbers. (A revival due under the
  new numbers happens as the floor opens.) **Whatever slot
  a campaign was loaded from, every save of it goes into that slot from then
  on** — a slot above the count too — and no other slot is touched, so loading
  asks nothing: LOAD GAME's keys line says it, `PLAY SAVES INTO THE SLOT YOU
  LOAD`. A slot you kept as a checkpoint is played forward from its first load,
  its save before one save deep as the backup, then gone.
- **Version 2** (Phase 6a): every weapon in a save carries its
  wear (`"wear"`) and the wear it has ever cashed at the enchanter
  (`"wearCashed"` — its capacity is worked out afresh from that under the
  `tuning.json` loaded now, never stored), and the save names which version's
  numbers its record of the tuning covers (`"tuningHashVersion"`). The campaign
  holds its stable (`"stable"`, each weapon whole in its order, and `null` while
  it is empty), each weapon in it read as every weapon in a save is; and the
  enchanter's shop (`"enchanter"`, each deposit with its weapon whole, in deposit
  order, `null` while it is empty) and how many deposits it has made
  (`"depositsMade"`), with the last run's word on the shop in its result
  (`"services"`, `null` for an empty shop), a service it brought back saying what
  its improvement roll did (`"improved"` and `"modifier"`, by name — the roll as
  it was drawn is the log's, never the save's). A transfer in the shop holds its
  source whole (`"source"`, `null` for a reorder) and its soul's place there
  (`"sourceIndex"`), and a run's report of one its source (`"sourceId"`) and,
  once it came back, the tier its soul landed at (`"tier"`). A save whose shop
  contradicts itself — a weapon in it worn, a service past its last run, an
  enchantment the campaign never met or one that does nothing yet (in a deposit
  or in a run's report of one), a deposit before the tutorial is
  beaten, a run's report of the shop that says what no run's end says (an
  improvement on a service not back yet, an outcome without its modifier, a
  modifier no weapon stacks, a landed tier on a transfer not back yet), a deposit
  whose take-back would give back a weapon no load reads, a transfer whose soul is
  not where it names, a reorder of the enchantment already last, or a source on
  any other deposit — is refused as one that
  cannot be read, naming where. A save an earlier build wrote —
  version 1, Phase 5's, the slots' included — loads as ever: it is lifted to
  version 2 as it is read, every weapon unworn at 0 of 30 and nothing in the stable
  or the shop, and on to version 3 (below), and saved as version 3 from then on. It says `THE NUMBERS HAVE
  CHANGED` only if a number it was played under moved: the ones Phase 6a began to
  play (the Enchanter group's) are none it was played under (PHASE 6a DECISION 9).
  **Do not open a slot this build has saved with an older checkout.** A Phase 5
  checkout reads version 2 as a newer build's save and sets it aside, kept and
  never deleted (renamed back to `campaign.json`, it loads again here), and loads
  nothing in its place. An earlier Phase 6a step's build does worse: every 6a step
  reads version 2 as its own, so it loads a save a later step wrote, passes over
  what it does not know of (the stable, the enchanter's queue), and its next save —
  from Phase 6a.2 on, the first weapon moved between runs — drops those, and the
  weapons in them: a 6a.1 or 6a.2 build loses every weapon in a 6a.3a save's shop
  (PHASE 6a DECISION 14). A 6a.3a, 6a.4 or 6a.5 build refuses a save with a
  transfer in its shop or its last run's report (`Transfer` is no service it knows)
  and sets it aside, kept and never deleted, as it does any it cannot read; a 6a.1
  or 6a.2 build loses both of the transfer's weapons, as it loses the rest of the
  shop.
  While Phase 6a is being built, one more thing of the steps between: a save an
  earlier step wrote may say `THE NUMBERS HAVE CHANGED` once when a later step
  loads it, though no number moved, since each step adds the numbers it plays to
  version 2's record. A released version 2 never does.
- **The format is version 3** (Phase 6b): a member carries potions in its bag
  (`"potions"`: each with its bag slot, counted from 0 as the inventory is, its
  type, `"Health"` or `"Mana"`, and its quality, the units it was mixed from;
  `null` while it carries none). The inventory stays a list of weapon rows, `null`
  at a potion's slot. A potion row that cannot be — in the hand, past the bag, on
  a slot a weapon fills, two on one slot or listed out of slot order, a type that
  is neither, a quality below 1 or past the sanity bound — is refused as one that
  cannot be read, naming where (`campaign.party[0].potions[0].slot`), and so is a
  potion carried before the alchemist could have mixed one: while every dungeon
  the campaign has cleared is retired — before a dungeon besides the tutorial is
  beaten (§6.6). A version-2 save — Phase 6a's — loads as ever: it is lifted to
  version 3 as it is read by a step that writes nothing (a stray `"potions"` a
  hand gave it is removed, since version 2 knew none), and saved as version 3 from
  then on. **Do not open a Phase 6b slot with an older checkout.** A Phase 6a
  checkout reads version 3 as a newer build's save and sets it aside, kept and
  never deleted, and loads nothing in its place. While Phase 6b is being built,
  every 6b step reads version 3 as its own, as every 6a step read version 2: an
  earlier step's build loads a save a later step wrote, passes over what it does
  not know of, and its next save drops it (PHASE 6a DECISION 14, extended). From
  6b.3 that costs more: the last run's record gains what the farm yielded, and
  every potion carried is checked against it, so a slot a 6b.3-or-later build
  saved and a 6b.1 or 6b.2 build then saved again with potions in its bags is
  refused by a 6b.3-or-later build — the yield it was mixed from is gone — and set
  aside, kept and never deleted. And a save an earlier 6b step wrote may say
  `THE NUMBERS HAVE CHANGED` once when a later step loads it, though no number
  moved, since the steps add the numbers they play to version 3's record. A
  released version 3 never does.
- **`campaign.bak.json`** is the save before the last one. Every write goes to a
  temp file first and then replaces the save in one step, the old save becoming
  the backup, so a write that fails — a full disk, a locked file, a folder with
  no rights — costs nothing: the save on the disk is the last good one, the log
  says why, and the game says `SLOT 2 COULD NOT BE SAVED` (or `THE OLD AUTOSAVE
  COULD NOT BE SAVED`) at once, over the floor you are playing or under the
  title between runs (there until a later save goes through, which takes it
  back), and again on the next screen — the screen between runs, or the title after QUIT TO TITLE —
  once, however many saves failed, and not at all if a later save has gone
  through by then, the quit's own included. It never saves into another slot
  instead. A quit that cannot save leaves `campaign.unsaved` beside the save of
  the campaign's slot, and the title says so the next time it
  opens — at once, after QUIT TO TITLE — before anything is loaded, for every
  slot that has one beside a save or a backup: `SLOT 2 COULD NOT BE SAVED WHEN
  YOU LAST LEFT PLAY - WHAT IT HOLDS IS FROM BEFORE THAT, BEHIND WHERE YOU LEFT
  IT` (for the old autosave, `THE OLD AUTOSAVE COULD NOT BE SAVED WHEN ...`). One
  beside neither — its files moved or deleted by hand since — is cleared, and
  only the log says it. **A slot whose saves keep
  failing costs what was played since its last save that went through**: every
  save of the campaign goes into its own slot, SAVE GAME's too, so there is
  nowhere else to save it meanwhile, and a quit that cannot save loses what came
  after the last save. The game says each failure as it happens, and a later save
  that goes through — once the disk has room, or the other program lets go —
  saves all of it. (What should become of such play is the user's to rule: SLOT
  DECISION 3 in `docs/roadmap/open-questions.md`.)
- **No write costs a file**: before any save goes into a slot, a `campaign.json`
  there that no save can be read from — one that is not a save, or one a newer
  build wrote — is set aside first as `campaign.unreadable-N.json`, and so is such
  a `campaign.bak.json` that the write would otherwise push out. The screen says
  so the way it says a failed save — a save by hand, in the menu's answer — and
  the log says why. A file another program holds open is left where it is, and
  the write fails as above. A file that can be neither read nor moved when a
  load, NEW CAMPAIGN or a save goes to set it aside stays where it is, and
  nothing is saved into its slot for the rest of that session, so nothing can
  take its place — even once the other program lets go: the notice ends `- THIS
  SLOT TAKES NO SAVES THIS SESSION, SO IT IS NEVER WRITTEN OVER`. Since every save
  of a campaign goes into its own slot, no campaign is played in such a slot that
  session, where it could never be saved: LOAD GAME and NEW CAMPAIGN mark it
  `TAKES NO SAVES THIS SESSION`, a later try from the title loads nothing and
  says `NOTHING COULD BE LOADED FROM SLOT 2` / `SLOT 2 TAKES NO SAVES THIS
  SESSION: EARLIER, ... - LAUNCH THE GAME AGAIN TO PLAY IN IT`, and NEW CAMPAIGN
  there asks nothing and begins nothing (`NO NEW CAMPAIGN BEGAN IN SLOT 2`, then
  why). CONTINUE still names it when its save is the most recent, and its load
  says the same. Every other slot plays as ever. Launch the game again to play
  in it.
- **A save that cannot be played is set aside, never deleted**: one that is not
  a save, one a newer build wrote, one naming content that is gone, one that
  contradicts itself or holds a number past what play can reach (below), or one
  that crashed the game as it resumed. A `campaign.resuming` marker sits beside the
  save while a resume is in flight — until the resumed screen is up, the screen
  between runs or the resumed floor, and no save goes into the slot before then
  but the quit's: a window close during the fade in writes the campaign as it
  was loaded (nothing, when the slot holds it already) and clears the marker,
  since the game quit cleanly — and a load that finds it knows the last resume
  never finished; the slot list says `ITS LAST LOAD DID NOT FINISH`. (A save
  written over the file a marker guarded takes the marker with it.) The save is
  renamed `campaign.unreadable-N.json` (or `campaign.crashed-N.json`) in its own
  slot's folder, the backup is tried — one save older, so at most a turn or a
  floor back mid-run — and failing that the load goes back to the title, which
  says `NOTHING COULD BE LOADED FROM SLOT 2` (or `FROM THE OLD AUTOSAVE`) and
  each file set aside, whole; nothing else is touched.
  A load that resumes says what became of the campaign under the title
  of the screen between runs, whole: `CAMPAIGN RESUMED FROM THE BACKUP OF SLOT 2,
  ONE SAVE OLDER` (`FROM THE OLD AUTOSAVE BACKUP, ...` for the old autosave),
  `THE NUMBERS HAVE CHANGED ...` when they have, and each file set aside by its
  full path (drawn with forward slashes, since the game's font has no backslash;
  Windows reads it either way). When the backup holds a run in progress, the
  floor it resumes on says the same, whole, over the floor banner; the banner
  moves down to make room, and the words stay up longer the more there are. Only
  what the data files raised after that is cut to three rows (`+N MORE - SEE THE
  LOG`). The log has every word. **Rename it back to `campaign.json` to try it
  again** (what NEW CAMPAIGN set aside is moved instead: Starting over, below).
  Nothing ever writes over a file set aside.
- **No field of a save is trusted** (the user's ruling of 2026-09-25: "since we
  are storing in raw json we should probably not trust the fields"). Each is
  checked on its own — an id the content has, a count no smaller than play
  writes it and, where a larger one would break the game's arithmetic, no larger
  than play can reach: a forged stack past 3, a live one past its forged count
  plus 50, an enchantment's tier past 2147, more than 10 on a weapon, a run
  counter in the billions, hit points past the largest bar plus what was earned,
  a weapon's wear past any capacity any `tuning.json` could give it (10000 plus
  1000 for each wear it has cashed), a service at the enchanter taking more than
  11 runs, a run's end working more than 10 of them — and then against the rest:
  a retired dungeon never cleared, a last run's summary that disagrees with the
  campaign (its run count, its lock, the dungeon it cleared), a run in progress
  in a dungeon the list could not have offered, a boss standing that the run has
  down, an enemy's weapon or a drop carrying wear, a shop that contradicts itself
  (above). Wear above the capacity today's
  `tuning.json` gives is no fault: lower `StartingPool` and a full weapon keeps
  its wear, reading `WEAR 30/20 FULL`. A save that fails any of it is set
  aside as one that cannot be read, the notice naming the field and what is
  wrong there: edit `"retired": ["tutorial"]` while `"cleared": []` and loading
  it says `CAMPAIGN.JSON COULD NOT BE READ (CAMPAIGN.RETIRED[0]: TUTORIAL IS
  RETIRED AND WAS NEVER CLEARED)`, sets it aside and resumes the backup; give a
  carried weapon's entry `"tier": 99999` and it names the tier and `IS ABOVE
  2147`. A lock that has already lifted is no contradiction: it stays in the
  save, and the list reads its dungeon as open.
- **Starting over**: NEW CAMPAIGN on the title starts fresh with the starting
  party, in the slot you choose for it: it opens its slot list, `NEW CAMPAIGN -
  CHOOSE A SLOT` — `SLOT 1` to `SLOT 3`, never the old autosave nor a slot above
  the count — on the lowest empty slot, so Enter pressed at once sets nothing aside
  while a slot is free. An empty slot begins at once. A slot that holds a
  campaign asks first, naming it, as old games ask before they overwrite a file
  (one that takes no saves this session asks nothing, and begins nothing: above) —
  `SLOT 2 HOLDS THE SPEAR HALL FLOOR 4 - 3 RUNS` (`SLOT 2 CANNOT BE READ` for
  files none of which reads) / `BEGIN HERE - WHAT IT HOLDS LEAVES LOAD GAME, KEPT
  AS FILES, NEVER DELETED - ENTER: YES - ESC: NO` — and, on Enter, sets that
  slot's save and its backup aside in the slot's own folder as
  `saves\slot-2\campaign.abandoned-N.json` (the backup first, so the higher number
  is the later save), never deleted; the old autosave and every other slot are
  untouched. **To play the old campaign again, move its file into an empty slot
  folder as `campaign.json`** — `saves\slot-3\campaign.json`, say — where LOAD GAME
  lists it: renamed back where it lies, it would take the new campaign's save's
  name. (Whether NEW CAMPAIGN sets a slot's campaign aside or saves over it is
  the user's to rule: SLOT DECISION 2 in `docs/roadmap/open-questions.md`.)
  The new campaign is saved into its slot at once, before it is played,
  and saves there from then on; the screen between runs says `A NEW CAMPAIGN
  BEGINS` and where the old files went: even a crash before its next save
  continues the new campaign, never the old one. `DistanceRPG --new-campaign` —
  `dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj --
  --new-campaign` — does the same at launch without asking, past the title
  (developer tooling), in the slot the list would start on: the lowest empty
  slot, or slot 1, set aside, when every slot is in use — chosen once the launch
  has moved the old autosave, so never the campaign just moved while a slot
  within the count is still free after the move. When the move takes the last
  free slot within the count, every slot is then in use, and slot 1 is set aside
  all the same — the campaign just moved, when that slot was slot 1. A new
  campaign that cannot be saved in its slot is never begun, since every save of
  it would fail: when another program holds either file open so it cannot be
  moved, or that first save fails, the save and its backup go back where they
  were, and the title says `NO NEW CAMPAIGN BEGAN IN SLOT 2`, then why
  (`--new-campaign` then opens on the title). The marker of a load that never
  finished stays beside them, as it was, and a quit's unsaved marker that
  `--new-campaign` took is said on that title, as it would have been. Deleting
  `campaign.json` by hand leaves CONTINUE the backup instead, if there is one.
- **The dev overrides play apart**: a launch with `DISTANCERPG_DUNGEON` (a
  dungeon's id: straight into it) or `DISTANCERPG_SEED` (the first run's map
  seed) plays a fresh campaign in `saves\override\`, emptied at every such
  launch, with no title, and never reads, writes or moves a slot or the old
  autosave — every override launch is as repeatable as ever, and your campaigns
  are untouched.
  Its menu's 3 reads `3  QUIT GAME` — the window closes, saving the scratch slot —
  and its 2 saves the scratch slot too (`SAVED TO THE SCRATCH SLOT`), nothing
  else. Unset them to go back to the title and your slots.

## Tuning the numbers: `tuning.json`

Every number the game is tuned by — the movement budget, the mana divisor, each
status's effect per level, what a stack of each modifier is worth, the farm's
odds, the boss depths, the carry — sits in one file, `tuning.json`, grouped
under the headings of `docs/roadmap/phase-5-save.md` §5.3 (and four of its own:
Progression, Combat, Drops and Saves) with a comment on every key. A few gameplay
numbers were never keys and are still compiled — an enemy's 50 hit points, 100
mana and 100 movement, the crowded-room healer's odds, the chart's ×1.5, the
proficiency steps, the room cap; whether they belong in `tuning.json` is open
(`docs/roadmap/open-questions.md`).

- **Where**: beside the executable — `GameEngine.DistanceRPG/bin/Debug/net10.0/tuning.json`
  after a Debug build. The log prints its full path at startup
  (`[Data] Read D:\...\net10.0\tuning.json`); the logs are in
  `%LOCALAPPDATA%\Sheckerol\GameEngine\logs`.
- **`GameEngine.DistanceRPG/Data/tuning.json` is the master copy**, and the only
  home of these numbers: the game keeps no copy of one in its code. The game is
  built with it inside, and plays that built-in copy wherever the copy beside
  the exe leaves a key out — or all of it, when that copy is missing or cannot
  be read. A build copies the master beside the exe, where a playtest edits it:
  **edit the copy beside the exe to playtest, and the master to change what
  ships.** A build replaces the copy beside the exe whenever the `Data/` master
  is newer — any change to the master, a checkout or a pull — so keep an edit
  worth keeping in `Data/`.
- **Restart to apply**: the file is read once, when the game starts, and never
  during play. If the file is there but the game cannot read it, the built-in
  numbers play and the screen says why. This happens when an editor holds the
  file open and locked, or the folder does not let the game read it. The screen
  shows `TUNING.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN) - THE
  BUILT-IN NUMBERS ARE PLAYING` and does not call the file missing. The log has
  the whole error. A file with nothing in it — as an editor that crashed while
  saving can leave one — plays the built-in numbers too, and says
  `TUNING.JSON: THE FILE IS EMPTY - THE BUILT-IN NUMBERS ARE PLAYING`.
- **Name only what you change**: a key left out keeps its built-in number, so
  `{ "MovementBudget": 240 }` is a whole file. Comments and trailing commas are fine.
- **A mistake costs a warning, never a crash**: an unknown key, a value out of
  range, or one that is not a whole number is shown on the title screen, under
  `DISTANCE RPG` (or over the first floor when `DISTANCERPG_DUNGEON` boots
  straight in, or under the BETWEEN RUNS title when `--new-campaign` or
  `DISTANCERPG_SEED` skips the title) and
  written to the log, and the game plays on — `"MovementUnitsPerMana": 0` plays 1
  and says `TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1`. A few keys
  are also fitted to the content: `"PartyCarrySlots": 4` plays 8, because every
  starting member carries two weapons. The log lists every number the file
  changed (`[Data] ...\tuning.json changes 2 numbers: MovementBudget 240, ...`).
- **Derived numbers do not follow**: `MovementUnitsPerMana` is derived from the
  movement budget and the cast costs, and turning `MovementBudget` leaves it
  where it was — so a longer budget also banks more mana a turn. Its comment
  gives the formula to re-derive it by hand.
- **The Enchanter's keys** are what a hit wears, what a deposit at the
  enchanter costs, as the user ruled on 2026-09-25
  (`docs/roadmap/phase-6-between-runs.md` §6.1, §6.2), how many deposits the
  enchanter works on, and the odds its services improve a weapon. **All of them
  play**, since Phase 6a: `WearPerHit`, the wear a weapon banks for every hit it
  lands (0 wears nothing, so nothing could be enchanted); `WearCapacityPerBar`,
  what a full bar of wear cashed adds to a weapon's capacity — worked out afresh
  from the wear the weapon has ever cashed, so a change moves every weapon's
  capacity at once; `FirstSlotCapacity` and `SlotCapacityStep`, where a weapon's
  enchantment slots open
  (slot n at `FirstSlotCapacity + SlotCapacityStep x (n - 1)`);
  `EnchantmentSlots`, the most enchantments a weapon holds
  (`"EnchantmentSlots": 0` is taken as 3 and says
  `TUNING.JSON: ENCHANTMENTSLOTS 0 IS BELOW 3 - USING 3`); `FirstEnchantmentWear`
  and `EnchantmentWearStep`, the deposit attaching enchantment n needs
  (`FirstEnchantmentWear + EnchantmentWearStep x (n - 1)`); `EnchanterWorksOn`,
  how many of the newest deposits a run that counts works (1 to 10; the doc's 2)
  — a transfer is one deposit, its two weapons together; and the improvement
  roll's:
  `GraftBaseChance`, the chance in 100 a service improves the weapon anyway (0 to
  100; flat when it enchants or moves an enchantment onto it, times 1 + wear ÷
  `StartingPool` when it refines), and two provisional numbers,
  `GraftSharePercent`, how many of the rolls that land graft (0 to 100; 25), and
  `DeepenSignatureWeight`, how many times likelier a deepen lands on the class's
  signature (1, no bias, to 1000; 3) — PHASE 6a DECISION 5. Turning any says
  `THE NUMBERS HAVE CHANGED` when a campaign saved under the other number resumes —
  but never to a save from before Phase 6a, whose record of its numbers covers
  only the keys played then; while Phase 6a is being built, a save an earlier
  step's build wrote says it once, though no number moved, since each step added
  the numbers it plays to version 2's record. The docs' older
  `WearCapacityLadder` and `EnchantmentWearCost` are no keys: a file naming either
  is told which key took its place.
- **`SaveSlots`** is how many save slots there are, each holding one campaign
  that saves only there (see [Saves](#saves)) — the user ruled it a key, "so that
  we don't need to fix the code every time I change my mind". At least 1 and at
  most 9, a digit choosing each: `"SaveSlots": 12` plays 9 and says `TUNING.JSON:
  SAVESLOTS 12 IS ABOVE 9 - USING 9`. Lowering it deletes nothing: a slot above
  the count keeps its save, which LOAD GAME lists as `BEYOND THE 1 SLOT`, and
  loaded, it goes on saving the campaign played from it; NEW CAMPAIGN never
  offers it. The launcher reads it and no rule of play does, so turning it never
  says `THE NUMBERS HAVE CHANGED`.
- **To reset** the copy, delete it (the built-in numbers play, and the screen
  says the file is missing), or write the built-in copies back:
  `dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj -- --write-data <exe folder>`
  writes the four built-in copies into the folder, byte for byte, and exits
  without opening a window. A number worth keeping goes into
  `GameEngine.DistanceRPG/Data/tuning.json`, the master, and the game is rebuilt;
  `--write-data` refuses that source `Data` folder, so it can never write the
  last build's copies over an edit there.
- **The master names every number.** A key or a table member left out of the
  master stops the game on `THE GAME CANNOT START`, naming
  `TUNING.JSON (BUILT-IN)` and the key, and ending
  `THE BUILT-IN TUNING.JSON IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/TUNING.JSON AND REBUILD - ESC QUITS`:
  nothing stands under it. (A copy beside the exe may leave out what it likes.)
- Nothing the golden tests pin — the map generator, the random streams, the
  pathfinder — is in it, and a test keeps it that way.
- **An edit to the master fails a test only where a design doc fixes the
  value**: `MovementBudget` 160, and `MovementUnitsPerMana` 10 derived from it
  (`docs/roadmap/phase-1-weapons.md`'s `(160 − 40) / (8 + 3)`), fail
  `MovementBudgetTests.TheBudgetIsTheTuningsKey` by name, and the doc and the
  derived key move with them; and `FriendlyFireEnabled` turned on fails
  `WandTests.FriendlyFire_OffByDefault_HalfWhenOn`, since the same doc ships
  wands with friendly fire off until the wand placement scorer exists; and
  `StartingPool` 30 and `WearCapacityPerBar` 1 fail
  `WearTests.ACapacityReplaysFullBars_TheRulingsNumbers`, since the user's ruling
  on what a deposit costs states them and the capacities they give; and so do
  the rest of that ruling's numbers — `FirstSlotCapacity` 30, `SlotCapacityStep`
  5, `EnchantmentSlots` 5, `FirstEnchantmentWear` 30 and `EnchantmentWearStep` 5
  — and `EnchanterWorksOn` 2, §6.2's "the two most recently deposited", which
  fail `EnchanterTests.TheRulingsClimb_WearDepositsAndRuns`, the ruling's worked
  climb; and `GraftBaseChance` 3, §6.4's, which fails
  `ImprovementRollTests.TheOddsAreTheRulings`, the 6, 7 and 9 in 100 the ruling
  works out from it. Every other number in it fails nothing: the tests read it from the master, or stand
  on numbers of their own (`Premise`, in the test project: the numbers the
  worked examples were written against, which most scenario suites play instead
  of the master's) — give `PerStack`'s `Block` 4, rebuild and run the suite, and
  it passes, the Bulwark absorbing 12 and its test saying so.

## The content files

Three content files sit beside `tuning.json` (`docs/roadmap/phase-5-save.md`
§5.4–§5.9): `restricted.json`, `weapons.json` and `dungeons.json`. They are read
once, when the game starts, in that order, after the game's own catalogues, and
everything in them is named by a stable string id.

- **`restricted.json`** holds the relations: what may sit together on a weapon
  and what may be rolled, for modifiers and enchantments alike. `excludes` is a
  list of groups (every id in a group excludes every other, both ways), `requires`
  maps an id to the ids it cannot exist without, `kind` limits a modifier to
  `melee`, `ranged` or `caster` (lower case), `forgedOnly` lists the modifiers
  that can be deepened but never added, and `neverRolled` lists the enchantments
  no roll grants and no service copies (the unique souls, as shipped). `kind` and
  `forgedOnly` are for modifiers only, and `neverRolled` for enchantments only:
  the wrong sort of id in one stops the game. `neverRolled` is this file's alone —
  the catalogue's unique flag only pins a soul's tier — so put `vampiric` in it
  and it stops dropping, and the enchanter stops offering it; take a soul out and
  it may be copied: the enchanter offers it once the campaign has met it (no drop
  carries a soul either way). The enchanter asks `excludes` and `requires` too: it
  attaches nothing a group pairs with an entry the weapon carries, nor anything
  whose requirement is not on the weapon ahead of it — and a move is
  asked the same of the weapon it lands on, and `requires` of the weapon it leaves:
  it strands nothing behind it. A deposit already in the shop when the file
  changes is served as it was bought.
  A relation is an edit here, never a rebuild: take the crit riders' group
  (`["CritWeaken", "CritSunder"]`) out and restart, and a dagger may hold both.
- **The enchantment catalogue and the unique souls are code**
  (`Logic/Enchantments/EnchantmentDefinitions.cs`); so is the starting party
  (`Logic/Content/StartingParty.cs`) — a change to either is a rebuild, and a new
  campaign starts with the party the code names (a saved campaign keeps the party
  it started with). Both are still checked when the game starts, against the
  files beside it. The catalogue's dialled numbers — the `applyPercent` of
  `flaming`, `cold`, `shocking`, `acidic` and `serrated`, and `arcane`'s
  `potency` — are not in the catalogue at all: they are `tuning.json`'s
  `ApplyPercent` and `ArcanePotency`, so turn them there, and the master
  `tuning.json` must name each. `enchantments.json` and `party.json` are no
  longer read: a build removes the old copies beside the exe (an edit in one is
  lost with it), and a copy found there anyway is named on screen as no longer read
  (`PARTY.JSON IS NO LONGER READ - THE STARTING PARTY IS IN THE CODE
  (LOGIC/CONTENT/STARTINGPARTY.CS)`), its path in the log.
- **`weapons.json`** holds every weapon class and every weapon, in three
  sections:
  - `classes` gives each of the eight classes (`Dagger`, `Sword`, `Spear`, `Axe`,
    `Ranged`, `Throwing`, `Staff`, `Wand`) one row: its `kind` (`melee`, `ranged`
    or `caster` — the Staff and the Wand cast, and no other class may), the
    `stats` it levels on (`["STR", "DEX"]` for the Sword, the better of the two
    counting; never `CON`), its `signature` (the modifier it is named for, which
    its Purity variant deepens and a dungeon's theme is drawn from; `null` for a
    caster), its `baseline` (`{ "Block": 1, "Push": 1 }`: what every weapon of the
    class is forged with — a martial class's signature and one more, a caster's
    one modifier or more, `{ "Resonant": 1 }` as shipped, and every staff and
    wand carries it alone) and the statline its weapons share (`range`, `damage`,
    `cost`, `manaCost`). Re-basing a class is one edit to its row: give the Dagger
    row `"damage": 17` and restart, and every dagger — the uniques made from them
    too — hits for 17. A class row left out, one the game cannot level or cast
    with (a `Staff` that is `melee`, `stats` naming `CON`, a signature another class
    has), or one whose `baseline` the forge refuses (a stack of `0`, one past its
    forge limit, a modifier `restricted.json` keeps off the class's kind) stops the
    game naming `WEAPONS.JSON` and the class, whose row the baseline is written in.
    The Staff and Wand rows also name their `dropOrder`: the variants a drop of
    the class rolls between, in the order the draw counts them. A staff or wand
    it leaves out never drops — add one to `variants`, then to the `dropOrder`,
    and it drops with no rebuild. Reordering `variants` moves no drop; an edit to
    a `dropOrder` moves what a dummy not yet collected in a saved campaign drops.
    An id there that is no variant of the class, or one named twice, stops the
    game.
  - `variants` lists the thirty-two weapons a drop or a dummy carries, each as its
    `class`, its `role` and the modifiers it `adds` to its class's baseline, with
    the enchantments it arrives with (`{ "id": "vampiric", "tier": 3 }`, the tier
    left out when it is 1) and a wand's `shape`. A variant may name its own
    `range`, `damage`, `cost` or `manaCost`, which then plays for it alone — the
    Staff of Renewal's and the Staff of Mire's mana do. Give the Weakspot Stiletto
    `"damage": 25` and restart, and A hits harder while the other daggers do not.
  - `uniques` lists the artifacts, each as the variant it is `derivedFrom` —
    whose class, statline and shape it takes — the modifiers it `raises` (set to
    the count given, not added to) and its enchantments.

  `arming_sword` (what a dummy holds) and `staff_of_renewal` (a crowded room's
  healer) are named by the code, so they must stay, and so must a variant of each
  class a dummy can be placed holding (Dagger, Sword, Spear, Axe, Throwing).
- **An old-format `weapons.json` beside the exe stops the game.** Before the
  classes section the file was one `weapons` list, every weapon naming all its
  fields. A copy in that format beside the exe — one edited there after its
  master, or one an earlier `--write-data <exe folder>` wrote, newer than the
  master so a build keeps it — opens the game on `THE GAME CANNOT START`, naming
  `WEAPONS.JSON` and `CLASSES` and saying the file predates the classes section:
  delete it and the built-in weapons play (the screen says the file is missing),
  or run `--write-data <exe folder>` to write the current copy there. A copy
  from before the Staff and Wand rows named their `dropOrder` stops the game the
  same way, naming `STAFF` and saying the same two ways out.
- **`dungeons.json`** holds the dungeons the screen between runs lists: `theme`,
  `attunement` (a damage type, or `null`), `bossFloorMin`/`bossFloorMax` (`null`
  plays `tuning.json`'s range), the `boss` (its statline and `innate` modifiers)
  and `retiresOnClear`, the tutorial's alone. Moved onto another dungeon once a
  campaign has deposits at the enchanter, it refuses no save and hides nothing in
  the shop: the enchanter takes no new deposit until that dungeon is beaten, and
  what is in the shop is listed and worked as ever. There is no drop table: what a boss
  drops is computed from its theme and `restricted.json`, so taking `Brace` out
  of `["Brace", "Opportunist", "Overwatch"]` puts axes in the Spear Hall's drops
  with no edit to this file.
- **A row marked `/* provisional */`** after its id is one the docs did not fix
  — the four dungeons beside the tutorial and their bosses, and the three
  unnamed uniques — a first guess, expected to move after play.
- **The files in `GameEngine.DistanceRPG/Data/` are the master copies** — of every
  relation, weapon class, weapon and dungeon — hand-kept, with the reason for each
  group, class and boss written beside it; the game keeps no copy of any in its
  code. As with `tuning.json`, the game is built with each inside and plays that
  built-in copy when the copy beside the exe is missing or cannot be read, and a
  build copies each beside the exe (`bin/Debug/net10.0/`) whenever the master is
  newer: edit the copy beside the exe to playtest (restart to apply), and the
  master to change what ships (rebuild). The file headers say what each key and
  field means.
- **An edit to a master fails a test only where a design doc fixes the
  number**: the eight class rows, every weapon's statline and spread and the
  wands' shapes (`docs/roadmap/phase-1-weapons.md` §1.2–§1.4) fail
  `WeaponContentTests.Statlines_MatchTheTables`, `WandShapes_MatchTheTable` and
  `WeaponClassTests.AClassRowIsWhatTheClassCarries` by name; the unique table
  (§1.5) fails `UniqueContentTests`; and the tutorial's row
  (`docs/roadmap/phase-5-save.md` §5.8) fails
  `DungeonContentTests.TheTutorialIsTheShippedDungeon` — its golem's 80 HP and
  movement budget of 40 that test alone, its boss's floor 2 every test that
  plays the tutorial. Each doc moves with its pin. Every other number — a
  provisional boss's statline, say — fails nothing: the tests read it from the
  master, or stand on numbers of their own (`Premise`). A relation, an id or a
  row is no number, and a test that names one fails where it names it.
- **Content is checked, never clamped.** A number out of range in `tuning.json`
  costs a warning; in a content file it is a broken rule — every number there
  has a range, below — and the game does not start. It opens on
  `THE GAME CANNOT START` instead, naming the file, the entry,
  the rule and what broke it, with what `tuning.json` changed and
  `FIX THE FILE AND START AGAIN - ESC QUITS`; the log has the same lines. A typo
  such as `"Riposte": ["Blok"]` shows `RESTRICTED.JSON`, `BLOK`, the rule
  `ID DOES NOT RESOLVE TO A MODIFIER OR AN ENCHANTMENT` and the detail
  `NOT A MODIFIER, NOR AN ENCHANTMENT IN THE ENCHANTMENT CATALOGUE - DID YOU MEAN BLOCK?`.
  A field the game does not know, a value of the wrong kind, or a file that is
  not JSON is refused the same way, naming the path in the file
  (`dungeons[1].boss.maxHp`) — or, for a file that is not JSON, the line, the
  column and the word the reader stopped at, on one line
  (`"shape": nul` reads `NUL... IS AN INVALID JSON LITERAL. EXPECTED THE LITERAL
  NULL`). Comments and trailing commas are fine.
- **Every number in a content file has a range** (the user's ruling of
  2026-09-25: "since we are storing in raw json we should probably not trust the
  fields"), and one outside it is refused the same way, under the rule
  `A NUMBER IN A CONTENT FILE LIES IN THE RANGE THE GAME PLAYS IT IN`, naming the
  entry, the field, the number and the whole range with why: give the Halberd
  `"cost": 0` and the screen shows `WEAPONS.JSON`, `HALBERD` and
  `COST 0 IS OUTSIDE 1 TO 10000 - MOVEMENT IS CHARGED A UNIT AT LEAST, ...`; pin
  the tutorial's boss to floor 100 and it shows `DUNGEONS.JSON`, `TUTORIAL` and
  `BOSSFLOORMIN 100 IS OUTSIDE 1 TO 99 - ...`, with no `tuning.json` notice above
  it. Where a rule bounded a number before — a stack below 1, a forge past its
  limit, a boss floor below 1, a boss with no hit point — that rule still refuses
  it, its detail in the same form (`BOSS.MAXHP 0 IS OUTSIDE 1 TO 10000 - ...`).
  The tops are the game's own, read off `tuning.json`'s clamps, never numbers of
  the masters, so every shipped number lies inside them:

  | Number | Range | Why |
  | --- | --- | --- |
  | `weapons.json`: a class row's `range`, and a variant's own | 1–10000 | a reach of 0 hits nothing; 10000 is the farthest `tuning.json` measures (`MovementBudget` at its most), past any map |
  | a class row's `damage`, and a variant's own | 0–1000 | a staff hits for 0, its cast being its innate; 1000 is the most a `tuning.json` magnitude can be (`PerStack` at its most) |
  | a class row's `cost`, and a variant's own | 1–10000 | movement is charged a unit at least, so a smaller cost would be one the game never charges, and the HUD's COST would show it |
  | a class row's `manaCost`, and a variant's own | 0–1000 | a martial class casts for 0; below 0 a cast would open on an empty pool |
  | a class row's `baseline` stack, a unique's `raises`, and every weapon's spread as the game composes it | 1 to the modifier's `MaxForged` (3 at most) | the forge's own rules, as before: a stack is one at least, and nothing is forged past its limit |
  | a variant's `adds` stack | 1–3 | an add adds a stack at least, and none is deeper than any forge goes (`MaxForged` at its most). Checked as the file is read, before it is added to the class's baseline, so the refusal names the number written and its path — `"adds": { "Light": 0 }` on the Flensing Knife shows `WEAPONS.JSON: VARIANTS[0].ADDS.LIGHT: 0 IS OUTSIDE 1 TO 3` |
  | an enchantment's `tier` | 1–3 | a weapon arrives at tier 1, and an artifact at 3 at most; tier grows in play, never in the file. A unique enchantment written above 1 plays at 1 |
  | a wand's `radius`, `length`, `width` and `targetReach`, each its shape measures | 1–10000 | a measured dimension of 0 covers nothing, and none is longer than the farthest the tuning measures. One its shape does not measure is 0, or refused, as before |
  | a Cone's `angleDegrees` | 1–360 | a cone of 0 degrees covers nothing, and none is wider than a full turn |
  | `dungeons.json`: `bossFloorMin` and `bossFloorMax`, where a dungeon pins them | 1–99 | a floor number starts at 1 (an inverted range is refused as before); 99 is the deepest floor `tuning.json` can name (`BossFloorMax` at its most), and one the boss offset can always be fitted to |
  | a boss's `maxHp` | 1–10000 | a boss has a hit point at least, and no more than the largest bar a pool opens on (`StartingPool` at its most) |
  | a boss's `movementBudget` | 1–10000 | a boss moves a unit at least, and no farther than the tuning measures |
  | a boss's `innate` stack | 1–53 | an innate stack is one at least — innate is no forge's, so the golem may stand deeper than any weapon — and none is deeper than a stack can ever grow (`MaxForged` 3 plus `AcquiredHeadroom` 50, each at its most) |

  `restricted.json` holds no number, and a `dropOrder` is ids. A copy beside the
  exe with a number out of range never falls back to the built-in copy; a
  built-in copy out of range is the master's own, whatever sits beside it.
- **What is reported first**: the load runs in the order the game's catalogues,
  tuning, relations, weapons, dungeons, the starting party, and the first check
  to fail is the one shown. Each file's shape is checked before anything reads
  it. A later file's mistake never hides an earlier one's: a typo in
  `dungeons.json` waits behind a broken relation. A stage that ran a built-in
  copy is marked `(BUILT-IN)`: either a file beside it broke it — a
  `restricted.json` there refusing a built-in weapon, and the line at the bottom
  still says `FIX THE FILE AND START AGAIN` — or the master itself is broken: its
  own text is not JSON of its shape, a number in it is out of its range, or no
  content file was read from beside the exe to have broken it. Then the screen ends `THE BUILT-IN WEAPONS.JSON IS
  BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/WEAPONS.JSON AND REBUILD - ESC QUITS`,
  since only fixing the master and rebuilding can mend it. A
  `restricted.json` edit that breaks the enchantment catalogue — an element
  opposed by two, `["flaming", "cold", "shocking"]`, say — stops on
  `RESTRICTED.JSON` itself, the file to fix,
  and one that makes a weapon illegal names both the weapon — or its class, where
  the relation refuses the class's baseline — and the relation that refused it,
  for modifiers and enchantments alike: add `["Push", "Block"]` to `excludes`,
  and since the Sword's baseline forges both, the game opens on `WEAPONS.JSON`,
  `SWORD`, and a detail ending
  `RESTRICTED.JSON EXCLUDES [PUSH, BLOCK]: PUSH IS ALREADY FORGED HERE`. A
  `weapons.json` that renames a weapon only the starting party names — A's
  `weakspot_stiletto`, C's `great_axe` — stops on `WEAPONS.JSON` too, naming the
  member.
- **A missing file plays the built-in one, and says so**: delete
  `weapons.json` and the game plays the built-in weapons, with
  `WEAPONS.JSON IS MISSING - THE BUILT-IN WEAPONS ARE PLAYING` under the title
  and the path it looked in in the log. The same goes for every content file.
- `--write-data <folder>` writes the four built-in copies: `tuning.json` and the
  three content files, byte for byte as the build carries them. Run on the folder
  beside the exe it resets every edited copy there. It refuses the source `Data`
  folder, which holds the masters.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
