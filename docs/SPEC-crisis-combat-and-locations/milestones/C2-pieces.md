# C2: piece library

Crisis milestone C2 (project plan §2). Built 2026-11-22; not yet checked in game (checkpoint 1).

## What's there

| Where | What |
| --- | --- |
| `Defs/PrefabDefs/RimChronicles_Pieces.xml` | The five starter pieces: sandbag line (5×1), sandbag corner (3×3), mortar pit (5×5), turret nest (5×5, needs power), barracks (7×6 room, three beds, a torch) |
| `src/RimChronicles.Core/Crisis/Pieces/` | `PieceSpec` (a piece at one rotation: a kind per cell, door sides, a text picture), `PieceRotation` (vanilla's prefab placement restated on plain data), `PieceCheck` (fits its size and doors at every rotation) |
| `src/RimChronicles.Game/Crisis/Pieces/` | `PieceExtension` (the `DefModExtension`), `PieceLibrary` (loads and checks every piece after the defs load; a broken one is logged and left out), `PieceStamper` (built, or as a faction's blueprints; materials from a `StuffPalette`), `PieceDevActions` |
| `tests/…/PieceTests.cs`, `TestSupport/PieceXml.cs` | Reads the pieces file without RimWorld and checks every piece at every rotation; rotation against vanilla's rules; the checks themselves |

## Decisions and findings

- **A piece is a vanilla `PrefabDef` with a `PieceExtension`** (tech spec R2). Positions and rects count from the
  south-west corner, unrotated. Things made from stuff leave it out; the stamper picks it from a palette.
- **Rotation mirrors vanilla exactly.** `PieceRotation` restates `PrefabUtility.GetRoot`, `GetAdjustedLocalPosition`
  and `GetAdjustedThingLocalPosition`, and `GenAdj.OccupiedRect` / `AdjustForRotation` (an even-sized thing shifts one
  cell, depending on the rotation). The planner's footprint is the game's footprint.
- **Vanilla's prefab blueprint mode always places the player's blueprints** (`PrefabUtility.SpawnThing` passes
  `Faction.OfPlayer`). `PieceStamper.Blueprints` places them itself for the given faction, all or nothing, at the cells
  `PrefabUtility.GetThings` gives.
- **Thing sizes in the tests** come from a table in `PieceXml.Footprints`, copied from the game's XML. A piece using a
  def that isn't in the table fails the tests until it's added. `FootprintsMatchTheGame` re-checks the table against
  `/workspaces/reference/data` when it's there (the codespace), and passes quietly in CI.
- **The mortar pit's mortar sits against the east sandbags:** a 2×2 mortar can't be centred in a 3×3 interior. Its
  crew cell (one south) is open.
- **Not checked yet:** interaction cells (a mortar's crew spot) at every rotation; doors on walls made of the chosen
  stuff; power for the turret nest. Checkpoint 1 covers them.

## Dev Mode

Under **Rim Chronicles**:

- **Crisis: turn pieces:** turns the next stamp a quarter turn clockwise.
- **Crisis: stamp piece:** pick a piece, then click; the click is the footprint's south-west corner. It also compares
  what spawned with the planner's footprint and says if they differ.
- **Crisis: stamp piece as Imperial blueprints:** the same, as Empire blueprints (or any enemy faction's).
- **Crisis: check pieces:** every piece with its picture, and any left out with why.
