# 3. Build and run a scenario

Goal: several plans waiting, started together or one by one, saved in a file. About ten minutes.

## Step 1 — Fill the list

With **TO LIST** (tutorial 2) put three or four plans in **☰ TRAFFIC LIST**. They appear in the **WAITING** list and on the
map as **hollow symbols**, at the place where they will start.

[![Plans in the list](gifs/t3-to-list.gif)](gifs/t3-to-list.gif)

## Step 2 — Change, hold, duplicate

On each row of WAITING:

- a click on the row opens the plan in TRAFFIC GENERATION: change it and press **UPDATE** (or **CANCEL**);
- **▶** starts it now; **⏸** holds it (it does not start; press again to release it);
- **⧉** duplicates it (the next callsign, one minute later); **✕** removes it.

[![Change, hold, duplicate](gifs/t3-rows.gif)](gifs/t3-rows.gif)

## Step 3 — Make a formation from singles

Tick the **☐** of two or three rows and press **GROUP**: they become **one formation**, led by the callsign of the first one
(at most 4). With one formation row ticked the button reads **UNGROUP**.

[![Group](gifs/t3-group.gif)](gifs/t3-group.gif)

## Step 4 — Start them

Each plan has a **Delay min**. **START ALL** starts every plan waiting after its own delay (held ones stay held). The time
left is shown on each row. A plan that starts moves its strip to **In contact** with its notes.

[![Start all](gifs/t3-start-all.gif)](gifs/t3-start-all.gif)

## Step 5 — Save and load

In **TRAFFIC GENERATION**: **SAVE…** writes all the plans of the list in a **scenario** file (`.json`); **LOAD…** reads one and
its plans **replace** those of the list. If the scenario was made for another aerodrome a message warns you that its gates may
be missing.

[![Save and load](gifs/t3-save-load.gif)](gifs/t3-save-load.gif)

A scenario is a good exam: the same traffic for every student.

Next: [fly the traffic](4-fly-traffic.md).
