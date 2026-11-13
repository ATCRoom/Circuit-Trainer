# 6. A lesson between two PCs

The instructor flies the aircraft on his PC; the student sees them on his own PC, also in another city. Both PCs run the
**same version** of Circuit Trainer; nothing else to install and no port to open.

## Step 1 — Instructor: start the lesson

**LESSON** (on the bar) → choose **Internet relays (normal)** → **START LESSON**. A **code of 6 letters and digits** appears;
**COPY CODE** copies it. Tell the code to the student (by voice, by chat…).

[![Start the lesson](gifs/t6-instructor.gif)](gifs/t6-instructor.gif)

## Step 2 — Student: join

**LESSON** → write the **code** → **JOIN**. The student's console opens (the instructor's window of that PC is hidden).

[![Join](gifs/t6-student.gif)](gifs/t6-student.gif)

## Step 3 — Check the link

On the instructor's PC the window says **● Student linked** and, the first time, *sending the map (n pieces)…*: the map goes to
the student in pieces and is kept **in his memory only**. The student sees the traffic every half second.

[![The link](gifs/t6-link.gif)](gifs/t6-link.gif)

## Step 4 — Fly

Fly the exercise as in the other tutorials. **STOP LESSON** (instructor) ends it; the student **closes his console** to leave.

## If the student stays on "waiting for the instructor"

1. Same **version** on both PCs?
2. Same kind of link on both (both *Internet relays*, or both *Direct*)?
3. The **code**: check it letter by letter, or send it with COPY CODE.
4. Wait a minute: the program tries another relay by itself.
5. Still nothing: send the file `Link.log` from the **Logs** folder (the **LOGS** button) of both PCs.

## Direct link (VPN or home network)

If the relays are blocked, choose **Direct** on both PCs. The instructor gives the student the **IP address** shown in his
window; the student writes it with the code. Both PCs in the same VPN or home network; port **18831**, and Windows may ask
once to allow the program. The guide for the VPNs is [VPN.md](../VPN.md).

## Things to know

- Whoever has the code can see the lesson: a new code for each lesson. Everything is encrypted with it.
- On the student's PC the simulation is paused and IVAO is off while he is in the lesson.
- To try it alone: two copies of the program in two folders, each with an empty `CircuitTrainer.settings.json` beside it
  (portable mode), one instructor and one student.

The full description is in the [manual](../MANUAL.md#142-a-lesson-between-two-pcs).
