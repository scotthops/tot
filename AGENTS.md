# AGENTS.md

This file is for coding agents working in this repository.

## Project Snapshot

`Tides of Time` is an early Godot 4 + C# prototype for a pirate-themed FTL-like:

- grid-based ship interiors
- rooms mapped to systems
- two ships shown side-by-side in battle
- simulation-first architecture
- vertical-slice-first development

This is a prototype ship, not a finished man-of-war. Keep it tidy, keep it moving, and do not bolt a cathedral onto the deck when a ladder will do.

## Current Prototype Focus

The current goal is to prove the tactical battle board and the core interaction loop, not to build the entire game at once.

Working features already include:

- data-driven ship layouts via `ShipLayoutDef` resources
- plain C# runtime state for battle / ship / grid / room / tile
- room selection
- contextual room actions
- structured battle intents
- visible crew markers
- crew selection

Do not criticize or "fix" missing systems that are intentionally out of scope for the current slice.

## Architecture Direction

Prefer this flow:

`Resource defs -> plain runtime state -> Godot view/input scripts`

Key rules:

- Keep core game state and logic in plain C# classes where practical.
- Keep Godot scenes/scripts mainly responsible for rendering, input wiring, and scene composition.
- `BattleState` is the battle-level owner of shared interaction state.
- `ShipState` owns ship-local runtime state.
- `ShipGridView` should stay mostly a presentation/input layer that reports intent upward.
- Avoid duplicating source of truth between tiles, crew, rooms, and UI.

## Existing Ownership Boundaries

Treat these as the current source of truth unless there is a strong reason to change them:

- `BattleState`
  - current battle selection
  - last issued action intent
  - battle-level interpretation of what actions are available
- `ShipState`
  - ship name / hull
  - `ShipGridState`
  - crew currently aboard that ship
  - ship-local room selection state used to support rendering and interaction flow
- `CrewState`
  - crew identity
  - allegiance
  - current position
- `ShipGridView`
  - renders tiles and crew markers
  - emits room/crew click events upward
  - should not become the owner of battle logic

## Practical Design Rules

When making changes, prefer these habits:

- Build the smallest vertical slice that proves a behavior.
- Preserve current working behavior unless the task explicitly changes it.
- Extend the existing seams before inventing new ones.
- Prefer small records/enums/helpers over deep abstractions.
- Favor readable, local code over framework-heavy indirection.
- Use data/resources for ship content rather than hardcoding layouts in scene scripts.

## Task Scope

- Prefer solving the current branch task cleanly over preparing speculative support for three future branches.
- Do not expand a task into adjacent systems unless the requested change truly requires it.
- If a task naturally suggests a follow-up improvement, note it briefly after the work instead of silently widening the patch.

## Godot + C# Guidance

- Keep simulation/state classes plain unless they truly need to be Godot types.
- `*Def` data classes should generally be `Resource` types.
- Avoid turning runtime state into `Node` subclasses by default.
- Keep rendering layers separate when that separation already exists.
  - Example: crew markers should stay separate from tile rendering.
- Prefer one stable local coordinate system for board rendering rather than fragile cross-parent coordinate conversions.

## Selection and Interaction Rules

The prototype currently uses a single active battle selection.

- Only one thing should be "currently selected" across the battle UI at a time.
- Selecting a crew member should clear room selection highlight as appropriate.
- Selecting a room should clear crew selection highlight as appropriate.
- Battle HUD/context should be derived from `BattleState`, not from scattered UI-only fields.

## Safety Rails

- Do not rename files, scenes, nodes, or public APIs unless the task genuinely requires it.
- Do not move responsibilities across major architecture boundaries without explaining why.
- Do not delete working prototype behavior just because it is temporary, ugly, or simplistic.
- Prefer additive, reversible changes during prototype iteration.

## Crew-Specific Rules

Crew is intentionally simple right now. Keep it that way unless the task says otherwise.

- Keep crew position as the source of truth for occupancy.
- Do not add duplicate occupancy state to tiles unless absolutely necessary.
- Keep allegiance separate from current location/ship.
- Seed prototype crew in battle setup / encounter setup rather than burying it deep in generic layout factory code.

## Movement / Topology Guidance

Movement is not implemented yet.

If you touch pre-movement groundwork:

- treat ship traversability as a topology problem first
- use orthogonal (4-direction) adjacency by default
- do not count diagonal-only contact as connected
- keep static layout validation separate from future dynamic blockers like doors, hazards, or time effects

## Repo Map

- `scenes/main/`
  - root scene setup
- `scenes/battle/`
  - battle scene composition and battle HUD
- `scenes/ships/`
  - reusable ship board/tile scenes
- `scripts/Battle/`
  - battle state, selection, intents, battle scene script
- `scripts/Ships/`
  - ship/grid/room/tile runtime state and factories
- `scripts/Crew/`
  - crew runtime state
- `scripts/Data/`
  - resource-backed data definitions
- `data/ships/`
  - current ship layout assets

## Editing Guidance

- Keep patches focused.
- Do not rewrite unrelated systems just because you see a cleaner architecture in the distance.
- Avoid introducing service locators, event buses, command frameworks, or other heavy infrastructure unless the repo genuinely needs them.
- Preserve battle-scene usability while iterating.
- If a change affects both simulation and rendering, stabilize the simulation-side shape first and keep the view thin.

## Validation

After code changes:

- run `dotnet build "Tides of Time.csproj" -nologo`
- if you changed battle board presentation or input, sanity-check the current battle scene behavior if possible

## Response Expectations

When reporting work back:

- list changed files first when that is helpful
- summarize what changed in plain language
- call out assumptions explicitly
- mention risks or likely follow-up work briefly
- avoid suggesting large refactors unless they were requested or are clearly necessary

## Style Notes

- Prefer explicit `TidesOfTime.*` namespaces in code.
- Keep names concrete and game-facing where useful: `Room`, `Crew`, `Ship`, `Battle`, `Intent`, `Selection`.
- Comments should be sparse and helpful, not decorative.

## When Unsure

Choose the smaller, cleaner step that:

- preserves the simulation-first direction
- keeps battle-level interpretation in `BattleState` / battle code
- keeps `ShipGridView` as view/input
- leaves room for later boarding, movement, and time-distortion work

In short: build the next plank, not the whole fleet.
