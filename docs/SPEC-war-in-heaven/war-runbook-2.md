# War in Heaven checkpoint 2 runbook: summons, the Choir's crisis, its sites and the Cathedral (W6–W9)

One in-game session on the reference PC covers W6 to W9. Royalty and Biotech must be active. Setup as in
`war-runbook-1.md` (the `war-in-heaven` branch, Development mode, a new test colony). Write what you find in
`war-results-2.md`, or tell Claude Code. Each milestone adds its steps here as it is built.

## W6: the summons

| # | Do | Pass when |
| --- | --- | --- |
| W6.1 | Colony with a mechanitor colonist (install a mechlink) and two mechs it controls. **Empire: trigger awakening**, **Empire: end stirring now**, answer the envoy "Stand with the Empire" (or **War in Heaven: set side** → empire). Let a day pass (or **War in Heaven: send a summons now**) | A quest "The Empire's summons: {name}" is on offer, naming the colonist and what happens if you refuse |
| W6.2 | Accept it | An Imperial shuttle lands; the colonist can board; it leaves once they are aboard |
| W6.3 | Wait 5 days (or **War in Heaven: return the summoned colonist now**, then let an hour pass) | The colonist comes back by shuttle as a psycaster: level 1 with a basic mechlink (bandwidth 6–9), higher with more bandwidth; no mechlink; a letter says what they became |
| W6.4 | Look at the two mechs | Both deactivated where they stood (inspect: "Deactivated."), never feral, even after a few days |
| W6.5 | Select any colonist, right-click a deactivated mech | "Disassemble …"; the colonist takes it apart for steel and components |
| W6.6 | Another colony: side with the Choir (wake it first), with a psycaster colonist (psylink 3) | "The Choir's summons"; after 5 days the colonist returns by drop pod as a mechanitor with 14 bandwidth (mechlink 6 + Chorus tuning 8), no psylink and no psycasts |
| W6.7 | Another summons: decline the offer (or let it lapse) | "The colony stands alone" letter; **War in Heaven: show allegiance** says independent, cause `summons`; both powers hostile |
| W6.8 | Another summons: accept, but forbid the colonist from boarding until the shuttle leaves | The same: the shuttle leaves empty and the colony stands alone |
| W6.9 | Save and reload with a summons on offer, and with the colonist away | The quest, the colonist (still away) and their return all survive |
| W6.10 | **War in Heaven: convert the selected colonist** both ways on a test pawn | Matches the rule: psylink level = 1 + (own bandwidth − 6) / 4, up to 6; back the other way gives 6 + 4 × (level − 1) bandwidth |

## W7: the Choir's crisis

Setup: a colony of 6–8 armed colonists with a stone wall, 2–3 turrets and a few doors (as for the Imperial crisis).
**Hollow Choir: wake now**, wait for the envoy (or **War in Heaven: set side** → independent), and answer **Stand against
it**, so the Choir is hostile. Save as **"choir-test-1"** and fire each operation from that save. Psychically
sensitive colonists help for W7.6. **Choir crisis: fire operation** fires any operation at any stage it allows.

| # | Do | Pass when |
| --- | --- | --- |
| W7.1 | **Choir crisis: show state** before and after the Choir marches; let a few days pass | Stage none, then **probing** with a "Choir swarms" letter and a news line; the ChoirCrisis plot starts; "Decided by" names the director model with the E4B war preset |
| W7.2 | **Choir crisis: fire operation** → ProbingSwarm → stage 1 | A "Choir swarm" letter; light mechs with one mechanitor (a chorister) walk in, test the defenses, and leave after about two hours or a quarter lost; **show state** lists it with its outcome |
| W7.3 | Watch the chorister during W7.2 | It stays behind its mechs, on the far side from the colony, and falls back when a colonist gets within about 7 cells; it shoots from there |
| W7.4 | Down or kill that chorister mid-fight | "With {name} down, N of the Choir's mechs fall silent"; its mechs stop where they stand ("Deactivated."), never go feral; right-click one with any colonist: "Disassemble" works |
| W7.5 | TunnelBreach → stage 2 | A "Choir tunnel breach" letter; tunnels rumble beside the turrets (or walls) for about half a minute, then tunnelers come up; the rest walks in from the edge |
| W7.6 | TheHum → stage 2, then stage 3 | One resonator (two at stage 3) drops 18–34 cells from the colony; psychically sensitive colonists get **the hum** (slower work, a mood penalty; loud with high sensitivity), the psychically deaf don't; destroying the resonators ends it within a few seconds; a mixed force walks in behind it |
| W7.7 | Burrowing → stage 3, with the leader outdoors | Tunnels rumble 3–12 cells from the leader (or a protagonist); the force comes up and goes for them; it pulls out once they're down |
| W7.8 | Leader in a room under overhead mountain | No tunnel under the mountain: they come up outside it, or the operation can't start ("couldn't start here") |
| W7.9 | Capture a Choir mechanitor (**Choir crisis: spawn unit**, down it, take it prisoner), then Retrieval → stage 2 | Tunnels near the prisoner; the squad frees them and leaves with them |
| W7.10 | Stage 3 with a cantor in a raid (TunnelBreach at stage 3 brings one) | Within a minute of seeing a turret or colonists: "Cluster drop called", the landing spot marked 6–14 cells from the target, on open sky; pods land 6–10 s later and the mechs join the raid |
| W7.11 | A drop is called: down the cantor (or block its sight) before it lands | "The cluster drop was called off"; nothing lands |
| W7.12 | **Choir crisis: call cluster drop** on a cell | A marked drop lands there with no cantor needed |
| W7.13 | FinalChorus → stage 4 | "The final chorus" letter; the First Voice leads every class of mech (a boss mech: war queen, diabolus or apocriton); it doesn't retreat at any loss; downing the First Voice silences the mechs it commands |
| W7.14 | A colony at war with both powers (**War in Heaven: set side** → independent, both awake and marching): let 20–30 days pass at speed 3 | The Empire's and the Choir's operations never overlap: one waits while the other runs, and for half the gap after (**Choir crisis: show state**: "Taking turns") |
| W7.15 | Regroup: hurt a TunnelBreach force by about 40% | It falls back, then Choir reinforcements drop in and it comes again under its mechanitors |
| W7.16 | Save mid-operation (a pending drop, tunnels rumbling, a resonator humming), reload | The operation carries on: the drop lands or is called off, the tunnels open, the hum continues; downing a commander after the load still silences its mechs |
| W7.17 | **Choir crisis: decide now** with the E4B war preset, then with E2B | E4B: an `Operation:*` with source `Model` in **Plot: director log**; E2B: a weighted pick |

The cluster siege (Operation:ClusterSiege) is W8's: see below.

## W8: the Choir's sites and the cluster siege

Setup: "choir-test-1" from W7 (the Choir hostile, its crisis at stage 2–3; **Hollow Choir: expand now** a few times
for a Choir-held town near the colony). **Crisis: generate site** lists the Choir's archetypes beside the Empire's.

| # | Do | Pass when |
| --- | --- | --- |
| W8.1 | **Crisis: batch sites** | Each Choir archetype (ClusterOutpost, Resonator, SignalSpire, ChoirHolding) "50/50 pass on the first seed", 0 fallbacks, as the Imperial ones |
| W8.2 | **Crisis: generate site** → ClusterOutpost → seed 1 | A walled outpost in plasteel and steel, a command room with a comms console deepest in, mech charge-turret nests, Choir mechs and mechanitors guarding it; no Imperial piece (no records room, mortar pit or uplink) |
| W8.3 | Resonator → seed 2 | The resonator stands in a chamber deepest in; colonists who arrive and can feel it get **the hum**; destroy it: "Choir resonator destroyed"; **Choir crisis: show state** stops offering TheHum for 15 days |
| W8.4 | SignalSpire → seed 3 | The spire in a station deepest in; destroy it: "Choir signal spire destroyed"; back home, a Cantor's raid calls no cluster drops ("knocked out") and ClusterSiege isn't offered for 15 days |
| W8.5 | On any Choir site, down or kill a mechanitor | "With their mechanitor down, N of the Choir's mechs fall silent"; they stop where they stand and can be dismantled |
| W8.6 | The Choir plot's sabotage job (**Plot: advance beat** on the Choir plot while opposed and the crisis runs) | "Destroy the Choir cluster outpost / resonator / signal spire" or "Free {town}" (with a Choir-held town near); the quest's site is that archetype, and destroying its objective succeeds |
| W8.7 | Free a Choir-held town (ChoirHolding) | "{town} liberated": the town goes back to its old owner (world map colour too), goodwill rises, a news line |
| W8.8 | **Choir crisis: fire cluster siege with goal** → Goal:BreakWalls → stage 3 | A "Choir cluster siege" letter naming its aim; the force walks to the spot **Crisis: show siege scoring** marks BEST |
| W8.9 | Watch the camp grow (speed 3, about 7 in-game hours a stage) | Stage 1 lands at once (sandbags, shelters), then by pod: auto-mortars (shelling starts), a signal spire, a shield and a resonator (the hum begins at home), a drop beacon; a message for each; no stage before the last landed |
| W8.10 | Kill every mechanitor at the camp before stage 3 | The camp stops growing; with most of them down "lost most of its mechanitors… it's coming in" (or it leaves after heavy losses) |
| W8.11 | Let the spire land, then watch the cluster drops | Drops come with no cantor in sight, aimed per the goal (walls and turrets for BreakWalls), still marked 6–10 s first |
| W8.12 | Let the beacon land | More of the Choir drop in at the beacon a few hours later, up to 3 waves |
| W8.13 | Leave the siege alone for 2 days | "grown all it will: it's coming in" |
| W8.14 | Beat the siege back (half its force) | It retreats; what it dropped in stays, ownerless: the turrets don't fire, the resonator falls silent, the spire calls nothing |
| W8.15 | Save at camp stage 2–3 and reload | The camp carries on growing on its timer |
| W8.16 | Imperial checks still hold: **Crisis: generate site** → each Imperial archetype; **Crisis: fire siege with goal** | Imperial sites and camps never show a Choir piece (no mech turrets, no spire, no resonator) |
