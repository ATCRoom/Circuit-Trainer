namespace ATCRoom.Sim
{
    /// <summary>The kinds of circuit (TWR Trainer).</summary>
    public enum CircuitType
    {
        /// <summary>The rectangular circuit: upwind, crosswind, downwind, base, final.</summary>
        Conventional,
        /// <summary>
        /// Close pattern (the jet circuit without the initial and the break): no crosswind and no base: from the upwind (or a
        /// go-around, above 400 ft) a 180° turn to the downwind, the downwind at the width of the jet circuit, a descending
        /// 180° turn that rolls out on the final (the class's roll out distance and height).
        /// </summary>
        Close,
        /// <summary>Wide circuit (outside downwind): as the conventional one, further out.</summary>
        Wide,
        /// <summary>
        /// Overhead pattern (jet circuit, "run and break"): from the initial point along the runway at the initial height and
        /// speed, the break (a level 180° turn, tight enough to meet the downwind distance), the downwind, a descending 180°
        /// turn to the final. After a go-around or a touch and go it flies the close pattern (not the initial point again).
        /// </summary>
        Overhead,
        /// <summary>
        /// Simulated flame out: gliding at the best glide speed, High Key over the runway, a 180° turn to the Low Key abeam it
        /// on the downwind side, the downwind gliding down, a 180° turn to the final. After a go-around or a touch and go
        /// it flies the close pattern.
        /// </summary>
        Sfo
    }

    /// <summary>Where the overhead pattern breaks.</summary>
    public enum BreakPoint
    {
        /// <summary>Over the middle of the runway (default).</summary>
        Middle,
        /// <summary>Over the beginning of the runway (the threshold).</summary>
        Beginning,
        /// <summary>Over the end of the runway.</summary>
        End,
        /// <summary>At a point given along the runway track (clicked on the map).</summary>
        Point,
        /// <summary>"Standby for break": on along the initial until "break now".</summary>
        Standby
    }

    /// <summary>The side of the circuit: the turns are to this side.</summary>
    public enum CircuitSide { Left, Right }

    /// <summary>What the aircraft does at the end of the final.</summary>
    public enum EndAction
    {
        /// <summary>Lands and rolls on: removed a few seconds later.</summary>
        FullStop,
        /// <summary>Touches down, rolls a few seconds, takes off and flies the circuit again.</summary>
        TouchAndGo,
        /// <summary>Goes around at about 200 ft and flies the circuit again.</summary>
        GoAround,
        /// <summary>Passes low over the runway (about 100 ft), then climbs and flies the circuit again.</summary>
        LowPass,
        /// <summary>Touch and go, then it leaves the circuit straight ahead (the instructor removes it, or flies it).</summary>
        Departure
    }

    /// <summary>Where a new aircraft of a circuit starts.</summary>
    public enum CircuitStart
    {
        /// <summary>On the final, at the long final distance (<see cref="SimEngine.LongFinalNM"/>), on the glide path.</summary>
        LongFinal,
        /// <summary>At the initial point (<see cref="SimEngine.InitialPointNM"/> before the threshold), at circuit height, along the runway.</summary>
        InitialPoint,
        /// <summary>On the downwind, at its start.</summary>
        Downwind,
        /// <summary>On the runway, at the threshold: it takes off.</summary>
        Runway,
        /// <summary>Simulated flame out: 1 NM before the High Key, along the runway at its height.</summary>
        HighKey
    }

    /// <summary>The circuit of an aircraft (only the engine changes it, under its lock).</summary>
    internal sealed class CircuitState
    {
        public CircuitType Type;
        public CircuitSide Side;
        public EndAction End;
        /// <summary>The instructor flies it (a heading, the stick): the circuit waits until it is given again.</summary>
        public bool Suspended;
        /// <summary>Flying the final: the descent is steered to the touchdown point (or to the low pass height).</summary>
        public bool OnFinal;
        /// <summary>Left the circuit (end action Departure): the circuit does nothing more.</summary>
        public bool Departed;
        /// <summary>On the runway: seconds since the touchdown (or since the start of the take-off roll).</summary>
        public double RollTime;
        /// <summary>The point of the route the last leg change was seen for (to set the vertical rate of each leg once).</summary>
        public int LegIndex = -1;
        /// <summary>Flying one of the 180° turns of the close pattern (to its downwind, to its final): the route waits for its end.</summary>
        public bool Turning;
        /// <summary>The 180° turn follows this circle (the plane, NM) with this bank limit: it ends at the right distance from the runway whatever the speed and the wind.</summary>
        public double CentreX, CentreY, Radius, TurnBank, TurnHeading;
        /// <summary>The circuit ordered while in a circuit (the close pattern), flown after the next go-around or touch and go, and its side.</summary>
        public CircuitType? NextType;
        public CircuitSide NextSide;
        /// <summary>Where the overhead pattern breaks (along the runway, NM, for <see cref="BreakPoint.Point"/>), and "break now".</summary>
        public BreakPoint Break = BreakPoint.Middle;
        public double BreakAlong;
        public bool BreakNow;
        /// <summary>An orbit (a 360° turn) going on: 1 the first half, 2 the second; the heading it started on.</summary>
        public int Orbit;
        public double OrbitHeading;
        /// <summary>A helicopter's landing point (the plane, NM): its final goes there, hovering down to the ground.</summary>
        public bool HasPad;
        public double PadX, PadY;
    }

    public sealed partial class SimEngine
    {
        /// <summary>Where the descent of the final aims (NM past the threshold), and the low pass height (ft).</summary>
        private const double TouchdownAim = 0.15;
        private const double LowPassHeight = 100;
        /// <summary>The go-around of the end action "go around": at this height (ft above the threshold), on the short final (NM before the threshold at most).</summary>
        private const double GoAroundHeight = 200;
        private const double GoAroundShortFinal = 0.7;
        /// <summary>The waveoff of the jet circuits: above this height (ft above the threshold) it turns to the downwind at once, climbing.</summary>
        private const double WaveoffTurnHeight = 400;
        /// <summary>Seconds of the roll after a touchdown before the aircraft accelerates again.</summary>
        private const double TouchAndGoRoll = 3;

        /// <summary>The helicopter on the final to its pad: slower when nearer (kt per NM), the rate of the vertical descent (ft/min), a pad reached within (NM).</summary>
        private const double PadSlowdown = 150;
        private const double PadDescent = 400;
        private const double PadRadius = 0.05;
        /// <summary>SFO: the turn to the final starts this far before the threshold (NM) and ends at this height (ft): nearly on the runway.</summary>
        private const double SfoTurnStartNM = 0.2;
        private const double SfoRollOutHeight = 120;

        private double longFinal = 10;
        private double initialPoint = 3;
        private AerodromeCircuits? aerodrome;
        private string aerodromeRunway = "";

        /// <summary>
        /// The circuits of the aerodrome and the runway in use (from now on: the aircraft added later): the side of the
        /// circuit of that runway is the default side, the heights of the classes it gives are those flown. Null: none
        /// (the left side, the heights of the classes).
        /// </summary>
        public void SetAerodrome(AerodromeCircuits? circuits, string? runway)
        {
            lock (sync)
            {
                aerodrome = circuits;
                aerodromeRunway = (runway ?? "").Trim();
            }
        }

        /// <summary>The default side of the circuit: the aerodrome's for the runway in use, else left.</summary>
        public CircuitSide DefaultSide
        {
            get { lock (sync) return aerodrome?.SideOf(aerodromeRunway) ?? CircuitSide.Left; }
        }

        /// <summary>Distance of the long final (NM from the threshold), 3–20, default 10.</summary>
        public double LongFinalNM
        {
            get { lock (sync) return longFinal; }
            set { lock (sync) longFinal = double.IsFinite(value) ? Math.Clamp(value, 3, 20) : 10; }
        }

        /// <summary>Distance of the initial point before the threshold (NM), 1–10, default 3.</summary>
        public double InitialPointNM
        {
            get { lock (sync) return initialPoint; }
            set { lock (sync) initialPoint = double.IsFinite(value) ? Math.Clamp(value, 1, 10) : 3; }
        }

        /// <summary>
        /// Adds an aircraft that flies a circuit of the reference runway (<see cref="SetGlidePath"/>, which must have a
        /// course) by itself: where it starts, the circuit, its side (null: the aerodrome's, <see cref="DefaultSide"/>) and
        /// what it does at the end of each final. Returns its callsign. The circuit is built from the runway and the values
        /// of the class (performance.json), with the heights of the aerodrome (<see cref="SetAerodrome"/>).
        /// </summary>
        public string AddInCircuit(CircuitStart start, CircuitType type, CircuitSide? sideGiven, EndAction end, string? callsign = null,
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null)
        {
            lock (sync)
            {
                if (double.IsNaN(glidePathCourse)) throw new InvalidOperationException("No runway: a circuit needs the runway in use.");
                CircuitSide side = sideGiven ?? aerodrome?.SideOf(aerodromeRunway) ?? CircuitSide.Left;
                // The overhead pattern starts at the initial point, the flame out at the High Key; elsewhere they are the close
                // pattern (their second part).
                if (start == CircuitStart.HighKey) type = CircuitType.Sfo;
                else if (type == CircuitType.Sfo || (type == CircuitType.Overhead && start != CircuitStart.InitialPoint)) type = CircuitType.Close;
                PerformanceClass classKind = (classes.FirstOrDefault(c => Same(c.Name, performanceClass ?? ""))
                    ?? classes.FirstOrDefault(c => Same(c.Name, PerformanceClass.DefaultName)) ?? classes[0]);
                PerformanceClass kind = aerodrome?.Apply(classKind) ?? classKind.Copy();
                double height = runwayElevation + kind.CircuitHeightFt;
                double glide = GlideFeetPerNM();
                (double x, double y, double altitude, double heading, double speed) = start switch
                {
                    CircuitStart.LongFinal => At(-longFinal, 0, side, runwayElevation + longFinal * glide, glidePathCourse, kind.ApproachSpeedKt),
                    CircuitStart.InitialPoint => type == CircuitType.Overhead
                        ? At(-initialPoint, 0, side, runwayElevation + kind.OverheadHeightFt, glidePathCourse, kind.OverheadSpeedKt)
                        : At(-initialPoint, 0, side, height, glidePathCourse, kind.CircuitSpeedKt),
                    CircuitStart.Downwind => At(DownwindStartAlong(type, kind), Width(type, kind, side), side, height, Normalize(glidePathCourse + 180), kind.CircuitSpeedKt),
                    CircuitStart.HighKey => At(HighKeyAlong() - 1, 0, side, runwayElevation + kind.HighKeyFt, glidePathCourse, kind.BestGlideKt),
                    _ => At(0.02, 0, side, runwayElevation, glidePathCourse, kind.MinSpeedKt)
                };
                string name = Add(x, y, altitude, heading, speed, callsign, kind.Name, takenElsewhere);
                SimAircraft plane = Find(name)!;
                // The class with the heights of this aerodrome.
                plane.Class = kind;
                plane.Circuit = new CircuitState { Type = type, Side = side, End = end };
                switch (start)
                {
                    case CircuitStart.LongFinal:
                        GiveRoute(plane, [Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")]);
                        plane.TargetSpeedKt = kind.ApproachSpeedKt;
                        break;
                    case CircuitStart.InitialPoint when type == CircuitType.Overhead:
                        GiveRoute(plane, OverheadRoute(plane));
                        break;
                    case CircuitStart.HighKey:
                        GiveRoute(plane, SfoRoute(plane));
                        break;
                    case CircuitStart.InitialPoint:
                        GiveRoute(plane, [Point(0, 0, side, height, kind.CircuitSpeedKt, "INITIAL"), .. Loop(plane, UpwindAlong(type, kind), withUpwind: true)]);
                        break;
                    case CircuitStart.Downwind:
                        GiveRoute(plane, Loop(plane, UpwindAlong(type, kind), withUpwind: false).SkipWhile(p => !IsDownwind(p)).ToList());
                        break;
                    default:
                        // On the runway: the take-off roll from standstill.
                        plane.SpeedKt = plane.TargetSpeedKt = plane.GroundSpeedKt = 0;
                        plane.Contact = GroundContact.Rolling;
                        plane.Circuit.RollTime = TouchAndGoRoll;
                        break;
                }
                return name;
            }
        }

        /// <summary>
        /// Instruction: fly this circuit, on this side, from where the aircraft is. The close pattern ordered to an aircraft
        /// already flying another circuit is flown after its next go-around, touch and go or low pass (as in reality: from a
        /// go-around, or after the take-off); from outside the circuits (arriving, flown by the instructor) it joins at once.
        /// The wide circuit (to make room) joins at once: on the circuit side between the base turn and the crosswind the new
        /// downwind ahead, elsewhere the start of the downwind. The overhead pattern: back to the initial point, out on the
        /// wide downwind (on the final: after the go-around); the flame out the same way, to the High Key. On the final (or
        /// rolling) the new circuit is flown after it.
        /// Also takes back an aircraft the instructor was flying.
        /// </summary>
        public void SetCircuit(string callsign, CircuitType type, CircuitSide side) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (double.IsNaN(glidePathCourse) || a.Contact is GroundContact.Landed or GroundContact.OnGround) return;
                bool inCircuit = a.Circuit is { Suspended: false, Departed: false };
                (double alongNow, double lateralNow) = RunwayFrame(a.X, a.Y, side);
                bool onFinal = a.Circuit is { OnFinal: true } && Math.Abs(lateralNow) < 0.3 && alongNow < runwayLength;
                // The same circuit on the other side, in flight: an S over the aerodrome to the new downwind (on the final: a go-around).
                if (inCircuit && a.Contact == GroundContact.Airborne && type == a.Circuit!.Type && side != a.Circuit.Side && ChangeSide(a, side, onFinal)) return;
                if (inCircuit && (a.Contact == GroundContact.Rolling || onFinal || (type == CircuitType.Close && a.Circuit!.Type != CircuitType.Close)))
                {
                    // Flown after the next go-around or touch and go.
                    a.Circuit!.NextType = type;
                    a.Circuit.NextSide = side;
                    return;
                }
                // An aircraft new to the circuits (arriving): the heights of this aerodrome for its class.
                if (a.Circuit == null && aerodrome != null) a.Class = aerodrome.Apply(a.Class);
                CircuitState circuit = a.Circuit ??= new CircuitState { End = EndAction.FullStop };
                circuit.NextType = null;
                circuit.Type = type;
                circuit.Side = side;
                circuit.Departed = false;
                if (a.Contact == GroundContact.Rolling) return;
                (double along, double lateral) = (alongNow, lateralNow);
                circuit.Suspended = false;
                circuit.OnFinal = false;
                if (type is CircuitType.Overhead or CircuitType.Sfo)
                {
                    GiveRoute(a, ToInitialPoint(a));
                    return;
                }
                double width = Width(type, a.Class, side);
                double upwind = DownwindStartAlong(type, a.Class);
                List<RoutePoint> loop = Loop(a, UpwindAlong(type, a.Class), withUpwind: false);
                List<RoutePoint> downwind = loop.SkipWhile(p => !IsDownwind(p)).ToList();
                double baseTurn = BaseAlong(type, a.Class);
                if (lateral > 0.3 * width && along > baseTurn + 0.3 && along < upwind + 1.5)
                {
                    // On the circuit side: join the downwind ahead (the points still ahead of it).
                    double entry = Math.Clamp(along - 0.4, baseTurn + 0.3, upwind);
                    List<RoutePoint> route = [Point(entry, width, side, runwayElevation + a.Class.CircuitHeightFt, a.Class.CircuitSpeedKt, "DOWNWIND")];
                    route.AddRange(downwind.Where(p => AlongOf(p, side) < entry - 0.05 || p.Name != "DOWNWIND"));
                    GiveRoute(a, route);
                }
                else
                {
                    // Elsewhere: a 45° entry from the outside: to a point 1 NM outside the downwind and 1 NM before its start,
                    // then onto the downwind at its start (both turns anticipated): on it parallel, with the runway reciprocal.
                    double joinHeight = runwayElevation + a.Class.CircuitHeightFt;
                    GiveRoute(a,
                    [
                        Point(upwind + 1, width + 1, side, joinHeight, a.Class.CircuitSpeedKt, "JOIN"),
                        Point(upwind, width, side, joinHeight, a.Class.CircuitSpeedKt, "JOIN"),
                        .. downwind
                    ]);
                }
            });

        /// <summary>
        /// The side of the circuit changed in flight (the user, 2 Nov, with a drawing): it does not leave the circuit. From
        /// anywhere in the circuit an S: a turn towards the runway, across it over the aerodrome towards its far end, a turn
        /// onto the new downwind at its start. On the final (or the base): a go-around along the runway, then the same turn
        /// onto the new downwind. On the upwind (the initial, before the High Key) the circuit goes on to the new side.
        /// False: joining from outside (a new join).
        /// </summary>
        private bool ChangeSide(SimAircraft a, CircuitSide side, bool onFinal)
        {
            CircuitState circuit = a.Circuit!;
            RoutePoint? target = a.Route != null && a.RouteNext < a.Route.Count ? a.Route[a.RouteNext] : null;
            if (target?.Name == "JOIN" && circuit.Type is not (CircuitType.Overhead or CircuitType.Sfo)) return false;
            circuit.Side = side;
            circuit.NextType = null;
            circuit.Suspended = false;
            circuit.OnFinal = false;
            (double along, _) = RunwayFrame(a.X, a.Y, side);
            double length = Math.Max(runwayLength, 0.3);
            double height = runwayElevation + a.Class.CircuitHeightFt;
            double speed = a.Class.CircuitSpeedKt;
            switch (target?.Name)
            {
                case "UPWIND" or "GO AROUND":
                    List<RoutePoint> upwind = Loop(a, Math.Max(UpwindAlong(circuit.Type, a.Class), along + 0.3), withUpwind: true);
                    upwind[0] = upwind[0] with { Name = target!.Name };
                    GiveRoute(a, upwind);
                    return true;
                case "INITIAL":
                    GiveRoute(a, OverheadRoute(a));
                    return true;
                case "HIGH KEY":
                    GiveRoute(a, SfoRoute(a));
                    return true;
                case "JOIN":
                    GiveRoute(a, ToInitialPoint(a));
                    return true;
            }
            if (onFinal || target == null || target.Name is "BASE" or "FINAL")
            {
                // Go-around along the runway, then onto the new downwind.
                NextCircuit(circuit);
                a.SpeedGiven = false;
                GiveRoute(a, [Point(Math.Max(length, along + 0.5), 0, side, height, speed, "GO AROUND"), .. OntoDownwind(a)]);
                a.SelectedVerticalRateFpm = Math.Max(MinVerticalRate, a.Class.MaxClimbFpm * 0.8);
                return true;
            }
            // The S: across the runway (a little ahead of where it is, over the aerodrome), then onto the new downwind.
            double cross = Math.Clamp(along + 0.3, 0.1, length - 0.1);
            GiveRoute(a, [Point(cross, 0, side, height, speed, "JOIN"), .. OntoDownwind(a)]);
            return true;
        }

        /// <summary>
        /// Onto the downwind at its start: to a point on its line 0.5 NM before its start (the turn onto it anticipated, so
        /// it rolls out on it), then the downwind and the rest of the circuit.
        /// </summary>
        private List<RoutePoint> OntoDownwind(SimAircraft a)
        {
            CircuitState circuit = a.Circuit!;
            CircuitSide side = circuit.Side;
            double width = Width(circuit.Type, a.Class, side);
            double start = DownwindStartAlong(circuit.Type, a.Class);
            List<RoutePoint> downwind = Loop(a, UpwindAlong(circuit.Type, a.Class), withUpwind: false).SkipWhile(p => !IsDownwind(p)).ToList();
            return
            [
                Point(start + 0.5, width, side, runwayElevation + a.Class.CircuitHeightFt, a.Class.CircuitSpeedKt, "JOIN"),
                .. downwind.Where(p => p.Name != "DOWNWIND" || AlongOf(p, side) < start - 0.3)
            ];
        }

        /// <summary>What the aircraft does at the end of each final, at once (it can be changed at any time).</summary>
        public void SetEndAction(string callsign, EndAction action) => Change(callsign, a =>
        {
            if (a.Circuit != null) a.Circuit.End = action;
        });

        /// <summary>Where the overhead pattern breaks, at once: middle (default), beginning, end, a point along the runway track (NM from the threshold), or standby.</summary>
        public void SetBreak(string callsign, BreakPoint where, double alongNM = double.NaN) => Change(callsign, a =>
        {
            if (a.Circuit == null) return;
            a.Circuit.Break = where == BreakPoint.Point && !double.IsFinite(alongNM) ? BreakPoint.Middle : where;
            a.Circuit.BreakAlong = double.IsFinite(alongNM) ? alongNM : 0;
            a.Circuit.BreakNow = false;
        });

        /// <summary>Instruction "break now" (after "standby for break", or to break earlier): on the initial, it breaks at once.</summary>
        public void BreakNow(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                // Only on the initial (else it would break at once on the next initial).
                if (a.Circuit != null && a.Route != null && a.RouteNext < a.Route.Count && a.Route[a.RouteNext].Name == "INITIAL") a.Circuit.BreakNow = true;
            });

        /// <summary>
        /// Instruction: one orbit (a 360° turn) where it is, then on along its circuit from there (on the downwind: to make room
        /// ahead of it). <paramref name="right"/>: the side of the turn; null: away from the runway (the outside of the
        /// circuit). Not on the final, not during another turn.
        /// </summary>
        public void Orbit(string callsign, bool? right = null) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                CircuitState? circuit = a.Circuit;
                if (circuit == null || circuit.Suspended || circuit.Departed || circuit.Turning || circuit.OnFinal || a.Contact != GroundContact.Airborne
                    || a.Route == null || a.RouteNext >= a.Route.Count || double.IsNaN(glidePathCourse)) return;
                // Away from the runway: the opposite of the side of the circuit (a left-hand circuit has the runway on its left).
                bool turnRight = right ?? circuit.Side == CircuitSide.Left;
                double s = turnRight ? 1 : -1;
                double rate = Math.Min(MaxTurnRate, RateForBank(a));
                double radius = Math.Max(0.1, Math.Max(30, a.GroundSpeedKt) / 3600 / (rate * Math.PI / 180));
                double h = a.HeadingTrue * Math.PI / 180;
                // The centre to the side of the turn, moved ahead by the roll-in.
                double rollIn = Math.Max(30, a.GroundSpeedKt) / 3600 * (rate / TurnRateChange) / 2;
                circuit.CentreX = a.X + rollIn * Math.Sin(h) + s * radius * Math.Cos(h);
                circuit.CentreY = a.Y + rollIn * Math.Cos(h) - s * radius * Math.Sin(h);
                circuit.Radius = radius;
                circuit.TurnBank = a.Class.CircuitBankDegrees;
                circuit.Orbit = 1;
                circuit.OrbitHeading = a.HeadingTrue;
                circuit.Turning = true;
                a.Navigation = NavigationMode.Manual;
                a.CircuitBankDegrees = circuit.TurnBank;
                a.TurnDirection = turnRight ? TurnDirection.Right : TurnDirection.Left;
                a.TargetHeading = Normalize(a.HeadingTrue + s * 180);
                circuit.TurnHeading = a.TargetHeading;
            });

        /// <summary>The turn rate of the circuit bank at the aircraft's speed (°/s).</summary>
        private static double RateForBank(SimAircraft plane) => RateForBank(plane.Class.CircuitBankDegrees, plane.SpeedKt);

        /// <summary>
        /// A helicopter's landing point (the plane, NM; NaN: none, the runway again): its final goes there, slowing down to
        /// a hover over it and down to the ground (landed: removed a few seconds later, as on the runway). With a pad the
        /// end of the final is always a full stop.
        /// </summary>
        public void SetHelipad(string callsign, double x, double y) => Change(callsign, a =>
        {
            if (a.Circuit == null) return;
            a.Circuit.HasPad = double.IsFinite(x) && double.IsFinite(y);
            a.Circuit.PadX = a.Circuit.HasPad ? x : 0;
            a.Circuit.PadY = a.Circuit.HasPad ? y : 0;
            ApplyPad(a);
        });

        /// <summary>Instruction: go around now: climbs to circuit height along the runway, then flies its circuit again.</summary>
        public void GoAround(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Circuit == null || a.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
                StartGoAround(a);
            });

        // ----- The steps ---------------------------------------------------------------------------------------------

        /// <summary>The circuit of an aircraft in the air, every step before the route: legs, final, end of the final.</summary>
        private void CircuitStep(SimAircraft plane)
        {
            CircuitState? circuit = plane.Circuit;
            if (circuit == null || circuit.Departed || plane.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
            bool routeOver = plane.Route != null && plane.RouteNext >= plane.Route.Count;
            if (circuit.Turning && !circuit.Suspended)
            {
                TurnStep(plane, circuit);
                return;
            }
            if (!circuit.Suspended && !routeOver && (plane.Route == null || plane.Navigation != NavigationMode.Route))
            {
                // The instructor took it (a heading, the stick, the route cleared): the circuit waits.
                circuit.Suspended = true;
                circuit.OnFinal = false;
                plane.CircuitBankDegrees = 0;
                plane.SelectedTurnRateDps = plane.TurnRateMode == TurnRateMode.Half ? HalfTurnRate : StandardTurnRate;
            }
            if (circuit.Suspended) return;
            plane.CircuitBankDegrees = plane.Class.CircuitBankDegrees;
            plane.SelectedTurnRateDps = MaxTurnRate;
            if (!routeOver && plane.Route != null && plane.RouteNext + 1 < plane.Route.Count)
            {
                RoutePoint target = plane.Route[plane.RouteNext];
                RoutePoint next = plane.Route[plane.RouteNext + 1];
                (double alongNow, double lateralNow) = RunwayFrame(plane.X, plane.Y, circuit.Side);
                if (circuit.Type == CircuitType.Overhead && target.Name == "INITIAL")
                {
                    // The break: over its point along the runway (not on standby), or at once on "break now".
                    double breakAt = circuit.Break switch
                    {
                        BreakPoint.Beginning => 0.05,
                        BreakPoint.End => Math.Max(runwayLength, 0.3),
                        BreakPoint.Point => circuit.BreakAlong,
                        BreakPoint.Standby => double.PositiveInfinity,
                        _ => Math.Max(runwayLength, 0.3) / 2
                    };
                    if (circuit.BreakNow || alongNow >= breakAt)
                    {
                        circuit.BreakNow = false;
                        PassPoint(plane, fromHere: true);
                    }
                    else if (!plane.AltitudeGiven && double.IsFinite(breakAt) && breakAt - alongNow > 0.05)
                    {
                        // Along the initial to the break height, reached at the break (on standby: at the initial height).
                        double change = runwayElevation + plane.Class.BreakHeightFt - plane.AltitudeFt;
                        if (Math.Abs(change) > 1)
                        {
                            double minutes = (breakAt - alongNow) / Math.Max(30, plane.GroundSpeedKt) * 60;
                            plane.VerticalMode = VerticalMode.Altitude;
                            plane.TargetAltitudeFt = runwayElevation + plane.Class.BreakHeightFt;
                            plane.SelectedVerticalRateFpm = Math.Clamp(Math.Abs(change) / Math.Max(0.1, minutes) * 1.1, MinVerticalRate,
                                Math.Max(MinVerticalRate, change > 0 ? plane.Class.MaxClimbFpm : plane.Class.MaxDescentFpm));
                        }
                    }
                }
                else if (circuit.Type == CircuitType.Close && target.Name is "UPWIND" or "GO AROUND")
                {
                    // Waveoff, take-off: above 400 ft over the runway track it turns to the downwind at once, climbing.
                    if (plane.AltitudeFt - runwayElevation > WaveoffTurnHeight && Math.Abs(lateralNow) < 0.3)
                    {
                        PassPoint(plane, fromHere: true);
                    }
                }
                else if (!next.Turn180 && (target.Name == "JOIN" || (circuit.Type is CircuitType.Conventional or CircuitType.Wide && target.Name is "CROSSWIND" or "DOWNWIND" or "BASE")))
                {
                    // The turn at a corner starts before it, so the next leg starts on its line (not wide of it).
                    double legCourse = Math.Atan2(target.X - plane.LegStartX, target.Y - plane.LegStartY);
                    double nextCourse = Math.Atan2(next.X - target.X, next.Y - target.Y);
                    double turn = Math.Abs(Wrap((nextCourse - legCourse) * 180 / Math.PI));
                    if (turn > 5)
                    {
                        double rate = Math.Max(MinTurnRate, Math.Min(MaxTurnRate, RateForBank(BankLimit(plane), plane.SpeedKt)));
                        double speed = Math.Max(30, plane.GroundSpeedKt) / 3600;
                        double radius = speed / (rate * Math.PI / 180);
                        double lead = radius * Math.Tan(turn / 2 * Math.PI / 180) + speed * (rate / TurnRateChange) / 2;
                        double toCorner = Math.Sqrt((target.X - plane.X) * (target.X - plane.X) + (target.Y - plane.Y) * (target.Y - plane.Y));
                        if (toCorner <= lead) PassPoint(plane, fromHere: false);
                    }
                }
            }
            if (!routeOver && plane.Route != null && plane.RouteNext != circuit.LegIndex)
            {
                // A new leg: the vertical rate that reaches the altitude of its point at its end (climbing: briskly).
                circuit.LegIndex = plane.RouteNext;
                RoutePoint point = plane.Route[plane.RouteNext];
                circuit.OnFinal = point.Name == "FINAL" && !point.Turn180;
                // The close pattern: its downwind and its final are reached by a 180° turn, not flown to as points.
                if (IsJetShape(circuit.Type) && point.Turn180)
                {
                    StartTurn(plane, circuit, point);
                    return;
                }
                if (double.IsFinite(point.AltitudeFt))
                {
                    double climb = point.AltitudeFt - plane.AltitudeFt;
                    double minutes = Math.Sqrt((point.X - plane.X) * (point.X - plane.X) + (point.Y - plane.Y) * (point.Y - plane.Y)) / Math.Max(30, plane.GroundSpeedKt) * 60;
                    plane.SelectedVerticalRateFpm = climb > 0 ? Math.Max(MinVerticalRate, plane.Class.MaxClimbFpm * 0.8)
                        : Math.Clamp(-climb / Math.Max(0.1, minutes) * 1.2, Math.Min(300, MaxDescent(plane)), MaxDescent(plane));
                }
            }
            if (!circuit.OnFinal) return;
            if (circuit.HasPad)
            {
                PadFinal(plane, circuit, routeOver);
                return;
            }
            (double along, double lateral) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            double height = plane.AltitudeFt - runwayElevation;
            // The end of the final: low pass over the threshold, go-around at its height, past the runway without landing.
            if ((circuit.End == EndAction.LowPass && along >= 0) || (circuit.End == EndAction.GoAround && height <= GoAroundHeight && along >= -GoAroundShortFinal)
                || (routeOver && along > Math.Max(runwayLength, 0.3)))
            {
                StartGoAround(plane);
                return;
            }
            // The descent steered to the touchdown point (the low pass: to its height), unless the instructor gave an altitude or a
            // rate; the speed of the final within 4 NM.
            if (!plane.SpeedGiven) plane.TargetSpeedKt = along < -4 ? plane.Class.ApproachSpeedKt : plane.Class.FinalSpeedKt;
            if (plane.AltitudeGiven) return;
            double floor = circuit.End == EndAction.LowPass ? LowPassHeight : 0;
            double distance = TouchdownAim - along;
            double wanted = distance > 0.05
                ? -(height - floor) / (distance / Math.Max(30, plane.GroundSpeedKt) * 60)
                : floor > 0 ? 0 : -400;
            plane.VerticalMode = VerticalMode.Rate;
            plane.CommandedVerticalSpeedFpm = Math.Clamp(wanted, -MaxDescent(plane), 300);
        }

        /// <summary>
        /// The helicopter's final to its pad: slower the nearer it is, down to a hover over it (the height of the hover on the
        /// way), then straight down to the ground. Unless the instructor gave a speed, an altitude or a rate.
        /// </summary>
        private void PadFinal(SimAircraft plane, CircuitState circuit, bool routeOver)
        {
            const double hover = 30;
            double distance = Math.Sqrt((circuit.PadX - plane.X) * (circuit.PadX - plane.X) + (circuit.PadY - plane.Y) * (circuit.PadY - plane.Y));
            double height = plane.AltitudeFt - runwayElevation;
            bool over = routeOver || distance <= 0.01;
            if (!plane.SpeedGiven)
            {
                plane.TargetSpeedKt = over ? 0 : Math.Min(distance > 4 ? plane.Class.ApproachSpeedKt : plane.Class.FinalSpeedKt, Math.Max(1, distance * PadSlowdown));
            }
            if (plane.AltitudeGiven) return;
            double wanted;
            if (distance > PadRadius && !over)
            {
                // To the hover height over the pad.
                double minutes = distance / Math.Max(10, plane.GroundSpeedKt) * 60;
                wanted = -(height - hover) / Math.Max(0.05, minutes);
            }
            else
            {
                // Over it: down to the hover height, the hover, then down to the pad once stopped.
                wanted = height > hover || plane.SpeedKt < 1 ? -PadDescent : 0;
            }
            plane.VerticalMode = VerticalMode.Rate;
            plane.CommandedVerticalSpeedFpm = Math.Clamp(wanted, -plane.Class.MaxDescentFpm, 300);
        }

        /// <summary>
        /// A 180° turn of the close and overhead patterns towards their side (to the downwind; to the final, descending to
        /// the roll out) on a circle: from where it starts to the downwind distance (or to the runway track), so it ends there
        /// with the heading parallel to the runway, whatever the speed and the wind (see <see cref="TurnStep"/>). The break
        /// of the overhead pattern turns with the break bank. The route waits at the point after the turn.
        /// </summary>
        private void StartTurn(SimAircraft plane, CircuitState circuit, RoutePoint after)
        {
            bool toFinal = after.Name == "FINAL";
            (double along, double lateral) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            double width = Width(CircuitType.Close, plane.Class, circuit.Side);
            double radius = Math.Max(0.15, toFinal ? lateral / 2 : (width - lateral) / 2);
            circuit.TurnBank = circuit.Type == CircuitType.Overhead && !toFinal ? plane.Class.BreakBankDegrees : plane.Class.CircuitBankDegrees;
            // The centre moved ahead by what it flies while it rolls into the turn (the rate grows at TurnRateChange).
            double rateNeeded = Math.Min(MaxTurnRate, Math.Min((Math.Max(30, plane.GroundSpeedKt) / 3600) / radius * 180 / Math.PI, RateForBank(circuit.TurnBank, plane.SpeedKt)));
            double rollIn = Math.Max(30, plane.GroundSpeedKt) / 3600 * (rateNeeded / TurnRateChange) / 2;
            (circuit.CentreX, circuit.CentreY) = FromRunway(along + (toFinal ? -rollIn : rollIn), toFinal ? lateral - radius : lateral + radius, circuit.Side);
            circuit.Radius = radius;
            circuit.Turning = true;
            plane.Navigation = NavigationMode.Manual;
            plane.CircuitBankDegrees = circuit.TurnBank;
            plane.SelectedTurnRateDps = Math.Clamp((Math.Max(30, plane.GroundSpeedKt) / 3600) / radius * 180 / Math.PI, MinTurnRate, MaxTurnRate);
            plane.TargetHeading = toFinal ? glidePathCourse : Normalize(glidePathCourse + 180);
            plane.TurnDirection = circuit.Side == CircuitSide.Left ? TurnDirection.Left : TurnDirection.Right;
            circuit.TurnHeading = plane.TargetHeading;
            if (!plane.AltitudeGiven && double.IsFinite(after.AltitudeFt))
            {
                // Down to the start of the final during the half circle (π r).
                double minutes = Math.PI * radius / Math.Max(30, plane.GroundSpeedKt) * 60;
                double change = after.AltitudeFt - plane.AltitudeFt;
                plane.VerticalMode = VerticalMode.Altitude;
                plane.TargetAltitudeFt = Math.Clamp(after.AltitudeFt, runwayElevation, MaxAltitude);
                // (A margin: it slows down in the turn, which then lasts less, and the level-off takes its time.)
                if (change < 0) plane.SelectedVerticalRateFpm = Math.Clamp(-change / Math.Max(0.1, minutes) * 1.5, Math.Min(300, MaxDescent(plane)), MaxDescent(plane));
            }
            if (!plane.SpeedGiven && double.IsFinite(after.SpeedKt)) plane.TargetSpeedKt = Math.Clamp(after.SpeedKt, plane.Class.MinSpeedKt, plane.Class.MaxSpeedKt);
        }

        /// <summary>
        /// During a 180° turn: on its circle (the turn tighter when outside it, wider when inside, within the bank limit); when
        /// it rolls out on the new heading, the route goes on from the point after the turn.
        /// </summary>
        private void TurnStep(SimAircraft plane, CircuitState circuit)
        {
            if (plane.StickX != 0 || Math.Abs(Wrap(plane.TargetHeading - circuit.TurnHeading)) > 0.01)
            {
                // The instructor gave a heading (or took the stick) during the turn: the circuit waits.
                circuit.Turning = false;
                circuit.Orbit = 0;
                circuit.Suspended = true;
                circuit.OnFinal = false;
                plane.CircuitBankDegrees = 0;
                plane.SelectedTurnRateDps = plane.TurnRateMode == TurnRateMode.Half ? HalfTurnRate : StandardTurnRate;
                return;
            }
            plane.CircuitBankDegrees = circuit.TurnBank;
            // Slowing down in the turn (power back, speed brakes): three times the class deceleration.
            if (!plane.SpeedGiven && plane.SpeedKt > plane.TargetSpeedKt)
            {
                plane.SpeedKt = Math.Max(plane.TargetSpeedKt, plane.SpeedKt - 2 * plane.Class.DecelerationKtPerSecond * Step);
            }
            double dx = plane.X - circuit.CentreX, dy = plane.Y - circuit.CentreY;
            double off = (Math.Sqrt(dx * dx + dy * dy) - circuit.Radius) / circuit.Radius;
            double rate = Math.Max(30, plane.GroundSpeedKt) / 3600 / circuit.Radius * (1 + 1.5 * off) * 180 / Math.PI;
            plane.SelectedTurnRateDps = Math.Clamp(rate, MinTurnRate, MaxTurnRate);
            if (circuit.Orbit == 1)
            {
                // Past two thirds of the first half: the target becomes the heading it started on (the same side, 240° still to
                // turn), so the turn goes on without rolling out half way.
                if (Math.Abs(Wrap(plane.HeadingTrue - plane.TargetHeading)) > 60) return;
                circuit.Orbit = 2;
                plane.TargetHeading = circuit.OrbitHeading;
                circuit.TurnHeading = plane.TargetHeading;
                return;
            }
            if (Math.Abs(Wrap(plane.HeadingTrue - plane.TargetHeading)) > 0.5 || Math.Abs(plane.TurnRateDps) > 0.2) return;
            circuit.Turning = false;
            if (circuit.Orbit == 2)
            {
                // The orbit done: on along the leg it was flying, from here.
                circuit.Orbit = 0;
                plane.LegStartX = plane.X;
                plane.LegStartY = plane.Y;
                plane.RouteApproached = false;
                plane.Navigation = NavigationMode.Route;
                plane.SelectedTurnRateDps = MaxTurnRate;
                return;
            }
            if (plane.Route == null) return;
            plane.RouteNext = Math.Min(plane.RouteNext + 1, plane.Route.Count);
            plane.LegStartX = plane.X;
            plane.LegStartY = plane.Y;
            plane.RouteApproached = false;
            plane.Navigation = NavigationMode.Route;
            plane.SelectedTurnRateDps = MaxTurnRate;
            circuit.LegIndex = -1;
            StartLeg(plane);
        }

        /// <summary>
        /// Touched down on the runway in a circuit: with a touch and go (or a departure) it rolls on and takes off again;
        /// true when so (not landed).
        /// </summary>
        private bool TouchAndGo(SimAircraft plane)
        {
            CircuitState? circuit = plane.Circuit;
            if (circuit == null || circuit.Suspended || circuit.HasPad || circuit.End is not (EndAction.TouchAndGo or EndAction.Departure)) return false;
            plane.Contact = GroundContact.Rolling;
            circuit.RollTime = 0;
            circuit.OnFinal = false;
            plane.AltitudeFt = runwayElevation;
            plane.VerticalSpeedFpm = 0;
            plane.Route = null;
            plane.Navigation = NavigationMode.Manual;
            return true;
        }

        /// <summary>On the runway: along it, a short roll after a touchdown, then faster until it lifts off at its final speed.</summary>
        private void Roll(SimAircraft plane)
        {
            CircuitState circuit = plane.Circuit!;
            circuit.RollTime += Step;
            PerformanceClass kind = plane.Class;
            double liftOff = kind.FinalSpeedKt;
            plane.HeadingTrue = plane.TargetHeading = plane.TrackTrue = glidePathCourse;
            plane.TurnRateDps = 0;
            plane.VerticalSpeedFpm = 0;
            plane.AltitudeFt = runwayElevation;
            double change = circuit.RollTime < TouchAndGoRoll ? -kind.DecelerationKtPerSecond * 2 * Step : Math.Max(4, kind.AccelerationKtPerSecond * 3) * Step;
            plane.SpeedKt = Math.Max(0, plane.SpeedKt + change);
            plane.GroundSpeedKt = plane.SpeedKt;
            plane.DriftDegrees = 0;
            double course = glidePathCourse * Math.PI / 180;
            plane.X += plane.SpeedKt * Math.Sin(course) * Step / 3600;
            plane.Y += plane.SpeedKt * Math.Cos(course) * Step / 3600;
            (double along, _) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            if (circuit.RollTime >= TouchAndGoRoll && (plane.SpeedKt >= liftOff || along >= Math.Max(runwayLength, 0.3)))
            {
                // Airborne: climbs along the runway, then the circuit (or away, for a departure).
                plane.Contact = GroundContact.Airborne;
                plane.SpeedKt = Math.Max(plane.SpeedKt, liftOff);
                plane.AltitudeFt = runwayElevation + 1;
                NextCircuit(circuit);
                double upwind = Math.Max(UpwindAlong(circuit.Type, kind), along + 0.3);
                double height = runwayElevation + kind.CircuitHeightFt;
                if (circuit.End == EndAction.Departure)
                {
                    circuit.Departed = true;
                    GiveRoute(plane, [Point(upwind, 0, circuit.Side, height, kind.CircuitSpeedKt, "UPWIND"), Point(upwind + 10, 0, circuit.Side, height, kind.CircuitSpeedKt, "DEPARTURE")]);
                }
                else if (circuit.Type is CircuitType.Overhead or CircuitType.Sfo)
                {
                    GiveRoute(plane, [Point(upwind, 0, circuit.Side, height, kind.CircuitSpeedKt, "UPWIND"), .. ToInitialPoint(plane)]);
                }
                else
                {
                    GiveRoute(plane, Loop(plane, upwind, withUpwind: true));
                }
                plane.SelectedVerticalRateFpm = Math.Max(MinVerticalRate, kind.MaxClimbFpm * 0.8);
            }
        }

        /// <summary>Go around: up along the runway to circuit height, then the circuit again (the close pattern: a 180° to its downwind).</summary>
        private void StartGoAround(SimAircraft plane)
        {
            CircuitState circuit = plane.Circuit!;
            circuit.Suspended = false;
            circuit.OnFinal = false;
            circuit.Departed = false;
            NextCircuit(circuit);
            (double along, _) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            double upwind = Math.Max(UpwindAlong(circuit.Type, plane.Class), along + 0.5);
            List<RoutePoint> route = circuit.Type is CircuitType.Overhead or CircuitType.Sfo
                ? [Point(upwind, 0, circuit.Side, runwayElevation + plane.Class.CircuitHeightFt, plane.Class.CircuitSpeedKt, "UPWIND"), .. ToInitialPoint(plane)]
                : Loop(plane, upwind, withUpwind: true);
            route[0] = route[0] with { Name = "GO AROUND" };
            plane.SpeedGiven = false;
            GiveRoute(plane, route);
            plane.SelectedVerticalRateFpm = Math.Max(MinVerticalRate, plane.Class.MaxClimbFpm * 0.8);
        }

        // ----- The shape of the circuits --------------------------------------------------------------------------------

        /// <summary>
        /// The points of one circuit, from the end of the upwind (when asked) to the threshold, in the runway frame of its
        /// side. Conventional and wide: crosswind at <paramref name="upwind"/>, downwind at the width of the circuit
        /// (descent from abeam the threshold), base as far before the threshold as the downwind is wide, the final.
        /// Close: a 180° turn from the upwind to the downwind (the first DOWNWIND point, its end), the downwind on past the
        /// threshold, a descending 180° turn (the first FINAL point, its end) that rolls out on the final.
        /// </summary>
        private List<RoutePoint> Loop(SimAircraft plane, double upwind, bool withUpwind)
        {
            CircuitState circuit = plane.Circuit!;
            PerformanceClass kind = plane.Class;
            CircuitSide side = circuit.Side;
            double width = Width(circuit.Type, kind, side);
            double height = runwayElevation + kind.CircuitHeightFt;
            double speed = kind.CircuitSpeedKt;
            double glide = GlideFeetPerNM();
            List<RoutePoint> points = [];
            if (withUpwind) points.Add(Point(upwind, 0, side, height, speed, "UPWIND"));
            if (IsJetShape(circuit.Type))
            {
                // The ends of the two 180° turns (flown as turns, see StartTurn): the downwind start, the roll out on the final.
                double turn = BaseAlong(CircuitType.Close, kind);
                points.Add(Point(upwind, width, side, height, speed, "DOWNWIND", turn180: true));
                points.Add(Point(turn, width, side, height, speed, "DOWNWIND"));
                points.Add(Point(turn, 0, side, runwayElevation + kind.RollOutHeightFt, kind.FinalSpeedKt, "FINAL", turn180: true));
            }
            else
            {
                double baseTurn = BaseAlong(circuit.Type, kind);
                points.Add(Point(upwind, width, side, height, speed, "CROSSWIND"));
                points.Add(Point(0, width, side, height, speed, "DOWNWIND"));
                points.Add(Point(baseTurn, width, side, Math.Min(height, runwayElevation + (width - baseTurn) * glide), speed, "DOWNWIND"));
                points.Add(Point(baseTurn, 0, side, runwayElevation + -baseTurn * glide, kind.FinalSpeedKt, "BASE"));
            }
            points.Add(Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL"));
            return points;
        }

        /// <summary>
        /// The overhead pattern from the initial point: the initial along the runway track at the initial height and speed
        /// (its point far beyond the runway: the break comes before it), the break (a 180° turn to the downwind, at circuit
        /// height and speed), the downwind, the 180° turn to the final, the final.
        /// </summary>
        private List<RoutePoint> OverheadRoute(SimAircraft plane)
        {
            PerformanceClass kind = plane.Class;
            CircuitSide side = plane.Circuit!.Side;
            double length = Math.Max(runwayLength, 0.3);
            double width = Width(CircuitType.Overhead, kind, side);
            double height = runwayElevation + kind.CircuitHeightFt;
            double turn = BaseAlong(CircuitType.Overhead, kind);
            return
            [
                Point(length + 5, 0, side, runwayElevation + kind.OverheadHeightFt, kind.OverheadSpeedKt, "INITIAL"),
                Point(length, width, side, height, kind.CircuitSpeedKt, "DOWNWIND", turn180: true),
                Point(turn, width, side, height, kind.CircuitSpeedKt, "DOWNWIND"),
                Point(turn, 0, side, runwayElevation + kind.RollOutHeightFt, kind.FinalSpeedKt, "FINAL", turn180: true),
                Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")
            ];
        }

        /// <summary>
        /// The simulated flame out from 1 NM before the High Key: the High Key over the middle of the runway
        /// at its height and the best glide speed, the 180° turn to the Low Key abeam it (the width of the close pattern),
        /// a short downwind along the runway gliding down to the threshold (as much height per NM as from the Low Key to the
        /// roll out), the 180° turn to the final there: it rolls out nearly on the runway (the user's drawing, 2 Nov).
        /// </summary>
        private List<RoutePoint> SfoRoute(SimAircraft plane)
        {
            PerformanceClass kind = plane.Class;
            CircuitSide side = plane.Circuit!.Side;
            double width = Width(CircuitType.Sfo, kind, side);
            double highKey = HighKeyAlong();
            // The turn to the final starts at the threshold: at its end the aircraft is nearly on the runway.
            double turn = -SfoTurnStartNM;
            double lowKey = runwayElevation + kind.LowKeyFt;
            double rollOut = runwayElevation + Math.Min(SfoRollOutHeight, kind.LowKeyFt);
            // The downwind and the half circle to the final, gliding at one slope.
            double downwind = Math.Max(0, highKey - turn);
            double finalTurn = Math.PI * width / 2;
            double endOfDownwind = lowKey - (lowKey - rollOut) * downwind / Math.Max(0.1, downwind + finalTurn);
            return
            [
                Point(highKey, 0, side, runwayElevation + kind.HighKeyFt, kind.BestGlideKt, "HIGH KEY"),
                Point(highKey, width, side, lowKey, kind.BestGlideKt, "LOW KEY", turn180: true),
                Point(turn, width, side, endOfDownwind, kind.BestGlideKt, "LOW KEY"),
                Point(turn, 0, side, rollOut, kind.FinalSpeedKt, "FINAL", turn180: true),
                Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")
            ];
        }

        /// <summary>The High Key along the runway (NM from the threshold): over its middle.</summary>
        private double HighKeyAlong() => 0.5 * Math.Max(runwayLength, 0.3);

        /// <summary>
        /// The descent limit (ft/min): in the simulated flame out one and a half times the class's (idle, speed brakes), so
        /// the compact pattern around the runway loses the height from the High Key.
        /// </summary>
        internal static double MaxDescent(SimAircraft plane) =>
            plane.Class.MaxDescentFpm * (plane.Circuit is { Type: CircuitType.Sfo, Suspended: false, Departed: false } ? 1.5 : 1);

        /// <summary>
        /// Back to the initial point for a new overhead (or to the High Key for a new flame out): out on the wide downwind to
        /// abeam a point 1 NM before the initial point, onto the runway track there, then the overhead pattern (the flame
        /// out: at the High Key height, to the High Key).
        /// </summary>
        private List<RoutePoint> ToInitialPoint(SimAircraft plane)
        {
            PerformanceClass kind = plane.Class;
            CircuitSide side = plane.Circuit!.Side;
            bool sfo = plane.Circuit.Type == CircuitType.Sfo;
            double before = initialPoint + 1;
            double wide = Width(CircuitType.Wide, kind, side);
            double height = runwayElevation + (sfo ? kind.HighKeyFt : kind.OverheadHeightFt);
            double speed = sfo ? kind.BestGlideKt : kind.OverheadSpeedKt;
            return
            [
                Point(-before, wide, side, height, kind.CircuitSpeedKt, "JOIN"),
                Point(-before, 0, side, height, speed, "JOIN"),
                .. (sfo ? SfoRoute(plane) : OverheadRoute(plane))
            ];
        }

        /// <summary>After a go-around or a touch and go: the circuit ordered meanwhile, else the overhead goes on as the close pattern.</summary>
        private static void NextCircuit(CircuitState circuit)
        {
            if (circuit.NextType is CircuitType next)
            {
                circuit.Type = next;
                circuit.Side = circuit.NextSide;
                circuit.NextType = null;
            }
            else if (circuit.Type is CircuitType.Overhead or CircuitType.Sfo)
            {
                circuit.Type = CircuitType.Close;
            }
        }

        /// <summary>The circuits of the jet shape (no crosswind, no base: 180° turns): close, overhead, flame out.</summary>
        private static bool IsJetShape(CircuitType type) => type is CircuitType.Close or CircuitType.Overhead or CircuitType.Sfo;

        /// <summary>Width of the downwind of a circuit (NM), as in the class (the overhead's: the jet circuit's).</summary>
        private static double BaseWidth(CircuitType type, PerformanceClass kind) => type switch
        {
            CircuitType.Close or CircuitType.Overhead or CircuitType.Sfo => kind.CloseDownwindNM,
            CircuitType.Wide => kind.WideDownwindNM,
            _ => kind.DownwindNM
        };

        /// <summary>
        /// Width of the downwind (NM) corrected for the crosswind, as pilots do: 0.1 NM for every 10 kt of wind across the
        /// runway, wider when it blows towards the runway from the circuit side, narrower when it blows away from it.
        /// </summary>
        private double Width(CircuitType type, PerformanceClass kind, CircuitSide side)
        {
            double c = glidePathCourse * Math.PI / 180;
            double s = side == CircuitSide.Right ? 1 : -1;
            double towards = (windFrom + 180) * Math.PI / 180;
            double east = windSpeed * Math.Sin(towards), north = windSpeed * Math.Cos(towards);
            // Wind component from the circuit side towards the runway (kt).
            double towardsRunway = -s * (east * Math.Cos(c) - north * Math.Sin(c));
            return Math.Max(0.3, BaseWidth(type, kind) + 0.01 * towardsRunway);
        }

        /// <summary>A point of the downwind itself (not the end of the 180° turn to it).</summary>
        private static bool IsDownwind(RoutePoint point) => point.Name == "DOWNWIND" && !point.Turn180;

        /// <summary>Where the downwind starts along the runway (NM from the threshold): the close pattern abeam the runway end.</summary>
        private double DownwindStartAlong(CircuitType type, PerformanceClass kind) =>
            IsJetShape(type) ? Math.Max(runwayLength, 0.3) : UpwindAlong(type, kind);

        /// <summary>
        /// Where the upwind ends, along the runway (NM from the threshold): beyond the runway end; the close pattern's is far
        /// ahead, as it turns to the downwind as soon as it is above 400 ft.
        /// </summary>
        private double UpwindAlong(CircuitType type, PerformanceClass kind)
        {
            double length = Math.Max(runwayLength, 0.3);
            return IsJetShape(type) ? length + 2 : length + 0.6 * BaseWidth(type, kind);
        }

        /// <summary>Where the base (the close pattern: its turn to the final, rolling out there) is, along the runway (negative: before the threshold).</summary>
        private static double BaseAlong(CircuitType type, PerformanceClass kind) =>
            IsJetShape(type) ? -kind.RollOutNM : -BaseWidth(type, kind);

        /// <summary>
        /// The point the aircraft flies to is passed now (a turn anticipated, a waveoff turn): the next one is flown to. From
        /// here (<paramref name="fromHere"/>) or, at a corner, from the point passed (the leg keeps its line).
        /// </summary>
        private void PassPoint(SimAircraft plane, bool fromHere)
        {
            RoutePoint passed = plane.Route![plane.RouteNext];
            plane.RouteNext++;
            plane.LegStartX = fromHere ? plane.X : passed.X;
            plane.LegStartY = fromHere ? plane.Y : passed.Y;
            plane.RouteApproached = false;
            StartLeg(plane);
        }

        /// <summary>Feet of the glide path per NM (3° when the runway has no angle).</summary>
        private double GlideFeetPerNM() => FeetPerNM * Math.Tan((glidePathAngle > 0 ? glidePathAngle : 3) * Math.PI / 180);

        /// <summary>A route point at this place of the runway frame (along the landing direction, to the circuit side).</summary>
        private RoutePoint Point(double along, double lateral, CircuitSide side, double altitudeFt, double speedKt, string name, bool turn180 = false)
        {
            (double x, double y) = FromRunway(along, lateral, side);
            return new RoutePoint(x, y, altitudeFt, speedKt, name, turn180);
        }

        private (double X, double Y, double Altitude, double Heading, double Speed) At(double along, double lateral, CircuitSide side, double altitude, double heading, double speed)
        {
            (double x, double y) = FromRunway(along, lateral, side);
            return (x, y, altitude, heading, speed);
        }

        /// <summary>The plane (NM east, north) of a place along the runway (NM from the threshold) and to the circuit side of it (NM).</summary>
        private (double X, double Y) FromRunway(double along, double lateral, CircuitSide side)
        {
            double c = glidePathCourse * Math.PI / 180;
            double s = side == CircuitSide.Right ? 1 : -1;
            return (along * Math.Sin(c) + s * lateral * Math.Cos(c), along * Math.Cos(c) - s * lateral * Math.Sin(c));
        }

        /// <summary>A place of the plane in the runway frame: along the landing direction from the threshold, to the circuit side (NM).</summary>
        private (double Along, double Lateral) RunwayFrame(double x, double y, CircuitSide side)
        {
            double c = glidePathCourse * Math.PI / 180;
            double s = side == CircuitSide.Right ? 1 : -1;
            return (x * Math.Sin(c) + y * Math.Cos(c), s * (x * Math.Cos(c) - y * Math.Sin(c)));
        }

        private double AlongOf(RoutePoint point, CircuitSide side) => RunwayFrame(point.X, point.Y, side).Along;

        /// <summary>A new route flown at once, from here (the circuit's own route, not the instructor's): its altitudes and speeds flown.</summary>
        private void GiveRoute(SimAircraft plane, List<RoutePoint> points)
        {
            plane.Route = points.ToArray();
            plane.RouteNext = 0;
            plane.LegStartX = plane.X;
            plane.LegStartY = plane.Y;
            plane.RouteApproached = false;
            plane.Navigation = NavigationMode.Route;
            plane.AltitudeGiven = false;
            plane.SpeedGiven = false;
            if (plane.Circuit != null)
            {
                plane.Circuit.LegIndex = -1;
                plane.Circuit.Turning = false;
                plane.Circuit.Orbit = 0;
            }
            ApplyPad(plane);
            StartLeg(plane);
        }

        /// <summary>The last point of the circuit's route (its FINAL, the threshold) at the helicopter's pad, or back at the threshold.</summary>
        private void ApplyPad(SimAircraft plane)
        {
            if (plane.Route is not { Count: > 0 } route || plane.Circuit is not CircuitState circuit) return;
            RoutePoint last = route[route.Count - 1];
            if (last.Name != "FINAL" || last.Turn180) return;
            (double x, double y) = circuit.HasPad ? (circuit.PadX, circuit.PadY) : FromRunway(0, 0, circuit.Side);
            RoutePoint[] points = route.ToArray();
            points[^1] = last with { X = x, Y = y };
            plane.Route = points;
        }

        /// <summary>The bank limit of the turns: the circuit's while it flies one, else the class's.</summary>
        private static double BankLimit(SimAircraft plane) => plane.CircuitBankDegrees > 0 ? plane.CircuitBankDegrees : plane.Class.MaxBankDegrees;
    }
}
