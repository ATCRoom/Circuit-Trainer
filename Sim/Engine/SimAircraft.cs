namespace ATCRoom.Sim
{
    /// <summary>Side of a turn to a heading.</summary>
    public enum TurnDirection
    {
        /// <summary>The shorter way.</summary>
        Shortest,
        Left,
        Right
    }

    /// <summary>Turn rate of an aircraft: for the heading instructions and with the stick at its edge.</summary>
    public enum TurnRateMode
    {
        /// <summary>Half rate: 1.5°/s.</summary>
        Half,
        /// <summary>Rate one: 3°/s.</summary>
        Standard,
        /// <summary>Heading instructions at 3°/s; with the stick held at its edge the turn grows up to 10°/s.</summary>
        Free
    }

    /// <summary>What the aircraft does vertically.</summary>
    public enum VerticalMode
    {
        /// <summary>Climbs or descends at the selected vertical rate to the commanded altitude, then levels off.</summary>
        Altitude,
        /// <summary>Keeps the commanded vertical speed until told otherwise (the "rate" of the PAR controller).</summary>
        Rate,
        /// <summary>The vertical speed that keeps the glide path at its ground speed ("Normal" rate), also when the speed changes.</summary>
        GlidePath
    }

    /// <summary>How the aircraft is navigated sideways.</summary>
    public enum NavigationMode
    {
        /// <summary>MANUAL: headings and the stick (a route, if any, is kept but not flown).</summary>
        Manual,
        /// <summary>ROUTE chosen, no point given yet: it keeps its heading until a point is given (direct to, next point).</summary>
        RouteArmed,
        /// <summary>ROUTE: it flies the points of its route.</summary>
        Route
    }

    /// <summary>Whether the aircraft is still flying.</summary>
    public enum GroundContact
    {
        Airborne,
        /// <summary>Touched down on the runway: it rolls on for a few seconds, then it is removed.</summary>
        Landed,
        /// <summary>Reached the ground outside the runway: it stops there until the instructor removes it.</summary>
        OnGround,
        /// <summary>On the runway in a circuit (touch and go, take-off): it rolls and lifts off by itself.</summary>
        Rolling
    }

    /// <summary>
    /// A point of a route: position in the <see cref="LocalPlane"/> (NM east, NM north of the threshold), altitude
    /// (ft above mean sea level) and speed (kt) to have there, and an optional name (a fix of the maps, for the lists).
    /// <paramref name="Turn180"/> (circuits): the point is the end of a 180° turn, reached by turning to the opposite
    /// heading, not flown to (the close pattern's turns to its downwind and to its final).
    /// </summary>
    public sealed record RoutePoint(double X, double Y, double AltitudeFt, double SpeedKt, string Name = "", bool Turn180 = false);

    /// <summary>
    /// An aircraft of the simulation at one moment, as given out by <see cref="SimEngine.Snapshot"/> (a copy: it does
    /// not change afterwards). Positions in the <see cref="LocalPlane"/> (NM), altitude above mean sea level (ft),
    /// headings true (degrees, 0–360), speed true airspeed (kt), vertical speeds ft/min (negative descending).
    /// </summary>
    public sealed record AircraftState(
        string Callsign,
        double X,
        double Y,
        double AltitudeFt,
        double HeadingTrue,
        double SpeedKt,
        double VerticalSpeedFpm,
        double TurnRateDps,
        double TargetHeading,
        TurnDirection TurnDirection,
        double SelectedTurnRateDps,
        TurnRateMode TurnRateMode,
        VerticalMode VerticalMode,
        double TargetAltitudeFt,
        double SelectedVerticalRateFpm,
        double CommandedVerticalSpeedFpm,
        double BestVerticalSpeedFpm,
        double TargetSpeedKt,
        bool Frozen,
        bool StickActive,
        int PendingCommands,
        string PerformanceClass,
        double GroundSpeedKt,
        double TrackTrue,
        double DriftDegrees,
        GroundContact Contact,
        // The route (null: none; the points already passed included) and the index of the point it flies to next
        // (= the number of points when all are passed).
        IReadOnlyList<RoutePoint>? Route = null,
        int RouteNext = -1,
        // Whether the route still gives the altitude and the speed (until the instructor gives one).
        bool RouteAltitude = false,
        bool RouteSpeed = false,
        NavigationMode Navigation = NavigationMode.Manual,
        // The circuit (TWR Trainer; null: not in a circuit), its side, what it does at the end of the final, and the leg
        // now (UPWIND, CROSSWIND, DOWNWIND, BASE, FINAL, INITIAL, JOIN, ORBIT, GO AROUND, ROLL, LANDED, DEPARTURE; MANUAL: the instructor flies it).
        CircuitType? Circuit = null,
        CircuitSide CircuitSide = CircuitSide.Left,
        EndAction EndAction = EndAction.FullStop,
        string Leg = "",
        // The circuit it will fly after the next go-around or touch and go (the close pattern ordered while in a circuit), and
        // where the overhead pattern breaks.
        CircuitType? NextCircuit = null,
        BreakPoint Break = BreakPoint.Middle,
        // SFO: on hold at the High Key (until "start SFO").
        bool SfoHold = false,
        // The side of the next circuit, and the downwind extended (until "turn base").
        CircuitSide NextSide = CircuitSide.Left,
        bool Extended = false,
        // Orbiting until "continue".
        bool OrbitForever = false,
        // Crossing the zone (from a gate to another one, not landing).
        bool Transit = false,
        // A formation: its members (the leader first; null: alone), which land together, and whether SPLIT can be given now.
        IReadOnlyList<string>? Formation = null,
        IReadOnlyList<bool>? Lands = null,
        bool SplitAllowed = false,
        // The circuits of its plan still to fly, this one included (0: on and on).
        int LapsLeft = 0,
        // Rolling out after the landing: the runway exit it goes to (empty: none).
        string Exit = "",
        bool ExpediteTaxi = false,
        // A helicopter (it can land on a helipad), and going to its pad (the user, 5 Nov).
        bool CanHover = false,
        bool HasPad = false,
        bool WantsPad = false);

    /// <summary>An aircraft inside the engine: actual values and the values commanded by the instructor. Only the engine changes it, under its lock.</summary>
    internal sealed class SimAircraft
    {
        public string Callsign = "";
        // Actual values.
        public double X;
        public double Y;
        public double AltitudeFt;
        public double HeadingTrue;
        public double SpeedKt;
        public double VerticalSpeedFpm;
        public double TurnRateDps;
        // Commanded values.
        public double TargetHeading;
        public TurnDirection TurnDirection = TurnDirection.Shortest;
        public double SelectedTurnRateDps = SimEngine.StandardTurnRate;
        public TurnRateMode TurnRateMode = TurnRateMode.Standard;
        public VerticalMode VerticalMode = VerticalMode.Altitude;
        public double TargetAltitudeFt;
        public double SelectedVerticalRateFpm = SimEngine.DefaultVerticalRate;
        public double CommandedVerticalSpeedFpm;
        public double TargetSpeedKt;
        /// <summary>Vertical speed that keeps the glide path at this heading and speed (ft/min, negative), 0 without an approach.</summary>
        public double BestVerticalSpeedFpm;
        /// <summary>Held where it is (the instructor explains a situation): it does not move and keeps its commands.</summary>
        public bool Frozen;
        /// <summary>Stick deflection (−1..1, X right, Y up) and for how long X has been at its edge (Free turn rate).</summary>
        public double StickX;
        public double StickY;
        public double StickHeld;
        /// <summary>What the aircraft can do (a copy of its class).</summary>
        public PerformanceClass Class = PerformanceClass.Defaults()[1];
        /// <summary>Over the ground, with the wind: speed (kt), track (degrees true in the plane) and drift (track − heading, −180..180).</summary>
        public double GroundSpeedKt;
        public double TrackTrue;
        public double DriftDegrees;
        /// <summary>Still flying, landed (and since which step) or on the ground outside the runway.</summary>
        public GroundContact Contact = GroundContact.Airborne;
        public long LandedStep;
        /// <summary>
        /// The route (null: none; the points passed included), the point it flies to next, where the leg to that point
        /// starts (for the course of the last leg), whether it has already flown towards that point (a point behind it
        /// is passed only after that), MANUAL / ROUTE, and whether the instructor has given an altitude (a rate) or a
        /// speed: from then on the points' altitudes or speeds are not flown.
        /// </summary>
        public IReadOnlyList<RoutePoint>? Route;
        public int RouteNext;
        public double LegStartX;
        public double LegStartY;
        public bool RouteApproached;
        public NavigationMode Navigation = NavigationMode.Manual;
        public bool AltitudeGiven;
        public bool SpeedGiven;
        /// <summary>The circuit it flies (null: none) and, while it flies it, the bank of its turns (0: the class limit).</summary>
        public CircuitState? Circuit;
        public double CircuitBankDegrees;
        /// <summary>
        /// The landing roll (the user, 4 Nov): braking along the runway to the exit chosen at the touchdown (its name, along
        /// the runway, to the right +1 / left −1; none: where it reaches the exit speed), then turning off; done: gone.
        /// </summary>
        public bool RollActive;
        public bool RollOver;
        public string ExitName = "";
        public double ExitAlong;
        public int ExitSide = 1;
        public long ExitTurnStep;
        /// <summary>EXPEDITE TAXI: at least 45 kt along the runway to its exit.</summary>
        public bool ExpediteTaxi;
        /// <summary>
        /// A formation (the user, 4 Nov): the callsigns of its wingmen (empty: alone), which members land together on the
        /// short final (index 0 this one, the leader) and the seconds between them when it splits.
        /// </summary>
        public List<string> Wingmen = [];
        public List<bool> Lands = [];
        public double SplitSeconds = 3;

        /// <summary>The formation can be split by command now (the user, 8 Nov: always, in any circuit).</summary>
        public bool SplitAllowed => Wingmen.Count > 0 && Contact == GroundContact.Airborne;

        /// <summary>
        /// An orbit flown by hand (the user, 8 Nov: ORB L / R in MANUAL): 0 none, 1 the first half, 2 the second; on and on
        /// until CONTINUE; the heading it had (it rolls out on it); the heading it was given last (another one: no more orbit).
        /// </summary>
        public int ManualOrbit;
        public bool ManualOrbitForever;
        public double ManualOrbitHeading, ManualOrbitTarget;

        /// <summary>A copy of this aircraft (a member of its formation splitting off): its own circuit and route, no wingmen.</summary>
        public SimAircraft Copy()
        {
            SimAircraft copy = (SimAircraft)MemberwiseClone();
            copy.Class = Class.Copy();
            copy.Route = Route?.ToArray();
            copy.Circuit = Circuit?.Copy();
            copy.Wingmen = [];
            copy.Lands = [];
            return copy;
        }

        /// <summary>The leg of the circuit now, for the label (empty: not in a circuit).</summary>
        public string Leg => Circuit == null ? ""
            : Contact == GroundContact.Rolling ? "ROLL"
            : Contact == GroundContact.Landed ? "LANDED"
            : Circuit.Departed ? "DEPARTURE"
            : Circuit.Suspended ? "MANUAL"
            : Circuit.Spiral ? (Circuit.Hold ? "HOLD" : "CLIMB")
            : Circuit.Extended ? "EXT DOWNWIND"
            : Circuit.Waiting ? (Navigation == NavigationMode.Route ? "GO AROUND" : "MANUAL")
            : Circuit.Orbit > 0 ? (Circuit.OrbitForever ? "ORBITS" : "ORBIT")
            : Route != null && RouteNext >= 0 && RouteNext < Route.Count ? Route[RouteNext].Name
            : Circuit.OnFinal ? "FINAL" : "";

        public AircraftState ToState(int pendingCommands) => new(Callsign, X, Y, AltitudeFt, HeadingTrue, SpeedKt, VerticalSpeedFpm, TurnRateDps,
            TargetHeading, TurnDirection, SelectedTurnRateDps, TurnRateMode, VerticalMode, TargetAltitudeFt, SelectedVerticalRateFpm,
            CommandedVerticalSpeedFpm, BestVerticalSpeedFpm, TargetSpeedKt, Frozen, StickX != 0 || StickY != 0, pendingCommands,
            Class.Name, GroundSpeedKt, TrackTrue, DriftDegrees, Contact,
            Route, Route != null ? RouteNext : -1, Route != null && !AltitudeGiven, Route != null && !SpeedGiven, Navigation,
            Circuit?.Type, Circuit?.Side ?? CircuitSide.Left, Circuit?.End ?? EndAction.FullStop, Leg,
            Circuit?.NextType, Circuit?.Break ?? BreakPoint.Middle, Circuit?.Hold == true,
            Circuit?.NextSide ?? CircuitSide.Left, Circuit?.Extended == true, Circuit?.OrbitForever == true || (ManualOrbit > 0 && ManualOrbitForever), Circuit?.Transit == true,
            Wingmen.Count > 0 ? new List<string> { Callsign }.Concat(Wingmen).ToList() : null, Wingmen.Count > 0 ? Lands.ToList() : null, SplitAllowed,
            Circuit?.LapsLeft ?? 0, ExitName, ExpediteTaxi, Class.CanHover, Circuit?.HasPad ?? false, Circuit?.WantsPad ?? false);
    }
}
