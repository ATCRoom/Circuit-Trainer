namespace ATCRoom.Sim
{
    /// <summary>The kinds of circuit (TWR Trainer). Overhead and SFO come with the next step.</summary>
    public enum CircuitType
    {
        /// <summary>The rectangular circuit: upwind, crosswind, downwind, base, final.</summary>
        Conventional,
        /// <summary>Close pattern: the downwind at the width of the jet circuit, a 180° turn from it to the final (and from a go-around to the downwind).</summary>
        Close,
        /// <summary>Wide circuit (outside downwind): as the conventional one, further out.</summary>
        Wide
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
        Runway
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
    }

    public sealed partial class SimEngine
    {
        /// <summary>Where the descent of the final aims (NM past the threshold), and the low pass height (ft).</summary>
        private const double TouchdownAim = 0.15;
        private const double LowPassHeight = 100;
        /// <summary>The go-around of the end action "go around": at this height (ft above the threshold), on the short final (NM before the threshold at most).</summary>
        private const double GoAroundHeight = 200;
        private const double GoAroundShortFinal = 0.4;
        /// <summary>Seconds of the roll after a touchdown before the aircraft accelerates again.</summary>
        private const double TouchAndGoRoll = 3;

        private double longFinal = 10;
        private double initialPoint = 3;

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
        /// course) by itself: where it starts, the circuit, its side and what it does at the end of each final. Returns
        /// its callsign. The circuit is built from the runway and the values of the class (performance.json).
        /// </summary>
        public string AddInCircuit(CircuitStart start, CircuitType type, CircuitSide side, EndAction end, string? callsign = null,
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null)
        {
            lock (sync)
            {
                if (double.IsNaN(glidePathCourse)) throw new InvalidOperationException("No runway: a circuit needs the runway in use.");
                PerformanceClass kind = (classes.FirstOrDefault(c => Same(c.Name, performanceClass ?? ""))
                    ?? classes.FirstOrDefault(c => Same(c.Name, PerformanceClass.DefaultName)) ?? classes[0]);
                double height = runwayElevation + kind.CircuitHeightFt;
                double glide = GlideFeetPerNM();
                (double x, double y, double altitude, double heading, double speed) = start switch
                {
                    CircuitStart.LongFinal => At(-longFinal, 0, side, runwayElevation + longFinal * glide, glidePathCourse, kind.ApproachSpeedKt),
                    CircuitStart.InitialPoint => At(-initialPoint, 0, side, height, glidePathCourse, kind.CircuitSpeedKt),
                    CircuitStart.Downwind => At(UpwindAlong(type, kind), Width(type, kind), side, height, Normalize(glidePathCourse + 180), kind.CircuitSpeedKt),
                    _ => At(0.02, 0, side, runwayElevation, glidePathCourse, kind.MinSpeedKt)
                };
                string name = Add(x, y, altitude, heading, speed, callsign, kind.Name, takenElsewhere);
                SimAircraft plane = Find(name)!;
                plane.Circuit = new CircuitState { Type = type, Side = side, End = end };
                switch (start)
                {
                    case CircuitStart.LongFinal:
                        GiveRoute(plane, [Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")]);
                        plane.TargetSpeedKt = kind.ApproachSpeedKt;
                        break;
                    case CircuitStart.InitialPoint:
                        GiveRoute(plane, [Point(0, 0, side, height, kind.CircuitSpeedKt, "INITIAL"), .. Loop(plane, UpwindAlong(type, kind), withUpwind: true)]);
                        break;
                    case CircuitStart.Downwind:
                        GiveRoute(plane, Loop(plane, UpwindAlong(type, kind), withUpwind: false).SkipWhile(p => p.Name != "DOWNWIND").ToList());
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
        /// Instruction: fly this circuit, on this side, from where the aircraft is (also to change circuit: the close
        /// pattern to stay near, the wide one to make room). On the circuit side between the base turn and the crosswind it
        /// joins the new downwind ahead of it; elsewhere it flies to the start of the downwind. On the final (or rolling)
        /// it goes on: the new circuit is flown after it. Also takes back an aircraft the instructor was flying.
        /// </summary>
        public void SetCircuit(string callsign, CircuitType type, CircuitSide side) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (double.IsNaN(glidePathCourse) || a.Contact is GroundContact.Landed or GroundContact.OnGround) return;
                CircuitState circuit = a.Circuit ??= new CircuitState { End = EndAction.FullStop };
                circuit.Type = type;
                circuit.Side = side;
                circuit.Departed = false;
                if (a.Contact == GroundContact.Rolling) return;
                (double along, double lateral) = RunwayFrame(a.X, a.Y, side);
                if (circuit.OnFinal && !circuit.Suspended && Math.Abs(lateral) < 0.3 && along < runwayLength) return;
                circuit.Suspended = false;
                circuit.OnFinal = false;
                double width = Width(type, a.Class);
                double upwind = UpwindAlong(type, a.Class);
                List<RoutePoint> loop = Loop(a, upwind, withUpwind: false);
                List<RoutePoint> downwind = loop.SkipWhile(p => p.Name != "DOWNWIND").ToList();
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
                    // Elsewhere: to the start of the downwind, then the circuit.
                    GiveRoute(a, [Point(upwind, width, side, runwayElevation + a.Class.CircuitHeightFt, a.Class.CircuitSpeedKt, "JOIN"), .. downwind]);
                }
            });

        /// <summary>What the aircraft does at the end of each final, at once (it can be changed at any time).</summary>
        public void SetEndAction(string callsign, EndAction action) => Change(callsign, a =>
        {
            if (a.Circuit != null) a.Circuit.End = action;
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
            if (!routeOver && plane.Route != null && plane.RouteNext != circuit.LegIndex)
            {
                // A new leg: the vertical rate that reaches the altitude of its point at its end (climbing: briskly).
                circuit.LegIndex = plane.RouteNext;
                RoutePoint point = plane.Route[plane.RouteNext];
                circuit.OnFinal = point.Name == "FINAL";
                // The close pattern: its downwind and its final are reached by a 180° turn, not flown to as points.
                if (circuit.Type == CircuitType.Close && point.Name is "CROSSWIND" or "BASE")
                {
                    StartTurn(plane, circuit, point);
                    return;
                }
                if (double.IsFinite(point.AltitudeFt))
                {
                    double climb = point.AltitudeFt - plane.AltitudeFt;
                    double minutes = Math.Sqrt((point.X - plane.X) * (point.X - plane.X) + (point.Y - plane.Y) * (point.Y - plane.Y)) / Math.Max(30, plane.GroundSpeedKt) * 60;
                    plane.SelectedVerticalRateFpm = climb > 0 ? Math.Max(MinVerticalRate, plane.Class.MaxClimbFpm * 0.8)
                        : Math.Clamp(-climb / Math.Max(0.1, minutes) * 1.2, Math.Min(300, plane.Class.MaxDescentFpm), plane.Class.MaxDescentFpm);
                }
            }
            if (!circuit.OnFinal) return;
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
            plane.CommandedVerticalSpeedFpm = Math.Clamp(wanted, -plane.Class.MaxDescentFpm, 300);
        }

        /// <summary>
        /// A 180° turn of the close pattern towards its side (to the downwind, or to the final, descending to the start of
        /// the final), at the rate that makes the turn as wide as the downwind; the route waits at the point after the turn.
        /// </summary>
        private void StartTurn(SimAircraft plane, CircuitState circuit, RoutePoint after)
        {
            double radius = Width(CircuitType.Close, plane.Class) / 2;
            double rate = (Math.Max(30, plane.SpeedKt) / 3600) / radius * 180 / Math.PI;
            circuit.Turning = true;
            plane.Navigation = NavigationMode.Manual;
            plane.SelectedTurnRateDps = Math.Clamp(rate, MinTurnRate, MaxTurnRate);
            plane.TargetHeading = after.Name == "BASE" ? glidePathCourse : Normalize(glidePathCourse + 180);
            plane.TurnDirection = circuit.Side == CircuitSide.Left ? TurnDirection.Left : TurnDirection.Right;
            if (!plane.AltitudeGiven && double.IsFinite(after.AltitudeFt))
            {
                // Down to the start of the final during the half circle (π r).
                double minutes = Math.PI * radius / Math.Max(30, plane.GroundSpeedKt) * 60;
                double change = after.AltitudeFt - plane.AltitudeFt;
                plane.VerticalMode = VerticalMode.Altitude;
                plane.TargetAltitudeFt = Math.Clamp(after.AltitudeFt, runwayElevation, MaxAltitude);
                if (change < 0) plane.SelectedVerticalRateFpm = Math.Clamp(-change / Math.Max(0.1, minutes), Math.Min(300, plane.Class.MaxDescentFpm), plane.Class.MaxDescentFpm);
            }
            if (!plane.SpeedGiven && double.IsFinite(after.SpeedKt)) plane.TargetSpeedKt = Math.Clamp(after.SpeedKt, plane.Class.MinSpeedKt, plane.Class.MaxSpeedKt);
        }

        /// <summary>During a 180° turn: when it rolls out on the new heading, the route goes on from the point after the turn.</summary>
        private void TurnStep(SimAircraft plane, CircuitState circuit)
        {
            plane.CircuitBankDegrees = plane.Class.CircuitBankDegrees;
            if (Math.Abs(Wrap(plane.HeadingTrue - plane.TargetHeading)) > 0.5 || Math.Abs(plane.TurnRateDps) > 0.2) return;
            circuit.Turning = false;
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
            if (circuit == null || circuit.Suspended || circuit.End is not (EndAction.TouchAndGo or EndAction.Departure)) return false;
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
                double upwind = Math.Max(UpwindAlong(circuit.Type, kind), along + 0.3);
                double height = runwayElevation + kind.CircuitHeightFt;
                if (circuit.End == EndAction.Departure)
                {
                    circuit.Departed = true;
                    GiveRoute(plane, [Point(upwind, 0, circuit.Side, height, kind.CircuitSpeedKt, "UPWIND"), Point(upwind + 10, 0, circuit.Side, height, kind.CircuitSpeedKt, "DEPARTURE")]);
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
            (double along, _) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            double upwind = Math.Max(UpwindAlong(circuit.Type, plane.Class), along + 0.5);
            List<RoutePoint> route = Loop(plane, upwind, withUpwind: true);
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
        /// Close: a 180° turn from the upwind to the downwind (point CROSSWIND, its end), the downwind, a descending 180° turn
        /// (point BASE, its end) to a short final.
        /// </summary>
        private List<RoutePoint> Loop(SimAircraft plane, double upwind, bool withUpwind)
        {
            CircuitState circuit = plane.Circuit!;
            PerformanceClass kind = plane.Class;
            CircuitSide side = circuit.Side;
            double width = Width(circuit.Type, kind);
            double height = runwayElevation + kind.CircuitHeightFt;
            double speed = kind.CircuitSpeedKt;
            double glide = GlideFeetPerNM();
            List<RoutePoint> points = [];
            if (withUpwind) points.Add(Point(upwind, 0, side, height, speed, "UPWIND"));
            if (circuit.Type == CircuitType.Close)
            {
                // The ends of the two 180° turns (flown as turns, see StartTurn): the downwind start, the final start.
                double turn = BaseAlong(CircuitType.Close, kind);
                points.Add(Point(upwind, width, side, height, speed, "CROSSWIND"));
                points.Add(Point(turn, width, side, height, speed, "DOWNWIND"));
                points.Add(Point(turn, 0, side, runwayElevation + -turn * glide, kind.FinalSpeedKt, "BASE"));
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

        /// <summary>Width of the downwind of a circuit (NM).</summary>
        private static double Width(CircuitType type, PerformanceClass kind) => type switch
        {
            CircuitType.Close => kind.CloseDownwindNM,
            CircuitType.Wide => kind.WideDownwindNM,
            _ => kind.DownwindNM
        };

        /// <summary>Where the upwind ends, along the runway (NM from the threshold): the close pattern at the runway end, the others beyond it.</summary>
        private double UpwindAlong(CircuitType type, PerformanceClass kind)
        {
            double length = Math.Max(runwayLength, 0.3);
            return type == CircuitType.Close ? length : length + 0.6 * Width(type, kind);
        }

        /// <summary>Where the base (the close pattern: its final turn) is, along the runway (negative: before the threshold).</summary>
        private static double BaseAlong(CircuitType type, PerformanceClass kind) =>
            type == CircuitType.Close ? -(0.3 + 0.5 * kind.CloseDownwindNM) : -Width(type, kind);

        /// <summary>Feet of the glide path per NM (3° when the runway has no angle).</summary>
        private double GlideFeetPerNM() => FeetPerNM * Math.Tan((glidePathAngle > 0 ? glidePathAngle : 3) * Math.PI / 180);

        /// <summary>A route point at this place of the runway frame (along the landing direction, to the circuit side).</summary>
        private RoutePoint Point(double along, double lateral, CircuitSide side, double altitudeFt, double speedKt, string name)
        {
            (double x, double y) = FromRunway(along, lateral, side);
            return new RoutePoint(x, y, altitudeFt, speedKt, name);
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
            }
            StartLeg(plane);
        }

        /// <summary>The bank limit of the turns: the circuit's while it flies one, else the class's.</summary>
        private static double BankLimit(SimAircraft plane) => plane.CircuitBankDegrees > 0 ? plane.CircuitBankDegrees : plane.Class.MaxBankDegrees;
    }
}
