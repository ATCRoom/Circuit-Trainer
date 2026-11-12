# ATCRoom Circuit Trainer — manual

Circuit Trainer is a simulator for training **tower control of traffic in the circuit**. The **instructor** flies the
aircraft (arrivals, locals, departures, crossings, formations, helicopters); the **student** controls them as a
tower controller, on his own console, with his own strip bay.

It is part of the ATCRoom family of programs, with AuroraPAR v2 Trainer. The two programs look and work alike
(the bar on the map, START SESSION, LESSON, PHOTO, LOGS, IVAO, the Traffic List, the strip bay).

**For training and simulation only.** Nothing the program does goes to any network: the traffic of IVAO is only
*shown* (chapter 15), nothing is sent.

## Contents

1. [What you need](#1-what-you-need)
2. [Starting for the first time](#2-starting-for-the-first-time)
3. [Setting up an aerodrome](#3-setting-up-an-aerodrome)
4. [The window of the instructor](#4-the-window-of-the-instructor)
5. [The map and the labels](#5-the-map-and-the-labels)
6. [Traffic generation](#6-traffic-generation)
7. [Traffic List](#7-traffic-list)
8. [Aircraft control](#8-aircraft-control)
9. [DCT TO and NOW: the difference](#9-dct-to-and-now-the-difference)
10. [The circuits](#10-the-circuits)
11. [Aircraft classes](#11-aircraft-classes)
12. [Settings](#12-settings)
13. [Strip bay](#13-strip-bay)
14. [The student console and the lesson](#14-the-student-console-and-the-lesson)
15. [IVAO traffic through Aurora](#15-ivao-traffic-through-aurora)
16. [Session, log and photos](#16-session-log-and-photos)
17. [Keys](#17-keys)
18. [Files and folders](#18-files-and-folders)
19. [Problems and answers](#19-problems-and-answers)

---

## 1. What you need

- **Windows 10 or 11**, 64 bit. Nothing to install: Circuit Trainer is **one file** (`CircuitTrainer.exe`). Put it in a
  folder of its own and double click it. If Windows SmartScreen warns about an unknown program, choose *More info*
  and then *Run anyway*.
- **A map of the aerodrome**, made with **ATCRoom Map Creator** (a separate download, on
  <https://github.com/ATCRoom/MapCreator/releases>). The Map Creator turns your own files (IVAO sector files,
  EuroScope, AIP charts, KML…) into the open ATCRoom map format (`docs/MAP-FORMAT.md`). Circuit Trainer does not
  come with maps: the maps you make are for **your own use and for simulation only**, never to be distributed.
- For a **lesson between two PCs**: the same version of the program on both PCs and an internet connection (or a VPN,
  chapter 14).
- *Optional:* **Aurora** (the IVAO client) on the same PC, to show the IVAO traffic and use its METAR (chapter 15).

The program works the same with or without IVAO.

## 2. Starting for the first time

1. Start `CircuitTrainer.exe`. The window of the instructor opens maximised. In the middle of the map a message tells
   you what to do first: **open a map**.
2. The panels (SETTINGS, TRAFFIC GENERATION, TRAFFIC LIST, AIRCRAFT CONTROL, STRIP BAY) are **floating panels**.
   At the first start the first four are closed to small boxes with a symbol in the top right corner of the map
   (⚙ ✎ ☰ ✈); click a box to open the panel (chapter 4). The strip bay opens as a narrow floating panel.
3. Open **⚙ SETTINGS › Map › Open map…** and choose the map of your aerodrome. Then place the runway in use
   (chapter 3).

Dark panels are the default. **Settings › Appearance › Panels** switches to light panels (from the next start).

## 3. Setting up an aerodrome

Everything you set up for an aerodrome is **saved in a file of that aerodrome** (one file per ICAO code, in the
`Aerodromes` folder): its runways, gates, exits, helipads, circuit sides and heights, views. The next time you open the
same map you find everything as you left it.

### 3.1 The map

**Settings › Map › Open map…** shows the maps of the shared maps folder (`%AppData%\ATCRoom\Maps`). Open the one you
want. The map layers (boundaries, MVA, FIX, VRP, airports, navaids, CTR, ATZ and the ground layout: taxiways,
aprons, buildings…) are turned on and off in **Settings › Map layers**; the choice is remembered for each map.

### 3.2 The runway in use

If the map holds no runway yet, or you want another one:

1. **Settings › Map › Name / elevation ft**: write the name of the runway (for example `14`) and the elevation of its
   threshold in feet. The elevation matters: the altitudes of the labels and of the circuits are counted from it.
2. Press **Place on the map** (zoom in with the mouse wheel first), then click the **threshold** of the runway and
   then its **end**. The same runway the other way (for example 32 for 14) is made for you at the same place, with
   the same sides; you only place one of the two. If aircraft are flying you are asked first: they are removed.
3. **Runway in use** shows one button for each runway placed: click the one in use. The circuits are flown on it.
   Changing the runway removes the aircraft flying (asked first).
4. **Remove runway** deletes the runway in use (and its opposite) from the aerodrome file.

**From runways.par…** takes the thresholds from the `runways.par` file of AuroraPAR (no clicks needed).

### 3.3 Range rings, pressure, view

- **Range rings NM**: thin circles around the middle of the runway, out to 30 NM, every **1, 2, 5 or 10 NM** (5 by
  default); the distance is written on each ring. The **RINGS** button on the bar of the map turns them on and off
  (instructor's map only).
- **Pressure**: **QNH** shows altitudes, **QFE** shows heights above the runway. The choice is for the aerodrome and
  changes *everything* shown and written: the labels (ALT / HGT), the heights of the circuits, the boxes of Traffic
  generation and Aircraft control, the Traffic List.
- **View buttons 1 and 2** on the bar of the map: level 1 is a view of the aerodrome (10 NM across), level 2 the area
  around it (20 NM). Right click on either button: *Keep this view* (the view you have now, for this aerodrome) or
  *Default*.

### 3.4 The circuits of the aerodrome (Settings › Circuits)

- **Sides**: for each kind of circuit at this runway, left- or right-hand: *Conventional, wide*; *Jet (overhead,
  close)*; *SFO*. A new aircraft takes this side unless you choose another.
- **Long final / IP NM**: the distance of the **long final** (where LONG FIN aircraft start) and of the **initial
  point** before the threshold.
- **Variation**: the magnetic variation of the aerodrome (for example `3E` or `2W`; empty: 0). The **radials of the
  departures are magnetic**; the headings you give on the label are true.
- **Altitudes of each class**: a table with one row for each aircraft class and the columns *conventional, wide,
  close, IP, break, HK* (High Key). White = this aerodrome's own value; grey = the class's value (an empty box).
  Saved at once. **Import heights…** copies them from another aerodrome of Circuit Trainer.

### 3.5 Gates, exits, helipads (Settings › Gates and RWY exits)

- **Entry gates** are where arriving and crossing aircraft start. At least **5 NM** from the field. Write a name (empty:
  the name of the nearest VRP of the map) and press **ADD GATE**, then click the place on the map.
  **REMOVE GATE** removes the one written.
- **Runway exits**: where landing aircraft leave the runway; each aircraft takes the first exit it reaches slowly
  enough. Write a name (empty: the next letter), press **ADD EXIT** and click on the edge of the runway.
- **Helipads**: for helicopters. **ADD HELIPAD** and click the place (a name; empty: H1, H2…). Any other point of the
  map can also be clicked when a helicopter is sent somewhere.

### 3.6 The base traffic (Settings › Aircraft)

Used by **Quick** and by any aircraft added without a callsign or type: the **callsign prefix** (1 to 6 letters or
digits; empty: `SIM`, so IBLA gives IBLA1, IBLA2…), the **class** and the **ICAO type** (free text: C172, F16, TOR…).
**Formation separation** says how a formation landing to a full stop is spaced: *full* (the next one lands once the
one ahead has left the runway) or *reduced* (when it has rolled a third of the runway).

### 3.7 The weather

See chapter 12.

---

## 4. The window of the instructor

The window is the **map** (a top view of the aerodrome, north up) with a **bar** on it and the **panels** floating
over it.

### 4.1 The bar on the map

The bar sits at the top middle of the map. Drag it by its grip **⋮⋮** to move it; double click the grip to bring it back
to the top middle. From left to right:

| Button | What it does |
|---|---|
| **PAUSE / RUN** | Shows **PAUSE** while the simulation runs: click it to stop it. Stopped, it shows **RUN**, lit in yellow: click it to go on. Also the **Space** bar or the **Pause** key. |
| **View 1 · 2** | The two views of the map (chapter 3.3). Keys F1 / F2 (Ctrl+F1 / F2 when an aircraft is selected). |
| **RINGS** | Range rings on / off. |
| **NORTH / RWY** | **NORTH**: the map with the north up. **RWY**: the map turned so that the runway in use lies across the screen, its threshold on the left; click **RWY** again to turn it round (threshold on the right). The labels stay upright. |
| **START SESSION / END SESSION** | The session of the training or the exam, for the log (chapter 16). |
| **STUDENT** | Opens the student console on this PC, as a second window (chapter 14). |
| **LESSON** | A lesson between two PCs: start one or join one (chapter 14). It is lit while a lesson is on. |
| **PHOTO** | A picture of the map with all its labels, for the debriefing (also **Ctrl+S**, or the **middle mouse button** on the map). |
| **📁** | Opens the folder of the photos of this session. |
| **LOGS** | Opens the folder of the logs (a CSV file for each session). |
| **IVAO** | The IVAO traffic through Aurora, shown on the map (chapter 15). |

At the top of the map the word **PAUSED** shows when the simulation is stopped, and the **METAR** is always written
there too (whole, or only its wind and QNH: Settings › Weather).

### 4.2 The panels

There are five panels. When closed, each one is a small box with its symbol in the top right corner of the map; click
it to open the panel.

| Symbol | Panel | For |
|---|---|---|
| ⚙ | **SETTINGS** | the aerodrome, the circuits, the simulation, the weather… (chapter 12) |
| ✎ | **TRAFFIC GENERATION** | making aircraft (chapter 6) |
| ☰ | **TRAFFIC LIST** | the aircraft flying and the flight plans waiting (chapter 7) |
| ✈ | **AIRCRAFT CONTROL** | the orders to the selected aircraft (chapter 8) |
| ▤ | **STRIP BAY** | the strips of the flights (chapter 13) |

Every panel **floats** over the map:

- drag its **bar** (the title) to move it;
- drag the **grip in its bottom right corner** to resize it; double click the grip: as tall as its content again;
- **▴** on its bar closes it to its small box in the top right corner; a click on the small box opens it again. The
  bar of an open panel shows a short summary of what it holds (for example *3 flying · 2 waiting*);
- a click on a panel brings it to the front.

The place, size and state of each panel are remembered. A panel always stays inside the window.

### 4.3 The messages

Each panel has a **message line at its bottom**: errors and answers appear there (for example *Select an aircraft
first*). Keep the panel open to read them.

---

## 5. The map and the labels

### 5.1 The mouse on the map

| Action | Result |
|---|---|
| Mouse wheel | Zoom. The point under the mouse stays under it. |
| Drag on empty map | Moves the map. |
| Click on an aircraft (its symbol or its label) | Selects it (yellow); its panel AIRCRAFT CONTROL and its big label show it. |
| Drag **from the symbol** of an aircraft | A line follows the mouse with a heading; release it to give that **heading** (the aircraft leaves its circuit, chapter 8). **Esc** while dragging: nothing is given. |
| Drag a **label** | Moves the label (it keeps its place relative to its aircraft). |
| Right click on an aircraft | The menu of all its orders. |
| Middle button | A PHOTO. |
| Click on the hollow symbol of a plan waiting | Opens that plan in TRAFFIC GENERATION. |

When the program asks for a point (a runway, a gate, a break point, a helipad) a message tells you to click it on the
map; **Esc** cancels.

### 5.2 What the symbols mean

- A **circle** is an aircraft; a **diamond** is a helicopter; a **larger symbol** is a formation.
- A short **line from the symbol** shows where it will be in **30 seconds**. The **dots** behind it are its trail (one
  every 4 seconds of simulation).
- **White**: flying. **Grey**: on the ground. **Yellow**: the selected aircraft.
- **Light blue squares** with a line one minute long: IVAO traffic (chapter 15), shown only.
- A **triangle** with a name: an entry gate. A short **bar across the runway edge**: a runway exit. A small **circle**
  with a name: a helipad.
- **Hollow symbols** with a small label: the flight plans **waiting** in the Traffic List, at the place where they will
  start (WAITING TFC; the button beside WAITING in the Traffic List shows or hides them).
- Thin lines every NM along the **extended centreline** of the runway in use.

### 5.3 The label of an aircraft

A label that is **not selected** has three lines:

```
IBLA1            callsign (a formation: IBLA1 F2)
015 090          height in hundreds of feet, ground speed in knots
DOWNWIND CONV L F/S
```

- Line 2: with **QNH** the altitude, with **QFE** the height above the runway (chapter 3.3). `015` means 1,500 ft.
- Line 3: the **leg** it flies, the **circuit** (CONV, CLS, WIDE, OVH, SFO), its **side** (L or R) and what it does at the
  end of the final (F/S, T/G, L/A, L/P, DEP). When the next circuit has been changed, `>OVH` (for example) follows:
  the circuit it will fly next.
- The legs: UPWIND, CROSSWIND, DOWNWIND, EXT DOWNWIND, BASE, FINAL, JOIN (joining the circuit), INITIAL, CIRCLING,
  CLIMB / HOLD / HIGH KEY (SFO), ORBIT, GO AROUND, MANUAL (flown by hand), TRANSIT (crossing), LANDED (with its
  exit: `LANDED EXIT B`).

The label of the **selected** aircraft is larger and is the quickest way to give orders (chapter 8). It is built as
rows: a title with the callsign, the class and the leg; then the fields — **ALT** (or **HGT** with QFE) and **SPEED**,
**PATTERN**, **SIDE**, **BRK** (break), **LAND** (formations) and **END** — then **DCT TO** and **NOW**.
In each field the value **now** is yellow and the other choices are outlined; a click chooses one, the mouse wheel on
the row moves to the next one.

---

## 6. Traffic generation

The panel has two tabs: **Quick** and **Advanced**.

### 6.1 Quick

Five buttons make an aircraft at once, using the base traffic of the aerodrome (class, ICAO type and callsign prefix of
Settings › Aircraft) and the circuit side of the aerodrome. They all end with a full stop.

| Button | What the aircraft does |
|---|---|
| **LONG FIN** | From the long final, straight in, full stop. |
| **INBOUND IP** | From the long final at the height of the initial point, kept to the IP, then the overhead, full stop. |
| **DEPARTURE** | From the runway, away along the runway heading, up to 3,000 ft QNH. |
| **LOCAL CONV** | From the runway: take-off, a conventional circuit, full stop. |
| **LOCAL WIDE** | From the runway: take-off, a wide circuit, full stop. |

A Quick aircraft is in the **FLYING** list until it is gone. For every other flight, use Advanced.

### 6.2 Advanced

Advanced writes a **flight plan**. You can start it at once (**ADD NOW**) or put it in the Traffic List
(**TO LIST**), to start it later with ▶ or with START ALL (chapter 7). Rows that do not apply to the flight you are
writing are hidden.

**What the aircraft is**

| Row | Meaning |
|---|---|
| **Type of AC** | The **class** (Light, Turboprop, Jet, Helicopter, or one you made): it gives the performance (chapter 11). |
| **ICAO type** | Free text (C172, F16, TOR, EUFI…), shown on the strips and in the log. The class stays for the performance. |
| **Callsign** | Optional. Empty: the next free callsign of the aerodrome's prefix. A formation: write the leader ending with its number (VIPER01); the others follow (VIPER02…). |
| **Rules** | **VFR** or **IFR**. They appear on the strips (V green, I blue). |
| **Formation** | **SINGLE**, 2, 3 or 4 aircraft: one symbol and one label until they split. **Break interval** (the seconds between the aircraft at the split) appears for formations. |

**Type of flight**

| Type | What it is |
|---|---|
| **ARRIVAL** | From a gate or the long final: its circuits, then it lands. |
| **LOCAL** | From the runway: it takes off, flies its circuits and lands. |
| **DEPARTURE** | From the runway: it takes off and leaves, at once (**OUT**) or after its circuits. Gone 10 NM out. |
| **TRANSIT** | A crossing from a gate to another gate, straight, over the field or with circuits; gone at the exit. |

**Where it starts and where it goes**

| Row | Meaning |
|---|---|
| **Start from** | A **gate** (it starts there and joins its circuit by itself); **LONG FIN** (on the final at the long final distance, on the glide path); **RWY** (on the runway: a take-off); **POINT** (a place you click on the map or write as radial and distance from the middle of the runway; drag its diamond to move it). |
| **Exit to** (departures and crossings) | **RWY HDG** (along the runway heading), **RADIAL**, a gate, or a **POINT**. |
| **Radial °M** | The radial to intercept, **magnetic**, outbound, from the middle of the runway (**MIDDLE**) or from a point you click (**FROM A POINT**). Above 400 ft the aircraft turns to intercept it. |
| **Exit turn** | **L** or **R**: the first turn after take-off to the way out. It lights by itself with the side of the runway the exit is on; click to change it. |
| **Exit altitude ft** | The altitude it climbs to when it leaves (empty: 3,000 ft QNH, and at least 1,000 ft above the runway). |
| **Via** (crossings) | **DIRECT** (straight from gate to gate), **OVERHEAD** (over the middle of the runway) or **CIRCUITS** (into the circuit from *Start from*, its circuits, then out to the exit gate). |

**The circuit**

| Row | Meaning |
|---|---|
| **Pattern type** | **CONV**, **CLS**, **WIDE**, **OVH**, **SFO** (chapter 10). After a take-off only CONV or WIDE. From the long final: CONV, CLS or WIDE. |
| **Pattern entry** | **DW** (a 45° entry onto the downwind), **BASE** (straight to the start of the base, then the final), **IP** (to the initial point: the overhead), **HK** (to the High Key: SFO), **FINAL** (straight in, onto the final 4 NM out; from LONG FIN: from the long final), **CIRCLING** (only from LONG FIN: approach to the opposite runway, then aside onto the downwind). IP goes with OVH and HK with SFO. |
| **Pattern side** | **L** or **R**. *AD* (the default) means the side of the aerodrome for that circuit, taken when the aircraft starts. |
| **HK altitude ft** | Only with SFO: the High Key of this aircraft (empty: Settings › Circuits). |
| **On Final** | What every final does, apart from the last one: **T/G** (touch and go), **L/A** (low approach: down to the short final, about 200 ft, then up to circuit height), **L/P** (low pass: down to 400 ft and on at 400 ft along the runway). **F/S** lands at the first final. For helicopters also **HPAD** (lands on a helipad). |
| **Numb. of Circuits** | How many circuits **more** than the first one an arrival or a local flies (1, 2, 3, 4 or **∞**, on and on); for departures and crossings, the circuits before they leave. **OUT**: a departure away at once. |
| **Helipad** | For helicopters: **RWY**, a registered helipad, or **POINT** (clicked on the map). |
| **Height ft** | The height it arrives at, at the gate (empty: 500 ft above its circuit). It reaches the circuit height before the circuit. For a crossing: its height. |
| **Delay min** | For the Traffic List: minutes after START ALL. |

**The buttons.** **ADD NOW** starts the aircraft at once. **TO LIST** puts the plan in the Traffic List. When you
change a plan from the list, **TO LIST** becomes **UPDATE** and **CANCEL** appears: the plan stays as it was.

**Scenario.** **SAVE…** saves all the plans of the list in a file (a *scenario*, `.json`), **LOAD…** loads one; its plans
replace those of the list. If the scenario was made for another aerodrome, a message warns that its gates may be missing.

---

## 7. Traffic List

The panel has two lists.

### 7.1 FLYING

Every aircraft in the simulation, one thin row each. On the row:

- a click **selects** it;
- **II** freezes it where it is (again: lets it go on);
- **↺** makes its plan again (a new aircraft from the same plan);
- **✕** removes it.

Above the list: **PAUSE / RUN** and **▼ WAITING LIST**, which takes **every** aircraft out of the simulation and puts
its plan back in WAITING.

### 7.2 WAITING

The flight plans written with **TO LIST**. Each row has:

- **☐** to tick it (for GROUP);
- **▶** start it now; **⏸** hold it (it does not start; again: it goes on); **⧉** duplicate it (the next callsign, one
  minute later); **✕** remove it;
- a click on the row opens the plan in TRAFFIC GENERATION, to change it (UPDATE).

Under the list:

- **START ALL**: every plan waiting starts after its own **Delay min** (held ones stay held). The time of each starts
  counting at once, and it is shown on the plan.
- **GROUP**: the ticked rows become **one formation**, led by the callsign of the first one (at most 4 aircraft).
  When one formation row is ticked the button becomes **UNGROUP**.
- **REMOVE ALL**: every row out of the list (the aircraft already flying go on).

The button next to **WAITING** shows or hides the plans waiting on the map.

---

## 8. Aircraft control

Select an aircraft (click it on the map, or in the FLYING list, or **Tab** / **Shift+Tab** to go to the next one or the
one before). The panel **AIRCRAFT CONTROL** shows it; its **big label** on the map has the same orders. Use whichever is
quicker. A **right click** on the aircraft opens a menu of all the orders.

The orders have **colours**: **blue** = *where it goes*; **orange** = *what it does now*; **red** = the *urgent* ones;
**grey** = *not possible where it is*. The key of each button is written on its right (and in its tooltip).

### 8.1 Altitude, speed and heading

Three boxes show the **value now**.

- Click in a box, or turn the **mouse wheel** over it, or type and press **Enter**; **Esc** goes back.
- **Altitude** (key **L**): hundreds of feet (`25` = 2,500 ft) or the feet; with QFE, the height. Wheel: ±100 ft.
- **Speed** (key **K**): knots. Wheel: ±10 kt.
- **Heading** (key **V**): degrees, true. Wheel: ±5°. You can also **drag from the symbol on the map**.

Altitude and speed keep the aircraft **in its circuit** (**AUTO** in the label gives it back the circuit's own value).
A **heading** takes it **out of its circuit**: it flies that heading and its label shows **MANUAL**. **Rejoin circuit**
(key **R**) puts it back in.

In the big label, click **ALT** or **SPEED** to open a row of presets under it (AUTO and the usual values of its class);
the wheel on the label changes the value too. A value chosen with the wheel and not ordered yet is shown with a **?**,
or as a change: `015↑020` (altitude) and `090→110` (speed).

### 8.2 Next circuit

What the aircraft will do **from the next touch and go or go-around**, not now.

- **Pattern**: CONV, CLS, WIDE, OVH, SFO (keys **1 2 3 4 5**). On the ground and taking off: only CONV or WIDE.
- **Side**: **Left-hand** or **Right-hand** (keys **←** and **→**). It is the side of the *next* circuit; to move the
  circuit it flies now, use **Change side**.
- **Break** (overhead and close pattern only): **MID**, **BEG**, **END**, **STBY** (standby: on along the initial until
  BREAK NOW), or **At a point…** (click it on the runway).
- **End**: what it does at the end of **this final**: **F/S**, **T/G**, **L/A**, **L/P**, **DEP** (keys **F T A P D**).
  **DEP**: touch and go, then it leaves straight out (you remove it or fly it).
- **Helipad…** (helicopters): the final ends on a helipad.
- **Land** (formations): who lands; the others go around (from a circuit at most 2 land).

### 8.3 Now, in the circuit

At once. See chapter 9 for the difference between the two groups.

**Direct to** (blue):

| Button | Key | Goes |
|---|---|---|
| Downwind conv. | F1 | At once onto the downwind of the conventional circuit. |
| Downwind close | F2 | At once onto the downwind of the close pattern (narrower). |
| Base conv. | F3 | At once straight to the start of the base of the conventional circuit, then the final. |
| Base close | F4 | At once straight to the start of the base of the close pattern, then the final. |
| Initial point | F5 | At once straight to the initial point, then the overhead. |
| High Key | F6 | At once straight to the High Key, then the SFO (on the final: along it to the High Key). |
| Final | F7 | At once onto the final: from beyond the base straight onto it (45° at most); nearer, by the base point on the side it flies; too high, farther out. Then down the glide path. |
| Helipad… | | Helicopters: at once straight to a helipad, a full stop there. |

**Now** (orange):

| Button | Key | Does |
|---|---|---|
| Extend downwind | E | On the downwind: straight on beyond the turn to the base, until *Turn in now*. |
| Turn in now | N | On the downwind (also extended, or in an orbit): turn in now towards the final, by a short base. Still joining: straight to the start of the base. |
| Break now | B | Overhead on the initial, close on the upwind: it breaks now (close: above 400 ft). Also during a touch and go before a close pattern. |
| Orbit left / right | O | One orbit (360°) to that side, then on. In its circuit only the side away from the runway. **Shift**: on and on (thick border); then a click: on. |
| Wide circuit now | W | This circuit becomes the wide one now, on the wide side of this runway. |
| Hold at High Key / Start SFO | H | SFO: waits circling over the High Key; on hold the button is *Start SFO*, which starts at the next pass over the High Key. |
| Split formation | U | Formation break-up: each aircraft on its own, one interval after the other. |
| Expedite taxi | X | Landed: off the runway quickly. |

**Urgent** (red):

| Button | Key | Does |
|---|---|---|
| Go around | G | Only on the final: it stops descending, passes the threshold and keeps the runway heading until a new order. |
| Change side | C | This circuit now on the other side: an S over the aerodrome onto the other downwind (on the initial or the upwind: the turn to the other side). |
| Breakout | Shift+U | Out of the circuit at once: on the downwind a turn away from the runway to 90° from it; on the base on round past the final. Then flown by hand (MANUAL) at its altitude, until *Rejoin circuit*. |

**Remove aircraft** (key **Del**) asks first.

---

## 9. DCT TO and NOW: the difference

The two groups look alike and are the most mixed up. Remember it like this:

> **DCT TO** (blue) = **WHERE it goes.** The aircraft leaves where it is, goes straight to that point of the circuit
> and goes on with the circuit from there.
>
> **NOW** (orange) = **WHAT it does now**, where it is, without being sent anywhere else.

Think of the circuit as a ring around the runway:

```
                 CROSSWIND
            ┌───────────────┐
            │               │
   UPWIND   │   RUNWAY ⇨    │   DOWNWIND
            │               │
            └───────────────┘
                   BASE        ... then FINAL, back to the runway
```

**DCT TO** puts the aircraft on a **point of the ring**:

- *Downwind conv.* — it flies straight to the downwind, wherever it is, and goes on round.
- *Base conv.* — it flies straight to the **start of the base** and turns final from there.
- *Initial point* — straight to the initial point, to start the overhead.
- *High Key* — straight to the High Key, to start the SFO.
- *Final* — straight onto the final.

**NOW** orders act **where the aircraft is**, on the ring it is already flying:

- *Turn in now* (BASE in the label) — from **the downwind it flies**, it turns base now. The aircraft is **already on
  the downwind** and just cuts the leg short.
- *Extend downwind* — on the downwind, it flies on beyond the usual turn.
- *Orbit* — a circle where it is.
- *Break now* — breaks here, over the runway.

Example. An aircraft on the **downwind** abeam the runway.
- *Turn in now*: it turns base **from there**, after a short leg.
- *Base conv.* (DCT TO): it goes **straight to the start of the base** of the normal circuit, wherever that is (it may
  be farther or nearer than where it would have turned).

A grey button means the order makes no sense where the aircraft is now (for example *Turn in now* on the final).

---

## 10. The circuits

All heights of the circuits are **heights above the threshold** of the runway in use; with QNH the elevation of the
threshold is added when they are shown. The values come from the **aircraft class** (chapter 11) and, where the
aerodrome has its own altitudes, from the table in Settings › Circuits (chapter 3.4).

| Circuit | Label | How it is flown |
|---|---|---|
| **Conventional** | CONV | The rectangular circuit: upwind, crosswind, downwind, base, final. |
| **Close pattern** | CLS | The jet circuit **without the initial and the break**: no crosswind and no base. From the upwind (or from a go-around, above 400 ft) a 180° turn to the downwind, the downwind at the narrow width of the jet circuit, then a descending 180° turn that rolls out on the final. |
| **Wide circuit** | WIDE | As the conventional one, **further out** (the outside downwind). |
| **Overhead** | OVH | The "run and break": from the **initial point** along the runway at the initial height and speed, the **break** (a level 180° turn, tight enough to meet the downwind distance) over the middle, the beginning or the end of the runway or a point you choose, the downwind, then a descending 180° turn onto the final. After a go-around or a touch and go it flies the **close pattern**, not the initial again. |
| **Simulated flame out** | SFO | Gliding at the best glide speed from the **High Key** over the runway, a 180° turn to the **Low Key** abeam the runway on the downwind side, the downwind gliding down, a 180° turn onto the final. At the High Key it can **hold**, circling, until *Start SFO*. After a go-around or a touch and go it flies the close pattern. |

**The final** is on a 3° glide path (or the angle of the runway). What the aircraft does at the end of it:

- **F/S**, full stop: it lands, brakes on the runway, leaves it by the first exit it reaches slowly enough and is
  removed shortly after.
- **T/G**, touch and go: it touches down, rolls a few seconds, takes off and flies the circuit again.
- **L/A**, low approach: down to the short final (about 200 ft), then up to the circuit height and the circuit again.
- **L/P**, low pass: down to **400 ft**, on at 400 ft along the whole runway, then the circuit again.
- **DEP**: touch and go, then it leaves straight out.
- **Go around** (an order, red): it stops descending, passes the threshold and keeps the runway heading until a new
  order.

**Helicopters** use their own class: they can hover, they can land on a **helipad** instead of the runway (*HPAD*), and
a helicopter departure can start from a helipad. Their circuit is lower and slower.

**Wind.** The METAR (or the wind you write) gives **one** direction and speed for all heights, with gusts when the METAR
has them. A *variable* wind (VRB) changes direction slowly by itself. The aircraft fly their headings *through* the wind,
so their ground speed, their drift and their turns change with it.

**Pilot reaction.** An order is carried out **after the pilot reaction time** (0 to 5 seconds, Settings › Simulation).

### The first values of the classes

These are the values the classes have the first time the program starts (you can change them, chapter 11). Heights are
in feet above the threshold, distances in NM.

| | Light | Turboprop | Jet | Helicopter |
|---|---|---|---|---|
| Circuit height | 1000 | 1500 | 1500 | 700 |
| Circuit speed (kt) | 90 | 150 | 200 | 70 |
| Downwind distance, conventional | 1.0 | 1.2 | 1.5 | 0.8 |
| Downwind distance, close | 0.6 | 0.8 | 1.2 | 0.5 |
| Downwind distance, wide | 2.5 | 3.0 | 3.5 | 1.5 |
| Initial (overhead): height / speed | 1500 / 110 | 1500 / 220 | 1500 / 300 | 1500 / 100 |
| SFO: High Key / Low Key | 2500 / 1200 | 4000 / 2000 | 7000 / 3500 | 1750 / 840 |
| SFO: best glide speed (kt) | 70 | 125 | 180 | 70 |

---

## 11. Aircraft classes

**Settings › Aircraft › Aircraft classes…** opens the window of the classes. Each row is a class and its performance,
for **all** aerodromes:

| Column | Meaning |
|---|---|
| **Min / Max** (kt) | The slowest and the fastest speed. |
| **Initial** (kt) | The default speed of a new aircraft, also on the long final. |
| **Final** (kt) | The speed near the threshold. |
| **Climb / Descent** (ft/min) | The largest rates. |
| **Bank** (°) | The bank limit of the turns. |
| **Brake** (kt/s) | The braking on the runway after touchdown. |

Type in a box, or turn the **mouse wheel** over it (**Shift**: 5 steps). The buttons:

- **Add class**: a new class, with the values of the Jet, at the end.
- **Duplicate**: a copy of the class whose name you have clicked (for example a slower Jet).
- **Remove**: removes the class whose name you have clicked; the aircraft of that class get another one.
- **Reset to defaults**: Light, Turboprop, Jet and Helicopter with their first values (other classes are removed; the
  altitudes of the aerodromes are kept).
- **Save** checks every value and saves it in `performance.json` (chapter 18). The new values are for the aircraft
  **added from then on**; those already flying keep theirs. **Cancel** closes without saving.

The helicopter's minimum speed is 0 (it can hover) and cannot be changed. The **altitudes of each class at an aerodrome**
are not here, they are in **Settings › Circuits** (chapter 3.4).

---

## 12. Settings

The panel ⚙ SETTINGS is divided into **sections** you open and close by clicking their title. Most of them are
described where they are used; here is the whole list.

| Section | What it holds |
|---|---|
| **Appearance** | **Panels**: dark or light (from the next start). The **colours of the strips** in the strip bay: *Departures*, *Arrivals*, *Local / transit*. |
| **Map** | Open map, runway in use, place / remove a runway, **From runways.par…**, range rings, **Pressure** QNH / QFE (chapter 3). |
| **Circuits** | Sides, long final and initial point, magnetic variation, the altitudes of each class, **Import heights…** (chapter 3.4). |
| **Simulation** | **Pilot reaction** (0 to 5 seconds) and **Speed** of the simulation: x1 (real time), x2, x4. The speed can be changed **only outside a session**; **START SESSION** puts it back to x1 and keeps it there until END SESSION. |
| **Aircraft** | The **base traffic** (prefix, class, ICAO type), **Aircraft classes…**, **Formation separation** (chapter 3.6). |
| **Network** | **My VID** and **My callsign** for the log (chapter 15). |
| **Weather** | The METAR and the wind (below). |
| **Gates and RWY exits** | Gates, exits and helipads (chapter 3.5). |
| **Map layers** | The categories of the map shown (chapter 3.1). |

Under the sections the panel writes the folders in use: the **maps** folder and the folder of the **aerodromes and
settings** (chapter 18).

### Weather: METAR and wind

- **METAR**: a line, for example `LIPI 031250Z 06012KT CAVOK 15/08 Q1018`. Write or paste it.
- **Get METAR** downloads the newest METAR of the aerodrome from the internet (aviationweather.gov, by its ICAO
  code), writes it in the box and applies its wind.
- **Apply METAR** takes the **wind** from the line in the box (direction, speed, gusts; `VRB` is a variable wind) and
  shows the METAR on the map. An **empty** box means **calm**. A line with no wind group changes nothing and says so
  (*No wind found in the METAR*).
- **On the map**: the METAR shown on the map whole, or only its **wind and QNH**.
- Under the buttons a line says what wind the aircraft feel: *Wind: 060° 12 kt*, *Wind: calm*, *Wind: variable 5 kt
  (changing slowly)*, with the gusts (*, gusts +8*). When IVAO is on (chapter 15), the METAR of the aerodrome is
  taken from the network every minute and applied.

---

## 13. Strip bay

The strip bay is the **flight progress strips** of the exercise. It has the **same strips** for the instructor and for the
student, but each one has **his own notes and his own order**: what the student writes and moves, the instructor does not
see, and the other way round.

### 13.1 Two sections

- **Coordination**: a strip for each flight plan **waiting** in the Traffic List, as if it had been coordinated to the
  tower. IVAO aircraft near the field (chapter 15) appear here too.
- **In contact**: the aircraft **flying**. When a plan starts, its strip moves here, **with its notes**. When you change a
  plan in the list (UPDATE) the notes follow it.

### 13.2 A strip

Each strip shows: the **callsign**, the **ICAO type**, the **rules** (**V** green, **I** blue), the **entry** (the gate, LF
for the long final, RWY…, and for a departure its exit) and the **altitude** in hundreds of feet. The **colour** of its
left edge is the **kind of flight**: departure, arrival, local or transit (chosen in Settings › Appearance). A
formation shows as `VIPER01 F2` until it splits.

### 13.3 Notes

1. **Click a strip** to choose it.
2. Click a **quick note**: **DW, BASE, FINAL, OVH, BRK, SFO, ORBIT, T/G, LAND, L/A, L/P, G/A, TKOF, FRQ CNG** — or
   write a free note in the box and press **Enter** or **ADD**.
3. **Click a note** to remove it. The last note is highlighted.

### 13.4 Moving strips

**Drag a strip** to change its place: a coloured line shows where it will go (above the strip it is over, or below it
when you drop on its lower half). A strip moves only **inside its own section**. An **IVAO** strip can also be dragged
from Coordination to **In contact** and back, onto the other section or its title.

---

## 14. The student console and the lesson

The student does **not** see the instructor's panels or any order. He sees what a tower controller would see: the
traffic, the runway, the weather and his own strips. There are two ways to have him work.

### 14.1 On the same PC (second monitor)

Press **STUDENT** on the bar. A second window opens (*Circuit Trainer - Student*): put it on the second monitor. It is
fed by the instructor's window, so there is nothing to connect.

The student's window shows:

- the **map** of the aerodrome with the runway in use, the extended centreline, the gates and exits;
- every aircraft as a **symbol with a label of three lines**: the callsign; the ICAO type with the **rules** well
  visible (**V** green on white, **I** white on blue; a formation shows `F2`, `F3`…); the altitude and the speed. Also a trail and a line
  30 seconds long. **Nothing else**: no leg, no circuit, no menu;
- at the top: the aerodrome, the runway, the **UTC time**, **PAUSED** when the instructor stopped the simulation, and
  the **METAR**;
- his own **STRIP BAY** (chapter 13), floating on the right: resizable, closable to a small box, or in a column;
- his own **NORTH / RWY** buttons and his own zoom (mouse wheel) and views (**F1** / **F2**). He can move the map and the
  labels with the mouse.

### 14.2 A lesson between two PCs

The instructor and the student are on **different PCs**, anywhere. Both need the **same version of the program**.

**The instructor** (the PC that flies the aircraft):

1. Open **LESSON** on the bar. Choose the kind of link (below): **Internet relays (normal)** unless you have a reason
   not to.
2. Press **START LESSON**. A **code of 6 letters and digits** appears (easy to read aloud: it never uses O / 0 or I / 1).
   **COPY CODE** copies it. The **LESSON** button on the bar becomes lit and shows the code.
3. Tell the code to the student (by chat, by voice…).
4. Under the code you see the state: *Connecting to the relay…* (the PC is reaching the internet), *Waiting for the
   student…*, then **● Student linked (his callsign)** — and *sending the map (n pieces)…* the first time.
5. Fly the exercise as always. **STOP LESSON** ends it (the same window, from the **LESSON** button).

**The student** (the other PC):

1. Open **LESSON**. In *Student* write the **code** and press **JOIN**.
2. The window of the instructor on that PC is hidden: only the **student console** is shown. It says *Lesson CODE:
   waiting for the instructor…* until the instructor is there. If the instructor stops hearing him it says **LINK LOST**.
3. To leave the lesson, **close the student console**: the program comes back to the instructor's window.

What the student's PC does meanwhile: its own simulation is **paused** and its own IVAO is **off** (a PC has only one
Aurora client). The student's own **IVAO** button shows the IVAO traffic through *his own* Aurora, and his callsign and
VID go in the instructor's log: from IVAO when he is connected, otherwise **Settings › Network › My callsign / My VID**.

**What travels, and what is kept.** The **map** goes to the student at the start, in pieces, and is kept **in his PC's
memory only**: it is never saved on his disk. Then, **every half second**, the traffic, the runway, the weather and the
strips as the student sees them. Every message is **encrypted with the code** of the lesson: do not give the code to
anyone who is not in the lesson. A new lesson has a new code.

#### The two kinds of link

| Kind | When | What you need |
|---|---|---|
| **Internet relays (normal)** | Normal use, anywhere. | Nothing to install, no ports to open. The two PCs meet through public relay servers on the internet. |
| **Direct (VPN or home network)** | The relays are slow or blocked. | Both PCs in the **same VPN** (Hamachi, Radmin VPN, ZeroTier, Tailscale…) or the **same home network**. |

**Both PCs must have the same kind selected.** For **Direct**: the instructor starts the lesson and **tells the student the
IP address** the window shows (and the code); the student writes that address in *Instructor's IP address* and the code,
then JOIN. Direct uses **port 18831**; Windows may ask the instructor **once** to allow the program in the firewall:
choose *Allow*. The step by step guide for the VPNs is in `docs/VPN.md`.

#### If the student stays on "waiting for the instructor"

1. **Same version** on both PCs? (A different version does not understand the other and gives no error.)
2. **Same kind of link** selected on both: both *Internet relays*, or both *Direct*.
3. The **code**: have the student check it letter by letter; or send it with COPY CODE and have him paste it. Then he
   closes the student console and joins again.
4. **Wait a minute.** If a network blocks the usual way, the program tries another relay by itself (the student after
   15 seconds, the instructor after 60).
5. Look at what the instructor's window says. *Connecting to the relay…* means **his** PC does not reach the internet
   (firewall, antivirus, company network). *Waiting for the student…* means his PC is linked and the problem is on the
   student's side.
6. Still nothing: send the file **`Link.log`** from the **Logs** folder (the **LOGS** button) of **both** PCs. It says which
   relay each one reached and the errors; it holds no names or personal paths.

---

## 15. IVAO traffic through Aurora

If you use **Aurora** (the IVAO client) on the same PC, Circuit Trainer can **show** the IVAO traffic around the
aerodrome and use its METAR. It only **reads**: nothing of the simulation is ever sent to the network.

1. Open Aurora, with its **link for other programs** turned on (the program reaches it on this PC).
2. Press **IVAO** on the bar.

The **IVAO** button tells you the state:

| Button | Meaning |
|---|---|
| **IVAO** (off look) | Off. Click: on. |
| **IVAO ?** (orange) | On, but Aurora is not found on this PC (not open, or its link off). |
| **● IVAO** (orange) | Aurora found, but not connected to IVAO: no traffic yet. |
| **● IVAO** (green) | Aurora is connected to IVAO with a callsign: the traffic is on the map. |

What you get:

- the IVAO traffic within 60 NM on the map, as **light blue squares** with a line one minute long and the callsign
  with altitude and speed beside it;
- a **strip** in *Coordination* for each IVAO aircraft within **25 NM** (its ICAO type, rules, kind of flight and
  level come from its IVAO flight plan, read at most once a minute); the entry is not known;
- the **METAR** of the aerodrome from the network, every minute, into the METAR box and applied;
- your **callsign** (from Aurora, in the log) and your **VID** (from the list of who is connected; if it cannot be found,
  the one written in **Settings › Network**).

The programs work the same **with or without IVAO**. IVAO **off** only removes the IVAO traffic from the instructor's
map; Aurora stays connected. Network trouble (Aurora lost, IVAO off…) is written in the **log** of the session.

---

## 16. Session, log and photos

### 16.1 Session

**START SESSION** marks the beginning of the training or of the exam. It starts a **new log file**, the time goes back to
**0:00:00**, a **new folder of photos** is made, and the simulation speed is put back to **x1** (x1 until the end).
**Nothing starts flying** by itself. The button then reads **END SESSION h:mm:ss** (green) with the time.

**END SESSION** asks first: the log is closed with its summary, the simulation is **paused** and the photos that follow
go to a folder of their own. Nothing is written in the log outside a session.

### 16.2 The log

A **CSV file for each session** in the **Logs** folder (the **LOGS** button): `Log_2026-11-12_20-31-05.csv`. It opens in
Excel in any language. At the top: the aerodrome, the runway, the start of the session (UTC), the network, and the
**callsign and VID** of the instructor and of the student (empty when not connected to IVAO). Then **one row for each
event**:

| Column | Meaning |
|---|---|
| Sim time | The time of the session (h:mm:ss, from 0). |
| UTC | The clock time. |
| Source | **SIM** (what the simulation did), **INSTRUCTOR** (his orders), **STUDENT**, **IVAO** (network). |
| Callsign, ICAO type, Rules, Runway, Pattern | The aircraft and its circuit. |
| Event, Detail | What happened. |

The events are the take-offs, touch and goes, landings, go-arounds, low approaches and low passes, the orders of the
instructor, the photos, the start and end of a lesson, the IVAO traffic in range, and any error. At the end a
**summary for each aircraft**: type, rules, take-offs, touch and go, landings, go-arounds, low approaches, low passes.
If something goes wrong in the program, it is written in the log too.

### 16.3 Photos

**PHOTO** (or **Ctrl+S**, or the **middle mouse button** on the map) saves a **picture of the map with all its labels**,
for the debriefing. It goes in the **Photos** folder, one subfolder for each session, as
`Photo-03_T+12m40s.png` (the number and the simulated time). The **📁** button opens the folder of the photos of this
session. Each photo is written in the log.

---

## 17. Keys

**F12** shows this list in the program. The keys of the orders work on the **selected aircraft** and **not while you are
writing in a box**.

| Key | Order |
|---|---|
| **1 2 3 4 5** | Next circuit: CONV, CLS, WIDE, OVH, SFO |
| **← →** | Side of the next circuit (left, right) |
| **F T A P D** | End of the final: F/S, T/G, L/A, L/P, DEP |
| **F1 F2** | DCT TO downwind conventional, downwind close |
| **F3 F4** | DCT TO base conventional, base close |
| **F5 F6** | DCT TO initial point, High Key |
| **F7** | DCT TO final |
| **C** | Change side now |
| **B** | Break now |
| **E** | Extend downwind |
| **N** | Turn in now |
| **O** | Orbit (the side lit; both lit: the side it turns). **Shift+O**: on and on; orbiting on and on: go on |
| **G** | Go around |
| **W** | Wide circuit now |
| **H** | Hold at the High Key / Start SFO |
| **X** | Expedite taxi |
| **U** / **Shift+U** | Split formation / breakout |
| **R** | Rejoin the circuit |
| **Del** | Remove the aircraft (asked first) |
| **L / K / V** | To the altitude / speed / heading box (type, **Enter**; **Esc** goes back) |
| **Tab / Shift+Tab** | Next / previous aircraft |
| **Space** or **Pause** | Run / pause |
| **F1 / F2** | The two views of the map when **no** aircraft is selected; with one selected, **Ctrl+F1 / Ctrl+F2** |
| **Ctrl+S** | Photo of the map |
| **F12** | The list of the keys |

A key that cannot be used where the aircraft is says so in the message line (*B: not now for IBLA1*).

---

## 18. Files and folders

Normally everything is in `%AppData%\ATCRoom\`:

| Where | What |
|---|---|
| `CircuitTrainer\CircuitTrainer.settings.json` | The settings: the places of the panels, the colours, your callsign and VID, the last lesson… |
| `CircuitTrainer\performance.json` | The classes of aircraft (chapter 11). Open format, can be edited by hand. A file written by a newer version is used but never overwritten. |
| `CircuitTrainer\Aerodromes\<ICAO>.json` | What you set up for each aerodrome (chapter 3). |
| `CircuitTrainer\Photos\` | The photos, one folder for each session. |
| `CircuitTrainer\Logs\` | The logs `Log_*.csv`, and `Link.log` (what the lesson link does, kept small). |
| `Maps\` | The maps, **shared** with the other ATCRoom programs (made with ATCRoom Map Creator). |

The scenarios (`.json`) go where you save them. Maps are in the open ATCRoom map format (`docs/MAP-FORMAT.md`); the
program never loses what it does not know in a map.

**Portable mode.** Put an (empty) file named `CircuitTrainer.settings.json` **beside** `CircuitTrainer.exe`: from then on
everything is kept beside the program, the `Maps` folder too. Useful for a USB stick.

---

## 19. Problems and answers

**"Open a map" in the middle of the screen.** Settings › Map › Open map…. No maps yet: make one with ATCRoom Map
Creator (<https://github.com/ATCRoom/MapCreator/releases>).

**"Place the runway in use".** Settings › Map: write the name and the elevation, then *Place on the map* (or *From
runways.par…*).

**The aircraft do not move.** The simulation is **paused**: the bar shows **RUN**; press it (or **Space**). Ending a
session also pauses it.

**A button is grey / a key says "not now".** The order has no meaning where the aircraft is (for example *Turn in now* on
the final, *Break now* away from the initial). Chapter 9.

**I gave a heading and the aircraft left its circuit.** That is what a heading does (MANUAL). **Rejoin circuit** (key **R**)
puts it back.

**The heights are not what I expected.** Check **Pressure** in Settings › Map: **QNH** shows altitudes, **QFE** the height
above the runway; and the **elevation** written for the runway.

**The lesson does not connect.** Chapter 14.2, *If the student stays on "waiting for the instructor"*.

**The student has the traffic but no map.** The instructor's window says *sending the map (n pieces)…*: wait a moment.
The map is sent again whenever another map is opened on the instructor's PC.

**IVAO ? on the button.** Aurora is not open on this PC, or its link for other programs is off. Chapter 15.

**"Something went wrong…" message.** The program goes on. If it happens again, tell us what you were doing: it is also in
the log of the session.

**The log could not be written.** The Logs folder cannot be written (a protected folder, a full disk). Move the program
to a folder of your own, or free the space.

---

*ATCRoom Circuit Trainer is part of the ATCRoom programs. The code of its simulation engine comes from AuroraPAR
(MIT license, see `LICENSE`). For training and simulation only.*

