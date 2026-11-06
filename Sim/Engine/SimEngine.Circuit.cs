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

    /// <summary>Where DCT TO sends an aircraft in flight at once (the user, 5 Nov): the conventional downwind, its base, the initial point, the High Key.</summary>
    public enum DirectPoint { Downwind, Base, InitialPoint, HighKey }

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
        HighKey,
        /// <summary>
        /// Circling (the user, 3 Nov: an aerodrome with the approach on one runway only, the wind for the other one): on the
        /// long final of the opposite runway, down to the height of the conventional circuit, aside by 45° onto its
        /// downwind, then the conventional circuit of the runway in use.
        /// </summary>
        Circling
    }

    /// <summary>How an aircraft coming from an entry gate (or from outside) joins its circuit.</summary>
    public enum CircuitEntry
    {
        /// <summary>A 45° entry onto the downwind.</summary>
        Downwind,
        /// <summary>Straight to the start of the base, then the base and the final.</summary>
        Base,
        /// <summary>Straight to the initial point (overhead pattern).</summary>
        InitialPoint,
        /// <summary>To the High Key, climbing on its circle (SFO), on hold there.</summary>
        HighKey,
        /// <summary>Straight in: onto the final 4 NM out.</summary>
        Final,
        /// <summary>Circling from the long final of the opposite runway (only from the long final, <see cref="CircuitStart.Circling"/>; from elsewhere: the downwind).</summary>
        Circling
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
        /// <summary>Left the circuit (end action Departure): the circuit does nothing more (gone at <see cref="SimEngine.DepartureGoneNM"/>).</summary>
        public bool Departed;
        /// <summary>
        /// A departure with no circuit (the user, 5 Nov): the radial it intercepts after the take-off (degrees true; NaN: on
        /// along the runway heading), from this point (the plane, NM), its first turn to it (left or right), its height
        /// (ft, altitude). Phase: 0 along the runway, 1 turning to the intercept heading, 2 on the radial, 3 the instructor's.
        /// </summary>
        public double DepartRadial = double.NaN;
        public double DepartFromX, DepartFromY, DepartHeight = double.NaN, DepartIntercept;
        public TurnDirection DepartTurn = TurnDirection.Shortest;
        public int DepartPhase;
        /// <summary>
        /// Away after the last circuit of its plan (the user, 5 Nov: a departure or a crossing with circuits): after that final
        /// (its touch and go, low approach or low pass) it leaves as a departure (the runway heading or its radial), or to the
        /// gate (<see cref="ExitX"/>, <see cref="ExitY"/>: the plane, NM; NaN: none), where it is gone.
        /// </summary>
        public bool LeaveAfterLast;
        public double ExitX = double.NaN, ExitY = double.NaN;
        /// <summary>A helicopter taking off from a helipad (the user, 6 Nov): seconds of its hover there (NaN: none).</summary>
        public double PadHover = double.NaN;
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
        /// <summary>
        /// SFO: on the circle over the field that passes over the High Key (climbing to its height, or holding there), and
        /// "hold at High Key" (it goes on circling there until released). The circle: <see cref="CentreX"/>,
        /// <see cref="CentreY"/>, <see cref="Radius"/> (its centre moves to the High Key's circle).
        /// </summary>
        public bool Spiral;
        public bool Hold;
        /// <summary>The downwind extended (until "turn base").</summary>
        public bool Extended;
        /// <summary>Orbits until "continue" (not one only).</summary>
        public bool OrbitForever;
        /// <summary>
        /// After the instructor's GO AROUND: on along the final at its altitude, then the runway heading, waiting for a new
        /// order (it does not fly the circuit by itself).
        /// </summary>
        public bool Waiting;
        /// <summary>
        /// Crossing the zone (the user, 3 Nov): from a gate to another one (over the field or not), at its height, never
        /// landing; gone at the exit gate (<see cref="TransitDone"/>). Its way: the exit point, its height and speed.
        /// </summary>
        public bool Transit;
        public bool TransitDone;
        public double TransitX, TransitY, TransitHeight, TransitSpeed;
        /// <summary>A helicopter's landing point (the plane, NM): its final goes there, hovering down to the ground.</summary>
        public bool HasPad;
        /// <summary>A pad wanted but not given yet (the user, 5 Nov: it starts anyway; none given by the final: the runway).</summary>
        public bool WantsPad;
        /// <summary>
        /// The height of this circuit given by the instructor (the user, 6 Nov: ft, altitude; NaN: the class's): its downwind,
        /// its initial and break, its High Key; from the base / the turn to the final / the Low Key it goes down as usual; the
        /// next circuit the class's again.
        /// </summary>
        public double LapHeight = double.NaN;
        public double PadX, PadY;
        /// <summary>A wingman just split off: straight on with this heading until this step, then its circuit.</summary>
        public long DelayUntilStep;
        public double DelayHeading;
        /// <summary>Straight in from the long final (a formation lands all together; from a circuit at most 2). Cleared by a new route.</summary>
        public bool StraightIn;
        /// <summary>A member of a formation going around: its turn to the downwind this many seconds later than the first one's.</summary>
        public double TurnWaitSeconds;
        /// <summary>
        /// The plan of the aircraft (the user, 4 Nov): the circuits still to fly, this one included (0: on and on), each
        /// ending with a touch and go but the last one, which ends with <see cref="FinalEnd"/>; a touchdown of a touch and
        /// go counted, taken off from (<see cref="LapTouched"/>).
        /// </summary>
        public int LapsLeft;
        public EndAction FinalEnd = EndAction.FullStop;
        /// <summary>How the circuits of the plan but the last one end (the user, 5 Nov, "On Final"): touch and go, low approach, low pass.</summary>
        public EndAction LapEnd = EndAction.TouchAndGo;
        public bool LapTouched;

        public CircuitState Copy() => (CircuitState)MemberwiseClone();
    }

    public sealed partial class SimEngine
    {
        /// <summary>The shortest base on TURN BASE: this far past the threshold along the downwind (NM, negative).</summary>
        private const double ShortBaseAlong = -0.3;
        /// <summary>Where the descent of the final aims (NM past the threshold), and the low pass height (ft).</summary>
        private const double TouchdownAim = 0.15;
        private const double LowPassHeight = 400;
        /// <summary>The go-around of the end action "go around": at this height (ft above the threshold), on the short final (NM before the threshold at most).</summary>
        private const double GoAroundHeight = 200;
        private const double GoAroundShortFinal = 0.7;
        /// <summary>The waveoff of the jet circuits: above this height (ft above the threshold) it turns to the downwind at once, climbing.</summary>
        private const double WaveoffTurnHeight = 400;
        /// <summary>A departure is gone this far from the aerodrome (NM, the user 5 Nov), and climbs to this height (ft) when none is given.</summary>
        public const double DepartureGoneNM = 10;
        private const double DepartureHeight = 3000;
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

        /// <summary>The side of a circuit at the runway in use: the aerodrome's for its group, else the runway's, else left.</summary>
        public CircuitSide SideFor(CircuitType type)
        {
            lock (sync) return aerodrome?.SideOf(aerodromeRunway, type) ?? CircuitSide.Left;
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
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null, bool hold = false)
        {
            lock (sync)
            {
                if (double.IsNaN(glidePathCourse)) throw new InvalidOperationException("No runway: a circuit needs the runway in use.");
                // The overhead pattern starts at the initial point, the flame out at the High Key; elsewhere they are the close
                // pattern (their second part).
                (type, CircuitSide side, PerformanceClass kind, double along, double lateral) = StartOf(start, type, sideGiven, performanceClass);
                double height = runwayElevation + CircuitHeight(type, kind);
                double glide = GlideFeetPerNM();
                (double x, double y, double altitude, double heading, double speed) = start switch
                {
                    CircuitStart.LongFinal => At(along, lateral, side, runwayElevation + longFinal * glide, glidePathCourse, kind.ApproachSpeedKt),
                    CircuitStart.InitialPoint => type == CircuitType.Overhead
                        ? At(along, lateral, side, runwayElevation + kind.OverheadHeightFt, glidePathCourse, kind.OverheadSpeedKt)
                        : At(along, lateral, side, height, glidePathCourse, kind.CircuitSpeedKt),
                    CircuitStart.Downwind => At(along, lateral, side, height, Normalize(glidePathCourse + 180), kind.CircuitSpeedKt),
                    CircuitStart.HighKey => At(along, lateral, side, runwayElevation + kind.HighKeyFt, glidePathCourse, kind.BestGlideKt),
                    CircuitStart.Circling => At(along, lateral, side, runwayElevation + longFinal * glide, Normalize(glidePathCourse + 180), kind.ApproachSpeedKt),
                    _ => At(along, lateral, side, runwayElevation, glidePathCourse, kind.MinSpeedKt)
                };
                string name = Add(x, y, altitude, heading, speed, callsign, kind.Name, takenElsewhere);
                SimAircraft plane = Find(name)!;
                // The class with the heights of this aerodrome.
                plane.Class = kind;
                // (The SFO on hold at the High Key by default, the user 3 Nov: until "start SFO".)
                plane.Circuit = new CircuitState { Type = type, Side = side, End = end, Hold = type == CircuitType.Sfo };
                switch (start)
                {
                    case CircuitStart.LongFinal:
                        GiveRoute(plane, [Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")]);
                        plane.TargetSpeedKt = kind.ApproachSpeedKt;
                        plane.Circuit.StraightIn = true;
                        break;
                    case CircuitStart.InitialPoint when type == CircuitType.Overhead:
                        GiveRoute(plane, OverheadRoute(plane));
                        break;
                    case CircuitStart.HighKey:
                        GiveRoute(plane, SfoRoute(plane));
                        break;
                    case CircuitStart.Circling:
                        GiveRoute(plane, CirclingRoute(plane));
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
        /// Where <see cref="AddInCircuit"/> would put an aircraft (the plane, NM), without adding it: for the plans waiting drawn
        /// on the instructor's map (the user, 6 Nov). Null with no runway.
        /// </summary>
        public (double X, double Y)? StartPoint(CircuitStart start, CircuitType type, CircuitSide? sideGiven, string? performanceClass)
        {
            lock (sync)
            {
                if (double.IsNaN(glidePathCourse)) return null;
                (_, CircuitSide side, _, double along, double lateral) = StartOf(start, type, sideGiven, performanceClass);
                return FromRunway(along, lateral, side);
            }
        }

        /// <summary>
        /// The start of <see cref="AddInCircuit"/> (one place, also for <see cref="StartPoint"/>): the circuit type as it
        /// starts, its side, its class at this aerodrome, the point (along the runway and across, NM).
        /// </summary>
        private (CircuitType Type, CircuitSide Side, PerformanceClass Kind, double Along, double Lateral) StartOf(CircuitStart start, CircuitType type,
            CircuitSide? sideGiven, string? performanceClass)
        {
            // The overhead pattern starts at the initial point, the flame out at the High Key; elsewhere they are the close
            // pattern (their second part).
            if (start == CircuitStart.HighKey) type = CircuitType.Sfo;
            else if (start == CircuitStart.Circling) type = CircuitType.Conventional;
            else if (type == CircuitType.Sfo || (type == CircuitType.Overhead && start != CircuitStart.InitialPoint)) type = CircuitType.Close;
            // (The user, 3 Nov: after a take-off only the conventional or the wide circuit, or a departure.)
            if (start == CircuitStart.Runway && type is not (CircuitType.Conventional or CircuitType.Wide)) type = CircuitType.Conventional;
            CircuitSide side = sideGiven ?? aerodrome?.SideOf(aerodromeRunway, type) ?? CircuitSide.Left;
            PerformanceClass classKind = (classes.FirstOrDefault(c => Same(c.Name, performanceClass ?? ""))
                ?? classes.FirstOrDefault(c => Same(c.Name, PerformanceClass.DefaultName)) ?? classes[0]);
            PerformanceClass kind = aerodrome?.Apply(classKind) ?? classKind.Copy();
            (double along, double lateral) = start switch
            {
                CircuitStart.LongFinal => (-longFinal, 0),
                CircuitStart.InitialPoint => (-initialPoint, 0),
                CircuitStart.Downwind => (DownwindStartAlong(type, kind), Width(type, kind, side)),
                CircuitStart.HighKey => (HighKeyAlong() - 1, 0),
                CircuitStart.Circling => (Math.Max(runwayLength, 0.3) + longFinal, 0),
                _ => (0.02, 0.0)
            };
            return (type, side, kind, along, lateral);
        }

        /// <summary>
        /// A departure with no circuit (the user, 5 Nov): the take-off, then on along the runway heading (<paramref name="radialTrue"/>
        /// NaN) or, above 400 ft, a turn (left or right: <paramref name="turnRight"/>; null: the shorter) to intercept the radial
        /// (degrees true, outbound) from the point (the plane, NM; null: the middle of the runway), then along it. Climbs to
        /// <paramref name="heightFt"/> above the runway (null: 3000 ft); gone <see cref="DepartureGoneNM"/> from the aerodrome.
        /// </summary>
        public string AddDeparture(double radialTrue, bool? turnRight, double? fromX, double? fromY, double? heightFt, string? callsign = null,
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null, double exitX = double.NaN, double exitY = double.NaN)
        {
            lock (sync)
            {
                string name = AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, null, EndAction.Departure, callsign, performanceClass, takenElsewhere);
                CircuitState circuit = Find(name)!.Circuit!;
                // (Out to a gate or a point, the user 6 Nov: straight to it after the upwind, gone there.)
                circuit.ExitX = exitX;
                circuit.ExitY = exitY;
                DepartureWay(circuit, double.IsFinite(exitX) ? double.NaN : radialTrue, turnRight, fromX, fromY, heightFt);
                return name;
            }
        }

        /// <summary>
        /// The plan of its circuits ends away (the user, 5 Nov): after its last final it leaves along the runway heading or
        /// the radial (as <see cref="AddDeparture"/>), or, with a gate (<paramref name="gateX"/>, <paramref name="gateY"/>),
        /// straight to it, gone there.
        /// </summary>
        public void LeaveAfterCircuits(string callsign, double radialTrue, bool? turnRight, double? fromX, double? fromY, double? heightFt,
            double gateX = double.NaN, double gateY = double.NaN) => Change(callsign, a =>
        {
            if (a.Circuit == null) return;
            a.Circuit.LeaveAfterLast = true;
            a.Circuit.ExitX = gateX;
            a.Circuit.ExitY = gateY;
            DepartureWay(a.Circuit, double.IsFinite(gateX) ? double.NaN : radialTrue, turnRight, fromX, fromY, heightFt);
        });

        /// <summary>The way out of a departure: its height, its radial (NaN: the runway heading) from its point, its turn.</summary>
        private void DepartureWay(CircuitState circuit, double radialTrue, bool? turnRight, double? fromX, double? fromY, double? heightFt)
        {
            circuit.DepartHeight = runwayElevation + (heightFt is double h && double.IsFinite(h) && h > 0 ? Math.Min(h, MaxAltitude) : DepartureHeight);
            // (To a gate or a point, the user 6 Nov: its turn kept, L or R; none given: the shorter, as before.)
            circuit.DepartTurn = turnRight switch { true => TurnDirection.Right, false => TurnDirection.Left, _ => TurnDirection.Shortest };
            if (!double.IsFinite(radialTrue)) return;
            circuit.DepartRadial = Normalize(radialTrue);
            (double mx, double my) = FromRunway(Math.Max(runwayLength, 0.3) / 2, 0, CircuitSide.Left);
            circuit.DepartFromX = fromX is double x && double.IsFinite(x) ? x : mx;
            circuit.DepartFromY = fromY is double y && double.IsFinite(y) ? y : my;
            circuit.DepartTurn = turnRight switch
            {
                true => TurnDirection.Right,
                false => TurnDirection.Left,
                _ => Wrap(circuit.DepartRadial - glidePathCourse) >= 0 ? TurnDirection.Right : TurnDirection.Left
            };
        }

        /// <summary>Away now (from a take-off, a touch and go, a go-around): up along the runway, then its way out (see <see cref="DepartureStep"/>).</summary>
        private void DepartNow(SimAircraft plane, CircuitState circuit, double along)
        {
            PerformanceClass kind = plane.Class;
            circuit.Departed = true;
            circuit.DepartPhase = 0;
            double upwind = Math.Max(UpwindAlong(circuit.Type, kind), along + 0.3);
            double away = double.IsFinite(circuit.DepartHeight) ? circuit.DepartHeight : runwayElevation + CircuitHeight(circuit.Type, kind);
            RoutePoint up = Point(upwind, 0, circuit.Side, away, kind.CircuitSpeedKt, "UPWIND");
            // (To its exit gate: from the end of the upwind straight to it.)
            GiveRoute(plane, double.IsFinite(circuit.ExitX)
                ? [up, new RoutePoint(circuit.ExitX, circuit.ExitY, away, kind.CircuitSpeedKt, "DEPARTURE")]
                : [up, Point(upwind + 20, 0, circuit.Side, away, kind.CircuitSpeedKt, "DEPARTURE")]);
            plane.SelectedVerticalRateFpm = Math.Max(MinVerticalRate, kind.MaxClimbFpm * 0.8);
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
            Instruct(callsign, Kind.Heading, a => FlyCircuitNow(a, type, side));

        /// <summary>
        /// Instruction: the next circuit (the user, 3 Nov: the plan of the aircraft): an aircraft flying a circuit finishes it
        /// and flies this one after its next touch and go, go-around or low pass (shown as the next circuit); the same
        /// circuit and side it flies: nothing changes. Not in a circuit (arriving, flown by the instructor): it joins it now.
        /// </summary>
        public void SetNextCircuit(string callsign, CircuitType type, CircuitSide side) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Circuit is { Suspended: false, Departed: false, Waiting: false, Transit: false } circuit && a.Contact is GroundContact.Airborne or GroundContact.Rolling)
                {
                    if (circuit.Type == type && circuit.Side == side && !circuit.Spiral) circuit.NextType = null;
                    else
                    {
                        circuit.NextType = type;
                        circuit.NextSide = side;
                    }
                    return;
                }
                FlyCircuitNow(a, type, side);
            });

        /// <summary>
        /// Instruction (on the downwind): extend the downwind: straight on along it, at circuit height, beyond the turn to the
        /// base, until <see cref="TurnBase"/>.
        /// </summary>
        public void ExtendDownwind(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                EndOrbit(a);
                if (a.Circuit is not { Suspended: false, Departed: false, Turning: false, Spiral: false } circuit || a.Contact != GroundContact.Airborne
                    || a.Route == null || a.RouteNext >= a.Route.Count || !IsDownwind(a.Route[a.RouteNext]) || double.IsNaN(glidePathCourse)) return;
                (double along, _) = RunwayFrame(a.X, a.Y, circuit.Side);
                GiveRoute(a, [Point(along - 15, Width(circuit.Type, a.Class, circuit.Side), circuit.Side, runwayElevation + CircuitHeight(circuit.Type, a.Class),
                    a.Class.CircuitSpeedKt, "DOWNWIND")]);
                circuit.Extended = true;
            });

        /// <summary>
        /// Instruction (on the downwind, extended or not): turn base now: the base from here (the jet circuits: the 180° turn to
        /// the final from here), then the final.
        /// </summary>
        public void TurnBase(string callsign) => Instruct(callsign, Kind.Heading, TurnBaseOf);

        /// <summary>
        /// Instruction DCT TO (the user, 5 Nov: "the WIDE is a way outside the circuits, back in at any moment"): at once, in
        /// flight, wherever it is in its circuit (the first one after the take-off too): onto the conventional downwind (DW),
        /// straight to the start of its base (BASE), to the initial point and the overhead (IP), to the High Key and the SFO (HK).
        /// </summary>
        public void DirectTo(string callsign, DirectPoint point, CircuitSide side) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
                EndOrbit(a);
                CircuitSide? flownSide = null;
                if (a.Circuit is CircuitState was)
                {
                    // (Its ordered circuit replaced; a departure back in the circuits: it lands at the end.)
                    was.NextType = null;
                    if (was.Departed || was.End == EndAction.Departure)
                    {
                        was.End = was.FinalEnd == EndAction.Departure ? EndAction.FullStop : was.FinalEnd;
                        was.LeaveAfterLast = false;
                        was.DepartPhase = 0;
                    }
                    flownSide = was.Side;
                    was.Turning = false;
                    was.Spiral = false;
                    was.StraightIn = false;
                    was.Extended = false;
                    was.Hold = false;
                }
                CircuitType type = point switch
                {
                    DirectPoint.InitialPoint => CircuitType.Overhead,
                    DirectPoint.HighKey => CircuitType.Sfo,
                    _ => CircuitType.Conventional
                };
                FlyCircuitNow(a, type, side, now: true);
                // To the IP from the side it flies, not across the runway (the user, 5 Nov).
                if (point == DirectPoint.InitialPoint && flownSide is CircuitSide flown && flown != side && a.Circuit?.Type == CircuitType.Overhead)
                    GiveRoute(a, ToInitialPoint(a, flown));
                if (point == DirectPoint.Base && a.Circuit != null && a.Route is { Count: > 0 })
                {
                    // Joining it: straight to the start of the base (see TurnBase); on the circuit side already past it: the
                    // base from here (not back to it).
                    // (Only on the downwind itself, flying it: elsewhere, the user 5 Nov, straight to the start of the base.)
                    (double along, double lateral) = RunwayFrame(a.X, a.Y, side);
                    bool onDownwind = Math.Abs(lateral - Width(CircuitType.Conventional, a.Class, side)) < 0.3
                        && Math.Abs(Wrap(a.HeadingTrue - (glidePathCourse + 180))) < 45;
                    bool past = onDownwind && along <= BaseAlong(CircuitType.Conventional, a.Class) + 0.3;
                    a.Route = [a.Route[a.RouteNext] with { Name = past ? "DOWNWIND" : "JOIN" }, .. a.Route.Skip(a.RouteNext + 1)];
                    a.RouteNext = 0;
                    TurnBaseOf(a);
                }
            });

        private void TurnBaseOf(SimAircraft a)
            {
                EndOrbit(a);
                if (a.Circuit is not { Suspended: false, Departed: false, Turning: false, Spiral: false, StraightIn: false } circuit || a.Contact != GroundContact.Airborne
                    || a.Route == null || a.RouteNext >= a.Route.Count || double.IsNaN(glidePathCourse)) return;
                RoutePoint now = a.Route[a.RouteNext];
                bool joining = now.Name == "JOIN" && circuit.Type is CircuitType.Conventional or CircuitType.Wide or CircuitType.Close;
                if (!joining && !IsDownwind(now)) return;
                (double along, double lateral) = RunwayFrame(a.X, a.Y, circuit.Side);
                circuit.Extended = false;
                PerformanceClass kind = a.Class;
                RoutePoint final = Point(0, 0, circuit.Side, double.NaN, kind.FinalSpeedKt, "FINAL");
                double downwindWidth = Width(circuit.Type, kind, circuit.Side);
                if (joining || (Math.Abs(lateral - downwindWidth) > 0.3 && along > BaseAlong(circuit.Type, kind) + 0.3))
                {
                    // Still joining the circuit (the user, 4 Nov, with a drawing): straight to the start of the base at its
                    // place, then the base and the final, as the gate entry BASE. The close pattern: onto the downwind line
                    // just before its 180° turn to the final, then that turn.
                    double baseAlong = BaseAlong(circuit.Type, kind);
                    double joinHeight = runwayElevation + CircuitHeight(circuit.Type, kind);
                    if (IsJetShape(circuit.Type))
                    {
                        GiveRoute(a,
                        [
                            Point(baseAlong + 0.8, downwindWidth, circuit.Side, joinHeight, kind.CircuitSpeedKt, "JOIN"),
                            Point(baseAlong, downwindWidth, circuit.Side, joinHeight, kind.CircuitSpeedKt, "DOWNWIND"),
                            Point(baseAlong, 0, circuit.Side, runwayElevation + kind.RollOutHeightFt, kind.FinalSpeedKt, "FINAL", turn180: true),
                            final
                        ]);
                    }
                    else
                    {
                        double cornerHeight = Math.Min(joinHeight, runwayElevation + (downwindWidth - baseAlong) * GlideFeetPerNM());
                        GiveRoute(a,
                        [
                            Point(baseAlong, downwindWidth, circuit.Side, cornerHeight, kind.CircuitSpeedKt, "JOIN"),
                            Point(baseAlong, 0, circuit.Side, runwayElevation + -baseAlong * GlideFeetPerNM(), kind.FinalSpeedKt, "BASE"),
                            final
                        ]);
                    }
                    return;
                }
                // (The user, 4 Nov: BASE before the threshold cut across to it. Now: on along the downwind — or onto it,
                // if still on the way — to 0.3 NM past the threshold, then a short base; past it: the base from here.)
                double turnAlong = Math.Min(along, ShortBaseAlong);
                double width = Width(circuit.Type, kind, circuit.Side);
                double circuitHeight = runwayElevation + CircuitHeight(circuit.Type, kind);
                List<RoutePoint> route = [];
                RoutePoint target = a.Route[a.RouteNext];
                if (AlongOf(target, circuit.Side) > turnAlong + 0.1) route.Add(target);
                if (along > turnAlong + 0.05)
                {
                    double downwindHeight = IsJetShape(circuit.Type) ? circuitHeight : Math.Min(circuitHeight, runwayElevation + (width - turnAlong) * GlideFeetPerNM());
                    route.Add(Point(turnAlong, width, circuit.Side, downwindHeight, kind.CircuitSpeedKt, "DOWNWIND"));
                }
                if (IsJetShape(circuit.Type))
                {
                    route.Add(Point(turnAlong, 0, circuit.Side, runwayElevation + kind.RollOutHeightFt, kind.FinalSpeedKt, "FINAL", turn180: true));
                }
                else
                {
                    double height = Math.Min(circuitHeight, runwayElevation + -turnAlong * GlideFeetPerNM());
                    route.Add(Point(turnAlong, 0, circuit.Side, height, kind.FinalSpeedKt, "BASE"));
                }
                route.Add(final);
                GiveRoute(a, route);
            }

        /// <summary>The circuit from where the aircraft is (see <see cref="SetCircuit"/>).</summary>
        private void FlyCircuitNow(SimAircraft a, CircuitType type, CircuitSide side, bool now = false)
        {
            {
                if (double.IsNaN(glidePathCourse) || a.Contact is GroundContact.Landed or GroundContact.OnGround) return;
                bool inCircuit = a.Circuit is { Suspended: false, Departed: false, Waiting: false, Transit: false };
                (double alongNow, double lateralNow) = RunwayFrame(a.X, a.Y, side);
                bool onFinal = a.Circuit is { OnFinal: true } && Math.Abs(lateralNow) < 0.3 && alongNow < runwayLength;
                // The same circuit on the other side, in flight: an S over the aerodrome to the new downwind (on the final: a go-around).
                // (Not for DCT TO: it goes there at once, on the new side.)
                if (inCircuit && !now && a.Contact == GroundContact.Airborne && type == a.Circuit!.Type && side != a.Circuit.Side && ChangeSide(a, side, onFinal)) return;
                if (inCircuit && !now && (a.Contact == GroundContact.Rolling || onFinal
                    || (type == CircuitType.Close && a.Circuit!.Type != CircuitType.Close && !a.Circuit.Spiral)))
                {
                    // Flown after the next go-around or touch and go.
                    a.Circuit!.NextType = type;
                    a.Circuit.NextSide = side;
                    return;
                }
                // (Another circuit now: a height given for the old one no more.)
                if (a.Circuit != null) a.Circuit.LapHeight = double.NaN;
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
                circuit.Waiting = false;
                circuit.Transit = false;
                if (type == CircuitType.Sfo)
                {
                    // Straight to the High Key: climbing on the circle over the field, on hold there (until "start SFO").
                    circuit.Hold = true;
                    StartSpiral(a, circuit);
                    return;
                }
                if (type == CircuitType.Overhead)
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
                    List<RoutePoint> route = [Point(entry, width, side, runwayElevation + CircuitHeight(type, a.Class), a.Class.CircuitSpeedKt, "DOWNWIND")];
                    route.AddRange(downwind.Where(p => AlongOf(p, side) < entry - 0.05 || p.Name != "DOWNWIND"));
                    GiveRoute(a, route);
                }
                else
                {
                    // Elsewhere: a 45° entry from the outside: to a point 1 NM outside the downwind and 1 NM before its start,
                    // then onto the downwind at its start (both turns anticipated): on it parallel, with the runway reciprocal.
                    double joinHeight = runwayElevation + CircuitHeight(type, a.Class);
                    GiveRoute(a,
                    [
                        Point(upwind + 1, width + 1, side, joinHeight, a.Class.CircuitSpeedKt, "JOIN"),
                        Point(upwind, width, side, joinHeight, a.Class.CircuitSpeedKt, "JOIN"),
                        .. downwind
                    ]);
                }
            }
        }

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
            if (circuit.Spiral)
            {
                // On the circle of the High Key: the circle on the other side.
                circuit.Side = side;
                StartSpiral(a, circuit);
                return true;
            }
            circuit.Side = side;
            circuit.NextType = null;
            circuit.Suspended = false;
            circuit.OnFinal = false;
            (double along, _) = RunwayFrame(a.X, a.Y, side);
            double length = Math.Max(runwayLength, 0.3);
            double height = runwayElevation + CircuitHeight(circuit.Type, a.Class);
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
                case "CIRCLING":
                    // Still circling: the circling to the downwind of the other side.
                    List<RoutePoint> circling = CirclingRoute(a);
                    GiveRoute(a, along < AlongOf(circling[0], side) + 0.3 ? circling.Skip(1).ToList() : circling);
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
                Point(start + 0.5, width, side, runwayElevation + CircuitHeight(circuit.Type, a.Class), a.Class.CircuitSpeedKt, "JOIN"),
                .. downwind.Where(p => p.Name != "DOWNWIND" || AlongOf(p, side) < start - 0.3)
            ];
        }

        /// <summary>What the aircraft does at the end of each final, at once (it can be changed at any time).</summary>
        public void SetEndAction(string callsign, EndAction action) => Change(callsign, a =>
        {
            if (a.Circuit == null) return;
            // (The user, 4 Nov, A: with a plan of circuits, the instructor's end of the final for this circuit only; then
            // the plan again.)
            a.Circuit.End = action;
        });

        /// <summary>
        /// The plan of its circuits (the user, 4 Nov): this many circuits (0: on and on, touch and go), each ending with a
        /// touch and go but the last one, which ends with <paramref name="last"/>. At once.
        /// </summary>
        public void SetLaps(string callsign, int laps, EndAction last, EndAction onFinal = EndAction.TouchAndGo) => Change(callsign, a =>
        {
            if (a.Circuit == null) return;
            // (0: on and on: −1, a plan of touch and go; 0 is no plan at all.)
            a.Circuit.LapsLeft = laps <= 0 ? -1 : laps;
            a.Circuit.FinalEnd = last;
            a.Circuit.LapEnd = onFinal is EndAction.GoAround or EndAction.LowPass ? onFinal : EndAction.TouchAndGo;
            a.Circuit.End = laps == 1 ? last : a.Circuit.LapEnd;
        });

        /// <summary>A circuit of the plan flown (a touch and go, a go-around): one less; the last one ends as planned.</summary>
        private static void LapDone(CircuitState circuit)
        {
            if (circuit.LapsLeft == 0) return;
            if (circuit.LapsLeft > 1) circuit.LapsLeft--;
            // The plan again (an end given for one circuit only: the plan's after it).
            circuit.End = circuit.LapsLeft == 1 ? circuit.FinalEnd : circuit.LapEnd;
        }

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
                // (The close pattern: on its upwind, the user 5 Nov.)
                if (a.Circuit != null && a.Route != null && a.RouteNext < a.Route.Count
                    && (a.Route[a.RouteNext].Name == "INITIAL" || (a.Circuit.Type == CircuitType.Close && a.Route[a.RouteNext].Name is "UPWIND" or "GO AROUND"))) a.Circuit.BreakNow = true;
            });

        /// <summary>
        /// Instruction: one orbit (a 360° turn) where it is, then on along its circuit from there (on the downwind: to make room
        /// ahead of it). <paramref name="right"/>: the side of the turn; null: away from the runway (the outside of the
        /// circuit). Not on the final, not during another turn.
        /// </summary>
        public void Orbit(string callsign, bool? right = null, bool forever = false) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                CircuitState? circuit = a.Circuit;
                if (circuit is { Orbit: > 0 } && forever)
                {
                    // Already orbiting: on until "continue".
                    circuit.OrbitForever = true;
                    return;
                }
                if (circuit == null || circuit.Suspended || circuit.Departed || circuit.Turning || circuit.Spiral || circuit.OnFinal || a.Contact != GroundContact.Airborne
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
                circuit.OrbitForever = forever;
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

        /// <summary>Instruction "continue" (after orbits): the orbit ends now, on along its circuit from here.</summary>
        public void ContinueAfterOrbit(string callsign) => Instruct(callsign, Kind.Heading, EndOrbit);

        /// <summary>An orbit going on ends now: on along the leg it was flying, from here.</summary>
        private static void EndOrbit(SimAircraft a)
        {
            if (a.Circuit is not { Orbit: > 0 } circuit) return;
            circuit.Orbit = 0;
            circuit.OrbitForever = false;
            circuit.Turning = false;
            a.LegStartX = a.X;
            a.LegStartY = a.Y;
            a.RouteApproached = false;
            a.TurnDirection = TurnDirection.Shortest;
            if (a.Route != null && a.RouteNext < a.Route.Count) a.Navigation = NavigationMode.Route;
            a.SelectedTurnRateDps = MaxTurnRate;
        }

        /// <summary>
        /// Instruction BREAKUP (the user, 3 Nov): out of the circuits. On the downwind: a turn away from the runway to 90° off
        /// it; on the base: on turning the same way past the final heading, to the reciprocal of the base. Then the
        /// instructor flies it (MANUAL, at the altitude it has); REJOIN brings it back.
        /// </summary>
        public void Breakup(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Circuit is not { Suspended: false, Departed: false } circuit || a.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
                string leg = a.Leg;
                bool left = circuit.Side == CircuitSide.Left;
                double heading;
                TurnDirection direction;
                if (leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS")
                {
                    // Downwind: course + 180; away from the runway: to the circuit side.
                    heading = Normalize(glidePathCourse + 180 + (left ? 90 : -90));
                    direction = left ? TurnDirection.Right : TurnDirection.Left;
                }
                else if (leg == "BASE")
                {
                    heading = Normalize(glidePathCourse + (left ? -90 : 90));
                    direction = left ? TurnDirection.Left : TurnDirection.Right;
                }
                else return;
                EndOrbit(a);
                circuit.Suspended = true;
                circuit.OnFinal = false;
                circuit.Extended = false;
                a.Navigation = NavigationMode.Manual;
                a.CircuitBankDegrees = 0;
                a.SelectedTurnRateDps = a.TurnRateMode == TurnRateMode.Half ? HalfTurnRate : StandardTurnRate;
                a.TargetHeading = heading;
                a.TurnDirection = direction;
                a.VerticalMode = VerticalMode.Altitude;
                a.TargetAltitudeFt = a.AltitudeFt;
                a.AltitudeGiven = true;
            });

        /// <summary>
        /// Adds an aircraft at an entry gate (the plane, NM), at the arrival height and speed of its class, heading to the
        /// aerodrome: it joins its circuit by itself (<paramref name="entry"/>: 45° to the downwind, straight to the base, to
        /// the initial point, to the High Key, straight in). The overhead joins at the IP and the SFO at the High Key
        /// whatever the entry. Returns its callsign.
        /// </summary>
        public string AddAtGate(double x, double y, CircuitEntry entry, CircuitType type, CircuitSide? sideGiven, EndAction end, string? callsign = null,
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null, double? arrivalHeightFt = null)
        {
            lock (sync)
            {
                if (double.IsNaN(glidePathCourse)) throw new InvalidOperationException("No runway: a circuit needs the runway in use.");
                if (type == CircuitType.Overhead) entry = CircuitEntry.InitialPoint;
                else if (type == CircuitType.Sfo) entry = CircuitEntry.HighKey;
                else if (entry == CircuitEntry.InitialPoint) type = CircuitType.Overhead;
                else if (entry == CircuitEntry.HighKey) type = CircuitType.Sfo;
                CircuitSide side = sideGiven ?? aerodrome?.SideOf(aerodromeRunway, type) ?? CircuitSide.Left;
                PerformanceClass classKind = (classes.FirstOrDefault(c => Same(c.Name, performanceClass ?? ""))
                    ?? classes.FirstOrDefault(c => Same(c.Name, PerformanceClass.DefaultName)) ?? classes[0]);
                PerformanceClass kind = aerodrome?.Apply(classKind) ?? classKind.Copy();
                // Heading to the middle of the runway.
                (double mx, double my) = FromRunway(Math.Max(runwayLength, 0.3) / 2, 0, side);
                double heading = Normalize(Math.Atan2(mx - x, my - y) * 180 / Math.PI);
                // (A class not normalized, the defaults of the engine: 500 ft above its circuit, its circuit speed.)
                // (Given for this aircraft: the height above the threshold it arrives at, the user 3 Nov.)
                double arrival = arrivalHeightFt is double given && double.IsFinite(given) && given > 0 ? Math.Min(given, MaxAltitude)
                    : kind.ArrivalHeightFt > 0 ? kind.ArrivalHeightFt : CircuitHeight(CircuitType.Conventional, kind) + 500;
                double arrivalSpeed = kind.ArrivalSpeedKt > 0 ? kind.ArrivalSpeedKt : kind.CircuitSpeedKt;
                string name = Add(x, y, runwayElevation + arrival, heading, arrivalSpeed, callsign, kind.Name, takenElsewhere);
                SimAircraft plane = Find(name)!;
                plane.Class = kind;
                plane.Circuit = new CircuitState { Type = type, Side = side, End = end, Suspended = true, Hold = type == CircuitType.Sfo };
                Join(plane, entry, type, side);
                return name;
            }
        }

        /// <summary>
        /// Adds an aircraft crossing the zone: from an entry gate to an exit gate (the plane, NM), over the field or straight,
        /// at a height above the threshold (null: 500 ft above the highest circuit of its class), never landing: gone at the
        /// exit gate. It can be given orbits, a heading (then MANUAL) and taken back onto its way (<see cref="ResumeTransit"/>).
        /// </summary>
        public string AddTransit(double fromX, double fromY, double toX, double toY, bool overhead, double? heightFt = null, string? callsign = null,
            string? performanceClass = null, Func<string, bool>? takenElsewhere = null)
        {
            lock (sync)
            {
                PerformanceClass classKind = (classes.FirstOrDefault(c => Same(c.Name, performanceClass ?? ""))
                    ?? classes.FirstOrDefault(c => Same(c.Name, PerformanceClass.DefaultName)) ?? classes[0]);
                PerformanceClass kind = aerodrome?.Apply(classKind) ?? classKind.Copy();
                double highest = new[] { CircuitType.Conventional, CircuitType.Wide, CircuitType.Close }.Max(t => CircuitHeight(t, kind));
                double height = runwayElevation + (heightFt is double given && double.IsFinite(given) && given > 0 ? Math.Min(given, MaxAltitude) : highest + 500);
                double speed = kind.ArrivalSpeedKt > 0 ? kind.ArrivalSpeedKt : kind.CircuitSpeedKt;
                (double mx, double my) = double.IsNaN(glidePathCourse) ? (0.0, 0.0) : FromRunway(Math.Max(runwayLength, 0.3) / 2, 0, CircuitSide.Left);
                (double fx, double fy) = overhead ? (mx, my) : (toX, toY);
                double heading = Normalize(Math.Atan2(fx - fromX, fy - fromY) * 180 / Math.PI);
                string name = Add(fromX, fromY, height, heading, speed, callsign, kind.Name, takenElsewhere);
                SimAircraft plane = Find(name)!;
                plane.Class = kind;
                plane.Circuit = new CircuitState { Transit = true, TransitX = toX, TransitY = toY, TransitHeight = height, TransitSpeed = speed };
                List<RoutePoint> route = [];
                if (overhead) route.Add(new RoutePoint(mx, my, height, speed, "TRANSIT"));
                route.Add(new RoutePoint(toX, toY, height, speed, "TRANSIT"));
                GiveRoute(plane, route);
                return name;
            }
        }

        /// <summary>
        /// Instruction (the label's AUTO, the user 3 Nov): the altitude (or the speed) given by the instructor no more: those
        /// of its route again (its circuit, its crossing), from now.
        /// </summary>
        public void ResumeRouteAltitude(string callsign) =>
            Instruct(callsign, Kind.Vertical, a =>
            {
                // (Circling to the High Key: the class's High Key again.)
                if (a.Circuit is { Spiral: true } circling)
                {
                    a.AltitudeGiven = false;
                    circling.LapHeight = double.NaN;
                    return;
                }
                // (Not flown by the instructor: its old route would give an old height.)
                if (a.Route == null || !a.AltitudeGiven || a.Navigation != NavigationMode.Route || a.Circuit is { Suspended: true }) return;
                a.AltitudeGiven = false;
                if (a.Circuit != null) a.Circuit.LapHeight = double.NaN;
                // (The vertical rate of the leg worked out again; not in a 180° turn, which would start again.)
                if (a.Circuit is { Turning: false } circuit) circuit.LegIndex = -1;
                StartLeg(a);
            });

        public void ResumeRouteSpeed(string callsign) =>
            Instruct(callsign, Kind.Speed, a =>
            {
                if (a.Route == null || !a.SpeedGiven || a.Navigation != NavigationMode.Route || a.Circuit is { Suspended: true }) return;
                a.SpeedGiven = false;
                StartLeg(a);
            });

        /// <summary>Instruction to an aircraft crossing the zone flown by the instructor: back onto its way, straight to its exit gate.</summary>
        public void ResumeTransit(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Circuit is not { Transit: true } circuit || a.Contact != GroundContact.Airborne) return;
                circuit.Suspended = false;
                GiveRoute(a, [new RoutePoint(circuit.TransitX, circuit.TransitY, circuit.TransitHeight, circuit.TransitSpeed, "TRANSIT")]);
            });

        /// <summary>
        /// Instruction (to an aircraft out of the circuits): join this circuit this way (see <see cref="AddAtGate"/>).
        /// </summary>
        public void JoinCircuit(string callsign, CircuitEntry entry, CircuitType type, CircuitSide side) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
                a.Circuit ??= new CircuitState { End = EndAction.FullStop };
                a.Circuit.Suspended = true;
                Join(a, entry, type, side);
            });

        /// <summary>The way into a circuit from outside it: to the downwind, the base, the IP, the High Key or the final.</summary>
        private void Join(SimAircraft plane, CircuitEntry entry, CircuitType type, CircuitSide side)
        {
            CircuitState circuit = plane.Circuit!;
            if (entry is CircuitEntry.Downwind or CircuitEntry.InitialPoint or CircuitEntry.HighKey or CircuitEntry.Circling)
            {
                FlyCircuitNow(plane, entry == CircuitEntry.InitialPoint ? CircuitType.Overhead : entry == CircuitEntry.HighKey ? CircuitType.Sfo : type, side);
                return;
            }
            circuit.Type = type;
            circuit.Side = side;
            circuit.Suspended = false;
            circuit.Waiting = false;
            circuit.Departed = false;
            circuit.NextType = null;
            PerformanceClass kind = plane.Class;
            double glide = GlideFeetPerNM();
            RoutePoint final = Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL");
            if (entry == CircuitEntry.Final)
            {
                // Straight in: onto the extended centreline 4 NM out, on the glide path.
                GiveRoute(plane, [Point(-4, 0, side, runwayElevation + 4 * glide, kind.ApproachSpeedKt, "JOIN"), final]);
                circuit.StraightIn = true;
                return;
            }
            // Straight to the start of the base (abeam its turn, on the downwind line), then the base and the final.
            double width = Width(type, kind, side);
            double baseAlong = IsJetShape(type) ? -kind.RollOutNM : BaseAlong(type, kind);
            double height = Math.Min(runwayElevation + CircuitHeight(type, kind), runwayElevation + (width - baseAlong) * glide);
            GiveRoute(plane,
            [
                Point(baseAlong, width, side, height, kind.CircuitSpeedKt, "JOIN"),
                Point(baseAlong, 0, side, runwayElevation + -baseAlong * glide, kind.FinalSpeedKt, "BASE"),
                final
            ]);
        }

        /// <summary>
        /// A helicopter's landing point (the plane, NM; NaN: none, the runway again): its final goes there, slowing down to
        /// a hover over it and down to the ground (landed: removed a few seconds later, as on the runway). With a pad the
        /// end of the final is always a full stop.
        /// </summary>
        public void SetHelipad(string callsign, double x, double y) => Change(callsign, a =>
        {
            // (Helicopters only, the user 5 Nov.)
            if (a.Circuit == null || (!a.Class.CanHover && double.IsFinite(x))) return;
            a.Circuit.HasPad = double.IsFinite(x) && double.IsFinite(y);
            a.Circuit.WantsPad = false;
            a.Circuit.PadX = a.Circuit.HasPad ? x : 0;
            a.Circuit.PadY = a.Circuit.HasPad ? y : 0;
            ApplyPad(a);
        });

        /// <summary>A helicopter's plan to land on a pad not given yet (shown HPAD?; given later with HPAD; none: the runway).</summary>
        /// <summary>
        /// A helicopter departing from a helipad (the user, 6 Nov): the aircraft just added on the runway (a take-off) starts
        /// still on the pad (the plane, NM), up to <see cref="PadHoverFt"/> in a hover of <see cref="PadHoverSeconds"/> at most,
        /// then it goes as from its take-off (the runway heading, its circuit or its way out). Instructions wait for the end of
        /// the hover. Not a helicopter, or not on the runway: nothing.
        /// </summary>
        public void TakeOffFromPad(string callsign, double x, double y) => Change(callsign, a =>
        {
            if (a.Circuit == null || !a.Class.CanHover || a.Contact != GroundContact.Rolling || !double.IsFinite(x) || !double.IsFinite(y)) return;
            a.X = x;
            a.Y = y;
            a.SpeedKt = a.TargetSpeedKt = a.GroundSpeedKt = 0;
            a.AltitudeFt = runwayElevation;
            a.Circuit.PadHover = 0;
        });

        public const double PadHoverFt = 15, PadHoverSeconds = 4;
        /// <summary>The climb to the hover height (s): the rest of <see cref="PadHoverSeconds"/> held there.</summary>
        private const double PadClimbSeconds = 3.5;

        /// <summary>The hover on the pad (see <see cref="TakeOffFromPad"/>): up smoothly, still; at its end away as from a take-off.</summary>
        private void PadHoverStep(SimAircraft plane, CircuitState circuit)
        {
            circuit.PadHover += Step;
            double f = Math.Clamp(circuit.PadHover / PadClimbSeconds, 0, 1);
            plane.AltitudeFt = runwayElevation + PadHoverFt * f * f * (3 - 2 * f);
            plane.VerticalSpeedFpm = f < 1 ? PadHoverFt * 6 * f * (1 - f) / PadClimbSeconds * 60 : 0;
            plane.SpeedKt = plane.GroundSpeedKt = 0;
            plane.TurnRateDps = 0;
            plane.DriftDegrees = 0;
            if (circuit.PadHover < PadHoverSeconds - 1e-9) return;
            circuit.PadHover = double.NaN;
            plane.Contact = GroundContact.Airborne;
            plane.AltitudeFt = runwayElevation + PadHoverFt;
            plane.VerticalSpeedFpm = 0;
            (double along, _) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            LiftOff(plane, circuit, along);
        }

        public void WantPad(string callsign) => Change(callsign, a =>
        {
            if (a.Circuit != null && a.Class.CanHover && !a.Circuit.HasPad) a.Circuit.WantsPad = true;
        });

        /// <summary>
        /// Instruction DCT TO HPAD (the user, 5 Nov): a helicopter in flight goes straight to the pad (the plane, NM) from
        /// wherever it is, down and slower on the way, a hover over it, then down to it (a full stop there).
        /// </summary>
        public void DirectToPad(string callsign, double x, double y) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (!a.Class.CanHover || a.Contact != GroundContact.Airborne || !double.IsFinite(x) || !double.IsFinite(y)) return;
                EndOrbit(a);
                if (a.Circuit == null && aerodrome != null) a.Class = aerodrome.Apply(a.Class);
                CircuitState circuit = a.Circuit ??= new CircuitState { End = EndAction.FullStop };
                circuit.NextType = null;
                circuit.Turning = circuit.Spiral = circuit.StraightIn = circuit.Extended = circuit.Hold = false;
                circuit.Suspended = circuit.Departed = circuit.Waiting = circuit.Transit = false;
                circuit.LeaveAfterLast = false;
                circuit.LapsLeft = 0;
                circuit.End = circuit.FinalEnd = EndAction.FullStop;
                circuit.HasPad = true;
                circuit.WantsPad = false;
                circuit.PadX = x;
                circuit.PadY = y;
                GiveRoute(a, [new RoutePoint(x, y, double.NaN, a.Class.ApproachSpeedKt, "FINAL")]);
            });

        /// <summary>
        /// Instruction (SFO): hold at the High Key: circling over the field on the circle of the High Key, at its height, until
        /// released (false: "start SFO": at the next pass over the High Key it starts). Given before the High Key: it goes to
        /// that circle at once.
        /// </summary>
        public void HoldAtHighKey(string callsign, bool hold) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                if (a.Circuit is not { Type: CircuitType.Sfo } circuit) return;
                // (Before the High Key: it goes onto the circle once its first turn is to the side of the SFO, see CircuitStep.)
                circuit.Hold = hold;
            });

        /// <summary>Instruction: go around now: climbs to circuit height along the runway, then flies its circuit again.</summary>
        public void GoAround(string callsign) =>
            Instruct(callsign, Kind.Heading, a =>
            {
                // Only on the final (the turn to it of the jet circuits included): the user, 3 Nov.
                if (a.Circuit is not { Suspended: false, Departed: false } circuit || a.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)
                    || a.Leg != "FINAL") return;
                // It stops descending, goes on to the threshold along the final, then keeps the runway heading until a new order.
                (double along, _) = RunwayFrame(a.X, a.Y, circuit.Side);
                double length = Math.Max(runwayLength, 0.3);
                List<RoutePoint> route = [];
                if (along < 0) route.Add(Point(0, 0, circuit.Side, a.AltitudeFt, double.NaN, "GO AROUND"));
                route.Add(Point(Math.Max(length, along + 0.5), 0, circuit.Side, a.AltitudeFt, double.NaN, "GO AROUND"));
                GiveRoute(a, route);
                circuit.Turning = false;
                circuit.OnFinal = false;
                circuit.Waiting = true;
                a.VerticalMode = VerticalMode.Altitude;
                a.TargetAltitudeFt = a.AltitudeFt;
                a.SelectedTurnRateDps = MaxTurnRate;
            });

        // ----- The steps ---------------------------------------------------------------------------------------------

        /// <summary>The circuit of an aircraft in the air, every step before the route: legs, final, end of the final.</summary>
        private void CircuitStep(SimAircraft plane)
        {
            CircuitState? circuit = plane.Circuit;
            if (circuit == null || plane.Contact != GroundContact.Airborne || double.IsNaN(glidePathCourse)) return;
            if (Delayed(plane, circuit)) return;
            if (circuit.Departed)
            {
                DepartureStep(plane, circuit);
                return;
            }
            if (circuit.Waiting) return;
            bool routeOver = plane.Route != null && plane.RouteNext >= plane.Route.Count;
            if (circuit.Turning && !circuit.Suspended)
            {
                TurnStep(plane, circuit);
                return;
            }
            if (circuit.Spiral && !circuit.Suspended)
            {
                SpiralStep(plane, circuit);
                return;
            }
            if (circuit.Transit)
            {
                // Crossing: its way (orbits above); a heading of the instructor: it waits; at the exit gate: gone.
                if (!circuit.Suspended && !routeOver && (plane.Route == null || plane.Navigation != NavigationMode.Route)) circuit.Suspended = true;
                if (!circuit.Suspended && routeOver) circuit.TransitDone = true;
                return;
            }
            if (circuit.Type == CircuitType.Sfo && !circuit.Suspended && plane.Route != null && plane.RouteNext < plane.Route.Count)
            {
                // To the High Key: after a go-around (or a take-off) above 400 ft; on hold before the High Key.
                string name = plane.Route[plane.RouteNext].Name;
                // (On the runway track: straight on to the High Key, where the track is tangent to its circle, and onto it there;
                // off the track: at once.)
                (double alongHere, double lateralHere) = RunwayFrame(plane.X, plane.Y, circuit.Side);
                bool sideward = Math.Abs(lateralHere) > 0.3 || alongHere >= HighKeyAlong() - 0.1;
                if ((name is "UPWIND" or "GO AROUND" && plane.AltitudeFt - runwayElevation > WaveoffTurnHeight && sideward) || (name == "HIGH KEY" && sideward && (circuit.Hold
                    // (Its High Key given and not there yet, the user 6 Nov: on the circle up or down to it first.)
                    || (double.IsFinite(circuit.LapHeight) && Math.Abs(plane.AltitudeFt - circuit.LapHeight) > 150))))
                {
                    StartSpiral(plane, circuit);
                    return;
                }
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
                    double breakAt = BreakAlongOf(circuit);
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
                    // Waveoff, take-off: above 400 ft over the runway track it turns to the downwind, climbing, at its break
                    // point (the user, 5 Nov: the close pattern is an overhead without the initial): past it already, at once;
                    // on standby, on along the runway track until "break now".
                    double breakAt = BreakAlongOf(circuit);
                    bool breaks = circuit.BreakNow || alongNow >= breakAt;
                    if (!breaks && Math.Abs(lateralNow) < 0.3 && AlongOf(target, circuit.Side) < alongNow + 1)
                    {
                        // Its break still ahead: the point it flies to moves on ahead of it, along the runway track.
                        (double tx, double ty) = FromRunway(alongNow + 1.5, 0, circuit.Side);
                        List<RoutePoint> moved = plane.Route.ToList();
                        moved[plane.RouteNext] = target with { X = tx, Y = ty };
                        plane.Route = moved;
                    }
                    if (breaks && plane.AltitudeFt - runwayElevation > WaveoffTurnHeight && Math.Abs(lateralNow) < 0.3)
                    {
                        if (circuit.TurnWaitSeconds > 0)
                        {
                            // (A formation going around: one after the other.)
                            Delay(plane, circuit.TurnWaitSeconds);
                            circuit.TurnWaitSeconds = 0;
                            return;
                        }
                        circuit.BreakNow = false;
                        PassPoint(plane, fromHere: true);
                    }
                }
                else if (!next.Turn180 && (target.Name is "JOIN" or "BASE" or "CIRCLING" || (circuit.Type is CircuitType.Conventional or CircuitType.Wide && target.Name is "CROSSWIND" or "DOWNWIND")))
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
                // The height of this circuit given (the user, 6 Nov): from the base, the turn to the final, the Low Key: down
                // as usual, by itself.
                if (double.IsFinite(circuit.LapHeight) && (point.Name is "BASE" or "FINAL" || (point.Name == "LOW KEY" && point.Turn180)))
                {
                    circuit.LapHeight = double.NaN;
                    plane.AltitudeGiven = false;
                    if (double.IsFinite(point.AltitudeFt))
                    {
                        plane.VerticalMode = VerticalMode.Altitude;
                        plane.TargetAltitudeFt = Math.Clamp(LowPassFloor(plane, point.AltitudeFt), runwayElevation, MaxAltitude);
                    }
                }
                // The close pattern: its downwind and its final are reached by a 180° turn, not flown to as points.
                if (IsJetShape(circuit.Type) && point.Turn180)
                {
                    StartTurn(plane, circuit, point);
                    return;
                }
                if (double.IsFinite(point.AltitudeFt))
                {
                    double climb = LowPassFloor(plane, point.AltitudeFt) - plane.AltitudeFt;
                    double minutes = Math.Sqrt((point.X - plane.X) * (point.X - plane.X) + (point.Y - plane.Y) * (point.Y - plane.Y)) / Math.Max(30, plane.GroundSpeedKt) * 60;
                    plane.SelectedVerticalRateFpm = climb > 0 ? Math.Max(MinVerticalRate, plane.Class.MaxClimbFpm * 0.8)
                        : Math.Clamp(-climb / Math.Max(0.1, minutes) * (circuit.Type == CircuitType.Sfo ? 1.0 : 1.2), Math.Min(300, DescentLimit(plane)), DescentLimit(plane));
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
            // A formation on the short final: the members chosen land, the others go around (the user, 4 Nov).
            // (Not on a low approach or a low pass: nobody lands, the formation stays one.)
            if (plane.Wingmen.Count > 0 && circuit.End is not (EndAction.GoAround or EndAction.LowPass) && height <= GoAroundHeight && along >= -GoAroundShortFinal && LandingSplit(plane, circuit)) return;
            // The end of the final: low pass over the threshold, go-around at its height, past the runway without landing.
            // (The low pass, the user 5 Nov: at 400 ft all along the runway, then its next circuit from its far end.)
            if ((circuit.End == EndAction.LowPass && along >= Math.Max(runwayLength, 0.3)) || (circuit.End == EndAction.GoAround && height <= GoAroundHeight && along >= -GoAroundShortFinal)
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
                : floor > 0 ? Math.Clamp((floor - height) * 2, -300, 300) : -400;
            plane.VerticalMode = VerticalMode.Rate;
            plane.CommandedVerticalSpeedFpm = Math.Clamp(wanted, -DescentLimit(plane), 300);
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
        /// SFO: onto the circle of the High Key, over the field: it passes over the High Key (tangent to the runway track
        /// there) and lies on the side of the SFO, half the width of the close pattern as radius: the same circle as the
        /// turn from the High Key to the Low Key. Fixed from the start; the aircraft is steered onto it (see SpiralStep).
        /// </summary>
        private void StartSpiral(SimAircraft plane, CircuitState circuit)
        {
            double radius = SpiralRadius(plane, circuit);
            (circuit.CentreX, circuit.CentreY) = FromRunway(HighKeyAlong(), radius, circuit.Side);
            circuit.Radius = radius;
            circuit.Spiral = true;
            circuit.Turning = false;
            circuit.Orbit = 0;
            circuit.OnFinal = false;
            circuit.LegIndex = -1;
            plane.Route = null;
            plane.Navigation = NavigationMode.Manual;
            plane.AltitudeGiven = false;
            plane.SpeedGiven = false;
            plane.TurnDirection = TurnDirection.Shortest;
            plane.TargetHeading = plane.HeadingTrue;
            circuit.TurnHeading = plane.TargetHeading;
        }

        private double SpiralRadius(SimAircraft plane, CircuitState circuit) => Math.Max(0.3, Width(CircuitType.Sfo, plane.Class, circuit.Side) / 2);

        /// <summary>
        /// The course (degrees true) that brings the aircraft along the circle of the High Key (the turns to the side of the
        /// SFO): 90° from the line from the centre, up to 70° more towards the circle (outside it: inwards; inside:
        /// outwards); and the distance from the centre (NM).
        /// </summary>
        private (double Course, double Distance) SpiralCourse(SimAircraft plane, CircuitState circuit)
        {
            double s = circuit.Side == CircuitSide.Right ? 1 : -1;
            double radius = SpiralRadius(plane, circuit);
            (double cx, double cy) = FromRunway(HighKeyAlong(), radius, circuit.Side);
            double dx = plane.X - cx, dy = plane.Y - cy;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double radial = Math.Atan2(dx, dy) * 180 / Math.PI;
            // (Gently near the circle, so it joins it without cutting inside.)
            double towards = Math.Clamp(Math.Atan2(distance - radius, 0.6 * radius) * 180 / Math.PI, -70, 70);
            return (Normalize(radial + s * (90 + towards)), distance);
        }

        /// <summary>
        /// On the circle of the SFO, climbing (or descending) to the High Key: the heading along the circle (the turns to the
        /// side of the SFO), turned towards it the more the farther the aircraft is from it (from far away nearly straight to
        /// it, joining it tangent: never a loop). On it, over the High Key along the runway, at its height and not on hold,
        /// the SFO starts (the turn to the Low Key, on the same circle).
        /// </summary>
        private void SpiralStep(SimAircraft plane, CircuitState circuit)
        {
            if (plane.StickX != 0 || Math.Abs(Wrap(plane.TargetHeading - circuit.TurnHeading)) > 0.01)
            {
                // The instructor gave a heading (or took the stick): the circuit waits.
                circuit.Spiral = false;
                circuit.Suspended = true;
                plane.CircuitBankDegrees = 0;
                plane.SelectedTurnRateDps = plane.TurnRateMode == TurnRateMode.Half ? HalfTurnRate : StandardTurnRate;
                return;
            }
            PerformanceClass kind = plane.Class;
            double s = circuit.Side == CircuitSide.Right ? 1 : -1;
            double radius = SpiralRadius(plane, circuit);
            (circuit.CentreX, circuit.CentreY) = FromRunway(HighKeyAlong(), radius, circuit.Side);
            circuit.Radius = radius;
            (double course, double distance) = SpiralCourse(plane, circuit);
            plane.CircuitBankDegrees = kind.CircuitBankDegrees;
            plane.SelectedTurnRateDps = MaxTurnRate;
            plane.TurnDirection = TurnDirection.Shortest;
            plane.TargetHeading = HeadingForCourse(course, plane.SpeedKt);
            circuit.TurnHeading = plane.TargetHeading;
            double highKey = double.IsFinite(circuit.LapHeight) ? circuit.LapHeight : runwayElevation + kind.HighKeyFt;
            if (!plane.AltitudeGiven)
            {
                plane.VerticalMode = VerticalMode.Altitude;
                plane.TargetAltitudeFt = highKey;
                plane.SelectedVerticalRateFpm = plane.AltitudeFt < highKey ? Math.Max(MinVerticalRate, kind.MaxClimbFpm * 0.8) : Math.Max(MinVerticalRate, Math.Min(1500, kind.MaxDescentFpm));
            }
            if (!plane.SpeedGiven) plane.TargetSpeedKt = Math.Clamp(kind.BestGlideKt, kind.MinSpeedKt, kind.MaxSpeedKt);
            // On the circle, over the High Key, along the runway (the top of the circle), at its height: the SFO.
            (double along, double lateral) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            // (The user, 6 Nov: START SFO given anywhere on the circle, at its height: off at once, the shortest way, never a
            // whole circle more.)
            if (!circuit.Hold && Math.Abs(plane.AltitudeFt - highKey) < 150)
            {
                double hk = HighKeyAlong();
                List<RoutePoint> sfo = SfoRoute(plane);
                if (Math.Abs(lateral) < 0.15 && Math.Abs(along - hk) < 0.3 && Math.Abs(Wrap(plane.HeadingTrue - glidePathCourse)) < 15)
                {
                    // Over the High Key along the runway: the flame out from here.
                    circuit.Spiral = false;
                    GiveRoute(plane, sfo.Skip(1).ToList());
                    return;
                }
                // Elsewhere: the shortest way that ends over the High Key on the runway track (a turn, straight, a turn), so the
                // flame out starts lined up.
                // (Its turns a little wider than it turns at its bank: it can follow them.)
                double rate = Math.Max(MinTurnRate, Math.Min(MaxTurnRate, RateForBank(BankLimit(plane), plane.SpeedKt)));
                double turnRadius = Math.Max(30, plane.GroundSpeedKt) / 3600 / (rate * Math.PI / 180);
                // (And what it flies while it rolls into a turn.)
                double rollIn = Math.Max(30, plane.GroundSpeedKt) / 3600 * (rate / TurnRateChange);
                List<RoutePoint> way = ShortestToHighKey(plane, Math.Max(radius, turnRadius * 1.5 + rollIn * 1.5), highKey, out double wayLength);
                // On its circle already: on round it to the High Key when that is shorter (never more than the rest of the circle).
                int turn = circuit.Side == CircuitSide.Right ? 1 : -1;
                double left = ((turn * (glidePathCourse - plane.HeadingTrue)) % 360 + 360) % 360;
                if (Math.Abs(distance - radius) < 0.25 * radius && radius * left * Math.PI / 180 <= wayLength) return;
                circuit.Spiral = false;
                sfo.InsertRange(0, way);
                GiveRoute(plane, sfo);
            }
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
            // A formation splits at the break, at the 180° turn of the close pattern to its downwind, at the High Key
            // (the user, 4 Nov): the wingmen after it, one interval each.
            if (!toFinal && plane.Wingmen.Count > 0) Split(plane);
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
                double change = LowPassFloor(plane, after.AltitudeFt) - plane.AltitudeFt;
                plane.VerticalMode = VerticalMode.Altitude;
                plane.TargetAltitudeFt = Math.Clamp(LowPassFloor(plane, after.AltitudeFt), runwayElevation, MaxAltitude);
                // (A margin: it slows down in the turn, which then lasts less, and the level-off takes its time.)
                // (The flame out: one vertical speed all along, the user 6 Nov.)
                if (change < 0) plane.SelectedVerticalRateFpm = Math.Clamp(-change / Math.Max(0.1, minutes) * (circuit.Type == CircuitType.Sfo ? 1.05 : 1.5), Math.Min(300, DescentLimit(plane)), DescentLimit(plane));
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
            if (circuit.Orbit == 2 && circuit.OrbitForever && Math.Abs(Wrap(plane.HeadingTrue - plane.TargetHeading)) <= 60)
            {
                // Orbits until "continue": the next half before rolling out.
                circuit.Orbit = 1;
                plane.TargetHeading = Normalize(circuit.OrbitHeading + 180);
                circuit.TurnHeading = plane.TargetHeading;
                return;
            }
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
            // A point of a straight leg the turn has already reached (a turn that ended late): the next one.
            while (plane.RouteNext + 1 < plane.Route.Count && !plane.Route[plane.RouteNext].Turn180)
            {
                RoutePoint point = plane.Route[plane.RouteNext];
                double h = plane.HeadingTrue * Math.PI / 180;
                if ((point.X - plane.X) * Math.Sin(h) + (point.Y - plane.Y) * Math.Cos(h) > 0.15) break;
                plane.RouteNext++;
            }
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
            circuit.LapTouched = true;
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
            if (double.IsFinite(circuit.PadHover))
            {
                PadHoverStep(plane, circuit);
                return;
            }
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
                LiftOff(plane, circuit, along);
            }
        }

        /// <summary>Just airborne from the runway (or a helipad): up along the runway, then the circuit (or away, for a departure).</summary>
        private void LiftOff(SimAircraft plane, CircuitState circuit, double along)
        {
            PerformanceClass kind = plane.Class;
            {
                // (The first take-off: the conventional or the wide, the user 5 Nov; another circuit ordered stays for later.)
                if (circuit.LapTouched || circuit.NextType is not (CircuitType.Overhead or CircuitType.Sfo)) NextCircuit(circuit);
                // (A circuit of its plan flown: the next one's end; the end of this one decides what it does now.)
                EndAction flown = circuit.End;
                // (Its last circuit flown, the plan ends away: it leaves, the user 5 Nov.)
                bool leave = circuit.LapTouched && circuit.LeaveAfterLast && circuit.LapsLeft == 1;
                if (circuit.LapTouched)
                {
                    circuit.LapTouched = false;
                    LapDone(circuit);
                }
                double upwind = Math.Max(UpwindAlong(circuit.Type, kind), along + 0.3);
                double height = runwayElevation + CircuitHeight(circuit.Type, kind);
                if (flown == EndAction.Departure || leave)
                {
                    DepartNow(plane, circuit, along);
                    return;
                }
                else if (circuit.Type == CircuitType.Overhead)
                {
                    GiveRoute(plane, [Point(upwind, 0, circuit.Side, height, kind.CircuitSpeedKt, "UPWIND"), .. ToInitialPoint(plane)]);
                }
                else if (circuit.Type == CircuitType.Sfo)
                {
                    // Up along the runway, then the spiral to the High Key (above 400 ft, see CircuitStep).
                    GiveRoute(plane, [Point(upwind, 0, circuit.Side, height, kind.CircuitSpeedKt, "UPWIND")]);
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
            // (Only its planned low approach or low pass: a go-around of the instructor's flies the circuit again.)
            bool leave = circuit.LeaveAfterLast && circuit.LapsLeft == 1 && circuit.End is EndAction.GoAround or EndAction.LowPass;
            LapDone(circuit);
            (double along, _) = RunwayFrame(plane.X, plane.Y, circuit.Side);
            if (leave)
            {
                // Its last final (a low approach, a low pass): away now.
                plane.SpeedGiven = false;
                DepartNow(plane, circuit, along);
                return;
            }
            double upwind = Math.Max(UpwindAlong(circuit.Type, plane.Class), along + 0.5);
            RoutePoint up = Point(upwind, 0, circuit.Side, runwayElevation + CircuitHeight(circuit.Type, plane.Class), plane.Class.CircuitSpeedKt, "UPWIND");
            List<RoutePoint> route = circuit.Type switch
            {
                CircuitType.Overhead => [up, .. ToInitialPoint(plane)],
                // The SFO again: the spiral to the High Key above 400 ft (see CircuitStep).
                CircuitType.Sfo => [up],
                _ => Loop(plane, upwind, withUpwind: true)
            };
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
            double height = runwayElevation + CircuitHeight(circuit.Type, kind);
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
        /// Circling (see <see cref="CircuitStart.Circling"/>): along the opposite final, at the height of the conventional
        /// circuit, to a point beyond the downwind start, aside by 45° to the downwind start, then the downwind, the base, the
        /// final (the conventional circuit).
        /// </summary>
        private List<RoutePoint> CirclingRoute(SimAircraft plane)
        {
            PerformanceClass kind = plane.Class;
            CircuitSide side = plane.Circuit!.Side;
            double width = Width(CircuitType.Conventional, kind, side);
            double upwind = UpwindAlong(CircuitType.Conventional, kind);
            double height = runwayElevation + CircuitHeight(CircuitType.Conventional, kind);
            List<RoutePoint> downwind = Loop(plane, upwind, withUpwind: false).SkipWhile(p => !IsDownwind(p)).ToList();
            return
            [
                Point(upwind + width + 0.5, 0, side, height, kind.CircuitSpeedKt, "CIRCLING"),
                Point(upwind, width, side, height, kind.CircuitSpeedKt, "CIRCLING"),
                .. downwind
            ];
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
            double height = runwayElevation + CircuitHeight(CircuitType.Overhead, kind);
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
            double highKeyHeight = double.IsFinite(plane.Circuit!.LapHeight) ? plane.Circuit!.LapHeight : runwayElevation + kind.HighKeyFt;
            // One vertical speed from the High Key to the touchdown (the user, 6 Nov): the heights of the Low Key, of the end of
            // the downwind and of the roll-out by the time to them; too high for the class's descent: a longer downwind.
            // (Down to the roll-out of the last turn, low and nearly on the runway, as the flame out does; then the landing.)
            double glide = Math.Max(30, kind.BestGlideKt), final = Math.Max(30, kind.FinalSpeedKt);
            double half = Math.PI * width / 2;
            double cap = DescentLimit(plane) * 0.8;
            double t1 = 0, t2 = 0, t3 = 0, total = 1, rollOut = runwayElevation, loss = 0;
            for (int stretch = 0; stretch < 60; stretch++)
            {
                // (A longer downwind rolls out farther: there on the glide path, never above it; then the normal final.)
                double onGlidePath = (TouchdownAim - turn) * 6076.12 * Math.Tan(3 * Math.PI / 180);
                rollOut = runwayElevation + Math.Min(Math.Max(SfoRollOutHeight, onGlidePath), Math.Max(0, highKeyHeight - runwayElevation));
                loss = Math.Max(0, highKeyHeight - rollOut);
                t1 = half / glide * 60;
                t2 = Math.Max(0, highKey - turn) / glide * 60;
                t3 = half / ((glide + final) / 2) * 60;
                total = t1 + t2 + t3;
                if (loss / total <= cap) break;
                turn -= 0.2;
            }
            double lowKey = highKeyHeight - loss * t1 / total;
            double endOfDownwind = highKeyHeight - loss * (t1 + t2) / total;
            return
            [
                Point(highKey, 0, side, highKeyHeight, kind.BestGlideKt, "HIGH KEY"),
                Point(highKey, width, side, lowKey, kind.BestGlideKt, "LOW KEY", turn180: true),
                Point(turn, width, side, endOfDownwind, kind.BestGlideKt, "LOW KEY"),
                Point(turn, 0, side, rollOut, kind.FinalSpeedKt, "FINAL", turn180: true),
                Point(0, 0, side, double.NaN, kind.FinalSpeedKt, "FINAL")
            ];
        }

        /// <summary>
        /// The shortest way from where the aircraft is, on its heading, to the High Key on the runway course (a turn of the given
        /// radius, a straight line, a turn to the runway track before it): its points (the High Key itself not included).
        /// </summary>
        private List<RoutePoint> ShortestToHighKey(SimAircraft plane, double radius, double altitudeFt, out double length)
        {
            static (double X, double Y) Dir(double degrees) => (Math.Sin(degrees * Math.PI / 180), Math.Cos(degrees * Math.PI / 180));
            // (To a point on the runway track before the High Key, two turns' width before it: lined up there.)
            (double tx, double ty) = FromRunway(HighKeyAlong() - 2 * radius, 0, plane.Circuit!.Side);
            double h = plane.HeadingTrue, ht = glidePathCourse;
            // A turn to the right (+1) or to the left (-1): its centre, and the point on it flying on a heading.
            (double X, double Y) Centre(double x, double y, double heading, int turn) { var d = Dir(heading + 90 * turn); return (x + radius * d.X, y + radius * d.Y); }
            (double X, double Y) On((double X, double Y) c, double heading, int turn) { var d = Dir(heading - 90 * turn); return (c.X + radius * d.X, c.Y + radius * d.Y); }
            // (A turn of nothing stays nothing, not a whole circle by rounding.)
            double Arc(double from, double to, int turn) { double a = ((turn * (to - from)) % 360 + 360) % 360; return a > 359.5 ? 0 : a; }
            double best = double.MaxValue;
            List<RoutePoint> way = [];
            foreach (int first in new[] { 1, -1 })
                foreach (int last in new[] { 1, -1 })
                {
                    var c1 = Centre(plane.X, plane.Y, h, first);
                    var c2 = Centre(tx, ty, ht, last);
                    double dx = c2.X - c1.X, dy = c2.Y - c1.Y, d = Math.Sqrt(dx * dx + dy * dy);
                    double bearing = Math.Atan2(dx, dy) * 180 / Math.PI;
                    double straightOn;
                    if (first == last) straightOn = bearing;
                    else if (d < 2 * radius) continue;
                    else straightOn = bearing + first * Math.Asin(2 * radius / d) * 180 / Math.PI;
                    var p1 = On(c1, straightOn, first);
                    var p2 = On(c2, straightOn, last);
                    double a1 = Arc(h, straightOn, first), a2 = Arc(straightOn, ht, last);
                    double total = radius * (a1 + a2) * Math.PI / 180 + Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
                    if (total >= best) continue;
                    best = total;
                    way = [];
                    // (Points a quarter of a turn apart at most: each one far enough to be turned to.)
                    for (double a = 90; a < a1 - 45; a += 90) { var q = On(c1, h + first * a, first); way.Add(new RoutePoint(q.X, q.Y, altitudeFt, plane.Class.BestGlideKt, "HIGH KEY")); }
                    way.Add(new RoutePoint(p1.X, p1.Y, altitudeFt, plane.Class.BestGlideKt, "HIGH KEY"));
                    way.Add(new RoutePoint(p2.X, p2.Y, altitudeFt, plane.Class.BestGlideKt, "HIGH KEY"));
                    int pieces = (int)Math.Ceiling(a2 / 90);
                    for (int k = 1; k < pieces; k++) { var q = On(c2, straightOn + last * a2 * k / pieces, last); way.Add(new RoutePoint(q.X, q.Y, altitudeFt, plane.Class.BestGlideKt, "HIGH KEY")); }
                    way.Add(new RoutePoint(tx, ty, altitudeFt, plane.Class.BestGlideKt, "HIGH KEY"));
                }
            // (No point too near the one before, or to the aircraft: it could not be turned to, the aircraft would circle it.)
            List<RoutePoint> kept = [];
            double lastX = plane.X, lastY = plane.Y;
            for (int k = 0; k < way.Count; k++)
            {
                RoutePoint point = way[k];
                if (k == way.Count - 1)
                    while (kept.Count > 0 && Math.Sqrt((point.X - kept[^1].X) * (point.X - kept[^1].X) + (point.Y - kept[^1].Y) * (point.Y - kept[^1].Y)) < 0.8 * radius)
                        kept.RemoveAt(kept.Count - 1);
                else if (Math.Sqrt((point.X - lastX) * (point.X - lastX) + (point.Y - lastY) * (point.Y - lastY)) < 0.8 * radius) continue;
                kept.Add(point);
                (lastX, lastY) = (point.X, point.Y);
            }
            // (Its length: to the runway track, then along it to the High Key.)
            length = best + 2 * radius;
            return kept;
        }

        /// <summary>The High Key along the runway (NM from the threshold): over its middle.</summary>
        private double HighKeyAlong() => 0.5 * Math.Max(runwayLength, 0.3);

        /// <summary>
        /// The descent limit (ft/min): in the simulated flame out one and a half times the class's (idle, speed brakes), so
        /// the compact pattern around the runway loses the height from the High Key.
        /// </summary>
        internal static double DescentLimit(SimAircraft plane) =>
            plane.Class.MaxDescentFpm * (plane.Circuit is { Type: CircuitType.Sfo, Suspended: false, Departed: false } ? 1.5 : 1);

        /// <summary>
        /// Back to the initial point for a new overhead (or to the High Key for a new flame out): out on the wide downwind to
        /// abeam a point 1 NM before the initial point, onto the runway track there, then the overhead pattern (the flame
        /// out: at the High Key height, to the High Key).
        /// </summary>
        private List<RoutePoint> ToInitialPoint(SimAircraft plane, CircuitSide? outSide = null)
        {
            PerformanceClass kind = plane.Class;
            CircuitSide side = plane.Circuit!.Side;
            // (Out to the wide downwind on the side it flies, the user 5 Nov: not across the runway to the overhead's side.)
            CircuitSide outOn = outSide ?? side;
            bool sfo = plane.Circuit.Type == CircuitType.Sfo;
            double wide = Width(CircuitType.Wide, kind, outOn);
            double height = runwayElevation + (sfo ? kind.HighKeyFt : kind.OverheadHeightFt);
            double speed = sfo ? kind.BestGlideKt : kind.OverheadSpeedKt;
            (double along, double lateral) = RunwayFrame(plane.X, plane.Y, outOn);
            List<RoutePoint> route = [];
            // (The user, 3 Nov: straight to the IP, then the turn towards the aerodrome, anticipated so it rolls out on the
            // initial at the IP. Only from the runway track ahead of the IP (a go-around, a take-off) it first goes out to
            // the wide downwind abeam where it is, not back along the runway.)
            (double fromAlong, double fromLateral) = (along, lateral);
            if (Math.Abs(lateral) < wide / 2 && along > -initialPoint)
            {
                route.Add(Point(Math.Max(along, 0), wide, outOn, height, kind.CircuitSpeedKt, "JOIN"));
                (fromAlong, fromLateral) = (Math.Max(along, 0), wide);
            }
            // The corner on the runway track beyond the IP by what the turn onto the initial needs, so it rolls out at the IP.
            double turn = Math.Abs(Math.Atan2(-fromLateral, -initialPoint - fromAlong) * 180 / Math.PI);
            // (At the speed of the initial, which it flies on the way there; the roll-in on top.)
            double speedNow = Math.Max(Math.Max(30, plane.GroundSpeedKt), speed);
            double rate = Math.Min(MaxTurnRate, RateForBank(kind.CircuitBankDegrees, speedNow));
            double radius = speedNow / 3600 / (rate * Math.PI / 180);
            double lead = Math.Min(4, radius * Math.Tan(Math.Min(turn, 170) / 2 * Math.PI / 180) + speedNow / 3600 * (rate / TurnRateChange) / 2);
            route.Add(Point(-initialPoint - lead, 0, side, height, speed, "JOIN"));
            route.AddRange(sfo ? SfoRoute(plane) : OverheadRoute(plane));
            return route;
        }

        /// <summary>
        /// After a go-around or a touch and go: the circuit ordered meanwhile, else the overhead goes on as the close pattern
        /// (the same jet side), the SFO as the close pattern on the jet side of the aerodrome (it is on the runway track: it
        /// just turns to that side).
        /// </summary>
        private void NextCircuit(CircuitState circuit)
        {
            // (A height given for one circuit: the class's again.)
            circuit.LapHeight = double.NaN;
            if (circuit.NextType is CircuitType next)
            {
                circuit.Type = next;
                circuit.Side = circuit.NextSide;
                circuit.NextType = null;
                if (next == CircuitType.Sfo) circuit.Hold = true;
            }
            else if (circuit.Type is CircuitType.Overhead or CircuitType.Sfo)
            {
                if (circuit.Type == CircuitType.Sfo && aerodrome?.SideOf(aerodromeRunway, CircuitType.Close) is CircuitSide jet) circuit.Side = jet;
                circuit.Type = CircuitType.Close;
            }
        }

        /// <summary>
        /// The height of the downwind of a circuit (ft above the threshold), of the class (with the aerodrome's heights):
        /// conventional, wide, close (also the overhead's downwind after the break, and the close pattern after an SFO).
        /// </summary>
        private static double CircuitHeight(CircuitType type, PerformanceClass kind)
        {
            double height = type switch
            {
                CircuitType.Conventional => kind.ConventionalHeightFt,
                CircuitType.Wide => kind.WideHeightFt,
                _ => kind.CloseHeightFt
            };
            // (A class not normalized, the defaults of the engine: the one height of the class.)
            return height > 0 ? height : kind.CircuitHeightFt;
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

        /// <summary>
        /// A departure with a radial (see <see cref="AddDeparture"/>): above 400 ft the turn (on its side) to a heading 45° onto
        /// the radial (onto it: its course), then along it. A heading of the instructor: his from then on.
        /// </summary>
        private void DepartureStep(SimAircraft plane, CircuitState circuit)
        {
            // To its exit gate or point: gone there (as a crossing).
            if (double.IsFinite(circuit.ExitX))
            {
                PointDepartureStep(plane, circuit);
                return;
            }
            if (!double.IsFinite(circuit.DepartRadial) || circuit.DepartPhase >= 2) return;
            double course = circuit.DepartRadial * Math.PI / 180;
            double dx = plane.X - circuit.DepartFromX, dy = plane.Y - circuit.DepartFromY;
            // Right of the radial (outbound): positive; along it from its point.
            double right = dx * Math.Cos(course) - dy * Math.Sin(course);
            double along = dx * Math.Sin(course) + dy * Math.Cos(course);
            if (circuit.DepartPhase == 0)
            {
                // (A heading or another route of the instructor's before the turn: his.)
                if (plane.Navigation != NavigationMode.Route || plane.Route is not { Count: 2 } way || way[1].Name != "DEPARTURE") { InstructorsDeparture(plane, circuit); return; }
                // (The user, 5 Nov: past the far end of the runway and above 400 ft; a turn as wide as the close pattern's.)
                (double runwayAlong, _) = RunwayFrame(plane.X, plane.Y, CircuitSide.Left);
                if (plane.AltitudeFt - runwayElevation <= WaveoffTurnHeight || runwayAlong < Math.Max(runwayLength, 0.3)) return;
                circuit.Radius = Math.Max(0.15, Width(CircuitType.Close, plane.Class, CircuitSide.Left) / 2);
                circuit.DepartIntercept = Math.Abs(right) < 0.2 ? circuit.DepartRadial : Normalize(circuit.DepartRadial + (right > 0 ? -45 : 45));
                plane.Navigation = NavigationMode.Manual;
                plane.TargetHeading = circuit.DepartIntercept;
                plane.TurnDirection = Math.Abs(Wrap(circuit.DepartIntercept - plane.HeadingTrue)) < 5 ? TurnDirection.Shortest : circuit.DepartTurn;
                plane.SelectedTurnRateDps = Math.Clamp(Math.Max(30, plane.GroundSpeedKt) / 3600 / circuit.Radius * 180 / Math.PI, MinTurnRate, MaxTurnRate);
                plane.CircuitBankDegrees = plane.Class.CircuitBankDegrees;
                if (!plane.AltitudeGiven)
                {
                    plane.VerticalMode = VerticalMode.Altitude;
                    plane.TargetAltitudeFt = circuit.DepartHeight;
                }
                circuit.DepartPhase = 1;
                return;
            }
            // Turning to the intercept heading, then on it to the radial.
            if (Math.Abs(Wrap(plane.TargetHeading - circuit.DepartIntercept)) > 0.5 || plane.Navigation != NavigationMode.Manual) { InstructorsDeparture(plane, circuit); return; }
            if (Math.Abs(Wrap(plane.HeadingTrue - circuit.DepartIntercept)) > 15) return;
            double lead = Math.Max(0.05, Math.Max(0.15, circuit.Radius) * Math.Tan(22.5 * Math.PI / 180));
            if (Math.Abs(right) > lead)
            {
                // (After a long turn it may be on the other side of the radial now: 45° onto it from there.)
                double onto = Normalize(circuit.DepartRadial + (right > 0 ? -45 : 45));
                if (Math.Abs(Wrap(onto - circuit.DepartIntercept)) > 1)
                {
                    circuit.DepartIntercept = plane.TargetHeading = onto;
                    plane.TurnDirection = TurnDirection.Shortest;
                }
                return;
            }
            // On the radial: along it, out of the zone.
            double ahead = Math.Max(along, 0) + DepartureGoneNM + 5;
            double height = double.IsFinite(circuit.DepartHeight) ? circuit.DepartHeight : plane.TargetAltitudeFt;
            // (An altitude or a speed of the instructor's kept.)
            bool altitudeGiven = plane.AltitudeGiven, speedGiven = plane.SpeedGiven;
            double altitude = plane.TargetAltitudeFt, speed = plane.TargetSpeedKt;
            VerticalMode vertical = plane.VerticalMode;
            GiveRoute(plane,
            [
                new RoutePoint(circuit.DepartFromX + ahead * Math.Sin(course), circuit.DepartFromY + ahead * Math.Cos(course), height, plane.Class.CircuitSpeedKt, "DEPARTURE")
            ]);
            if (altitudeGiven)
            {
                plane.AltitudeGiven = true;
                plane.TargetAltitudeFt = altitude;
                plane.VerticalMode = vertical;
            }
            if (speedGiven)
            {
                plane.SpeedGiven = true;
                plane.TargetSpeedKt = speed;
            }
            circuit.DepartPhase = 2;
        }

        /// <summary>
        /// A departure to its exit gate or R/D point (the user, 6 Nov): after the upwind straight to it, its turn on its side (L / R;
        /// none: the shorter), gone there. Phase: 0 the upwind, 1 turning on its side, 2 straight to the point, 3 the instructor's
        /// (a heading, another route: then gone <see cref="DepartureGoneNM"/> out as the others).
        /// </summary>
        private void PointDepartureStep(SimAircraft plane, CircuitState circuit)
        {
            if (circuit.DepartPhase == 3) return;
            bool toPoint = plane.Navigation == NavigationMode.Route && plane.Route is { Count: > 0 } way && way[^1].Name == "DEPARTURE";
            if (circuit.DepartPhase != 1 && toPoint && plane.RouteNext >= plane.Route!.Count)
            {
                circuit.TransitDone = true;
                return;
            }
            if (circuit.DepartPhase == 2)
            {
                if (!toPoint) InstructorsDeparture(plane, circuit);
                return;
            }
            double bearing = Normalize(Math.Atan2(circuit.ExitX - plane.X, circuit.ExitY - plane.Y) * 180 / Math.PI);
            if (circuit.DepartPhase == 0)
            {
                if (!toPoint || plane.Route is not { Count: 2 }) { InstructorsDeparture(plane, circuit); return; }
                if (circuit.DepartTurn == TurnDirection.Shortest) { circuit.DepartPhase = 2; return; }
                // (As the radial: past the far end of the runway and above 400 ft, or at the end of its upwind.)
                (double runwayAlong, _) = RunwayFrame(plane.X, plane.Y, CircuitSide.Left);
                bool upwindOver = plane.RouteNext >= 1;
                if (!upwindOver && (plane.AltitudeFt - runwayElevation <= WaveoffTurnHeight || runwayAlong < Math.Max(runwayLength, 0.3))) return;
                // (Almost straight ahead: no turn to speak of, on to it.)
                if (Math.Abs(Wrap(bearing - plane.HeadingTrue)) < 10) { circuit.DepartPhase = 2; return; }
                circuit.Radius = Math.Max(0.15, Width(CircuitType.Close, plane.Class, CircuitSide.Left) / 2);
                circuit.DepartIntercept = bearing;
                plane.Navigation = NavigationMode.Manual;
                plane.TargetHeading = bearing;
                plane.TurnDirection = circuit.DepartTurn;
                plane.SelectedTurnRateDps = Math.Clamp(Math.Max(30, plane.GroundSpeedKt) / 3600 / circuit.Radius * 180 / Math.PI, MinTurnRate, MaxTurnRate);
                plane.CircuitBankDegrees = plane.Class.CircuitBankDegrees;
                if (!plane.AltitudeGiven)
                {
                    plane.VerticalMode = VerticalMode.Altitude;
                    plane.TargetAltitudeFt = circuit.DepartHeight;
                }
                circuit.DepartPhase = 1;
                return;
            }
            // Turning on its side: a heading of the instructor's is his; facing the point (or on the heading): straight to it.
            if (plane.Navigation != NavigationMode.Manual || Math.Abs(Wrap(plane.TargetHeading - circuit.DepartIntercept)) > 0.5) { InstructorsDeparture(plane, circuit); return; }
            if (Math.Abs(Wrap(bearing - plane.HeadingTrue)) > 10 && Math.Abs(Wrap(plane.TargetHeading - plane.HeadingTrue)) > 2) return;
            double height = double.IsFinite(circuit.DepartHeight) ? circuit.DepartHeight : plane.TargetAltitudeFt;
            bool altitudeGiven = plane.AltitudeGiven, speedGiven = plane.SpeedGiven;
            double altitude = plane.TargetAltitudeFt, speed = plane.TargetSpeedKt;
            VerticalMode vertical = plane.VerticalMode;
            GiveRoute(plane, [new RoutePoint(circuit.ExitX, circuit.ExitY, height, plane.Class.CircuitSpeedKt, "DEPARTURE")]);
            plane.TurnDirection = TurnDirection.Shortest;
            if (altitudeGiven)
            {
                plane.AltitudeGiven = true;
                plane.TargetAltitudeFt = altitude;
                plane.VerticalMode = vertical;
            }
            if (speedGiven)
            {
                plane.SpeedGiven = true;
                plane.TargetSpeedKt = speed;
            }
            circuit.DepartPhase = 2;
        }

        /// <summary>The instructor took a departure (a heading, a route): his from now on, turning as any aircraft he flies.</summary>
        private static void InstructorsDeparture(SimAircraft plane, CircuitState circuit)
        {
            circuit.DepartPhase = 3;
            plane.CircuitBankDegrees = 0;
            plane.SelectedTurnRateDps = plane.TurnRateMode == TurnRateMode.Half ? HalfTurnRate : StandardTurnRate;
        }

        /// <summary>Where the overhead pattern breaks, and the close pattern turns to its downwind (NM along the runway; standby: never).</summary>
        private double BreakAlongOf(CircuitState circuit) => circuit.Break switch
        {
            BreakPoint.Beginning => 0.05,
            BreakPoint.End => Math.Max(runwayLength, 0.3),
            BreakPoint.Point => circuit.BreakAlong,
            BreakPoint.Standby => double.PositiveInfinity,
            _ => Math.Max(runwayLength, 0.3) / 2
        };

        /// <summary>A low pass (the user, 5 Nov): never below its 400 ft before the runway (the base, the turn to the final).</summary>
        private double LowPassFloor(SimAircraft plane, double altitudeFt) =>
            plane.Circuit is { End: EndAction.LowPass, Departed: false } && double.IsFinite(altitudeFt)
                ? Math.Max(altitudeFt, runwayElevation + LowPassHeight) : altitudeFt;

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
                plane.Circuit.StraightIn = false;
                plane.Circuit.Turning = false;
                plane.Circuit.Orbit = 0;
                plane.Circuit.Spiral = false;
                plane.Circuit.Extended = false;
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
