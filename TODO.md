# TWR Trainer — to do

Tower circuit simulator (instructor / student). Name provisional: **TWR Trainer** (other candidate: Circuit Trainer).
Repository **ATCRoom/TWR-Trainer** (own repository, option B, decided 2 Nov 2026). Work rules in `CLAUDE.md`.

## Status

- **2 Nov 2026, step 1 (go of the user): structure of the repository.** Copied from ATCRoom/AuroraPAR-v2 `master`
  commit `408abbb` (without history): the engine `Sim/Engine` + `Sim/SelfTest` with `Earth.cs`, the maps library
  `MapCreator/Core`, ATCRoom Map Creator `MapCreator/App`, `MapCreator/SelfTest`, `docs/MAP-FORMAT.md`, `LICENSE`
  (MIT of the original author, kept). Workflow: builds, the two self-tests, Map Creator as an artifact; no release yet.
  The TWR Trainer program itself is not there yet. Next: the ground layout in the map format and the Map Creator
  (see the decisions below), then the engine (circuits), then the program.
- **Fixed rule (decision 2 Nov): the home of ATCRoom Map Creator and of the maps library is THIS repository.** Before
  every new release of AuroraPAR v2 Trainer (ATCRoom/AuroraPAR-v2), copy `MapCreator/` from here into AuroraPAR-v2.
  A fix of the engine (`Sim/Engine`) found here or there is copied by hand to the other when it applies.

Below: the notes written in AuroraPAR-v2 (`TODO-TowerSim.md`, 30 Oct - 2 Nov) with all the decisions taken so far.
Notes that speak of "the branch `tower`", "waits for the merge" or of a downloads repository / token are history:
the decisions marked DECIDED / CORRECTION further down replace them.

---

(Original first lines, 30 Oct:) First step: answer the questions below and make a few mock-ups, no code.

## Starting point: what already exists (AuroraPAR v2 Trainer, repository ATCRoom/AuroraPAR-v2, branch `trainer`)

Two libraries without WPF, already separate projects (net9.0) — the natural base:

- **`Sim/Engine` = ATCRoom.Sim** (`SimEngine.cs`, `SimAircraft.cs`, `PerformanceClass.cs`, `LocalPlane.cs`,
  `MetarWind.cs`; links the root `Earth.cs`) with its self-test `Sim/SelfTest` (run by the CI). Flight model in a local
  plane around a reference runway: heading / altitude / speed / rate commands with pilot reaction, turn rates, wind and
  gusts, pause (all or one aircraft), ROUTE (waypoints with altitude and speed, Direct to, Next WPT) and MANUAL modes,
  "Slow dwn to final", performance classes editable by the user (`performance.json`: Light, Turboprop, Jet; min /
  final / initial / max speeds...). The "manual mode" and the "pre-set routes" of this project already exist in it as
  MANUAL / ROUTE; missing: circuit legs built from the runway, landing / touch-and-go / go-around, helicopters (hover,
  slow speeds), ground.
- **`MapCreator/Core` = ATCRoom.Maps** (open map format `docs/MAP-FORMAT.md`, one file per airport, categories
  incl. ATZ / CTR / VRP / FIX; importers IVAO / Aurora, EuroScope `.ese` / `.sct`, KML / KMZ, AIP PDF tables) with
  its self-test, and the program **ATCRoom Map Creator** (`MapCreator/App`). Maps: personal use only, never
  distributed.

WPF parts of the Trainer (folder `Trainer/`, compiled with the root files of AuroraPAR v2 under `#if TRAINER`), to
reuse by copy or by moving them into a shared library (to decide):

- **Top view** `PlanView.cs` (+ `.Labels.cs`, `.RouteEdit.cs`): maps by airport and category with symbols, range
  rings, runway and centreline, tracks with history dots, labels (drag, hide, larger label with clickable / wheel
  fields), CENTRE modes (FREE, MOUSE, AIRPORT, TRAFFIC), routes drawn and edited by click / drag, points snapped to
  VRP / FIX. `MapLibrary.cs`, `MapsWindow.cs`, `CustomSymbols.cs`.
- **Instructor station** `InstructorWindow*.cs` (root `InstructorWindow.cs` + `Trainer/InstructorWindow.*.cs`):
  floating panels (`FloatingPanel.cs`), flight plans and scenarios (`FlightPlans.cs`, `.Fpl.cs`), TRAFFIC LIST, aircraft
  control, commands on the label, keys / macro keyboard (`.Keys.cs`), performance table (`PerformanceWindow.cs`), QNH
  and METAR from the internet (`.Qnh.cs`, `OnlineMetar.cs`). `InstructorBridge.cs` (root): engine → traffic, the
  "Aurora-like" positions.
- **Lesson between two PCs** `LessonLink.cs`, `LessonWindow.cs`, `MainWindow.Lesson.cs`: public MQTT relays
  (`CoordinationLink.Brokers` in `Coordination.cs`), channel from a lesson code, JSON messages both ways.
- Build and release pattern: `.github/workflows/build.yml` (self-tests, single-file publish, releases with their own
  tag prefix and "not latest"), own icon linked as `AuroraPAR.ico`, portable settings file.
- Docs and tutorials style: `docs/TRAINER.md`, `docs/tutorials/`.

## Questions to answer first (before any code)

1. **Name** of the program and **repository**: a new repository in the ATCRoom organisation, or a new folder / branch
   in ATCRoom/AuroraPAR-v2?
2. **Sharing with the Trainer:** move ATCRoom.Sim / ATCRoom.Maps (and the top view) into a shared place that both
   use, or copy them into the new project (simpler, but two copies to keep)?
3. **What the student sees** exactly: the top view of the aerodrome as in Aurora (which map categories, labels, tags),
   anything else (strips, wind, QNH, ATIS)?
4. **The instructor's "two levels"**: what each one shows (mock-ups first).
5. **Circuit routes**: built automatically from the runway in use (left / right circuit, circuit height, downwind
   distance) or drawn by the instructor per aerodrome? Which legs and actions (joining, downwind, base, final,
   touch-and-go, full stop, go-around, departure, orbit, extend downwind, hold)?
6. **Aircraft database**: which types / classes (aeroplanes slow and fast, helicopters), which data, how the user adds
   types (the performance table of the Trainer is a start).
7. **IVAO**: is the simulated traffic ever mixed with IVAO traffic (as in the Trainer: only between instructor and
   student, never on the network)? Does it work without Aurora (as the Trainer)?

## Answers of the user (1 Nov 2026) and decisions

- **Name:** undecided; working name "Tower Trainer". A trainer for the tower, specialised on **traffic in flight**; ground is secondary (almost absent).
- **Ground layout (minimum):** the aerodromes also get a small ground layout (proposal: runway outlines, taxiway centrelines with names, aprons, holding points; no stands or signs). Needs a new map category "Ground" in the map format and an importer in the Map Creator; samples wanted from the user (IVAO `[TAXIWAY]` / `[GATES]` / `[RUNWAY]`, or KML) of a small and a medium aerodrome.
- **Student screen:** top view as proposed, plus a box with the **METAR line** (full raw line; needed for simulations not connected to IVAO). Strips / ATIS later.
- **Instructor two levels:** as proposed (close view of the aerodrome + wide view ~20-30 NM). Mock-ups first.
- **Circuits are built from the runway in use; every distance, height and speed is set by the user, per aircraft class, saved in files.** Heights per class, editable.
  - **Long final:** like an ILS, distance set by the user, default **10 NM**.
  - **Initial Point (IP):** set by the user, default **3 NM** (on the extended centreline).
  - **Conventional (rectangular) circuit.**
  - **Overhead pattern** (Jet Circuit in Italy, "run and break" in the UK): from the IP, heights and speeds by aircraft type; the **break can be anywhere**: default **mid-runway**, or forced at the **beginning** or at the **end** of the runway, or at another point ("standby for break" then "break now"); the break turn is much tighter than a standard turn (higher bank); the distance from the runway depends on the speed of execution and does **not exceed 1 NM**.
  - **Close pattern:** same width as the jet circuit; like the overhead pattern without the initial-break part. From the downwind a 180° turn to final; from a go-around a 180° turn straight to the downwind (no base / cross leg).
  - **Normal pattern / outside downwind:** a wider circuit outside the published ones, to reposition for a new jet circuit or a final; as a conventional circuit made wider (external), or through VRP points (VRP later).
  - **SFO (simulated flame-out):** starts over the aerodrome at a variable height (by performance), engine at idle, a **spiral** to the runway threshold; either side; the low-key point is variable, like a downwind (heading about 180° to the runway); speed = **best glide** of the class; ends in a landing **or** a go-around.
  - **Re-insertion** (wide ↔ close): re-joining the wide circuit is to go back to the IP or to "decompress" the ATC work; re-joining the close circuit avoids flying the whole circuit and passing the IP again.
  - **Actions:** touch-and-go, full stop, go-around, departure, orbit, extend downwind, hold (manual).
- **Helicopters:** conventional circuit, direct landing on a helipad.

## Still open (asked 1 Nov)

Best-glide speed and descent per class (proposal below), where the helipad is (a point on the map?), how "another point" for the break is given (click on the map?), samples for the ground layout, the name.

## Samples of the ground layout (1 Nov 2026, LIBN; the data is not kept in the repository)

Two files for the same aerodrome, made by the user (same data in two forms):
- **`.pol` (filled polygons, 416 lines):** blocks that start with a header `STATIC;TYPE;1;TYPE;` (TYPE = GRASS, TAXIWAY, APRON, CONCRETE, BUILDING, RUNWAY; meaning of the `1` not known), a `//name` comment before the header, then one vertex per row `lat;lon;` (dotted DMS with thousandths, as the other Aurora files); a block ends at a blank line, a comment or the next header; the polygon is not repeated at the end (closed by the reader). Content: 1 boundary (GRASS), 4 TAXIWAY polygons (DFAB, E, I, G), 4 APRON, 2 CONCRETE (run-up areas), 3 BUILDING, 1 RUNWAY.
- **`.geo` (lines, 684 lines):** the same polygons as segments `lat;lon;lat;lon;TYPE;` (the segment tags are BUILDING also for the boundary and the concrete areas, so the tag is NOT the category: the section comments are), plus what the `.pol` does not have: **taxiway centrelines** (TAXI_CENTER, 12 paths: DFA, runup, B, E, oscar...) and **stop lines** (STOPLINE, 4: C, B, A, RU); the STAND section is empty.
- **Quirks:** the `.geo` has two rectangles both named 14/32 (about 1840 x 56 m and about 2140 x 62 m; the `.pol` has only the longer one); centreline names like `Esplit`, `E_out`, `M_in_split` are technical, not taxiway labels. (Corrected after the KML: the "jumps" in the TAXIWAY polygons are long straight edges of the taxiways, not errors.)
- **KML of the same aerodrome (1 Nov 2026, `LIBN_Lecce_Galatina_AB.kml`, 55 KB, Google Earth, coordinates `lon,lat,0`, no descriptions or extended data; not kept in the repository):** it is the source of the other two. **Folders = categories:** Ad_Boundaries (LineString), Ad_Concrete_Areas (apron and run-up polygons), Aprons (empty), Buildings, Twys (4 polygons), Ad_Grass_Holes (empty), Rwys, Rwy_Boundaries, TwyCtrls (12 LineStrings), Stopbars (4), Stands (empty). It answers the runway question: **Rwys = the paved rectangle (2144 x 60 m), Rwy_Boundaries = the runway proper (1836 x 57 m, = the 1830 m of `runways.par`)**; the long one extends past both ends (overruns). **TwyCtrls also holds 21 Point placemarks `A_C`, `B_C`, `F_C`... = the taxiway label points** (the letter + `_C`; letters repeat where the label is shown more than once; `RUNUP_W` / `RUNUP_E`); the `.pol` / `.geo` do not have them. The `.pol` order of the blocks is a draw order (boundary, taxiways, aprons, concrete, buildings, runway): keep it. A preview made from the KML (scratchpad) looks right: boundary, taxiway ring, aprons, buildings, runway, centrelines.
- **Proposal (to confirm):** category "Ground" with parts runway, taxiway, apron, concrete, building, boundary (areas with outline + light fill) and taxiway centreline, stop line (lines); each on / off with its colour; the Map Creator reads either file or both (the polygons of the `.geo` are rebuilt by joining the chained segments); shown only when zoomed in. Taxiway labels off for now.

**Decided 1 Nov (the user): the Map Creator reads all the formats for the ground layout: KML / KMZ, `.pol` and `.geo`** (also together for the same aerodrome, merged into the category Ground). Defaults proposed, to confirm: taxiway labels off at first (KML label points kept in the file); apron recognised by name or folder also when it is in the concrete folder; the category of each block chosen by the user in the preview with the suggestion from the folder / section / header name. Still no code: it waits for the go.

**Taxiway and apron labels: file `.txi` (1 Nov 2026, `libn.txi`, 22 rows, not kept in the repository; the user: "use this format").** One row per label: `TEXT;ICAO;lat;lon;`, no header, no comments. Coordinates in the same file in two spellings: compact `N0401404403` (= N 040° 14′ 04.403″, no dots) and dotted `N040.14.31.724`. It is the same data as the KML label points (`A_C` = row `A` to the 4th decimal), refined: where the KML has one point `M_C`, `O_C`, `T_C`, the `.txi` has two (`M west` / `M east`, `O north` / `O south`, `T west` / `T east`); the same text repeats where the label is shown more than once (`A` x2, `E` x2, `F` x3, `RUNUP` x2). One file for taxiways and aprons together (letters of aprons such as M, O, T, N, I, L are mixed with the taxiway letters A-G): **one label category "Ground labels"**, not two (proposal). **Quirks:** the ICAO field of a row can be wrong (`LINB` for LIBN, row `M east`): the airport comes from the file name (`libn.txi`) and a different one is only reported in the preview; typo in a text (`O notrh`); a text with a suffix (`M west`): proposal = the label shows the first word (`M`) and the suffix stays as a note (to confirm). Labels on by default, visible only when zoomed in (the earlier "off at first" is replaced). Format of the ground layout therefore: areas (`.pol` / KML), lines (`.geo` / KML) and labels (`.txi` / KML points).

**Coordinate formats that every importer must read (1 Nov 2026, the user, from the IVAO description "Coordinates Format"):** (1) DMS with 14 characters, zero padded: `N038.10.04.627;E024.33.34.904;`; (2) DMS without separators, 11 characters: `N0381004627;E0243334904;`; (3) decimal, at most 13 characters, no letters, south and west negative with a minus: `38.02235785;24.12223578;`; (4) reference point: a FIX / VOR / NDB name instead of the coordinates, `NAME;NAME;`. IVAO warns against the abbreviated forms with or without delimiter (`S040`, `N038.10`, `E024.33.34`, `N03810`, `E0243334`): unpredictable, usually truncated results. **State of the code** (`MapCreator/Core/MapCoordinates.cs`, read only): (1), (2) and (3) are read (also lowercase letters, 4 decimals of the seconds, EuroScope `N013.32861`); **missing:** (4) the lookup of the name in the FIX / VOR / NDB files read together (planned with the STAR step, unknown names listed in the preview, not guessed); the **abbreviated forms are not refused**: `N038.10` is read as 38.10 degrees and `S040` as 40 degrees without a warning, and the plain decimal accepts a latitude above 90 (the limit is 180 for both). **Decided 1 Nov (the user): the abbreviated forms are ACCEPTED too, all of them (ignore the IVAO warning):** a missing part counts as zero: `S040` = 40°, `N038.10` = 38°10′, `E024.33.34` = 24°33′34″, `N03810` = 38°10′, `E0243334` = 24°33′34″ (the digits tell the parts: latitude DD, DDMM, DDMMSS, longitude DDD, DDDMM, DDDMMSS, optional decimals of the last part). The only ambiguity left: a letter and one dot (`N038.10`) is degrees and minutes in the IVAO / Aurora files, a decimal in the EuroScope files (`.ese`, `.sct`, `N013.32861`): the reader knows the kind of file. The plain decimal checks ±90 for the latitude. KML has its own decimal `lon,lat,alt` (already read by `KmlImport.cs`, not yet checked on this sample).

**Decided 1 Nov (the user), evening:** (1) labels of the ground layout are shown **exactly as written** in the file (`M west` in full, no shortening); (2) best glide and the SFO descent: provisional values per class, **tested with various speeds** (values editable in `performance.json`); (3) the helipad is a **point on the map** chosen by the instructor; (4) the break at "another point" is given by a **click on the map** ("standby for break" then "break now" from the label). Next step: mock-ups (student view, instructor level 1 and level 2) with the ground of LIBN.

**Mock-ups made 1 Nov evening (scratchpad `mock/`: `student.png`, `instructor.png`, `label.png`; invented traffic and values; LIBN ground from the KML and the `.txi`).** Student: top view with the ground, extended centreline with ticks every NM, rings 1 / 2 NM, tags, METAR line. Instructor: level 1 (the aerodrome, with the circuits drawn), level 2 (wide area 30 NM, IP, long final), side panel (selected aircraft, circuit, actions, traffic list). Label with commands (like the Instructor+ of the Trainer): larger label of the selected aircraft with fields NEXT, CCT, LEG, BRK; click = list, wheel = change, right click = menu; overhead: [STBY] and [BREAK NOW]. **Decided 1 Nov (the user): build BOTH the commands on the label and the side panel; after the tests the user chooses whether to keep both or only one** (so the code of the commands must be shared by the two, not written twice). Seen in the mock-ups: the ground is small at the scale of a circuit, so ground and labels only when zoomed in (wheel zoom as in the Trainer).

**Decided 1 Nov (the user): ONE Map Creator for all the programs** (Trainer, tower simulator, later ones): one source (`MapCreator/App` + `MapCreator/Core` = ATCRoom.Maps), one open format (`docs/MAP-FORMAT.md`), no copy for the tower. Consequences, to design before any code: (1) every new thing of the format must be **backward compatible**: read in `MapCreator/Core/MapFile.cs` (read only): the file has Lines, Labels and Points, each with a `Category`, `CurrentFormatVersion` = 2; **a category a reader does not know becomes "" (empty)** (`MapCategory.Normal(...) ?? ""`), so a Trainer 1.0.0 that opens a file with the new Ground category could show those items as uncategorised: to check on the Trainer's drawing before the format changes; (2) the ground needs **areas with fill** (taxiway, apron, concrete, building, grass, runway), which the format does not have (only lines, labels, points): a new optional element (and the draw order of the `.pol`), so older readers ignore it; the category list (`MapCategory.All`) gets Ground and Ground labels; (3) the Map Creator is built and shipped from the same source in each package; the Trainer 1.0.0 on `trainer` is not touched, the changes live on `tower` and reach the Trainer with its next release.

**Decided 2 Nov (the user): the maps folder is COMMON to all the programs** (option 1): e.g. `%AppData%\ATCRoom\Maps`, or a `Maps` folder beside the program in portable mode; the Map Creator button becomes "Save into the maps" (today "Save into the maps of the Trainer", folder `%AppData%\AuroraPARTrainer\Maps`, see `MapCreator/App/MainWindow.cs`; "Open an existing map" the same); the Trainer reads the new folder and also the old one, so the maps already made are not lost (in its next release, not in 1.0.0). **Tower view, cleaner screen (the user, 2 Nov): some maps are not shown — MVA, CTR and FIX** (proposal: off by default in the tower, still switchable in its Maps window; the other categories as the user sets them: ground, ground labels, ATZ, VRP, airports, boundaries). **Confirmed 2 Nov (the user): MVA, CTR and FIX stay in the Maps window of the tower, OFF by default.**

**CORRECTION 2 Nov: the Trainer 1.0.0 IS PUBLISHED** (release `trainer-v1.0.0` of 31 Oct 20:00 UTC, with AuroraPAR v2 2.0.2 one second later; `trainer` is merged into `master`; the site has `trainer.html`). The notes above that say "waits for the merge" or "not published" (and the first line of this file) were written from the old `TODO.md` of the branch and are out of date. The branch `tower` is now rebased on `master`.

**Decided 2 Nov (the user): ATCRoom Map Creator is a SEPARATE download (its own release), not inside the zip of each program.** What it changes (the Trainer 1.0.0 zip, published, DOES contain `ATCRoomMapCreator.exe`, workflow on `master`): a new Trainer release (1.0.1 or 1.1.0) without it; the work (written for the situation before the release, to read as a list of what to change): (1) workflow `build.yml`: the Trainer zip loses `ATCRoomMapCreator.exe` (lines 75-76, 88, 136) and the release notes "with ATCRoom Map Creator" (143); a new step publishes the Map Creator release with its own tag (e.g. `mapcreator-v<Version>`, "not latest", like the Trainer); (2) the Trainer: the "Open ATCRoom Map Creator" button of the Maps window (`Trainer/MapsWindow.cs`, enabled only if `ATCRoomMapCreator.exe` is beside the program, `MapLibrary.MapCreatorPath`) should also look in the folder where the Map Creator is (and, when it is missing, say where to download it); (3) docs: `docs/TRAINER.md`, tutorial 5 ("installed with the Trainer"), `docs/trainer.html` ("with ATCRoom Map Creator"), README, a download link on the site. **Same trap as the tower:** AuroraPAR v2 skips only the tags that begin with `trainer-` (`UpdateCheck.cs` line 80): a `mapcreator-` tag, like a `tower-` one, would look like a new version to its users (and the Trainer looks only at `trainer-v…`): the check must accept only tags `v<number>`. **2.0.2 was published WITHOUT this fix** (`UpdateCheck.cs` on `master`, line 80, still skips only `trainer-`), and the installed 2.0.0 / 2.0.1 / 2.0.2 cannot be changed: every release of this repository with a tag that is not `v…` or `trainer-…` shows "new version" to their users. So **no `mapcreator-` or `tower-` release in this repository** (or: only after an AuroraPAR v2 release with the fix is out, and even then the installed older ones would warn). Proposal: **the Map Creator and the tower in their own repositories of ATCRoom** (the AuroraPAR v2 check looks only at its own repository), which also answers question 1 of this file differently from the first proposal. All of this touches the workflow: to do only with the user's go.

**DECIDED 2 Nov 10:26 (the user), replaces "Map Creator as a separate download" above:**
- **1b: ONE Map Creator (one source), shipped INSIDE the zip of each program** (Trainer and tower). The Trainer 1.0.0 stays as it is: no new Trainer release now; the new things of the Map Creator (ground, common maps folder) reach the Trainer with its next release. **Before the first tower release:** a Map Creator must never lose what it does not know: when it opens a map of a newer format or with unknown categories / elements, it keeps them on save (or refuses to overwrite the file with a clear message), because two copies of different versions may be on the same PC and both use the common maps folder.
- **2c: the code stays in ATCRoom/AuroraPAR-v2; the releases of the tower are published by the CI in a separate repository of ATCRoom used only for the downloads** (so the update check of the installed AuroraPAR v2 2.0.0-2.0.2, which skips only `trainer-` tags, never sees them). To prepare before the first tower release: the downloads repository (name with the program name), a token in the Secrets of AuroraPAR-v2 that can publish there (to renew; the user creates it), the tower's own update check looking at that repository, the site page. The issues of the users will arrive on the downloads repository.

**DECIDED 2 Nov 10:40 (the user), replaces 2c above: option B, its own repository.** No token, no downloads repository, no change to the update check of AuroraPAR v2. **Name (provisional): "TWR Trainer"** (the other candidate was "Circuit Trainer"); repository proposed `ATCRoom/TWR-Trainer`, created empty by the user (Claude cannot create repositories). Layout: copies of the engine (`Sim/Engine` + self-test, will diverge for the tower), of the maps library and the Map Creator (`MapCreator/Core`, `/App`, `/SelfTest`), of the plan view and the few base files (`Earth.cs`, coordinate parser...), adapted; own workflow, own releases (tags `v…` of that repository), own update check, own `CLAUDE.md` and TODO (this file moves there). **The home of the Map Creator becomes the TWR Trainer repository** (one Map Creator for all, decision 1b still valid: shipped in each zip); AuroraPAR-v2 keeps its copy as it is and **before every new Trainer release the Map Creator / maps library are copied from TWR Trainer into AuroraPAR-v2** (a fixed step of the publishing rules). A bug found in the engine of one is fixed in the other by hand when it applies.

## The idea

A simulation environment for training **tower control of traffic in the circuit**: the **instructor** flies the aircraft, the **student** controls them as a tower controller.

- **Base (decided 13 Oct):** the shared engine library of the Instructor (*ATCRoom Sim*: flight model, scenarios, instructor controls, top view, link, open data format) + its own tower view; **not** the PAR radar views. Originally: possibly the same foundation as AuroraPAR and AuroraPAR Instructor (WPF program, test traffic flight model, instructor/student link through the coordination relay with a session code, same PC / two PCs / over the network).
- **Views:**
  - **Student:** only the view "from above", as Aurora for IVAO (radar-like top view of the aerodrome and its circuit).
  - **Instructor:** views on **two levels**, for a wider picture (e.g. the same top view plus a wider area / a second level of detail — to be defined).
- **Aircraft database:** aeroplanes and **helicopters**, **slow and fast** (performance per type: speeds in the circuit, climb / descent rates, turn rates; helicopters with their own manoeuvres such as hover).
- **Pre-set routes:** each aircraft starts with a **pre-set route** (e.g. joining, downwind, base, final, touch-and-go, departure, circuit...) and flies it by itself.
- **Go around key** (idea from the PAR test traffic, not needed there): the aircraft climbs away on the missed approach / circuit.
- **Manual mode:** when needed the instructor **switches an aircraft to manual** and flies it to do manoeuvres not in the pre-set route (orbit, extend downwind, go-around, hold a position, ...), then back to a route.

## To decide when we start (older list, merged into the questions above)

- Name of the project and its repository (probably in the dedicated GitHub organisation, see TODO.md).
- What is shared with AuroraPAR (code library or copy) and what is new.
- What the student sees exactly (labels, data from Aurora-like tags, aerodrome map, runways and circuit drawing).
- How the instructor's two levels look (mockups first).
- The aircraft database: which types, which data, how the user can add types.
- Pre-set routes: how they are defined and edited (per aerodrome and runway in use).
- Whether the simulated traffic is ever mixed with IVAO traffic (as for the PAR Instructor: only between instructor and student, never on the network).

## Step 2 (ground in the map format and the Map Creator) — TESTED OK 2 Nov (all 10 checks of the user), build of `dev` 41444447654

- **Map format version 3:** block `Ground` (Areas, Lines, Labels) with ten ground categories (`GroundGrass`, `GroundTaxiway`,
  `GroundApron`, `GroundConcrete`, `GroundBuilding`, `GroundRunway` = areas drawn in this order; `GroundRunwayOutline`,
  `GroundCentreline`, `GroundStopBar` = lines; `GroundLabels`). In memory the ground items are in Lines / Labels with their
  categories; only the file keeps them apart, so AuroraPAR v2 Trainer 1.0.0 and Map Creator 1.x neither see nor spoil them
  (Map Creator 1.8 keeps unknown top-level fields when it merges into an existing file). A file without ground stays
  version 2. **Nothing lost:** unknown categories are kept with their name (before: emptied), unknown fields of the file,
  lines, labels, points and of the Ground block are written back.
- **Coordinates:** all the IVAO short forms (IVAO / Aurora files; `N038.10` = 38°10′, but more than two digits after the
  dot = decimal degrees as before, `N041.00850773`), EuroScope letter + one dot stays decimal.
- **Known, accepted (review 2 Nov):** a KML part with label points put into the ground (TwyCtrls with `A_C`) also
  replaces the ground labels already saved (e.g. from a `.txi`): the confirmation says "Replaced: Ground: labels".
  Opening a map again: categories of a newer program are not shown (a note says so) and stay in the file with "Save
  into the maps", not with "Save as...".
- **Importers:** `.pol` (new), `.txi` (new), `.geo` ground tags suggested, KML folders → ground categories (aprons among
  the concrete areas a part of their own; no automatic labels of area names in the ground; point labels cut at the
  underscore). Into a ground category points and labels become ground labels; the ground-labels category takes no lines.
- **Map Creator 2.0.0:** shared maps folder `%AppData%\ATCRoom\Maps` (portable: `Maps` beside it when
  `AuroraPARTrainer.settings.json` or `TWRTrainer.settings.json` is beside it — the name of the TWR Trainer settings
  file is a guess, to keep when the program is written); "Save into the maps"; "Open an existing map" lists the shared
  folder and the old one of the Trainer 1.0.0; the preview fills the ground areas. Trainer 1.0.0 users: "Save as..."
  into `%AppData%\AuroraPARTrainer\Maps`.
- Not done: reference points `NAME;NAME;` (needs the fix lookup, STAR step); the ground in the Trainer (its next release).
- **To test (checklist after the build):** drop the LIBN KML, `.pol`, `.geo`, `.txi` together; check the suggestions,
  the preview (areas filled, runway on top, labels `A`, `M west`), save, open it again, the "Open an existing map" list.

- **KML taxiway / apron labels (decided 2 Nov, the user):** divisions may write them in different ways; when the name of
  a KML label point has an **underscore, what follows it is not part of the name** and is dropped (`A_C` → `A`,
  `RUNUP_W` → `RUNUP`). The `.txi` labels stay exactly as written (`M west`).

## Step 3a (circuits in the engine) — WRITTEN 2 Nov (go of the user), to test with the build

- **Circuits built from the runway** (`Sim/Engine/SimEngine.Circuit.cs`): conventional, close, wide; left or right;
  heights above the threshold; the route points named by their leg (UPWIND, CROSSWIND, DOWNWIND, BASE, FINAL, INITIAL,
  GO AROUND, JOIN, DEPARTURE; ROLL, LANDED, MANUAL), shown as LEG. The close pattern's two 180° turns are commanded
  turns (opposite heading, towards the circuit side, at the rate that makes them as wide as its downwind), not points
  (review 2 Nov: as points a jet overshot and circled).
- **Starts:** long final (10 NM, `LongFinalNM`), initial point (3 NM, `InitialPointNM`), downwind, runway (take-off).
- **End of the final (`EndAction`):** full stop, touch and go (roll 3 s, lift-off at the final speed), go-around (at
  200 ft on the short final, within 0.7 NM: so a conventional circuit goes around right at 200 ft), low pass (about 100 ft over the runway), departure (touch and go, then
  straight out at circuit height). Commands: `SetCircuit` (re-join close / wide / another circuit from where it is),
  `SetEndAction`, `GoAround`; a heading or the stick takes the aircraft out of the circuit (MANUAL) until it is given again.
- **The final:** the descent is steered to a point 0.15 NM past the threshold (low pass: to 100 ft), unless the
  instructor gave an altitude; approach speed beyond 4 NM, final speed within.
- **Class values (performance.json, provisional):** CircuitHeightFt, CircuitSpeedKt, DownwindNM, CloseDownwindNM (≤ 1 NM),
  WideDownwindNM, CircuitBankDegrees; missing in an old file: the values of the class name.
- **Self-test:** 10 circuits flown and checked; `circuits.html` (artifact "circuits-of-the-self-test") draws them.
- Known, minor: an instructor heading given during a 180° turn of the close pattern is overridden at the end of the
  turn; a runway of length 0 never counts a landing (the tower always gives the length).
- **Step 3b next:** overhead pattern with the break, SFO, helicopter.

## Corrections before step 3b (asked 2 Nov, from the user's pictures of the overhead / jet circuit) — WRITTEN 2 Nov (go), to check on circuits.html

- **Names of the legs of the close pattern:** no CROSSWIND and no BASE (they do not exist there): during the 180° turn
  from the upwind the leg is DOWNWIND, during the 180° turn to the final it is FINAL.
- **Turn anticipation** at the corners of the conventional and wide circuits (start turning before the point), so the
  downwind starts parallel instead of wide (seen in circuits.html).
- **Clearer circuits.html:** runway vertical (final from the bottom), a colour per leg, the leg name in the middle of the
  leg, a legend (the labels at the start of each leg confused the user).
- **Roll out (F/A-18 kneeboard "Overhead break / FLP", given by the user):** per class distance and height of the roll
  out on final after the 180° turn: Jet 1 NM / 450 ft, Turboprop 0.75 NM / 350 ft, Light 0.5 NM / 250 ft (provisional);
  the downwind goes on past the threshold to the turn point.
- **Waveoff in the jet circuits (close, overhead):** above 400 ft it turns to the downwind at once while climbing to
  circuit height (not at the runway end).
- **Crosswind correction of the downwind:** 0.1 NM per 10 kt of crosswind component (wider when the wind blows towards
  the runway, narrower when away).
- **Close / jet downwind: at most 1.5 NM** (decided by the user 2 Nov: 1 to 1.5 NM, even 2 NM happens with a gentle turn
  or a high speed; stay within 1.5). Defaults: Light 0.6, Turboprop 0.8, **Jet 1.2** (the user, 2 Nov).
- Kneeboard also: initial 3-5 NM on the runway track at circuit height (1500 ft), 300-400 kt; break over the first half
  of the runway or as soon as practical, a level turn tight enough to meet the abeam distance (for step 3b).
- Written as: `RoutePoint.Turn180` marks the ends of the close pattern's 180° turns (legs DOWNWIND and FINAL); the close
  upwind ends 2 NM past the runway, the turn to the downwind starts above 400 ft on the runway track (take-off, touch and
  go, go-around); the downwind of the close pattern goes on to abeam the roll out (`RollOutNM`, `RollOutHeightFt`);
  turn anticipation `radius × tan(turn / 2)` + half the roll-in; width corrected by the mean wind.

## Close / overhead corrections and overhead pattern (step 3b, part 1) — WRITTEN 2 Nov (go of the user)

- **180° turns on a circle** (close, overhead): centre and radius fixed at the start of the turn (from where it starts to
  the downwind distance, or to the runway track), centre moved ahead by the roll-in; each step the rate makes it follow
  the circle (tighter outside, wider inside, within the bank limit): the downwind comes parallel at the class distance
  whatever the speed (the jet accelerating after take-off / go-around made a "drop" shape before) and the wind. In the
  turns the aircraft slows down at three times the class deceleration (power back, speed brakes). A heading or the stick
  during a turn takes it out of the circuit.
- **Into the close pattern (the user, 2 Nov): only after a go-around (or the take-off), or from outside the circuits.**
  `SetCircuit(Close)` to an aircraft already flying another circuit is kept as the next circuit (`NextCircuit`, for the
  label) and flown after the next go-around, touch and go or low pass; from outside (no circuit, MANUAL) it joins at once.
  The wide circuit still joins at once (to make room). On the final or rolling, any new circuit is flown after it.
- **Overhead pattern** (`CircuitType.Overhead`): from the initial point (3 NM, `InitialPointNM`) along the runway at the
  class's initial height and speed (`OverheadHeightFt` 1500, `OverheadSpeedKt` Light 110 / Turboprop 220 / Jet 300), the
  break with the class's break bank (`BreakBankDegrees` 45 / 60 / 75) on the circle to the jet-circuit downwind, the
  downwind at circuit height, the 180° turn to the final (roll out). `SetBreak`: middle (default), beginning, end, a
  point along the runway (click), standby; `BreakNow` (only on the initial). After a go-around or touch and go: the close
  pattern. `SetCircuit(Overhead)`: back to the initial point out on the wide downwind (on the final: after the go-around).
- **circuits.html:** the re-join test replaced by realistic ones (close pattern ordered on the final → after the
  go-around; arriving from outside → at once); overhead pictures (break middle / end / beginning / standby + break now /
  touch and go); labels on the outer side, moved so they do not cover each other.
- Known, low: arriving from outside abeam the runway end, the join may make a turn towards the runway before settling
  on the downwind (review 2 Nov).
- **Join from outside (the user, 2 Nov: "it must turn earlier and be on the close downwind with the runway reciprocal"):**
  a 45° entry from the outside: to a point 1 NM outside the downwind and 1 NM before its start, then onto the downwind at
  its start (both turns anticipated; the joins of the overhead's way back to the initial point too).
- **Orbits on the downwind (asked 2 Nov, conventional, overhead and close):** `Orbit(callsign, right?)`: a 360° turn on a
  circle (circuit bank), by default away from the runway (the outside of the circuit), then on along the leg from there;
  leg ORBIT. Not on the final, not during another turn.

## Aerodrome circuits, SFO, helicopter (step 3b, part 2) — WRITTEN 2 Nov (the user's "Ok"), to check on circuits.html

- **File of the circuits of an aerodrome (the user, 2 Nov: "every aerodrome has its circuit"):** `AerodromeCircuits`,
  one JSON file per aerodrome named with its ICAO code (e.g. `LIBN.json`; where it lives is decided with the program):
  `Runways` [{`Runway`, `Side`}] = the side of the circuit of each runway (the default of every circuit of that runway;
  the instructor still changes it in flight with `SetCircuit`), `Classes` [{`Class`, `CircuitHeightFt`,
  `InitialHeightFt`, `BreakHeightFt`, `HighKeyFt`, `LowKeyFt`}] = the heights of the classes that fly there differently
  (missing: the class's from `performance.json`; break missing: the circuit height of the file). Unknown fields kept.
  Example: `{ "Airport": "LIXX", "Runways": [ { "Runway": "14", "Side": "Right" } ], "Classes": [ { "Class": "Jet",
  "CircuitHeightFt": 2000, "InitialHeightFt": 2500, "BreakHeightFt": 2000 } ] }`. Engine: `SetAerodrome(file, runway)`,
  `DefaultSide`; `AddInCircuit` side null = the aerodrome's.
- **Break height:** the overhead descends along the initial so it is at `BreakHeightFt` at the break (standby: it stays
  at the initial height and the break turn descends); the downwind at circuit height.
- **SFO** (`CircuitType.Sfo`, start `CircuitStart.HighKey` 1 NM before it): High Key over the runway (a third of the way
  along it) at its height and the best glide speed, a 180° turn to the Low Key abeam (width of the close pattern), the
  downwind gliding down, the 180° turn to the final, landing or go-around (then the close pattern). Provisional values:
  High Key Light 2500 / Turboprop 4000 / Jet 7000 ft, Low Key 1200 / 2000 / 3500 ft, best glide 70 / 125 / 180 kt
  (`HighKeyFt`, `LowKeyFt`, `BestGlideKt` in `performance.json`). `SetCircuit(Sfo)`: back to the High Key the way of
  the overhead. Known: with the jet the Low Key comes higher than 3500 (a 180° on 1.2 NM is short for 3500 ft) — to
  look at on circuits.html.
- **Helicopter** (4th default class `Helicopter`, `CanHover`): circuit 700 ft, 70 kt, downwind 0.8 NM;
  `SetHelipad(callsign, x, y)` = the pad given by the instructor (a point on the map): the final goes there, slowing
  down, a hover at about 30 ft over it, then down to the ground (landed, removed as on the runway); with a pad the end
  is always a full stop. Below 20 kt a helicopter flies over the ground (no drift; the hover holds its place).
- Review 2 Nov (independent, read-only): no compile errors found; fixed: the helicopter stops on the pad (not 45 m
  before), an aircraft arriving from outside gets the aerodrome heights too. Known, low: a `performance.json` written
  by an earlier build has no Helicopter class (delete it to get the 4 defaults); the SFO jet/turboprop Low Key comes
  higher than the class value (values to tune).
- **Side changed in flight (the user, 2 Nov: "it does not leave the circuit unless the controller orders it"):** in the
  circuit an S over the aerodrome (across the runway at circuit height) onto the new downwind; on the base or the final a
  go-around, then the new downwind; on the upwind / initial / before the High Key the circuit simply goes on to the new
  side. (The first version with the 45° join from outside is replaced.)
  Corrected the same evening from the user's drawing: the S turns towards the runway, crosses it over the aerodrome
  towards its far end, then turns onto the new downwind at its start (from a point on its line 0.5 NM before it); the
  go-around from the final turns the same way onto the new downwind. The self-test fails on a loop (270°+ one way).
- **SFO more compact (the user, 2 Nov, from a flameout-pattern picture: "at the end of the turn to the final it is
  nearly on the runway"):** High Key over the middle of the runway, the 180° to the Low Key, a short downwind along the
  runway, the turn to the final starting about at the threshold (0.2 NM before it), rolling out nearly on the runway
  (about 120 ft). In the SFO the descent limit is 1.5 × the class's (idle, speed brakes).

## Step 4a (the program: the instructor's window) — WRITTEN 3 Nov (go of the user)

- `Trainer/` (TWRTrainer.exe, version 0.1.0, code-only WPF): the top view (map in the engine's plane, runway in use,
  extended centreline with a tick every NM, aircraft with labels "callsign / height above the runway in hundreds of ft
  and ground speed / leg, circuit and side, end action", trails, the route still to fly of the selected one; wheel zoom,
  drag, click to select; F1 level 1 ~7 NM, F2 level 2 ~40 NM) and the side panel: open map (shared maps folder),
  runway in use, a new runway placed with two clicks (threshold, end) and its elevation (the same runway the other way
  made at once), its circuit side, the heights of a class at this aerodrome, METAR wind, pause, speed x1/x2/x4, long
  final and IP distances, map layers (MVA, CTR, FIX off by default), a new aircraft, the commands of the selected one
  (circuit and side, end action, break middle/beginning/end/standby/point on the map/BREAK NOW, orbit, go around,
  helipad on the map, remove). The commands are in one class (`TowerCommands`) for the label of step 4b.
- The aerodrome file (`%AppData%\ATCRoom\TWRTrainer\Aerodromes\<ICAO>.json`) now also holds where each runway is
  (threshold, end, elevation).
- The build artifact `TWRTrainer-win-x64` holds TWRTrainer.exe and ATCRoomMapCreator.exe.
- Review 3 Nov (independent, read-only): no compile errors found; fixed: the other way of a runway placed again
  follows it, a point is taken on a click (the map can be dragged meanwhile), unexpected errors are said and the
  program goes on, the saved runway missing → the first, the layers saved as those turned off, lines without a
  category drawn, hints in the empty view, the elevation required. Later: an icon.
- **A side for each circuit (the user, 3 Nov: "every circuit has its own side: conventional right, jet circuit left, SFO
  too"):** in the aerodrome file each runway has `ConventionalSide` (conventional, wide), `JetSide` (overhead, close),
  `SfoSide` (missing: the runway's `Side`). The program: three choices under the runway; a new aircraft with
  "Aerodrome's" gets its circuit's side; for the selected one, another circuit chosen sets the side of that circuit.
  The SFO after a touch and go / go-around flies the close pattern on the jet side.
- **SFO: to the High Key by a climbing spiral, and hold (the user, 3 Nov):** the SFO ordered in flight, or again after a
  go-around / touch and go (above 400 ft), goes straight to the High Key: it climbs on a circle over the field (half the
  width of the close pattern as radius, the turns to the SFO side) whose centre moves to the circle of the High Key (the
  same circle as the turn to the Low Key); over the High Key, along the runway, at its height, the SFO starts (leg CLIMB).
  `HoldAtHighKey(callsign, true)`: it circles there at the High Key height until `false` ("Start SFO") (leg HOLD); given
  before the High Key it goes to the circle at once; a new aircraft at the High Key can start on hold. The wide loop out
  to the initial point is gone for the SFO.
  Corrected after the user's pictures (3 Nov: loops and figures of eight, the hold up to 2 NM away): the circle is
  fixed from the start (over the High Key, on the SFO side) and the aircraft is steered by heading along it, turned
  towards it the more the farther it is (no loops); the SFO starts only on the circle over the High Key. Self-test:
  the hold within 0.2 NM of the circle, the climb never turning back, the final aligned.

## Heights of each circuit and step 4b (the commands on the label) — WRITTEN 3 Nov (the user: "one build with everything")

- **A height for each circuit (the user, 3 Nov: "every circuit its height: a jet the conventional at 2000, a turboprop
  at 1500; the wide can differ from the conventional"):** the class has `ConventionalHeightFt`, `WideHeightFt`,
  `CloseHeightFt` (missing: `CircuitHeightFt`); the aerodrome file per class too (`CircuitHeightFt` of the first files:
  for all of them). Close: also the overhead's downwind after the break and the close pattern after an SFO; the break
  missing: the close height. An aircraft given another circuit in flight flies it at its height. The panel: seven boxes
  with their names (Conventional, Wide, Close, Initial, Break, High Key, Low Key).
- **Step 4b, the commands on the label (mock-up of 1 Nov):** the selected aircraft has a larger label with its fields:
  CCT (click: the list of the circuits; the side: the aerodrome's for that circuit), L/R (click: the other side, the S),
  NEXT (the end of the final: click: list, wheel: the next one), BRK (overhead: list with "point on the map", wheel),
  the buttons BREAK NOW (overhead), HOLD / START (SFO), ORB, G/A. Right click on any aircraft: the menu of all its
  commands (also helipad and remove). The same commands as the panel (`TowerCommands`). The wheel does not change the
  circuit (each notch would be an order to fly another circuit).
- **Buttons instead of drop-down lists (the user, 3 Nov: "too many drop-down menus, better buttons, also chained"):**
  the panel's new aircraft and selected aircraft are rows of buttons (the value now in yellow); the selected one's are
  orders at once and can be chained (CLS then R). The label: the circuit field is `Ⓒ` (the user's choice; CCT and the
  turning arrow were not liked); a click on Ⓒ, NEXT or BRK opens a row of buttons under the label; the wheel on Ⓒ
  shows the circuit with "?" and orders it when the wheel stops (1.2 s). The right-click menu is kept.

## From the user's tests of the program (3 Nov afternoon) — NOTED, to do in the next build (the user: "do nothing for now")

- **New aircraft: start IP sets the circuit to overhead by itself** (and HK → SFO), so the buttons do not show a circuit
  that will not be flown.
- **The route drawn for the selected aircraft is not what it flies** (it flies correctly): the dashed line joins the
  raw points of the route: the overhead's INITIAL point far beyond the runway (the break comes before it), the ends of
  the 180° turns joined straight, the close pattern's upwind point 2 NM out. To draw instead the shape it will fly
  (the turns as arcs, the break where it will be, only the points that are flown to).
- **"Extend downwind" button missing** (asked by the user; it was never in the list). **Decided (the user, 3 Nov: A):**
  "Extend downwind": on the downwind it goes on beyond the base turn (straight, at circuit height) until "Turn base"
  (a second button), then it turns to the base / to the final from there. Both on the label and in the panel.
- **To review absolutely (the user, 3 Nov, picture): the way to the initial point from the wide downwind.** Now it
  makes two "figures of eight" to reposition (the route: a point 1 NM beyond the IP out on the wide downwind, then a
  point on the runway track 1 NM before the IP, then the initial: turns of 90° and more between close points). Wanted:
  it simply flies straight to the IP and turns towards the aerodrome (onto the runway track, the turn anticipated so
  it rolls out on the initial at the IP). Same for the SFO's way back if it is ever used, and a self-test with the
  track drawn (no turn of more than ~180° in all before the initial).
- **Movable labels (the user, 3 Nov):** each label can be dragged with the mouse (left button on the label, not on
  its buttons) to where it is wanted around its aircraft; the offset is kept for that aircraft (it follows it), the
  leader line from the symbol to the label. The selected label too. (A drag on the empty map still moves the map.)
- **SFO ordered as the next circuit starts at once (the user, 3 Nov):** now `SetCircuit(Sfo)` to an aircraft in a
  circuit starts the spiral at once, leaving the circuit. Wanted: it finishes the circuit it is flying, then (after
  the touch and go / go-around / low pass) climbs to the High Key; shown as the next circuit (">SFO" on the label).
  From outside the circuits (arriving, flown by the instructor) at once. (To ask: the overhead the same way?)
- **Heading by drag and drop on the map (the user, 3 Nov), besides the heading written:** drag from the aircraft's
  heading line (its tip) or from its symbol: while dragging, a line from the aircraft to the mouse with the heading
  (and the side of the turn); on release the heading is given (the aircraft leaves its circuit: MANUAL, group 4 "out
  of the circuit"; REJOIN brings it back). Esc while dragging: nothing given. (A drag elsewhere still moves the map,
  a drag on a label moves the label.)
- **Panel in four groups (proposed 3 Nov, mock-up; two questions open: all circuit changes "from the next circuit" in
  group 2 with the immediate ones only in group 3? the label the same groups or kept compact?):** 1 new aircraft;
  2 next circuit (circuit, side, break: from the next touch and go / go-around; the end of the final: this one);
  3 now in the circuit (extend downwind, turn base, orbit, go around, break now, hold / start SFO, side now, wide now);
  4 out of the circuit (heading, altitude, speed written or dragged; rejoin, depart now, helipad, remove).

## Build of 3 Nov afternoon (the user: "I prefer to test with a different UI: make a build") — WRITTEN

All the notes above in one build. The two open questions decided by default (to confirm by the user): all circuit
changes of group 2 are "from the next circuit" (the immediate ones, side now and wide now, are in group 3); the label
stays compact (Ⓒ now sets the next circuit). Engine: `SetNextCircuit` (in a circuit: after the next touch and go /
go-around; outside: joined now), `ExtendDownwind` / `TurnBase` (leg EXT DOWNWIND), the way to the IP straight to it
(out to the wide downwind first only from the runway track ahead of the IP). Program: the panel in four framed groups,
IP ↔ overhead and HK ↔ SFO linked in the new aircraft, heading / height / speed boxes and REJOIN, the heading dragged
from the aircraft's symbol (Esc cancels), movable labels, the route drawn as it is flown (half circles, the initial to
the break), EXT DWN and BASE on the label. Headings are true (no magnetic variation yet).
- **Aerodrome and Simulation sections collapsible (the user, 3 Nov, picture of the panel):** each one closes to its
  title bar (state remembered between starts), with a one-line summary on the bar when closed (aerodrome: map, runway
  in use, wind; simulation: running / paused, speed). Set once, then closed: the traffic commands stay in view.
- **Advanced traffic generation, entry gates (the user, 3 Nov: aircraft must not start on the downwind / IP / HK but
  at least 5 NM out, entering the ATZ from entry gates; "an advanced generation module as in the PAR trainer").**
  Proposed: gates in the aerodrome file (a VRP of the map or a clicked point, name, warned when nearer than 5 NM);
  from the gate (and optional points) the aircraft joins the circuit by itself (45° to the downwind; overhead: to the
  IP; SFO: spiral to the HK; or straight-in long final); quick generation from a gate; advanced as the PAR trainer
  (flight plans, duplicate, traffic list with ▶ / start all, scenarios saved per aerodrome). Questions asked (arrival
  height and speed per gate or per class; points after the gate; delayed starts in scenarios; keep the start already
  in the circuit for tests).
  **Answers of the user (3 Nov, 17:05):** (1) arrival height and speed **per class** (the gate gives only the place);
  (2) after the gate it goes to the circuit planned: **IP, downwind, HK, and also a direct entry to the base** (and
  straight-in to the final, as the long final today); (3) **delayed starts yes**, and the instructor can still pause /
  hold them; (4) the **take-off** too says what it does after: **DEP** (it leaves) or it joins the circuits, **only the
  conventional or the wide** (no IP or HK after a take-off); (5) **continuous orbits** until cleared to go on:
  proposed: a button ORBIT ∞ (and Shift+ORBIT) that turns into CONTINUE (yellow, outlined) while it orbits; the label
  shows ORBITS and a CONT button.
- **Immediate orders during an orbit (the user, 3 Nov):** EXTEND DWN and TURN BASE are ignored during an orbit (and
  during the 180° turns of the jet circuits), so they seem to wait for its end. Agreed: TURN BASE ends the orbit and
  turns to the base from there; EXTEND DWN ends the orbit on the downwind heading and goes on extended.
- **SFO: on hold at the High Key by default (the user, 3 Nov):** every SFO (next circuit, ordered now, a new aircraft
  to the HK) reaches the High Key and circles there (HOLD) until START SFO; the checkbox "Hold at the High Key" of
  the new aircraft goes away; HOLD HK stays to stop one already started towards the HK.
- **GO AROUND only on the final (the user, 3 Nov):** today it works on every leg: on the downwind it turns towards the
  aerodrome to go back to the upwind. It must work only on the final (base included? to confirm); on the other legs
  the button does nothing (greyed, with a short message). New command **BREAKUP** to leave the circuits from any leg:
  proposed: a turn away from the runway (outwards) to a heading 90° off the leg, at circuit height, then MANUAL (the
  instructor vectors it; REJOIN brings it back). To confirm with the user.
  **Confirmed by the user (3 Nov, 18:02):** GO AROUND (the button) in the conventional and the wide only on the final
  (base and downwind: grey); in the overhead, the close pattern and the SFO from the start of the turn to the final on.
  It stops descending (keeps its altitude), goes on to the threshold along the final, then keeps the runway heading
  until a new order (it does not rejoin by itself). Only the button: the end action G/A (automatic at 200 ft) stays as
  it is (it climbs and flies the circuit again). BREAKUP: on the downwind out of the circuit, turning away from the
  runway (90°), at circuit height, then MANUAL; on the base it goes on turning the same way past the final heading and
  stops on the reciprocal of the base heading (runway 36, left circuit: base 090 → 360 → 270), then MANUAL.

## Build of 3 Nov evening (the user: "let's try it this way") — WRITTEN

Collapsible Aerodrome / Simulation (summary on the bar, state remembered); entry gates in the aerodrome file (ADD GATE
by a click, a VRP gives its name, warned under 5 NM; drawn as triangles); group 1: From (gates, RWY, IN CCT for
tests) × Entry (DW, BASE, IP, HK, FINAL) × circuit × side × class × end, ADD NOW / TO PLANS; flight plans (▶, ⏸ hold,
⧉ duplicate with the next callsign, ✕, START ALL with delays, scenarios saved / loaded in `Scenarios`); arrival height
and speed per class (`ArrivalHeightFt`, `ArrivalSpeedKt`: default 500 ft above the circuit, the circuit speed); the
take-off only into the conventional or the wide; SFO on hold at the HK by default; ORBIT ∞ / CONTINUE (Shift+ORBIT),
EXTEND DWN and TURN BASE end an orbit; GO AROUND only on the final (level, runway heading, waiting); BREAKUP; group 3
by the order of the circuit with grey buttons. DEP after a take-off: straight out as before (an exit gate later?).
- **TESTED OK by the user (3 Nov, 20:24): "tutto ok"** (the build of 3 Nov evening, run 41682399498).
- **Bug (the user, 3 Nov): the gate row is too wide, REMOVE is hidden** (label column 130 px + box + two buttons in a
  horizontal row that does not wrap). Fix: the name box on its row, ADD GATE and REMOVE on the row below (a wrap panel).
- **Detachable, resizable panels (the user, 3 Nov, for the next build):** the panel split into its parts (Aerodrome,
  Simulation, New aircraft with the flight plans, Selected aircraft): each one can be taken out of the column and
  float over the map, dragged by its bar and resized from its edges, as the floating panels of the PAR trainer
  (`Trainer/FloatingPanel.cs` of AuroraPAR-v2: ▴/▾ closes it to its bar, double click on the bar puts it back in the
  column; place, size and open/closed remembered). To ask: floating over the map (as the PAR trainer) or separate
  windows (e.g. on a second screen)?
- **Long final as a start of its own (the user, 3 Nov: not IN CCT + FINAL):** in "From" a button LONG FIN (on the final
  at the long final distance, on the glide path) beside the gates and RWY; with it (and with RWY) the Entry row is
  grey (it does not apply). IN CCT stays for the tests (DW, IP, HK); its FINAL goes away.

## Build of 3 Nov night (the user: "procedi con la build") — WRITTEN

The panel in four parts (Aerodrome, Simulation, New aircraft, Selected aircraft), each one in the column or taken
out (⇱) to float over the map: moved by its bar, resized by its corner, ⇲ or a double click back into the column, ▴/▾
closed to its bar, a summary on the bar; place / size / state remembered (`Parts` in the settings). Floating over
the map (the PAR trainer's way; the question "or separate windows" left open: by default over the map). The gate row
on two rows (REMOVE GATE visible). From: gates, LONG FIN (its own start), RWY, CIRC (was IN CCT; its BASE / FINAL
grey); Entry grey with LONG FIN and RWY. Tooltips on every button, choice and box.
- **Arrival height (the user, 3 Nov: option 2):** a box "Height ft" in the new aircraft (saved in its flight plan):
  the height above the runway it arrives at, at the gate; empty: 500 ft above its circuit. It climbs or descends to the
  circuit height before reaching the circuit (along the join).
- **Crossing traffic (the user, 3 Nov: aircraft crossing the zone that do not land):** in New aircraft, Type ARRIVAL /
  TRANSIT; a crossing goes from the gate From to the gate To, DIRECT or OVERHEAD (over the middle of the runway), at its
  Height ft (empty: 500 ft above the highest circuit of its class), never landing, gone at once at the exit gate (the
  user's answer). Orbits (until continue) also for it (the user: yes); a heading: MANUAL; REJOIN: back onto its way.
  In the flight plans and scenarios too. Leg TRANSIT. A crossing given a circuit (next circuit, rejoin of a circuit)
  becomes an arrival.
- **METAR (the user, 3 Nov; done, build of 3 Nov night):** a Get METAR button beside the box: the newest METAR of the map's ICAO code
  from aviationweather.gov (as OnlineMetar in the PAR Trainer; only when pressed; no internet / no METAR: a message);
  it fills the box and applies the wind. The whole METAR always shown in a line at the top of the map (wrapping). A
  flag "Wind + QNH only": the line shows only code, time, wind and QNH (the box keeps it whole); remembered. The QNH
  only shown (heights are above the field). VRB: a direction changing slowly instead of 000°.
- **Names (the user, 3 Nov; done):** in the plain label CCT becomes CONV (as in the selected label), with a space
  before the side: `FINAL CONV R T/G` (CLS L, WIDE R...). From CIRC becomes PTRN (CIRC could be read as circling; old
  CIRC / IN CCT plans still load). Sides stay L/R (arrows would read as directions).
- **Traffic List (the user, 3 Nov; done):** the Flight plans part renamed Traffic List (as in the PAR Trainer),
  dockable / floating and resizable like the other parts, its place remembered.
- **Height and speed from the label (3 Nov; done as A+B, the proposal, the user not having chosen yet):** the fields ALT and SPD: wheel ±100 ft / ±10 kt (ordered 0.6 s after it stops), click: AUTO and the values; the
  aircraft stays in its circuit; the value given until AUTO, a REJOIN or a new circuit; shown as `015↑020`.
- **Circling (the user, 3 Nov; done):** aerodromes with the approach on one runway only: from the long final of the
  opposite runway, down to the conventional circuit height (the same heights of each class), aside by 45° onto the
  conventional downwind of the runway in use, then the conventional circuit. New aircraft: Entry CIRCLING, only with
  From LONG FIN (the circuit CONV). Leg CIRCLING until the downwind. Also in the Traffic List and scenarios.
- **Traffic List commands (the user, 3 Nov; done 4 Nov):** ADD NOW also puts the aircraft in the Traffic List (a row
  flying; back to waiting when it is gone). Each row: ▶ start; ⏸ hold before the start / freeze the aircraft flying
  (again: on); ⧉ duplicate; ✕ the row and its aircraft (no confirmation). At the bottom: RUN / PAUSE (the whole
  simulation, also kept in Simulation), START ALL, REMOVE ALL (only the aircraft: the rows back to waiting; also kept in
  Simulation), SAVE..., LOAD..., CLEAR ALL (only the rows). A row restarted: an automatic callsign gets a new free one,
  a callsign written is used again.
- **Messages in their panels (the user, 3 Nov; done 4 Nov):** one message line per part instead of the one at the bottom of
  the column: commands to the aircraft (label, heading drag, buttons) in Selected aircraft; ADD NOW / TO LIST errors in
  New aircraft; starts, errors, scenarios in Traffic List; map, runway, gates, heights, METAR in Aerodrome. A part
  closed to its bar: the message for a few seconds in its bar. The note "Maps: %AppData%..." at the bottom of Aerodrome.
- **Formations (the user, 4 Nov; done):** one aircraft simulated (the leader, a larger symbol, label `VIPER01 F4`)
  carrying up to 3 wingmen (callsigns after its number, reserved: VIPER02 leads 03, 04). The overhead splits only at
  the break, the close pattern at its 180° turn to the downwind, the SFO at the High Key (by themselves); the long final,
  the conventional and the wide circuit: SPLIT at any time. At the split each wingman goes straight on for its share of
  the interval (0/3/5/10 s, default 3), then its circuit (the leader's). On the short final (200 ft) the members chosen
  (LAND, any of them; label toggles and wheel, also in flight) land together (from the long final all, from a circuit at
  most 2), the others go around alone into the close pattern (CLS by default), one interval apart. New aircraft:
  Formation 1-4, Land, Split s; Traffic List: GROUP the rows ticked (before the flight). Later idea: rendezvous (joining
  aircraft already flying into a formation).
- **The empty column (the user, 4 Nov; done):** all the parts floating: the column on the right hidden, the map on the
  whole width of the window; a part docked again (⇲, double click on its bar): the column back.
- **Split s renamed (the user, 4 Nov; done 4 Nov, values 3, 4, 5, 10: "with 3 seconds they are already close", no 2):** the row "Split s" of New aircraft becomes "Interval s" (the seconds
  between one aircraft and the next when the formation splits), with the values 2, 3, 4, 5, 10 (no 0: they would turn
  on top of each other); 3 by default.
- **Split aircraft in the Traffic List (the user, 4 Nov; done):** a formation's plan shows each of its aircraft once
  split, in a row of its own under it (⏸ freeze, ✕ remove that one); the plan's ✕ removes all of them; it waits again
  only when all of them are gone.
- **Traffic List as in the PAR trainer (4 Nov; done):** two lists,
  FLYING (a row per aircraft: callsign, height, speed, leg; II/▶, ↺ its plan again, ✕) and WAITING (the plans not
  started: ☐ ▶ ⏸ ⧉ ✕); thin one-line rows (11 pt, monospace), each list up to about 8 rows then it scrolls; ADD NOW only
  in FLYING (a plan only with TO LIST).
- **New aircraft simpler (the user, 4 Nov; done): "anyone must be able to set it up; I must think of nothing until I
  want to step in".** A plan is the whole flight: Circuits 1/2/3/4/∞ (each a touch and go but the last) and Last FS /
  DEP (LOW left out: what after it would be unclear); the instructor's end of the final (NEXT, At the end) ends the plan.
  The Land row out of New aircraft (all land; the rules decide; LAND stays in the label). Jet the default class. The
  crossing's To / Via only with TRANSIT, Interval s only for a formation; Callsign, Height ft, Delay min under More….
- **Spacing after the split (the user, 4 Nov; done):** "they keep the interval of the break down to the runway; in
  reality each one lengthens its downwind to be at least a third of the runway after the one before": each member
  after the split (and each one going around from a formation) moves the end of its downwind and its turn to the base /
  final further out, by what the interval does not give (a third of the runway at the final speed), plus a margin.
- **NEXT during a plan (the user, 4 Nov; done 4 Nov as A, the user not having chosen):** now a NEXT changed by the instructor
  (e.g. T/G → LOW) ends the plan: LOW on and on, no final FS. Proposed: A) only the circuit going on, then the plan
  again (T/G..., Last at the end; recommended), or B) every remaining middle circuit, the last one as Last. To decide.
- **Simulation speed with buttons (the user, 4 Nov; done):** x1 / x2 / x4 as a row of buttons (ChoiceRow) instead of
  the drop-down list.
- **REMOVE and the Traffic List (the user, 4 Nov; done with the two lists):** REMOVE (Selected aircraft, the right-click menu) removes the
  aircraft but its row stays in the Traffic List. With the two lists of the PAR trainer (FLYING / WAITING): the aircraft
  out of FLYING; its plan back to WAITING (a plan of TO LIST) or gone (an aircraft of ADD NOW).
- **Landing roll and runway exits (the user, 4 Nov; done; braking: a class value BrakingKtPerSecond, Light 3, Turboprop 3.5, Jet 4, Helicopter 3 kt/s):** a landed aircraft is not gone at once: it stays on the
  runway slowing down with its class's deceleration (performance.json) to an exit speed (about 15 kt) — no fixed 2/3
  of the runway ("a Light does not need all that runway"). No taxiways in the simulation, so: runway exits clicked once
  per aerodrome in Aerodrome (ADD EXIT, a name A, B, C..., on the runway edge; saved in the aerodrome file like the
  gates, backwards compatible), drawn as a small mark. Each aircraft takes the first exit it reaches at the exit speed,
  turns off across the runway edge, then it is gone; its label LANDED, its speed and EXIT B. No exits: it slows down
  with its class and is gone where it reaches the exit speed. Later: the exit chosen from the label ("EXIT C").
- **Formation landing separation (the user, 4 Nov; done: Aerodrome, row Formation FS, REDUCED RWY SEP / FULL RWY SEP):** only when both the one ahead and the one behind end with
  a full stop, the one behind lengthens its downwind further (with the braking it came too close). Two rules, chosen in
  Aerodrome (saved in the aerodrome file: it depends on the country):
  - **Reduced rwy separation** (default; "in a formation the next one can be cleared with the runway occupied"): when
    the one behind touches down, the one ahead has rolled at least a third of the runway, braking included (one that
    leaves before a third: its exit).
  - **Full rwy separation**: the one behind touches down only when the one ahead has left the runway at its exit.
  Otherwise (not both FS) the spacing of now. Self-tests: overhead, 2 jets FS, with each rule.
- **Exit speed (the user, 4 Nov; done, "facciamo una prova così"):** the landing roll brakes down to 30 kt (15 was
  "too little for the taxi"), keeps 30 kt along the runway, and near its exit slows down to 15 kt to turn off (braking of
  its class). The exit chosen: the first one it reaches at 30 kt. Self-test: Light still at B, Jet at C, 30 kt on the way
  and 15 kt at the exit.
- **EXP TAXI (the user, 4 Nov; done: label, Selected aircraft row Runway, right-click menu):** a button (label of a landed aircraft, Selected aircraft) "expedite taxi": it
  keeps at least 45 kt along the runway, then slows down near its exit to 15 kt to turn off. Its exit stays the
  nearest one ahead (the user: "deve prendere l'uscita più vicina comunque"): the speed adapted so as not to pass it
  (braking started in time with its class's braking; too close for 45 kt: less than 45, still the same exit).
- **The selected label again (the user, 4 Nov, after the mock-ups: "ora ci siamo"; done):** "it is counter-intuitive
  that what is chosen now is white and what is available yellow": as in the panel, the value chosen now yellow (filled),
  the others outlined; orders blue (grey: not where it is now). The callsign and the leg on top; a row per choice with its
  name small on the left: HEIGHT / SPEED (click: the values, AUTO; wheel), PATTERN, SIDE, BREAK (overhead), LAND
  (formation), END (with "N left"); under a line NOW: BREAK NOW / START SFO / HOLD / REJOIN when they apply, EXT DWN, BASE,
  ORB, G/A, SPLIT; landed: SPEED and EXP TAXI. No drop-down strip any more but for the height and the speed.
- **ORB on and on (the user, 4 Nov; done):** CONT could be read as CONV: the button stays ORB; Shift + click orbits on
  and on, shown with a thick border; a click again: out of the orbit. The panel's ORBIT ∞ the same (thick border).
- **Icon (the user, 4 Nov; done: Trainer/TWRTrainer.ico 256/64/48/32 with TWR, 24/16 without it, thicker; the exe and the window):** Trainer/TWRTrainer-icon.svg: the ATCRoom
  family tile, TWR in orange, the runway from above, the circuit dashed light blue, the white aircraft on the downwind.
  To do: TWRTrainer.ico (256, 48, 32, 16; matched in size to the other ATCRoom icons; 32 and 16 px with thicker strokes),
  ApplicationIcon in the project and the window icon.
- **The student's map (the user, 4 Nov; decided, for step 4c):** the student makes nothing: when the two PCs connect,
  the instructor's program sends the map (made with Map Creator) and the aerodrome file (runway in use, sides, heights,
  gates, exits, separation rule) over the link, for the session only: kept in memory, never saved on the student's PC
  ("it is like a remote view of my position"), so the rule "maps never distributed" holds. The extended centreline is
  drawn from the runway, as now.
- **Edit a plan (the user, 4 Nov; done):** a click on a row of WAITING loads all its values into New aircraft; TO LIST
  becomes UPDATE (the plan replaced in the same place of the list), ADD NOW still starts a copy at once, CANCEL (or a
  click on another row) leaves it as it was. Also for a plan back in WAITING after it flew.
- **Traffic List look (the user, 4 Nov; done):** the callsigns stand out (bold, a colour of their own, in a column of
  their own at the start of each row, FLYING and WAITING); the buttons at the bottom (PAUSE, START ALL, REMOVE ALL,
  GROUP, SAVE..., LOAD..., CLEAR ALL) on one row, wrapping onto more rows only when the part is narrow.
- **Buttons on mouse over (the user, 4 Nov; to do):** the buttons of the Traffic List (all of them) turn very light
  under the mouse and their text cannot be read: the default Windows template paints its own light hover colour under
  our light text. Fix: a template of our own for the action buttons (everywhere, not only the Traffic List): on mouse
  over a slightly lighter shade of the same blue, pressed a darker one, the text always readable; disabled greyed.
