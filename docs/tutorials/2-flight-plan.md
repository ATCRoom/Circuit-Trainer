# 2. Create a flight plan

Goal: write the plans of an arrival, a local flight, a departure, a crossing and a formation. About ten minutes.

In **✎ TRAFFIC GENERATION** open the tab **Advanced**. It writes a **flight plan**. You can start it at once
(**ADD NOW**) or put it in the Traffic List (**TO LIST**) for later (tutorial 3). Rows that do not apply to the flight
you are writing are hidden.

## Step 1 — An arrival

1. **Type of AC**: *Light*. **ICAO type**: `C172`. **Rules**: *VFR*. Leave the callsign empty: the program gives the next
   one.
2. **Type of flight**: **ARRIVAL**.
3. **Start from**: one of the **gates**.
4. **Pattern type**: **CONV**, **Pattern entry**: **DW** (a 45° entry onto the downwind), **Pattern side**: *AD* (the side of
   the aerodrome).
5. **On Final**: **F/S** (full stop). Press **ADD NOW**.

[![An arrival](gifs/t2-arrival.gif)](gifs/t2-arrival.gif)

The aircraft starts at the gate, reaches the circuit height, joins the downwind and lands.

## Step 2 — A local flight with circuits

1. **Type of flight**: **LOCAL**. **Start from**: **RWY** (it takes off).
2. **Pattern type**: **CONV**. **On Final**: **T/G** (touch and go).
3. **Numb. of Circuits**: **2**: two circuits more than the first one, then a full stop.
4. Press **TO LIST** (not ADD NOW): the plan waits in the Traffic List.

[![A local flight](gifs/t2-local.gif)](gifs/t2-local.gif)

**On Final** says what *every* final does apart from the last: **T/G** touch and go, **L/A** low approach (down to about
200 ft), **L/P** low pass (400 ft).

## Step 3 — A departure

1. **Type of flight**: **DEPARTURE**.
2. **Exit to**: **RADIAL**. **Radial °M**: for example `270` — it is **magnetic**, from the middle of the runway.
3. **Exit turn**: the program lights it by itself; click to change. **Exit altitude ft**: empty means 3,000 ft QNH.
4. **Numb. of Circuits**: **OUT** (away at once) or a number (circuits before it leaves). **ADD NOW**.

[![A departure](gifs/t2-departure.gif)](gifs/t2-departure.gif)

## Step 4 — A crossing

1. **Type of flight**: **TRANSIT**. **Start from**: a gate. **Exit to**: another gate.
2. **Via**: **DIRECT** (straight), **OVERHEAD** (over the middle of the runway) or **CIRCUITS** (into the circuit, then out).
3. **Height ft**: the height of the crossing.

[![A crossing](gifs/t2-transit.gif)](gifs/t2-transit.gif)

## Step 5 — A formation

1. **Formation**: **2**, **3** or **4**. Write the leader's callsign ending with its number (`VIPER01`): the others follow
   (`VIPER02`…).
2. **Break interval**: the seconds between the aircraft at the split.
3. It is **one** symbol with one label (`VIPER01 F2`) until you split it (*Split formation*, key **U**).

[![A formation](gifs/t2-formation.gif)](gifs/t2-formation.gif)

## Things to know

- **Start from → POINT** lets you click a place on the map (or write its radial and distance) and drag it afterwards.
- A plan in the list can be opened with a click and changed: **TO LIST** becomes **UPDATE** and **CANCEL** appears.
- The full description of every row is in the [manual](../MANUAL.md#62-advanced).

Next: [build and run a scenario](3-scenario.md).
