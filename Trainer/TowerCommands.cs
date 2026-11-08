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
        /// <summary>A circuit as on the screen (the log of the flights): CONV L, OVH R...</summary>
        private static string Named(CircuitType type, CircuitSide side) =>
            $"{Circuits.FirstOrDefault(c => c.Type == type).Short ?? type.ToString()} {(side == CircuitSide.Left ? "L" : "R")}";

        /// <summary>An order of the instructor given (the user, 7 Nov: in the log of the flights): callsign, what, detail.</summary>
        public Action<string, string, string>? Logged { get; set; }

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
            (EndAction.FullStop, "Full stop", "F/S"),
            // (The user, 5 Nov: the low approach and the low pass; G/A is only the order of the moment.)
            (EndAction.GoAround, "Low approach", "L/A"),
            (EndAction.LowPass, "Low pass", "L/P"),
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
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Settings › Map).";
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

        /// <summary>A new aircraft at an entry gate (the plane, NM), joining its circuit by itself.</summary>
        public string? AddAtGate(double x, double y, CircuitEntry entry, CircuitType type, CircuitSide? side, EndAction end, string? performanceClass, string? callsign, double? heightFt, out string added)
        {
            added = "";
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Settings › Map).";
            try
            {
                added = engine.AddAtGate(x, y, entry, type, side, end, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass, arrivalHeightFt: heightFt);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        /// <summary>Quick INBOUND IP (the user, 7 Nov): from the long final at the initial's altitude of its class, kept to the IP, then the overhead.</summary>
        public string? AddInboundIp(CircuitSide? side, EndAction end, string? performanceClass, string? callsign, out string added)
        {
            added = "";
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Settings › Map).";
            try
            {
                added = engine.AddInboundIp(side, end, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        public void OrbitForever(string callsign)
        {
            Logged?.Invoke(callsign, "ORBIT (on and on)", "");
            engine.Orbit(callsign, forever: true);
        }

        public void Continue(string callsign)
        {
            Logged?.Invoke(callsign, "CONTINUE", "");
            engine.ContinueAfterOrbit(callsign);
        }

        public void Breakup(string callsign)
        {
            Logged?.Invoke(callsign, "BREAKUP", "");
            engine.Breakup(callsign);
        }

        /// <summary>Fly this circuit on this side from where it is (the side changed: an S over the aerodrome).</summary>
        public void FlyCircuit(string callsign, CircuitType type, CircuitSide side)
        {
            Logged?.Invoke(callsign, "CIRCUIT NOW", Named(type, side));
            engine.SetCircuit(callsign, type, side);
        }

        /// <summary>The next circuit (the plan): flown after the next touch and go / go-around; not in a circuit: joined now.</summary>
        public void NextCircuit(string callsign, CircuitType type, CircuitSide side)
        {
            Logged?.Invoke(callsign, "NEXT CIRCUIT", Named(type, side));
            engine.SetNextCircuit(callsign, type, side);
        }

        /// <summary>DCT TO (the user, 5 Nov): at once to the conventional downwind, its base, the initial point or the High Key, on the aerodrome's side of that circuit.</summary>
        public void DirectTo(string callsign, DirectPoint point)
        {
            Logged?.Invoke(callsign, "DCT TO", point switch
            {
                DirectPoint.Downwind => "DW CONV",
                DirectPoint.CloseDownwind => "DW CLS",
                DirectPoint.Base => "BASE CONV",
                DirectPoint.CloseBase => "BASE CLS",
                DirectPoint.InitialPoint => "IP",
                DirectPoint.HighKey => "HK",
                _ => $"{point}"
            });
            engine.DirectTo(callsign, point, engine.SideFor(point switch
        {
            DirectPoint.InitialPoint => CircuitType.Overhead,
            DirectPoint.HighKey => CircuitType.Sfo,
            DirectPoint.CloseBase or DirectPoint.CloseDownwind => CircuitType.Close,
            _ => CircuitType.Conventional
        }));
        }

        public void ExtendDownwind(string callsign)
        {
            Logged?.Invoke(callsign, "EXTEND DOWNWIND", "");
            engine.ExtendDownwind(callsign);
        }

        public void TurnBase(string callsign)
        {
            Logged?.Invoke(callsign, "TURN BASE", "");
            engine.TurnBase(callsign);
        }

        /// <summary>Out of the circuit: a heading (degrees true), the circuit waits (REJOIN brings it back).</summary>
        public void Heading(string callsign, double headingTrue)
        {
            Logged?.Invoke(callsign, "HEADING", $"{headingTrue:000}° true");
            engine.SetHeading(callsign, headingTrue);
        }

        /// <summary>An altitude given as a height above the runway (ft), and a speed (kt).</summary>
        public void Height(string callsign, double heightFt)
        {
            Logged?.Invoke(callsign, "ALTITUDE", $"{airfield.ElevationFt + heightFt:0} ft QNH");
            engine.SetAltitude(callsign, airfield.ElevationFt + heightFt);
        }

        public void Speed(string callsign, double speedKt)
        {
            Logged?.Invoke(callsign, "SPEED", $"{speedKt:0} kt");
            engine.SetSpeed(callsign, speedKt);
        }

        /// <summary>A formation: the aircraft just added leads it (null, or why not).</summary>
        public string? MakeFormation(string leader, int size, IReadOnlyList<bool>? lands, double splitSeconds) => engine.MakeFormation(leader, size, lands, splitSeconds);

        public void Split(string callsign)
        {
            Logged?.Invoke(callsign, "SPLIT", "");
            engine.SplitFormation(callsign);
        }

        /// <summary>Landed, rolling out: expedite taxi (at least 45 kt to the nearest exit it can still take).</summary>
        public void ExpediteTaxi(string callsign)
        {
            Logged?.Invoke(callsign, "EXPEDITE TAXI", "");
            engine.ExpediteTaxi(callsign);
        }

        /// <summary>The plan of its circuits: how many (0: on and on), the last one's end.</summary>
        public void SetLaps(string callsign, int laps, EndAction last, EndAction onFinal) => engine.SetLaps(callsign, laps, last, onFinal);

        public void FormationLanding(string callsign, IReadOnlyList<bool> lands) => engine.SetFormationLanding(callsign, lands);

        /// <summary>AUTO: the height (the speed) of its circuit or its way again.</summary>
        public void HeightAuto(string callsign) => engine.ResumeRouteAltitude(callsign);

        public void SpeedAuto(string callsign) => engine.ResumeRouteSpeed(callsign);

        /// <summary>Back into its circuit (the one it was flying, on its side) from where it is; a crossing one: back onto its way.</summary>
        public void Rejoin(string callsign)
        {
            AircraftState? a = engine.Get(callsign);
            if (a == null) return;
            Logged?.Invoke(callsign, "REJOIN", "");
            if (a.Transit) engine.ResumeTransit(callsign);
            else engine.SetCircuit(callsign, a.Circuit ?? CircuitType.Conventional, a.CircuitSide);
        }

        /// <summary>A departure with no circuit: along the runway heading (radial NaN) or onto a radial (degrees true) from a point (null: the middle of the runway).</summary>
        public string? AddDeparture(double radialTrue, bool? turnRight, double? fromX, double? fromY, double? heightFt, string? performanceClass, string? callsign, out string added,
            double exitX = double.NaN, double exitY = double.NaN)
        {
            added = "";
            if (double.IsNaN(airfield.Course)) return "There is no runway in use yet: place it on the map first (Settings › Map).";
            try
            {
                added = engine.AddDeparture(radialTrue, turnRight, fromX, fromY, heightFt, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass, null, exitX, exitY);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        /// <summary>After the last circuit of its plan: away along the runway heading or the radial (degrees true), or to the gate (the plane, NM; NaN: none).</summary>
        public void LeaveAfterCircuits(string callsign, double radialTrue, bool? turnRight, double? fromX, double? fromY, double? heightFt, double gateX, double gateY) =>
            engine.LeaveAfterCircuits(callsign, radialTrue, turnRight, fromX, fromY, heightFt, gateX, gateY);

        /// <summary>A new aircraft crossing the zone from a gate to another one (the plane, NM).</summary>
        public string? AddTransit(double fromX, double fromY, double toX, double toY, bool overhead, double? heightFt, string? performanceClass, string? callsign, out string added)
        {
            added = "";
            try
            {
                added = engine.AddTransit(fromX, fromY, toX, toY, overhead, heightFt, string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(), performanceClass);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        public void SetEnd(string callsign, EndAction end)
        {
            Logged?.Invoke(callsign, "END", Ends.FirstOrDefault(e => e.End == end).Short ?? $"{end}");
            engine.SetEndAction(callsign, end);
        }

        public void SetBreak(string callsign, BreakPoint where)
        {
            Logged?.Invoke(callsign, "BREAK AT", Breaks.FirstOrDefault(b => b.Break == where).Short ?? $"{where}");
            engine.SetBreak(callsign, where);
        }

        /// <summary>The break over a point clicked on the map: its distance along the runway.</summary>
        public void SetBreakAt(string callsign, double x, double y)
        {
            double c = airfield.Course * Math.PI / 180;
            engine.SetBreak(callsign, BreakPoint.Point, x * Math.Sin(c) + y * Math.Cos(c));
        }

        public void BreakNow(string callsign)
        {
            Logged?.Invoke(callsign, "BREAK NOW", "");
            engine.BreakNow(callsign);
        }

        public void Orbit(string callsign)
        {
            Logged?.Invoke(callsign, "ORBIT", "");
            engine.Orbit(callsign);
        }

        /// <summary>SFO: hold at the High Key (circling over the field) or start it (at the next pass over the High Key).</summary>
        public void HoldAtHighKey(string callsign, bool hold)
        {
            Logged?.Invoke(callsign, "HOLD AT HIGH KEY", hold ? "on" : "off");
            engine.HoldAtHighKey(callsign, hold);
        }

        public void GoAround(string callsign)
        {
            Logged?.Invoke(callsign, "GO AROUND (ordered)", "");
            engine.GoAround(callsign);
        }

        public void Helipad(string callsign, double x, double y) => engine.SetHelipad(callsign, x, y);

        /// <summary>DCT TO HPAD (the user, 5 Nov): a helicopter straight to the pad now, a full stop there.</summary>
        public void DirectToPad(string callsign, double x, double y) => engine.DirectToPad(callsign, x, y);

        /// <summary>A helicopter departing from a helipad (the user, 6 Nov): the aircraft just added on the runway starts there, its hover first.</summary>
        public void TakeOffFromPad(string callsign, double x, double y) => engine.TakeOffFromPad(callsign, x, y);

        /// <summary>A helicopter's pad wanted, not chosen yet (HPAD?).</summary>
        public void WantPad(string callsign) => engine.WantPad(callsign);

        public void Remove(string callsign)
        {
            Logged?.Invoke(callsign, "REMOVED by the instructor", "");
            engine.Remove(callsign);
        }
    }
}
