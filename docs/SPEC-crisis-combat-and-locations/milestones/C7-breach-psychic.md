# C7: operations II (breach, psychic assault)

Crisis milestone C7 (project plan §2). Built 2026-11-22; not yet checked in game (checkpoint 2).

## What's there

- **Breach** and **Psychic Assault** are registered with the crisis, so it offers them from stage 2 (picked by weight
  until C11), and Dev Mode **Crisis: fire operation** can fire them.
- Both use the standard shape from C6: their share of the budget (75%) walks in from the edge and attacks; after 40%
  losses it regroups at its entry point, and comes back once with the reserve (25%, by drop pod); after 60% it leaves.
  Each has its own letter.
- **Breach** opens with vanilla's breaching assault (`LordToil_AssaultColonyBreaching`) instead of a plain assault:
  pawns with building-destroying weapons cut a path to the colony and the rest escort them through. The second
  assault, after the regroup, is the normal crisis assault.
- **Breachers** now carry **frag grenades or EMP grenades** (each one gets one of the two). Frag grenades make vanilla
  count them as breachers (`ai_IsBuildingDestroyer`), and they open the walls. Those with EMP grenades throw them at the
  nearest working colony turret within 30 cells they can hit (`JobGiver_RcEmpTurret`, inserted into every humanlike
  think tree like the psyker node, at most every 120 ticks). This replaces C3's "EMP grenades only": EMP grenades
  can't breach walls in vanilla.
- **Mixes** (`ForceMix`, crisis spec §5.3.1):
  - Breach: Praetorians 6, Breachers 3, a Ward and a Skip Adept, a Herald from stage 3.
  - Psychic Assault: Praetorians 6, Mind 2 (from stage 3), Control 2, Ward 1, Skip 1, so it's about as many psykers
    as Praetorians. Before stage 3 the Mind Adepts' places go to the others.
- Psykers keep casting under vanilla's breaching duties too, since their think node isn't tied to a duty (C4).

## Tests

`OperationTests`: the psychic assault leaves out Mind Adepts before stage 3 and follows the spec's ratio after; the
breach has its breachers and a Herald from stage 3; the breach opens with a breaching phase and still retreats after
losses; every operation's mix has Praetorians.

## In-game checks (for checkpoint 2)

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: fire operation** → Breach → stage 2, against a walled colony with turrets. | "Imperial breach" letter; grenadiers blast through a wall and the rest follow; EMP grenades land on turrets, which stop. |
| 2 | Hurt it badly (40%). | It falls back to where it came in, and a few minutes later reinforcements drop in and it attacks again. |
| 3 | **Crisis: fire operation** → Psychic Assault → stage 2, then stage 3. | "Imperial psychic assault" letter; about as many adepts as Praetorians; stage 2 has no Mind Adepts, stage 3 does. |
| 4 | Watch with **Crisis: psyker debug on/off**. | Adepts cast throughout, including during the breach. |
