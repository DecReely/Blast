# Blast

A level-based blast puzzle game.

Tap a group of two or more adjacent same-coloured cubes to blast them. Larger groups collapse into
special items, adjacent special items merge into combos, and the level is won by clearing every
obstacle before the moves run out.

Unity **6000.3.10f1**, built-in render pipeline, portrait 9:16, Input System package.

## Running it

Open the project and play `Assets/Scenes/MainScene.unity`. The Game view should be set to a portrait
aspect; the project's player settings are portrait-only at 1080x1920.

`LevelScene` can also be played directly. It loads whichever level your saved progress points at,
or you can force one with the **Level Number Override** field on the `LevelSession` object.

## How a turn works

The board is the single source of truth. Every system reads cell contents from it and mutates it
only through its API; items never work out where they are or what is next to them on their own.

```
BoardInput ──world position──▶ BoardCoordinator
                                    │
      ┌─────────────────────────────┼──────────────────────────────┐
      ▼                             ▼                              ▼
  cube group                  group of 4+                    special item
  (2 or 3)                    MergeAnimator ▶                ExplosionSystem
      │                       SpecialItemFactory             (or ComboDetector
      ▼                             │                          ▶ ComboRules)
  blast + damage                    ▼                              ▼
  neighbours                  GravitySystem ──FallRequests──▶ FallAnimator
                                    │
                                    ▼
                              HintController ▶ GoalTracker ▶ LevelOutcome
```

`BoardCoordinator` is the only place that decides what a tap means, and it holds a "resolving" gate
for the whole turn so taps cannot interleave with an explosion or a fall.

## Tests

Open **Window ▸ General ▸ Test Runner** and run the EditMode suite, or from the command line:

```
Unity.exe -batchmode -runTests -projectPath . -testPlatform EditMode -testResults results.xml
```

`Blast.Tests.Editor` holds two kinds of test. `GameRuleTests` covers the pure functions — the
group-size thresholds and the chalice shelf split — and runs in milliseconds. `GameplayVerificationTests`
is the integration layer: each test opens `LevelScene`, plants a board and plays real turns through
the real coordinator. That works outside play mode because every time-based system exposes a
`Tick(delta)` which `SceneTicker` drives at a fixed 1/60, making a whole turn reproducible.

## Project layout

Three assemblies, so the test assembly has something to reference — assembly definitions cannot
reference the predefined `Assembly-CSharp`.

```
Assets/
  Art/               supplied case study art
  Data/Levels/       level_01.json … level_10.json
  Prefabs/           generated item and effect prefabs
  Scenes/            MainScene, LevelScene
  Input/             the shared input actions asset
  Scripts/           Blast.Runtime
    Levels/          level parsing, the level database, persisted progress
    Gameplay/        board model, turn flow, gravity, explosions, goals
    Gameplay/Explosions/   the explosion shapes
    Items/           the GridItem hierarchy
    Motion/          hand-coded fall and merge animation
    Effects/         pooled particle playback
    UI/              menu, top bar, popup, celebration, flow control
  Editor/            Blast.Editor
    Debugging/       offscreen preview and verification tooling
  Tests/Editor/      Blast.Tests.Editor
```

## Design notes

**The grid is the authority.** `Board` owns a `Cell[,]` and wraps Unity's `Grid` component for every
cell/world conversion, so nothing else does cell-size arithmetic. Input picking is a
`Grid.WorldToCell` plus a lookup rather than colliders or raycasts — the board is a regular grid, so
there is nothing for a physics query to add.

**Items decide how they react to damage.** `GridItem.ApplyDamage(DamageInfo)` is abstract rather
than virtual, so a new item type cannot silently inherit "immune". `DamageInfo` carries how the
damage arrived and a source identity, and each item interprets it: stone ignores blasts, a vase
takes at most one damage per blast, and a chalice box counts sources during its door phase but cells
during its chalice phase. A blast reports *how many* of its cubes touched each obstacle and lets the
obstacle decide what that is worth.

It answers with `Ignored`, `Damaged` or `Destroyed` rather than a bool, because "still standing" is
two different things and they need different feedback: a vase that cracked has to show it, while
stone shrugging off a blast it is immune to must not.

**Presentation belongs to the thing it describes.** A rocket half is one prefab that carries all four
direction sprites and its own exhaust, so the sweep only has to say which way it is going and an
artist has one asset to open. `SpecialItemVisuals` keeps only what no item can hold — the combo
blast and the puff a new special arrives in both happen at a moment when the items involved have
already left the board. The chalice box works the same way: `ChaliceShelfView` is told how many
chalices remain and works out the rest.

**The shelves are computed, not authored.** A box can be left holding anything from ten down to one,
so the count is split as evenly as the two shelves allow with the remainder underneath (5-5, then
4-5, then 4-4), spread across the shelf width by equal shares, and eased larger and further forward
towards the middle of each row so a shelf reads with depth. Eleven authored layouts would have been
eleven things to keep in step.

**Input is data.** `BoardInput` takes two `InputActionReference`s out of
`Assets/Input/InputSystem_Actions.inputactions`, so which devices count as a tap is authored in the
input asset rather than compiled in. It still polls in `Update` rather than subscribing, because
Unity does not support `EventSystem.IsPointerOverGameObject` from inside an input callback — UI
state has not settled at that point — and that check is what stops a tap on the fail popup reaching
the board.

**Five explosion shapes, three classes.** A lone TNT and the TNT-TNT combo are `AreaPattern` with
radius 2 and 3; the Rocket-Rocket and TNT-Rocket combos are `CrossRocketPattern` with thickness 1
and 3; a lone rocket is `RocketPattern`. Adding a combo means adding a class and a selection rule.

**Combos are safe by construction.** Every member of a combo is taken off the board before anything
explodes, so "individual explosions of special items are ignored" needs no flag — a detached item
cannot be found, damaged or triggered. The same ordering makes chain reactions safe: a special is
removed before its own explosion is created, so nothing it hits can re-trigger it.

**Falling is integrated, not interpolated.** `FallAnimator` accumulates velocity under constant
acceleration up to a terminal speed, so a six-row drop is genuinely faster than a one-row drop and a
falling column keeps its spacing. Gravity resolves entirely in the model first and only then
animates, so the board is never in a half-fallen state that another system could observe.

**Constant cell size.** The camera shows a fixed number of cells across and derives its orthographic
size from the aspect ratio, so a cube is the same size on every level and on any screen shape. A
small level occupies less of the screen rather than being zoomed to fit.

**Art metrics.** The supplied art is authored on a 150 px square cell — confirmed independently by
the 2x2 chalice box being exactly 300x300 px and by the row and column pitch in the case study's own
screenshots. Importing every board sprite at 150 PPU with a plain centred pivot makes one cell
exactly one world unit and removes the need for per-prefab offsets anywhere, including the 2x2 box.
Sorting order increases with row, so the shadowed bottom edge of an upper item overlaps the
highlight of the one below and produces the separation line seen in the reference.

Special items are the one exception. Their art is smaller than a cell — a rocket is 140x140, a TNT
142x142 — so they would sit visibly undersized next to a cube. Their renderers are given sliced draw
mode and an explicit one-unit size, which fits the sprite to the cell without touching the
transform, so every prefab stays at scale one and both fields remain editable in the inspector.
Cubes deliberately keep their 140x160 overhang, since that is what draws the shadow line above.

## Editor tooling

**Debug** contains the verification suites and the offscreen capture tools. The suites are the same
code the tests run — see [Tests](#tests) — exposed here as menu items that log the full report rather
than just passing or failing. They work outside play mode because the gameplay systems expose a
`Tick(delta)` that `SceneTicker` drives at a fixed 1/60 step, which is what makes a whole turn —
merge, explosion, fall, refill — reproducible in a batch-mode editor run.

| Menu item | What it checks |
| --- | --- |
| Run Board Stress Test | A settled board never has a floating item or an unfilled reachable cell, across long random play on every level |
| Verify Combos | Each combo clears exactly the footprint the case study describes |
| Verify Chalice Box | Both damage phases, including the case study's worked examples |
| Verify Obstacles | Vase and stone damage rules, and that clearing the last obstacle wins |
| Verify Edge Cases | Input gating, taps that are not moves, the win/lose boundary on the final move, multi-box goals, and that a settled board always offers a legal tap |
| Play All Levels | Plays every level to an outcome with a heuristic bot |
| Capture Level Previews / Turn Simulation | Renders levels and a full turn to PNG |
| Capture Chalice Box States | Renders a box at every count it can hold, closed and 10 down to 0 |
| Capture UI Previews | Renders both interfaces to PNG |

The board invariant in the stress test is written independently of `GravitySystem` rather than
reusing its logic, so a bug in the collapse algorithm cannot hide behind a check that makes the same
mistake. Combo footprints are measured by filling the board with stone first: stone never falls and
is never spawned by refill, so any cell that held stone and no longer does was destroyed by the
explosion — an exact measurement even though gravity and refill run before the turn ends.

`Play All Levels` reports how often a greedy bot finishes each level. It fails only when a level ends
with no outcome at all, since that would leave a player with a board that can be neither won nor
lost; a level the bot cannot finish is a difficulty observation, not a defect. It currently clears
nine of the ten, level 9 being the exception noted above.
