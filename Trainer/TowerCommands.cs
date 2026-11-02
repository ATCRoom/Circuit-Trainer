using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The instructor's commands to the aircraft of the circuits, in one place: the side panel uses them now, the
    /// commands on the label (step 4b) will use the same ones. Each returns an error message for the user, or null.
    /// </summary>
    internal sealed class TowerCommands(SimEngine engine, Airfield airfield)
    {
        public static readonly (CircuitStart Start, string Text)[] Starts =
        [
            (CircuitStart.Downwind, "Downwind"),
            (CircuitStart.LongFinal, "Long final"),
            (CircuitStart.InitialPoint, "Initial point"),
            (CircuitStart.Runway, "Runway (take-off)"),
            (CircuitStart.HighKey, "High Key (SFO)")
        ];

        public static readonly (CircuitType Type, string Text)[] Circuits =
        [
            (CircuitType.Conventional, "Conventional"),
            (CircuitType.Close, "Close"),
            (CircuitType.Wide, "Wide"),
            (CircuitType.Overhead, "Overhead (run and break)"),
            (CircuitType.Sfo, "SFO")
        ];

        public static readonly (EndAction End, string Text)[] Ends =
        [
            (EndAction.TouchAndGo, "Touch and go"),
            (EndAction.FullStop, "Full stop"),
            (EndAction.GoAround, "Go-around"),
            (EndAction.LowPass, "Low pass"),
            (EndAction.Departure, "Departure")
        ];

        public static readonly (BreakPoint Break, string Text)[] Breaks =
        [
            (BreakPoint.Middle, "Middle"),
            (BreakPoint.Beginning, "Beginning"),
            (BreakPoint.End, "End"),
            (BreakPoint.Standby, "Standby")
        ];

        /// <summary>A new aircraft in a circuit. <paramref name="side"/> null: the side of the runway in the aerodrome file.</summary>
        public string? Add(CircuitStart start, CircuitType type, CircuitSide? side, EndAction end, string? performanceClass, string? callsign, out string added)
        {
            added = "";
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Aerodrome, above).";
            try
            {
                added = engine.AddInCircuit(start, type, side, end, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass);
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

        public void GoAround(string callsign) => engine.GoAround(callsign);

        public void Helipad(string callsign, double x, double y) => engine.SetHelipad(callsign, x, y);

        public void Remove(string callsign) => engine.Remove(callsign);
    }
}
