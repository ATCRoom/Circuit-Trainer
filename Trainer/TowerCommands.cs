using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The instructor's commands to the aircraft of the circuits, in one place: the side panel uses them now, the
    /// commands on the label (step 4b) will use the same ones. Each returns an error message for the user, or null.
    /// </summary>
    internal sealed class TowerCommands(SimEngine engine, Airfield airfield)
    {
        // Each value with its name (menus) and its short name (buttons, label).
        public static readonly (CircuitStart Start, string Text, string Short)[] Starts =
        [
            (CircuitStart.Downwind, "Downwind", "DWN"),
            (CircuitStart.LongFinal, "Long final", "LONG FIN"),
            (CircuitStart.InitialPoint, "Initial point", "IP"),
            (CircuitStart.Runway, "Runway (take-off)", "RWY"),
            (CircuitStart.HighKey, "High Key (SFO)", "HK")
        ];

        public static readonly (CircuitType Type, string Text, string Short)[] Circuits =
        [
            (CircuitType.Conventional, "Conventional", "CONV"),
            (CircuitType.Close, "Close", "CLS"),
            (CircuitType.Wide, "Wide", "WIDE"),
            (CircuitType.Overhead, "Overhead (run and break)", "OVH"),
            (CircuitType.Sfo, "SFO", "SFO")
        ];

        public static readonly (EndAction End, string Text, string Short)[] Ends =
        [
            (EndAction.TouchAndGo, "Touch and go", "T/G"),
            (EndAction.FullStop, "Full stop", "FS"),
            (EndAction.GoAround, "Go-around", "G/A"),
            (EndAction.LowPass, "Low pass", "LOW"),
            (EndAction.Departure, "Departure", "DEP")
        ];

        public static readonly (BreakPoint Break, string Text, string Short)[] Breaks =
        [
            (BreakPoint.Middle, "Middle", "MID"),
            (BreakPoint.Beginning, "Beginning", "BEG"),
            (BreakPoint.End, "End", "END"),
            (BreakPoint.Standby, "Standby", "STBY")
        ];

        /// <summary>A new aircraft in a circuit. <paramref name="side"/> null: the side of the runway in the aerodrome file.</summary>
        public string? Add(CircuitStart start, CircuitType type, CircuitSide? side, EndAction end, string? performanceClass, string? callsign, bool hold, out string added)
        {
            added = "";
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Aerodrome, above).";
            try
            {
                added = engine.AddInCircuit(start, type, side, end, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass, hold: hold);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        /// <summary>Fly this circuit on this side from where it is (the side changed: an S over the aerodrome).</summary>
        public void FlyCircuit(string callsign, CircuitType type, CircuitSide side) => engine.SetCircuit(callsign, type, side);

        public void SetEnd(string callsign, EndAction end) => engine.SetEndAction(callsign, end);

        public void SetBreak(string callsign, BreakPoint where) => engine.SetBreak(callsign, where);

        /// <summary>The break over a point clicked on the map: its distance along the runway.</summary>
        public void SetBreakAt(string callsign, double x, double y)
        {
            double c = airfield.Course * Math.PI / 180;
            engine.SetBreak(callsign, BreakPoint.Point, x * Math.Sin(c) + y * Math.Cos(c));
        }

        public void BreakNow(string callsign) => engine.BreakNow(callsign);

        public void Orbit(string callsign) => engine.Orbit(callsign);

        /// <summary>SFO: hold at the High Key (circling over the field) or start it (at the next pass over the High Key).</summary>
        public void HoldAtHighKey(string callsign, bool hold) => engine.HoldAtHighKey(callsign, hold);

        public void GoAround(string callsign) => engine.GoAround(callsign);

        public void Helipad(string callsign, double x, double y) => engine.SetHelipad(callsign, x, y);

        public void Remove(string callsign) => engine.Remove(callsign);
    }
}
