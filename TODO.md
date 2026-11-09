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

## Open after the review of 6 Nov (SFO, QNH/QFE) — FIXED for the next build (the user, 7 Nov: "correggi nella prossima build")
- Fixed: an SFO plan of a list older than 7 Nov with no HK and a Height ft: that value is its High Key again; the way to the
  High Key sizes its turns on air speed + wind (the fastest over the ground); QNH / QFE: the altitudes written and not saved
  yet in Settings › Circuits are saved first.
- Traffic lists saved with the dev builds of 6 Nov morning: an SFO plan kept its High Key in "Height ft"; now it is read as the gate height. Only those lists; to load them right, move the value to HK altitude by hand.
- The way to the High Key (START SFO off its circle) sizes its turns on the ground speed: with strong wind they may be a little tight (margin 1.5).
- Switching QNH/QFE redraws Settings › Circuits from the saved values (edits not saved yet are lost).

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
- **1b: ONE Map Creator (one source)** (CHANGED 9 Nov, the user: a SEPARATE download, its releases in a new repository ATCRoom/MapCreator; not in the zips any more), (Trainer and tower). The Trainer 1.0.0 stays as it is: no new Trainer release now; the new things of the Map Creator (ground, common maps folder) reach the Trainer with its next release. **Before the first tower release:** a Map Creator must never lose what it does not know: when it opens a map of a newer format or with unknown categories / elements, it keeps them on save (or refuses to overwrite the file with a clear message), because two copies of different versions may be on the same PC and both use the common maps folder.
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
- The build artifacts: `TWRTrainer-win-x64` (TWRTrainer.exe) and `ATCRoomMapCreator-win-x64` (since 9 Nov the Map Creator is a separate download).
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
- **Buttons on mouse over (the user, 4 Nov; done 4 Nov; "anzi, tutti i pulsanti": every button of the program, the action
  buttons, the rows of choices (ChoiceRow, yellow chosen), the bars of the parts, Run / Pause):** they turn very light
  under the mouse and their text cannot be read: the default Windows template paints its own light hover colour under
  our light text. Fix: one template of our own for every button (a style for all of them): on mouse
  over a slightly lighter shade of the same blue, pressed a darker one, the text always readable; disabled greyed.
  **Chosen (the user, 4 Nov, after the picture): B.** On mouse over the background does not change: only the text and
  the border turn white (the text normally a little less light, so the change shows: action buttons #B9CBD6 on
  #2F6F86, choices #A9B6C0 on #2A333C); the yellow choice chosen gets only a white border (its dark text stays); pressed
  a little darker; disabled grey. One style for every button of the program.
- **GROUP lights up (the user, 4 Nov; done):** with two or more rows ticked (☑, not flying) the GROUP button lights
  up (the yellow border of a choice, or yellow), with a message in the Traffic List "GROUP: 2 ticked → PONY01 F2";
  greyed when fewer than two are ticked. The formation takes the plan of the first row ticked (said in the message).
- **UNGROUP (the user, 4 Nov; done):** one row ticked that is a formation (not flying): the GROUP button becomes
  UNGROUP and lights up the same way. It splits the formation into single rows in the same place of the list: a
  formation made with GROUP gives back the rows it was made of, each with its own plan (the formation keeps them, also
  in the saved scenario: a list of the plans grouped, read back by older versions as an unknown field kept); one made
  as a formation in New aircraft gives single rows with the callsigns in sequence (PONY01, PONY02...), each with the
  formation's plan.
- **Parts as tall as their content (the user, 4 Nov; done; every part):** a floating part grows (and shrinks) by
  itself to show all its content without scrolling: an expander opened (Map layers, More...), rows added; the Traffic
  List too, as plans are added (no fixed height for FLYING / WAITING any more). Only when it would pass the bottom of
  the window: as tall as fits, then it scrolls. The grip in the corner stays free in both directions (the user: "libero"):
  a height set by hand holds (auto height off for that part, remembered) until a double click on the grip, or docking
  it again, gives the automatic height back.
- **Settings and the map bar (the user, 4 Nov; done; the runway in use and the sides as buttons too):** Aerodrome and Simulation become one part, Settings, with
  collapsible sections: Map (open map, runway in use, new runway), Circuits (sides, heights per class, long final and
  IP — now per aerodrome, in its file; none: 10 and 3 NM —, Formation FS), Gates and exits, Weather (METAR), Map layers.
  The controls of the lesson (Run / Pause, speed x1 x2 x4, Level 1 / Level 2) in a small bar at the top of the map,
  always visible; Remove all aircraft only in the Traffic List (REMOVE ALL). Parts: Settings, New aircraft, Traffic
  List, Selected aircraft (their places of the old two parts: Settings takes Aerodrome's).
- **The bar on the map movable (the user, 4 Nov; done):** "non mi piace lì" (top middle of the map): a grip ⋮⋮ on its
  left to drag it anywhere over the map (always inside it), its place remembered; a double click on the grip: back to
  the top middle. (The user had thought of it on the frame of the program, the title bar: movable is better.)
- **L1 / L2 (the user, 4 Nov; done):** by default L1 10 NM, L2 20 NM across the map (now 7 and 40). Programmable
  (proposed): a right click on L1 or L2 keeps the view of now (centre and zoom) in that button, saved with the
  aerodrome (each field its own); a click (or F1 / F2) goes back to it; in the right click a "Default" item gives the
  10 / 20 NM back.
- **Order of the Traffic List by drag and drop (the user, 4 Nov; done, FLYING too):** the rows moved above or below the others,
  as a list of priorities: a grip ⋮⋮ at the start of each row (FLYING and WAITING) dragged up or down, a line showing
  where it goes. WAITING: the order of the plans (saved in the scenario; START ALL with the same delay: in this order).
  FLYING: an order of the instructor's (new aircraft at the bottom, the gone ones out), kept while the program runs.
- **TURN BASE before the threshold (the user, 4 Nov, a bug; done):** it flew straight at a point 0.3 NM before the
  threshold, across the field. Now: on along the downwind (onto it, if still on the way) to 0.3 NM past the threshold,
  a short base; past it, the base from where it is; the jet circuits the same for their 180° turn to the final.
- **BASE while still joining (the user, 4 Nov, after a test; done 5 Nov, test 45):** the short base is right only for an aircraft
  already on the downwind. One still to join the circuit (from a gate, on the 45° entry, heading to the downwind:
  off the downwind line by more than 0.3 NM, or its target JOIN) goes straight to the start of the base at its normal
  place (the end of the downwind, BaseAlong), then the base and the final, as the gate entry BASE decided at the moment
  (the user's drawing: an arrow from the aircraft straight to the corner of the base). Jet circuits: straight to the
  point of their 180° turn to the final. BASE usable on JOIN too (now grey there). On the downwind before the threshold:
  the short base 0.3 NM past it (as now); past it: the base at once. Self-test: from a gate, BASE before the downwind,
  never on the runway side of the downwind before the base, lands. (Done: the jet circuits onto the downwind line
  0.8 NM before their 180° turn, then the turn.)
- **The break of the close pattern (the user, 5 Nov: "the close is like an OVH without the initial"; done, test 44):** the
  180° turn to the downwind after a touch and go or a go-around at the break point of the aircraft: MID (default),
  BEG, END, a point on the map, above 400 ft as before (past the point at 400 ft: at once); STBY on along the runway
  track until BREAK NOW (on the label and in the panel for the close pattern on its upwind too). The Break row of
  Selected aircraft (and BREAK AT A POINT) only with OVH and CLS; with CONV, WIDE, SFO hidden (they have no break).
  Note: a waveoff of the close pattern now turns over the middle of the runway (before: just above 400 ft).

## Build of 5 Nov afternoon (the user: "vai") — WRITTEN
- **Departures (the user, 5 Nov):** New aircraft: Type first, ARRIVAL / DEPARTURE / TRANSIT. RWY only for DEPARTURE (lit
  by itself), off for the others. A departure starts with the conventional or the wide only (the others once it flies,
  as for every aircraft). Circuits 0 (only for a departure): no circuit, the row Departure: RWY HDG (on along the runway
  heading) or RADIAL (magnetic, outbound; turn L / R, by default the shorter way from the runway heading; from the middle
  of the runway or FROM A POINT clicked on the map, MIDDLE back). Above 400 ft the turn to 45° onto the radial, then
  along it. Up to the height of More… (empty: 3000 ft). Gone 10 NM from the aerodrome (every departure, DEP at the end of
  the circuits too). Test 46.
- **Magnetic variation (first part of "magnetic headings"):** Settings > Circuits > Variation (3E, 2W), kept in the
  aerodrome file; used for the radials of the departures only for now (headings and runways still true).

## Build of 5 Nov evening (the user: "vai") — WRITTEN
- **Departure turn softer (the user, 5 Nov: "too violent"):** the turn to the radial only past the far end of the runway
  and above 400 ft, as wide as the close pattern's 180° turn (radius half its downwind width), with the circuit bank.
- **New aircraft:** Class, Formation (Interval s), Type, From, To / Via, Entry, Pattern (was Circuit, as on the label; also
  in Selected aircraft), Side, Circuits, On Final, Last, Departure / Radial. A departure: Circuits 0 by default. PTRN gone
  from From (old plans with it still start in the circuit; edited: the first gate).
- **On Final (circuits 2 or more, ∞):** how every circuit but the last ends: T&G (default), L/A (low approach: to the short
  final, then up to the circuit height), L/P (low pass: down to 400 ft, at 400 ft all along the runway, then the next
  circuit from its far end). Saved in the plan (OnFinal). Test 39b.
- **END on the label:** G/A became L/A (the same low approach), LOW became L/P (now 400 ft, all along the runway, test 6).
  G/A stays in NOW only, the order of the moment.

## Build of 5 Nov late afternoon (the user: "vai") — WRITTEN
- **Four types, each with its end (the user, 5 Nov):** ARRIVAL (a gate or LONG FIN) and LOCAL (from RWY) land; DEPARTURE
  (RWY) leaves along its Exit (RWY HDG or RADIAL); TRANSIT leaves at its gate To: Via DIRECT, OVERHEAD or CIRCUITS (into
  the circuit from the gate From, its circuits, out to the gate To, gone there).
- **On Final** (after Side): F/S (land at the first final: arrivals and locals only), T/G, L/A, L/P. **Circuits**:
  arrivals and locals the circuits more than the first one (1-4, ∞; lit when On Final is not F/S); departures OUT (away at
  once, default) or 1-4 circuits before leaving; crossings with circuits 1-4. Last gone (the type decides the end).
- Formation SINGLE, 2, 3, 4. FS became F/S everywhere. Side keeps AD (the aerodrome's side when it starts: a scenario
  follows the runway in use).
- Older scenarios converted when read (TowerPlan.Upgrade): the same flights (an arrival with DEP at the end: F/S).
- Engine: CircuitState.LeaveAfterLast, ExitX/ExitY; LeaveAfterCircuits; after the last final it leaves (DepartNow). Test 47.
- **Clearer names in New aircraft (the user, 5 Nov evening):** Type of AC, Formation, Break interval (3s 4s 5s 10s), Type
  of flight, Start from, Exit to, Via, Pattern type, Pattern entry, Pattern side (in this order), On Final, Numb. of
  Circuits; a wider column for them.

## Next group — WRITTEN (5 Nov night, the user: "procedi")
- **Exit → Exit via (the user, 5 Nov):** in New aircraft, the departure's row Exit (RWY HDG / RADIAL) named "Exit via".
- **Rows that do not apply hidden (the user, 5 Nov):** in New aircraft a row that is off for the type of flight is hidden,
  not shown grey (Pattern entry for LOCAL / DEPARTURE, Exit to and Via for the non crossings, ...). The note at the top
  rewritten for the four types of flight.
- **Pattern entry in flight (the user, 5 Nov: "the WIDE is not a circuit, a way outside them: back in at any moment, as
  the controller clears it"):** New aircraft: Pattern entry hidden for LOCAL / DEPARTURE. In flight, on the label and in
  Selected aircraft, a row DCT TO (the user, 5 Nov: not "ENTRY", it goes there at once): DW (join the conventional downwind now), BASE (straight to the start of the conventional
  base, then the final), IP (straight to the initial point, the overhead), HK (straight to the High Key, the SFO); lit on
  the WIDE and for an aircraft out of a circuit (flown by the instructor), at once (not the next circuit).
  **Changed (the user, 5 Nov, after a test):** DCT TO lit for every aircraft in flight in a circuit (the first circuit
  after the take-off too), not only on the WIDE: it can go to a new circuit before the first one is over.
- **Take-off: CONV or WIDE only, on the label too (the user, 5 Nov, after a test: OVH chosen on the label while taking
  off, the aircraft turned the other way to the IP at the lift-off).** On the ground and in the take-off roll, PATTERN on
  the label and in Selected aircraft lights CONV and WIDE only (that choice is its first circuit); once airborne all (the
  next circuit, as now). The way to another circuit at once in flight: DCT TO. Test: OVH not taken at the lift-off.

## Build of 5 Nov night (the user: "vai") — WRITTEN
- **Helipads (the user, 5 Nov):** registered at the aerodrome (Settings: Helipad name, ADD / REMOVE HELIPAD; H1, H2...; in
  the aerodrome file; drawn on the map) and any point clicked. Helicopters only: label END + HPAD and DCT TO + HPAD
  (straight to the pad from wherever, down and slower, hover, down: a full stop); Selected aircraft: Helipad (HPAD) in
  "2 · Next circuit", HPAD in DCT TO; HELIPAD gone from "4 · Out of the circuit". The choice: a menu of the registered
  pads + "Point on the map...". New aircraft: On Final HPAD for helicopter arrivals / locals, the row Helipad (the
  registered ones, POINT). Test 51.

## Group of 5 Nov late night — WRITTEN (the user: "si vai")
- **DCT TO BASE fixed (the user, 5 Nov, a crossing on the way to the final: it went far up and back):** the base from where
  it is only on the downwind itself (near its line, flying it); elsewhere straight to the start of the base. Test 52.
- **Tooltips made coherent (the user, 5 Nov):** Settings › Circuits for AD and the class heights, Start from / Exit to,
  Pattern entry, Exit via, Settings › Map, levels 10 / 20 NM, WIDE "outside downwind/normal pattern", DEP gone 10 NM out,
  BREAK AT A POINT overhead and close, PATTERN on the ground CONV or WIDE; HELIPAD and T&G tips gone.
- **HPAD without a pad (the user, 5 Nov):** New aircraft with On Final HPAD and no pad (none registered, or POINT not
  clicked): it starts anyway (no error), its plan "on a pad to be chosen"; the label shows HPAD? until the pad is given in
  flight (HPAD on END or DCT TO); on the final with no pad yet: a full stop on the runway. A note instead of the error:
  "no pad: choose it in flight with HPAD, else it lands on the runway".

## Build of 6 Nov (the user: "fai subito") — WRITTEN
- **A height for one circuit (the user, 6 Nov):** HEIGHT given in a circuit (on its route, or circling to the High Key)
  is the height of that circuit: the downwind (CONV, WIDE, CLS), the initial and the break (OVH), the High Key (SFO; the
  Low Key moved as much, not below the roll-out); from the base, the turn to the final, the Low Key down as usual by itself
  (no AUTO); the next circuit (touch and go, go-around) the class's again; AUTO at once. Out of the circuit (MANUAL) as
  before. New aircraft with Pattern type SFO: Height ft is its High Key. Test 53.

## Build of 7 Nov (the user: "procedi") — WRITTEN (tests 57-59)
- **Exit turn L / R** (no SHORT; the user, 6 Nov): row under Exit via / Exit to for departures and transits using the runway
  (hidden with RWY HDG); lit by itself each time the way out changes, by the side of the runway axis the exit point (gate, R/D,
  radial) is on, seen from the take-off; on the axis (about 0.3 NM) or the opposite radial: the aerodrome's circuit side (AD);
  clicked by hand: kept until the way out changes. Engine: the turn side kept also to a gate / R/D point (today the shorter).
- **Exit alt ft** (ALT / HGT): the altitude it climbs to when it leaves (empty: 3000 ft above the runway), apart from Height ft
  (the entry); old lists: a departure's Height ft is its exit altitude.
- **Fixes of the review of the R/D build:** (1) departures saved before kept their last gate in To: ignored (RWY HDG or their
  radial as before); (2) a departure to a point taken off its way by the instructor: gone at 10 NM as the others; (3) a departure
  with circuits to an empty R/D or a gate no more there: said, not started; (4) no gates: Start from falls back to LONG FIN, not
  R/D; R/D text with the decimal point, radial 0-359; gate names R/D, RWY HDG, RADIAL, LONG FIN, RWY refused; hand-written files
  (transit from LONG FIN without CIRCUITS, circuits ∞) handled safely.
- **360 = 0** (the user, 6 Nov): every radial box (R/D start, R/D exit, Radial °M) takes 360 as north, the same as 0; shown as
  written (R360/10).
- **Helicopter departing from a helipad (the user, 6 Nov night; decided, to build with the "vai"):** New aircraft, a helicopter
  departure: the row Helipad (the registered ones, POINT) as for the arrivals; empty/none = from the runway as now. It starts
  still on the pad, hover of 15 ft (about 3 m) above it, then it departs "like a Light": it rolls along the runway in use and
  flies the departure chosen (Exit to / Exit via, circuits, turn, exit altitude). The hover is 4 s at most in all (the climb to
  15 ft takes about 3.5 s with the class vertical acceleration, no extra wait). A command of the controller (DCT TO to leave,
  heading...) during the hover does NOT cut it short: the hover ends first, then the command is obeyed. Strip and traffic list: departure from H1, H2... A hand-written
  file with a pad on a departure of another class: ignored.
- **Settings: new section "Aircraft" (the user, 6 Nov night: "mettiamo Aircraft"):** next to Circuits; the button Aircraft
  classes... leaves Circuits and goes there (tooltips "Settings › Circuits › Aircraft classes" updated). Other rows that could go
  there: Formation FS (a behaviour of the aircraft, now in Circuits) - DECIDED (the user, 6 Nov night): it moves to Aircraft and
  is renamed **Formation separation** | FULL | REDUCED |, with a note above the row ("Formation landing to a full stop:
  separation of the next aircraft"); tooltips: FULL = the next lands when the one ahead has left the runway, REDUCED = when it
  has rolled a third of the runway.
- **PHOTO for the debriefing (the user, 6 Nov night; decided; the screenshot in the next build, the marker with the log):** a
  button PHOTO on the bar of the map (next to the level), the **middle mouse button on the map**, and **Ctrl+S** (S = Screenshot;
  the user: Ctrl+P is too far on the Italian keyboard; F1 / F2 / Pause already used; Ctrl+S free). It takes the picture of the map **with all the labels of the tracks** (the map view only, not
  the other windows), at the moment of the click; the simulation does not stop. The file goes in `Photos\<one folder for each
  session: date and time of the start>\`, next to Maps / Logs (portable: the program folder; else the Trainer folder in
  AppData), named like `Photo-03_T+12m40s.png` (progressive number of the session, simulated time, not the clock); the number
  is the next free one of the folder. A button **Open folder** next to PHOTO (the friends do not find AppData). The message
  shows the file name only (no full path: the Windows user name) and comes after the shot (not in the picture); the PNG is
  written on another thread; an error (folder read-only, disk full) is said and the marker is made anyway. **Marker:** an event
  "PHOTO n" with the simulated time in the log (CSV) and in the recording (a mark on the time bar and in the list of events of
  the player, click = go there); to add when the log / the recording exist. The student console takes no photo (instructor
  only); the PNGs are the instructor's view (the player of the student shows the marks, not the pictures).

## Next group (local commits, with the next build)
- **The label of the selected aircraft (the user, 7 Nov: "ok, per il prossimo gruppo"):** SPEED was against the ALT field;
  a name in the middle of a row is now against the field after it (right-aligned in its place). WRITTEN.

- **Advanced, from LONG FIN (the user, 7 Nov: "nascondile per non creare confusione"):** the first approach is always straight
  in, the pattern is only for the circuits after it. FINAL with F/S (or a transit OUT): Pattern type and Pattern side
  hidden; CIRCLING: Pattern type hidden (always conventional), the side kept; with circuits after it: both shown, OVH and
  SFO grey. WRITTEN.
- **Advanced (the user, 7 Nov):** Callsign under ICAO type; Height ft and Delay min always in sight at the bottom, no "More…"
  to open. WRITTEN.
- **ADD NOW and Quick in the Traffic List (the user, 7 Nov: "ok va bene anche per i quick"):** their plans stay in the list as
  the others (not gone with the aircraft): ✕ or its end puts it back in WAITING, ↺ again, saved in the scenario; the
  callsign given is kept in the plan (one aircraft). WRITTEN.
- **Tab (the user, 7 Nov):** Tab selects the next aircraft, Shift+Tab the one before, in the order of FLYING (round); none
  selected: the first; in a text box Tab stays the next box. WRITTEN.
- **DCT TO IP: the turn in abeam the IP + 2 NM (the user, 7 Nov, pictures from the WIDE and the CONV; "IP + 2NM"):** the
  45° turn in made the way out longer the wider it was (from the WIDE to about 7.5 NM). Now out parallel to abeam the IP
  + 2 NM, then towards the IP rolling out on the initial at the IP (about 45° from the CONV, 60° from the WIDE). Test 62
  from the CONV and the WIDE. WRITTEN, next group.
- **POINT instead of R/D (the user, 7 Nov: "solo click", moved by drag & drop):** in Start from and Exit to the button R/D is
  now POINT; its row (Start point / Exit point): CLICK ON MAP, then the point shown as radial / distance (not typed); on the
  map a mark START / EXIT (green diamond) while the plan is in Advanced: dragged, the point moves. Kept in the plan as
  radial °M / distance NM from the middle of the runway, as before (old scenarios the same). RADIAL (outbound) unchanged.
  Settings: "Gates and exits" → "Gates and RWY exits". WRITTEN, next group.
- **Range rings (the user, 7 Nov; "5NM va bene") — WRITTEN:** instructor only (not in the student console).
  Centre: the middle of the runway in use (the user: "dal centro dell'aeroporto (pista)"). Proposed: spacing 1 / 2 / 5 / 10
  NM in Settings › Map (per aerodrome, default 5 NM, up to 30 NM), thin and faint, the distance written small on each;
  a RINGS button on the bar of the map (kept in the settings), on at first; a spacing chosen lights it.
- **DCT TO IP near the aerodrome (the user, 7 Nov, picture of the triangle; "via") — WRITTEN (test 62):** today, before the
  IP and near the runway track, it goes out to the WIDE downwind abeam where it is, then diagonally to the axis. New: out
  parallel to the final on its own side, at its distance from the axis (at least the conventional downwind, at most the
  WIDE; nearer: first aside to it), down to abeam the turn-in point, then 45° onto the axis 1 NM before the IP, the initial
  as now (its height and speed). On the other side of the runway: it stays on its side (the user: "D che rimanga pure su
  quel lato"). The same way for DCT TO HK (SFO). Beyond the IP: straight to it as now. A test: never wider than the WIDE,
  on the axis before the IP. (Mock-up of 4 positions shown 7 Nov.)

- **Log of the flights (the user, 6-7 Nov: "si. ok") — WRITTEN:** Logs\Log_<date>.csv (sep=; for Excel in any language),
  made at the first event, closed with the program or when another map is opened. On top: aerodrome, runway, start UTC,
  network (IVAO or empty), instructor callsign and VID, student callsign and VID (empty when not connected; the student's
  when the student console exists). Rows: sim time, UTC, source (SIM / INSTRUCTOR / IVAO), callsign, ICAO type, rules V/I,
  runway, pattern, event, detail. Events: ADDED, TAKE-OFF, TOUCH AND GO, LANDING, GO AROUND, LOW APPROACH, LOW PASS, OUT OF
  THE CIRCUIT, GONE; the instructor's orders (DCT TO, circuit, break, orbit, heading / altitude / speed, G/A, remove...);
  PHOTO n; IVAO IN RANGE / OUT OF RANGE; NETWORK CONNECTED / DISCONNECTED. At the end: a summary per aircraft.
  **The VID (the user, 7 Nov: "se è automatico è meglio, altrimenti lo scrive manualmente l'istruttore") — DECIDED, to write:**
  Aurora's link gives only the callsign (#CONN; checked in IVAO's documentation for 3rd parties). Automatic: the callsign
  looked up in IVAO's public list of who is connected (Whazzup API v2, refreshed every 15 s by IVAO; asked rarely, at the
  connection) → its VID. Not found (no internet, not listed): the VID typed by the instructor in Settings (kept), else empty.
  The student's: sent by his console (later). Also seen: Aurora gives the flight plan (#FP;CALLSIGN): for the IVAO strips.
- **IVAO traffic from Aurora (the user, 7 Nov) — WRITTEN, to test by the user (no Aurora here):** IVAO on the bar of the map
  (off at first, kept): Aurora on this PC (port 1130, as AuroraPAR v2; Aurora.cs copied), asked every 2 s; the traffic
  within 60 NM on the map in light blue (square, one minute ahead, callsign, A + hundreds of ft, speed), only shown, nothing
  sent; its callsign every 30 s (in the log); **the METAR of the aerodrome from Aurora every minute** (into the METAR box,
  applied: the user, 7 Nov). Button: grey off, amber "IVAO ?" Aurora not found / not on IVAO, green "● IVAO" on IVAO.
  Not yet: strips of IVAO aircraft, selecting them, the student.

- **Group of 8 Nov (the user: "Ok. Procedi") — WRITTEN:** the VID found in IVAO's list (Whazzup v2, IvaoDirectory.cs) for the
  callsign of Aurora, else "My VID" of Settings › Network; in the log only while connected (a row VID when known). NORTH /
  RWY on the bar of the map (RWY again: turned round; the labels upright; headings dragged stay true). ⏸ HELD stops the
  countdown too (HELD m:ss shown), let go it goes on. The log with the names of the screen (DCT TO IP, CONV L, F/S,
  altitudes QNH).

- **Group of 8 Nov afternoon (the user: "via") — WRITTEN (test 63):** DCT TO BASE CONV / BASE CLS (the base of the close
  pattern); ← / → the side of the selected aircraft (as SIDE L / R of the label), Shift: now (SIDE NOW); START SESSION /
  END SESSION (only the log: a new file, the time from 0:00:00, a new folder of photos; END asks first, pauses; the program
  closing or another map ends it; outside a session no log); the list buttons ▼ WAITING LIST (flying) and REMOVE ALL
  (waiting); in the log AURORA LOST / IVAO LOST / IVAO DATA STOPPED (aircraft flying not moving for 30 s) and RESTORED
  after m:ss; ERROR and CRASH (the session closed); a log left open marked "ended unexpectedly" at the next start.

- **Group of 8 Nov evening (the user: "via") — WRITTEN (tests 64-66):**
  - NOW orange (fill #8A4A1C, edge #D0803A) on the label and in Aircraft control; DCT TO and REJOIN stay blue.
  - Keys on the selected aircraft (fixed, not while writing in a box): 1-5 pattern; ← / → side (Shift: now); F T A P D end;
    F1-F6 DCT TO (DW CONV, DW CLS, BASE CONV, BASE CLS, IP, HK); B E N O G W H S U (Shift+U) X R; Del remove (asked);
    L / K / V the ALT / SPD / HDG box; Space run / pause; F12 the list of the keys. The levels of the map: Ctrl+F1 / F2
    (F1 / F2 still with no aircraft selected). A key acts as its button (the same log; grey: "not now").
  - BREAK NOW also during the touch and go before a close pattern (rolling, or the final not F/S): kept, it turns above
    400 ft; another break point cancels it. Test: STBY after a T/G, then BREAK NOW.
  - WIDE NOW on the side of the WIDE at this runway; DCT TO BASE CLS / DW CLS on the side of the close pattern.
  - DCT TO DW CLS (onto the downwind of the close pattern), on the label and in the panel.
  - Aircraft control again (mock-up v5): 1 Altitude, speed and heading (narrow boxes with the value now, wheel or
    type + Enter, Esc back; ALT under 200 = hundreds; AUTO and presets, the one now yellow; REJOIN on the HDG row);
    2 Next circuit (PATTERN, SIDE as on the label, BREAK, END, LAND for formations, HPAD); 3 Now (DCT TO; NOW rows; SPLIT /
    BREAKUP); REMOVE red at the bottom; the old group 4 and ORBIT ∞ gone (ORB with Shift). WIDE NOW and BREAKUP on the label.

- **Group of 8 Nov night (the user: "ok procedi") — WRITTEN (tests 67-69):**
  - L / R (label, panel, ← / →): only the side of the next circuit; before the turn of this one (upwind, joining, the
    initial before the break, to the High Key) this one too. The label and the panel show the side of the next circuit.
  - CHG SIDE (red, key C; label and panel; SIDE NOW L / R and Shift+← / → gone): this circuit now on the other side (an S
    over the aerodrome; on the initial / upwind: the turn to the other side); the next circuit ordered stays. On the
    final (base, SFO low key): it goes on, then the other side (as L / R). Not on the ground.
  - DCT TO and NOW in columns, 4 a row (label and panel). Red: G/A, CHG SIDE, BREAKUP (the row below); ORB orange. The
    FORMATION row gone (SPLIT among the orange ones). EXP TAXI on the red row, last column (it is orange).

- **Group of 8 Nov late (the user: "ok") — WRITTEN (test 70):** DCT TO IP from the WIDE: out only to abeam the IP, one turn
  of about 180° onto the initial (no IP + 2 NM; only if the turn fits in its width). And: Aircraft control, the HDG row: "HDG [078] or drag from its symbol", REJOIN on the row
  below (under the box), not between the box and the words.

- **Group of 9 Nov (the user: "procedi ora") — WRITTEN (tests 71-74; DCT TO FINAL key F7, HOLD HK / START SFO key H):**
  - **DCT TO FINAL** (new button in DCT TO, label and panel, with a key): for an aircraft out of the circuits, in the WIDE,
    or on its way to the IP (it changes its mind). Enabled when a 45° intercept towards the runway meets the final farther
    out than where the base joins it (the class's base): then straight to the final is shorter than the base. On the map:
    two lines at 45° crossing on the runway track at the base, the wedge outwards. It turns onto the final (45° or less),
    lines up, descends on the glide path, then its END.
  - **SPLIT always** (the user: "può avvenire sempre e comunque"): no more "not before the break of the OVH / CLS, the
    High Key of the SFO". Split in those circuits: each one flies it on its own, one interval behind the other.

  - **ORB L / ORB R instead of ORB** (the user, 8 Nov night; ORB did nothing when flying it by hand, MANUAL): two buttons,
    label and panel. In its circuit only the right one is on (away from the runway); out of the circuits (transit, MANUAL
    after a BREAKUP) and inbound to a circuit both are on. The engine: in MANUAL one orbit of 360° on the spot, then the
    heading it had, still MANUAL, same altitude and speed. Shift+click: on and on; orbiting on and on, a thick border and a
    click is CONTINUE. Key O: the one on; both on: the side it already turns, else right; Shift+O on and on.
    13 buttons in the NOW grid: a 4th row (EXP TAXI alone); idea: HOLD HK and START SFO as one button.

  - **HOLD HK and START SFO as ONE button** (the user: "ok va bene aggiungi", 8 Nov night): it shows the action it does
    at the click, orange: HOLD HK (not on hold: it holds on the circle of the High Key), START SFO (on hold: it starts at the
    next pass over the High Key); grey (text HOLD HK) outside an SFO circuit or once the SFO has started. One key, H (S free).
    It sends "hold yes / no" by the text shown, not "toggle": a double click is harmless. 12 buttons, 3 rows.

  - **DCT TO HK from the final: no offset** (the user, 8 Nov night: "ok va bene. per il prossimo"): an aircraft on the final
    (near the runway track, before the High Key) stays on the track up to the High Key point and only there joins the circle,
    which is tangent to the track there (no cutting across to the side of the circle). From farther away it is as now (the
    steering towards the circle, up to 70°; the rule of 3 Nov against the loops). Test from the long final: it stays
    within a small distance of the track until the High Key, then the hold within 0.2 NM of the circle, the climb never
    turning back, the final aligned.

- **Group of 9 Nov afternoon (the user: "vai") — WRITTEN (test 71 for the new DCT TO FINAL):** all the items below, and:
  - **Parked windows** (the user, 9 Nov): a floating window closed (▾) becomes a small box in the top right corner of the
    map (from the right, in the order closed) with its symbol and ▾ (back where it was, its size); double click: back into
    the column; its name and summary in the tooltip; a message: the box lit yellow a moment. Symbols: ⚙ Settings,
    ✎ Traffic generation, ☰ Traffic List, ✈ Aircraft control, ▤ Strip bay.
  - **The tooltips** of the panel, the label, the right-click menu and the F12 list checked with the new names.
  - **The manual at the end** (the user, 9 Nov), when everything is ready and decided.
- **(was: Next group, the user 9 Nov):**
  - **BREAKUP → BREAKOUT** (the phraseology of an urgent exit from the approach / circuit; "break-up" is the splitting of
    a formation): button, label, tooltip, log, F12 list, the right-click menu; key Shift+U as now.
  - **DCT TO FINAL, new logic** (the user, 9 Nov): always on (grey only on the ground, already on its final or landing).
    The line through the base point, square to the runway (the base of its circuit: CONV out of the circuits, WIDE, CLS for
    CLS / OVH / SFO). Out beyond the line: straight to the point on the final (45° at most; never nearer than the base).
    Between the line and the runway, or beyond the runway: first the base point ON THE SIDE IT FLIES (not the side of
    "Circuits"; only a help for a wide turn, not joining the circuit, no downwind), then the final. On the runway track
    beyond the runway: the side it is off to / turns, aside first so as not to pass over the runway. Flying away from the
    runway: the point on the final moved in by a full turn. Too high (the user: "calcola la migliore traiettoria"): the
    point on the final, or the base point, moved out so that it is on the glide path there (never a climb).

  - **Aircraft control with full names** (the user, 9 Nov; the label keeps its short names, no symbols):
    - Columns that adapt to the width of the panel (it can be resized): all the buttons of a group as wide (a minimum
      width), as many columns as fit, so they stay lined up.
    - Full names: Conventional circuit, Close pattern, Wide circuit, Overhead join, Sim. Flame Out; Left-hand,
      Right-hand; Runway beginning, Midfield, Runway end, Standby for break, At a point...; Full stop, Touch and go, Low
      approach, Low pass, Departure; Downwind conv., Downwind close, Base conv., Base close, Initial point, High Key, Final;
      Extend downwind, Turn base, Break now, Orbit left, Orbit right, Wide circuit now, Hold at High Key / Start SFO, Split
      formation, Expedite taxi; Go around, Change side, Breakout; Rejoin circuit; Remove aircraft.
    - Lines between the groups, between DIRECT TO and NOW, a dark red one before the row "Urgent" (with its tag).
    - The key small on the right of each button.
    - Symbols (Segoe UI Symbol, forced plain, not coloured; chosen by the user): Extend downwind ⟷ U+27F7, Final ⇥ U+21E5,
      Base conv. / Base close ⇲ U+21F2, Turn base ↪ U+21AA, High Key ⤒ U+2912, Hold at High Key ⬭ U+2B2D, Start SFO ↧
      U+21A7, Wide circuit now ⏥ U+23E5, Split formation ⇶ U+21F6, Go around ⏫ U+23EB, Expedite taxi ↴ U+21B4, Breakout ↫
      U+21AB. The others as in the mock-up: Downwind conv. / close ➝, Initial point ◎, Orbit left ↺, Orbit right ↻, Break
      now ⤵, Change side ⇄, Remove aircraft ✕.

  - **TURN BASE → TURN IN** (the user, 9 Nov: it was mixed up with DCT TO BASE): label TURN IN, panel "↪ Turn in now",
    key N; the same order (turn in before the base point, or on command after EXTEND DOWNWIND).
  - **Arrows turned with the side** (proposed 9 Nov, to confirm): mirrored by the side it turns, not by the map: Turn in
    ↪, Break now ⤵, Base ⇲ towards its side; Breakout ↫ away from the runway. Fixed: orbits, Change side, Extend downwind,
    Wide, Final, Downwind, High Key, Go around. Out of the circuits: the side of "Circuits".

  - **SPLIT tooltip:** "Formation break-up: each aircraft on its own, one interval after the other (the interval of the
    formation)." The button keeps the name SPLIT.

- **Group of 9 Nov late (the user: "si") — WRITTEN:** SFO on its way to the circle of the High Key: a dashed line from
  the aircraft to where it joins the circle, turning its way (counterclockwise left-hand); none inside the circle.
- **Group of 9 Nov evening (the user) — WRITTEN:** the two items below, and ATCRoom Map Creator a SEPARATE download (not beside TWR Trainer, its own artifact; the hint of the map points to github.com/ATCRoom/MapCreator/releases). **Done 9 Nov:** the repository ATCRoom/MapCreator (created by the user), the source of MapCreator/, its workflow and the release v2.0.0; TWR Trainer's own future update check must look only at its own tags.
  - **SFO climbing / on hold: its circle drawn** (the user, 9 Nov: "solo il primo"): while it climbs on the circle of the
    High Key or holds there (no route then, so nothing was drawn), the selected aircraft shows that circle dashed as the
    other ways, with a mark on the High Key point. Not the SFO pattern ahead (only after START SFO, as now).
  - **WIDE NOW grey on the final and rolling** (the user: "A"): there it only became the next circuit, after a touch and
    go or go-around, never flown with F/S (from LONG FINAL it looked dead). For the WIDE after the final: Pattern → Wide
    circuit; at once: G/A, then WIDE NOW. The same on the label and the panel.

## Manual (the user, 8 Nov: "va spiegata molto bene nel manuale")
- **DCT TO and NOW look very much alike: explain them well.** DCT TO (blue) = WHERE it goes: it leaves where it is, goes
  straight to that point of the circuit (DW CONV, DW CLS, BASE CONV, BASE CLS, IP, HK, HPAD) and goes on with the circuit
  from there. NOW (orange) = WHAT it does now, where it is, without being sent elsewhere (EXT DWN, BASE, ORB, G/A, BREAK
  NOW, SIDE NOW, WIDE NOW, HOLD HK, START SFO, EXP TAXI, SPLIT, BREAKUP). Picture of the circuit as a ring; an example for each.
  Also note: BASE (NOW) turns base from the downwind it flies; DCT TO BASE goes straight to the start of the base from anywhere.

## Ideas for a next build (the user, 7 Nov)
- **A "horizontal" view of the map, besides the north-up one (the user, 7 Nov):** the map turned so the runway in use lies
  horizontal on the screen (as seen from the tower); a button on the bar of the map NORTH | RWY switches, kept in the settings.
  DECIDED (the user, 7 Nov evening): buttons NORTH and RWY on the bar; the RWY view from both sides: a click on RWY when it
  is already on turns it round (the threshold of the runway in use on the left, then on the right; kept in the settings).
  The labels and the texts stay upright; L1 / L2 and the views kept on them in both; the photos as on the screen.
- **⏸ HELD (the user, 7 Nov evening):** a plan held stops everything, its countdown too (it goes on from there when let go).

## Build of 7 Nov afternoon (the user: "procedi con la build") — WRITTEN (tests 60-61)
- The three sections below, as decided. Choices made while writing them (simple, to check with the build):
  - The class altitudes become heights at the aerodrome at least 300 ft above the runway; the initial (IP) and the High Key
    keep the engine's minimum of 500 ft. The arrival from a gate stays at least 500 ft above the conventional circuit flown
    there. A class made for an aerodrome is not made again (no double correction).
  - Settings › Circuits: the table of the altitudes (a row per class: Conv, Wide, Close, IP, Break, HK); a box is saved when
    left, with Enter or with the mouse wheel (no Save button). Without a runway placed the boxes are grey (the elevation is
    needed). Import heights...: a menu of the other aerodromes of TWR Trainer (the folder Aerodromes), then asked before replacing.
  - Quick: the class of the aerodrome (Settings › Aircraft); none chosen there: the class chosen in Advanced. A plan clicked
    in the Traffic List (or on the map) opens the Advanced tab. The tab chosen is kept.
  - The callsign prefix in the aerodrome file (CallsignPrefix, IcaoType, DefaultClass; missing: SIM, none, none).
  - Fixed on the way: ⧉ (duplicate) now keeps the exit altitude, the helipad of a departure and its way out.
  - The self-test circuits fly at an elevation of 0 (the class altitudes = heights there); test 60 at 1200 ft.

## Next build (the user, 7 Nov) — WRITTEN 7 Nov afternoon (the user: "procedi con la build")
- **The runway on the map (the user, 7 Nov: "mi piace la 1"):** today the tower draws its own light grey strip (B8C0C6, 0.03 NM
  wide, from the threshold, the length placed) over the map, covering the black runway of the Map Creator (only the ends past
  the thresholds show). Change: draw only a **thin line along the runway axis** (1-2 px, grey or white), from the threshold to
  the end; the black runway of the map shows all. The centreline before the threshold and its ticks stay. Without a runway on
  the map the thin line still shows the runway placed.

## Traffic generation, defaults and Quick (the user, 7 Nov afternoon) — WRITTEN 7 Nov afternoon
- **Names of the panels as in AuroraPAR Trainer:** New aircraft -> **Traffic generation**; Selected aircraft -> **Aircraft control**
  (titles, tooltips and messages that name them; the saved places of the panels keep working: the key of the place is kept or moved).
- **Scenario / flight plans LOAD...: SAVE... in Traffic generation** (in AuroraPAR Trainer they are in Traffic generation, Advanced;
  here they are in the Traffic List panel). Proposed: move TWR's to Traffic generation; AuroraPAR stays as it is.
- **Settings › Aircraft: default callsign** for each aerodrome (in the aerodrome file, optional field), instead of SIM (the engine
  has CallsignPrefix, 1 to 6 letters / digits; no row in the UI yet): the base traffic, then BASE1, BASE2... A callsign typed wins.
- **Settings › Aircraft: default type of aircraft** (ICAO, free text, not the class) for each aerodrome: the base traffic; it
  fills the ICAO type of the aircraft without one (the strip shows it) and of the Quick ones.
- **Settings › Aircraft: default class** (per aerodrome, next to the default type, above or below the ICAO one): the class of the
  Quick aircraft (the ICAO type is only a label; the engine needs a class).
- **Quick traffic** (a tab of Traffic generation, Quick | Advanced as in AuroraPAR; one click = an aircraft at once, as ADD NOW;
  class, ICAO type and callsign of the aerodrome's defaults; rules VFR) — DECIDED (the user, 7 Nov):
  1. **LONG FIN**: from the long final point, on final, F/S.
  2. **Inbound IP**: starts at the long final point (as set in the Settings of the aerodrome), keeps its height to the IP, then the
     overhead entry; F/S. Height to the IP: the height of the initial of the class (OverheadHeightFt, 1500 ft above the threshold
     by default, in the Aircraft classes window) — the user, 7 Nov.
  3. **Departure**: exit RWY HDG, no circuits, exit altitude 3000 ft.
  4. **Local CONV** and 5. **Local WIDE**: from the RUNWAY, one flight = a departure + an arrival: it takes off, flies a circuit
     (the conventional one / the wide one, the side of the aerodrome) and lands F/S.
  6. On final always **F/S** (arrivals and locals).
- **Callsign number**: not already in the simulation (the engine already skips the callsigns flying, the reserved ones of the
  plans waiting and those flying outside; the counter goes on, a number is not given twice).

## Default heights as altitudes (QNH) and import of the heights of other aerodromes (the user, 7 Nov) — WRITTEN 7 Nov afternoon
- **IMPORTANT (the user: "la stragrande maggioranza degli aeroporti lavora sulle altitudini") — DECIDED (7 Nov):** the default
  values of the classes (circuit Conv / Wide / Close, IP, Break, HK, LK) are from now **altitudes ft QNH**, not heights above the
  aerodrome: a new aerodrome shows them as they are, with no correction (today: elevation + 500 / 1000 / 1500...). The engine
  keeps the heights above the runway: a missing value of the aerodrome = the class altitude minus the elevation, at least 300 ft
  above the runway (the minimum of the engine); QFE aerodromes show the height (altitude - elevation). The values written by hand
  in an aerodrome file stay as they are (above the threshold). Not altitudes: the roll-out height (250 ft...) and the rules
  "500 above the circuit". The numbers of the defaults stay as they are (they fit the aerodromes near
  sea level; each aerodrome gets its own, the import below helps). The default exit altitude is **3000 ft QNH** too (not above the
  runway; at least 1000 ft above it). The user, 7 Nov: "da ora tutto deve essere ragionato per usare il QNH, chi usa il QFE sono pochi
  nel mondo": QNH is the first thought everywhere (labels, tooltips, defaults); QFE stays as the option.
- **The heights table leaves the Aircraft classes window (the user, 7 Nov: "si esatto, intendevo questo"):** Aircraft classes keeps
  only the performance (speeds, climb, descent, bank, brake: all aerodromes); the table of the heights of this aerodrome (a row
  for each class: Conv, Wide, Close, IP, Break, HK; ALT = QNH) goes to **Settings › Circuits**, with the button Import heights.
  The file of the aerodrome does not change for this (the same fields).
- **Tooltips and messages follow the new names and places (the user, 7 Nov: "aggiorna anche i vari tooltips"):** everything that
  names a panel or a setting: Traffic generation / Aircraft control (the waiting plans on the map "it goes into Traffic
  generation", the plan row "Click: change it in Traffic generation (UPDATE)", the empty list "None: Traffic generation, then
  TO LIST", the part titles and the comments), the Quick | Advanced tabs, Settings › Aircraft (classes, default callsign / type /
  class, Formation separation), the heights table and Import heights in Settings › Circuits, the Class row tooltip (where the
  classes and their heights are), QNH first in every text about heights (ALT / HGT, defaults, Exit altitude), and the notes
  in docs/. A search for every old name at the end, before the review.
- **The aerodromes already made (the user, 7 Nov: "1"):** they follow the new defaults: the heights written stay (above the
  threshold), the empty ones become the class altitudes (QNH); nothing is written into the files by itself. The file format is
  the same; the new optional fields (callsign, ICAO type, class) are missing = SIM, none. Import from an aerodrome with no
  elevation in the file (runway not placed from runways.par): a warning "place the runways first".
- **Import of the heights of other aerodromes — DECIDED (7 Nov): from another aerodrome of the program.** In the Aircraft classes
  window a button **Import heights...**: the list of the maps (the Maps folder), the table of the chosen aerodrome is copied into
  this one; the **altitudes stay the same** (1700 stays 1700; the heights above the runway follow this elevation). Nothing else of
  the other aerodrome is copied; the heights not written there stay empty (the defaults). Asks before replacing.

## Waiting traffic on the instructor's map (the user, 6 Nov) — DECIDED, next build
- **Every plan in WAITING** drawn on the instructor's map as a still, faint grey symbol at its starting point: the gate, the long
  final at its distance, the threshold (departures, local), the helipad. Small label: callsign, ICAO type, starting altitude,
  state (countdown / HELD). A formation one symbol (F2, F3...). Several at the same point (e.g. departures at the threshold):
  labels staggered. A click: its plan chosen in the Traffic List and loaded in New aircraft (as a click on its row). It goes when
  the plan starts (the aircraft takes its place) or is removed. Instructor only: not in the simulation, the log, the recording
  or the student's map. (Mock-up of the ones on the ground shown 6 Nov.)
- **A button shows / hides them** (the user, 6 Nov): **👁 WAITING TFC** in the Traffic List, lit = shown (yellow), off = grey
  with the eye crossed out; the choice saved in the settings (default: shown).
- **Traffic List buttons by section (mock-up 6 Nov, to confirm):** FLYING header: ⏸ PAUSE, ✕ REMOVE ALL (the aircraft flying);
  WAITING header: 👁 WAITING TFC; under the WAITING rows: ▶ START ALL, ☐ GROUP, and on the right ✕ CLEAR ALL (apart, it empties
  the list); at the bottom a line "Scenario": LOAD..., SAVE... (plain buttons, not blue).

## Transit: long final, R/D points (the user, 6 Nov evening) — WRITTEN 6 Nov night (with the fixes of the review of the strip bay)
- **R/D point:** a point given as radial °M / distance NM from the middle of the runway in use, instead of a gate (not saved
  as a gate: only in the plan / scenario). Just a way to name a point: an aircraft goes **straight to it** (no interception).
  In Start from (arrivals and transits, as a gate) and in Exit to (transits, as a gate: straight to it, gone there).
- **Transit from LONG FIN** (the user, 6 Nov): it comes to the aerodrome to "do something" (On Final T/G, L/A, L/P; also
  several circuits in a row; OUT = only the approach), then it leaves. **Four ways out:** RWY HDG, intercept a RADIAL (as a
  departure, turn L/R), a gate, an R/D point (straight to it). **Confirmed (the user, 6 Nov):** the same four for every transit
  that uses the runway (Via CIRCUITS); a transit that only crosses (DIRECT / OVERHEAD from a gate or R/D): a gate or an R/D
  point. **The four ways out also for DEPARTURES** (today RWY HDG and RADIAL; to add: a gate, an R/D point).
- Strip / list text: "R270/12", "LF", gate name.

## Student console, link, log, replay (the user, 6 Nov evening) — DECIDED, design (mock-up of the student shown 6 Nov)
- **One "snapshot" each second** (aircraft, runway, METAR/QNH/wind, time) used three times: sent on the link, written in the
  recording, read back by the player.
- **Student console:** the same program with a role (Instructor / Student). The student sees everything of the traffic: the top
  view (map sent by the instructor, kept in memory only), labels, METAR line, UTC time, runway, link light; and a **compact strip
  list** (narrow column): line 1 callsign, ICAO type, flight rules V / I; line 2 entry gate (or exit for a departure) and
  altitude. **Strip colour by type of flight** (the user, 6 Nov): arrivals one colour, transit and local the same colour,
  departures one colour. **Nothing of the instructor's actions** on the student's side (no leg, no commands, not in the strip,
  not in the student view of the player). No commands to the aircraft.
- **Strips (the user, 6 Nov):** compact, about 26 px (line 1 callsign, ICAO type, V/I, entry short N / S / LF / RWY or exit
  for a departure, altitude in hundreds; line 2 the student's notes, the last highlighted, empty until he writes). A strip
  **goes** when its aircraft is off the runway after landing, or is removed from the simulation; a departure when the
  simulation ends it (at its exit / end of its route), or removed. (Colours: see Strip colours below.)
- **Strip bay (the user, 6 Nov; mock-up shown):** a floating panel (like the others: dragged by its bar, docked or in a window
  of its own) with two sections. **Coordination:** a strip for every plan in the instructor's Traffic List (WAITING), shown at
  once, as if coordinated (no countdown, nothing of the instructor). **In contact:** when the plan flies, its strip moves here
  (as if the radar unit had sent it to the tower frequency). The quick note buttons and the free note box at the bottom of the
  bay. Held plans (⏸) stay in Coordination (the user, 6 Nov: yes). **IVAO aircraft:** their strip in Coordination when in
  range; the student moves it to In contact by hand when it really calls (the user, 6 Nov). Simulated aircraft: moved to In contact
  by themselves when they really start (the user, 6 Nov).
- **Strip colours (the user, 6 Nov):** by type of flight, **changeable by the user**; default departures **white**, arrivals
  **yellow**, local and transit **pink**. (Set in Settings › Appearance.)
- **Student's notes on the strips** (the user, 6 Nov; proposal shown in a mock-up, to confirm): click a strip to choose it, quick
  buttons (DW, BASE, FINAL, OVH, BRK, SFO, ORBIT, T/G, LAND, L/A, L/P, G/A, TKOF, FRQ CNG) and a free text box add a note at the end
  of its note line (the last highlighted); click a note to remove it; drag a strip to change its place. The notes stay with the
  student; kept in the recording (with their time) for the debrief.
  **The instructor does not see them during the simulation** (the user, 6 Nov: not graded live): only in the player. The EXIT
  button is **FRQ CNG** instead (the user, 6 Nov): the aircraft was sent to the radar unit's frequency.
- **Same PC:** the student console is a second window of the same program (for a second monitor), no network. **Two PCs:** the
  link of AuroraPAR v2 Trainer (public relays, session code, no IP / ports); the map sent in pieces at the start; fixed delay.
- **Log of the flights:** one CSV per session (Logs folder): a row per event (added, take-off, each T/G, L/A or L/P, G/A,
  landing, out of the circuit, removed; main instructor commands), time, callsign, ICAO type, rules, runway, pattern; a summary at
  the end. "For now like this, then we see."
- **Recording and player:** snapshots + events in a compressed file (no video, no map inside). Player: play / pause, x1-x8,
  time bar, list of events (click = go there), student or instructor view. The student may replay at home (without the map:
  runway and aircraft only).
- **IVAO traffic too (the user, 6 Nov: it must work with IVAO).** As AuroraPAR v2 Trainer, three cases: IVAO only (Aurora),
  mixed (IVAO + simulated), simulated only. Aurora's traffic read with the code of AuroraPAR v2 (Aurora.cs, copied here: today
  TWR Trainer has no Aurora link). Simulated aircraft never go to IVAO. Proposal (to confirm): the student gets IVAO from his own
  Aurora, the simulated aircraft from the link (as decided for AuroraPAR v2 Trainer); on one PC both from the same program. Strips
  for IVAO aircraft too: callsign, ICAO type and rules from the flight plan, type of flight from departure / destination against
  the aerodrome (departure, arrival, else transit), entry gate and altitude empty (not known); **a field not given by IVAO
  can be filled in by hand by the student on the strip** (the user, 6 Nov; a field given by IVAO is not changed); a strip goes as for the simulated
  ones (off the runway, or out of range / disconnected). A callsign used by both: warned (the simulated one renamed). **In the log
  and the recording: IVAO aircraft too (marked IVAO; the user, 6 Nov: required).** On the instructor's view the simulated labels in another colour (as the
  Trainer); nothing different for the student.
- **New fields of an aircraft (New aircraft, plans, label, strip, log):** **flight rules VFR / IFR**; **ICAO type** free text
  (F16, TOR, EUFI, C172...; no list; the class stays for the performance).
- **The user, 9 Nov (decided):** (1) the student console FIRST (same PC, second window), then the link between two PCs,
  then recording + player; (2) the student's label: callsign, ICAO type, **flight rules well visible** (a V / I mark),
  altitude in hundreds, speed; (3) the instructor's strip bay STAYS on his screen (the instructor decides what to do with
  it); (4) mock-up of the student console first (shown 9 Nov), then the code.
- **Group 1, student console on the same PC (the user, 9 Nov: "ok"; the strip bay resizable and closed to a small box as
  the other parts; the strips moved by hand) — WRITTEN, local commit, waiting for the go of the build:** `Lesson.cs`
  (StudentTarget, LessonSnapshot: the photo of each second), `StudentWindow.cs` (button STUDENT on the bar of the map; F1 / F2
  levels; its own zoom; the strip bay a DockablePart, floating on the right by default, "Student strip bay" in the settings),
  TowerView `Student` (label: callsign, ICAO type + V / I mark, altitude speed; labels and map dragged, nothing else; the
  map, gates, exits, helipads, categories as the instructor's; north up), StripBay: the V / I square on the left of each strip
  (both bays), strips dragged to change their place inside their section. Not yet: IVAO strips (moved to In contact by hand),
  turning the student's map (NORTH / RWY).
- **Tests of group 1 (the user, 9 Nov): 1-4, 6, 8 OK.** 5/7: while dragging a strip, a coloured line shows where it will go
  (above or below the strip under the mouse; none where it cannot go) — DECIDED (the user: "prossimo gruppo"), with the link.
- **Student console: NORTH / RWY (the user, 9 Nov: "mancano i bottoni NORTH/RWY")** — next group: a small bar on the
  student's map with NORTH and RWY (RWY again: turned round), as the instructor's; the student's choice kept in his settings.
- **Link between two PCs (the user, 9 Nov) — DECIDED, next group:** (1) the student takes IVAO's traffic from **his own
  Aurora** (in an IVAO training he is connected anyway); the simulated aircraft from the link. (2) The student's callsign and
  VID for the log: connected to IVAO, from his Aurora and IVAO's list (as the instructor's); else those written in his
  Settings. Plus: "Join a lesson" with the code, the map sent in pieces (memory only), the photo of each second; the drop
  line of the strips; NORTH / RWY on the student's map.
- **Group 2 (the user, 9 Nov: "ok procedi") — WRITTEN, to review, then the build:** LESSON on the bar of the map: START LESSON
  (a code of 6 letters / digits, COPY CODE, the state: connecting, waiting for the student, sending the map, linked) or JOIN
  with the code (this PC the student: the student console linked, the main window hidden until it is closed). `LessonLink.cs`
  copied from AuroraPAR v2 Trainer (own channel "atcroom/twr/lesson/v1/", Logs\Link.log). `Lesson.cs`: the messages (the photo
  of each second "snap" with the aerodrome, the traffic, the strips, the keys changed by UPDATE; the map "map" compressed in
  pieces of 48 000 characters, sent every 250 ms until the student says he has it; the student's "s": his map, callsign, VID).
  The student: IVAO from his own Aurora (IVAO on his bar), callsign / VID from IVAO when connected, else Settings › Network
  (My callsign, My VID); in the instructor's log STUDENT with callsign and VID. NORTH / RWY on the student's map (kept).
  Strips: the orange line where a dragged strip goes; the notes follow the plan when it starts (also on the student's side).
- **Recording and replay: ATCRoom Replay, an app of its own (the user, 9 Nov: "si ok") — DECIDED, steps:** as ATCRoom Map
  Creator (a shared folder copied into both projects, its own download). One recording format for TWR Trainer and AuroraPAR v2
  Trainer: a photo each second (position of each aircraft and the lines of its label already as text), events, the student's
  notes, PHOTO marks; no map inside. Each trainer writes its files; ATCRoom Replay plays them (also the student at home, with
  no trainer and no map). Steps: (1) the common format, recording in TWR Trainer, Replay with the top view; (2) recording in
  AuroraPAR v2 Trainer (its RDR seen at once in the top view); (3) the PAR view in Replay. Before the code: a mock-up of the
  player. The IVAO strips (B) stay in TWR Trainer, apart.
- **The log for AuroraPAR v2 Trainer too (the user, 9 Nov: to make the two programs alike)** — to design with the user.
- **Order of work:** (1) log, (2) recording + player on one PC, (3) student console as a second window, (4) link between two PCs.
  The new fields (rules, ICAO type) first, they go in all of them.

## Build of 6 Nov evening (the user: "puoi farmi una build anche con il strip bay") — WRITTEN
- Settings: the row Panels DARK / LIGHT out of Map layers: a new section **Appearance** at the top of Settings, with the colours
  of the strips (departures, arrivals, local / transit; default white, yellow, pink).
- New aircraft: **ICAO type** (free text) and **Rules** VFR / IFR (in the plans, the Traffic List text, the strips).
- **Strip bay** (floating part, here on the instructor's screen to see how it behaves): Coordination (plans waiting) and In
  contact (aircraft flying; a formation split: a strip each); quick notes and free note; colours by kind of flight. Not yet:
  dragging strips, IVAO, the student's console.
- **WAITING TFC** (👁) in the Traffic List: the plans waiting drawn still on the map (grey; held in ochre); a click: into
  New aircraft. Traffic List buttons by section (FLYING: PAUSE, REMOVE ALL; WAITING: WAITING TFC; START ALL, GROUP, CLEAR ALL
  apart; Scenario: LOAD, SAVE).

## Build of 6 Nov, runways.par (the user: "ok") — WRITTEN
- Settings › Map › From runways.par...: the runways of the map's aerodrome (ICAO) from AuroraPAR's runways.par (the file chosen
  each time, the last one proposed; not copied into this repository). Each runway once (its glide paths "14 3.0", "14 2.5"
  together); both ways in the file: the two thresholds are its ends; one way: the other end along the true heading by the length.
  Each threshold its own elevation. Asked before (a runway already placed is moved). Aerodromes not in the file: on the map by hand.

## Build of 6 Nov, panels (the user: "intanto facciamo TWR trainer") — WRITTEN
- Settings › Map layers › Panels: DARK / LIGHT, from the next start (settings LightPanels). Only the inside of the panels changes
  (Settings, New aircraft, Traffic List, Selected aircraft, the bar on the map, Aircraft classes); their frames (bar with the title,
  edge) and the map stay dark. Colours in Trainer/Theme.cs; buttons: blue commands, yellow chosen, the others light blue on LIGHT.

## AuroraPAR v2 Trainer: panels LIGHT / DARK (the user, 6 Nov) — TO DO, in ATCRoom/AuroraPAR-v2
- As in TWR Trainer, the other way round: there the frames are already dark and the inside light (Windows' own controls). A choice
  Panels: LIGHT / DARK in Simulation settings (LIGHT by default: nothing changes for who uses it), from the next start.
- DARK means a dark look for Windows' controls inside the panels: text boxes, buttons, combo boxes (their drop-down list too),
  check and radio boxes, the table of Aircraft performance (headers, rows). The most delicate: combo boxes and the table.
- First a mockup for the user; then on the usual test branch of AuroraPAR-v2 (not twr-pointer), an independent review, a look at it.

## Build of 6 Nov afternoon (the user: "vai") — WRITTEN
- Aircraft classes window (Settings › Circuits › Aircraft classes...), as Aircraft performance of AuroraPAR v2 Trainer: a row per
  class; performance (min, max, initial, final speed, climb, descent, bank, brake) into performance.json (the other values of the
  file kept); heights at this aerodrome (conv, wide, close, IP, break, HK) in ALT/HGT: white = of this aerodrome, grey = the class's
  (empty box). Add class, Duplicate, Remove, Reset to defaults; Save checks every value. Helicopter: IP, break, HK off.
  The class / heights rows of Settings › Circuits gone (the old Low Key of a class dropped from the aerodrome file on save).

## Build of 6 Nov midday (the user: "vai") — WRITTEN
- **SFO START at once (the user, 6 Nov):** START SFO anywhere on the High Key circle, at its height: off at once; over the
  High Key along the runway: the flame out from there; lined up before it (within 45°, turn under 90°): straight to it;
  else onto the runway track before it first (the shortest turn), never a whole circle more. Test 54.
- **SFO at one vertical speed:** the Low Key, the end of the downwind and the roll-out by the time to them from the High
  Key; too high for the class's descent: a longer downwind. The Low Key gone from Settings. Test 54.
- **HK altitude / height ft in New aircraft** (Pattern type SFO; empty: Settings › Circuits); Height ft only the gate's.
- **QNH / QFE per aerodrome (Settings › Map, Pressure; QNH by default):** every height shown and written follows it: the
  label ALT / HGT, the map tags, New aircraft (Altitude / Height ft, HK), Selected aircraft, Settings › Circuits, the
  Traffic List. Inside, heights above the runway as before.

