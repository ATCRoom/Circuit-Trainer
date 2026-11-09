namespace ATCRoom.Sim
{
    /// <summary>
    /// Formations (the user, 4 Nov): aircraft flying together, one aircraft simulated (the leader) carrying its wingmen
    /// (their callsigns reserved) until they split; then each one is an aircraft of its own. The rules of the user:
    /// - the overhead pattern splits only at the break, the close pattern at its 180° turn to the downwind, the SFO at the
    ///   High Key (each wingman after the one before by the split interval);
    /// - by command (SPLIT) at any time (the user, 8 Nov), each one after the one before by the interval;
    /// - on the short final the members chosen land together (any of them; from the long final all of them can, from a
    ///   circuit at most 2), the others go around, each one alone, flying the close pattern;
    /// - callsigns in sequence: VIPER01 leads VIPER02, 03...; VIPER02 leads VIPER03, 04...
    /// </summary>
    public sealed partial class SimEngine
    {
        /// <summary>At most this many aircraft in a formation.</summary>
        public const int MaxFormation = 4;
        /// <summary>From a circuit (not straight in) at most this many land together.</summary>
        private const int MaxCircuitFormationLanding = 2;
        /// <summary>The wingmen split by command: each this far behind and outside the one before (NM).</summary>
        private const double EchelonNM = 0.03;

        /// <summary>Aircraft made during a step (split wingmen), added to the traffic once the step is over.</summary>
        private readonly List<SimAircraft> spawned = [];

        /// <summary>
        /// Makes an aircraft just added the leader of a formation of <paramref name="size"/> (2 to 4): its wingmen are
        /// the callsigns after its own number (VIPER01: VIPER02, VIPER03...), reserved. Which members land together on
        /// the short final (null: all; index 0 the leader) and the seconds between the wingmen when it splits. Returns
        /// null, or why not (the callsign has no number, a wingman's callsign is taken).
        /// </summary>
        public string? MakeFormation(string leader, int size, IReadOnlyList<bool>? lands = null, double splitSeconds = 3)
        {
            lock (sync)
            {
                SimAircraft? plane = Find(leader);
                if (plane == null) return $"{leader} is not in the simulation.";
                if (size < 2) return null;
                size = Math.Min(size, MaxFormation);
                List<string>? names = FormationNames(plane.Callsign, size);
                if (names == null) return $"The callsign of a formation's leader ends with its number (for example VIPER01), not {plane.Callsign}.";
                foreach (string name in names)
                {
                    if (name.Length > 10) return $"{name}: a callsign has at most 10 letters or digits.";
                    if (Find(name) != null || Reserved(name)) return $"{name} (a wingman of {plane.Callsign}) is already in the simulation.";
                }
                plane.Wingmen = names;
                plane.Lands = Enumerable.Range(0, size).Select(i => lands == null || i >= lands.Count || lands[i]).ToList();
                plane.SplitSeconds = double.IsFinite(splitSeconds) ? Math.Clamp(splitSeconds, 0, 30) : 3;
                return null;
            }
        }

        /// <summary>The callsigns of the wingmen of a leader: its number + 1, + 2... with as many digits (null: no number at its end).</summary>
        public static List<string>? FormationNames(string leader, int size)
        {
            string text = (leader ?? "").Trim().ToUpperInvariant();
            int digits = 0;
            while (digits < text.Length && char.IsAsciiDigit(text[text.Length - 1 - digits])) digits++;
            if (digits == 0 || digits == text.Length) return null;
            string stem = text[..^digits];
            long number = long.Parse(text[^digits..], System.Globalization.CultureInfo.InvariantCulture);
            return Enumerable.Range(1, Math.Max(0, size - 1))
                .Select(i => stem + (number + i).ToString(new string('0', digits), System.Globalization.CultureInfo.InvariantCulture)).ToList();
        }

        /// <summary>Instruction: the formation splits now (the user, 8 Nov: always; in its circuit each one after the one before).</summary>
        public void SplitFormation(string callsign) =>
            Instruct(callsign, Kind.Formation, a =>
            {
                if (a.Wingmen.Count == 0 || a.Contact != GroundContact.Airborne || !a.SplitAllowed) return;
                bool spiral = a.Circuit is { Spiral: true, Suspended: false };
                int before = spawned.Count;
                Split(a);
                // (Climbing to the High Key: each one on the same circle, after its interval straight on.)
                if (spiral) foreach (SimAircraft wingman in spawned.Skip(before)) if (wingman.Circuit != null) wingman.Circuit.Spiral = true;
            });

        /// <summary>Which members land together on the short final (index 0 the leader); the others go around. At once.</summary>
        public void SetFormationLanding(string callsign, IReadOnlyList<bool> lands) => Change(callsign, a =>
        {
            if (a.Wingmen.Count == 0) return;
            a.Lands = Enumerable.Range(0, a.Wingmen.Count + 1).Select(i => i >= lands.Count || lands[i]).ToList();
        });

        /// <summary>A callsign kept for a wingman of a formation.</summary>
        private bool Reserved(string callsign) =>
            aircraft.Any(a => a.Wingmen.Any(w => Same(w, callsign))) || spawned.Any(a => Same(a.Callsign, callsign) || a.Wingmen.Any(w => Same(w, callsign)));

        /// <summary>The aircraft made in the step, into the traffic.</summary>
        private void AddSpawned()
        {
            if (spawned.Count == 0) return;
            aircraft.AddRange(spawned);
            spawned.Clear();
        }

        /// <summary>
        /// The split: each wingman an aircraft of its own, a little behind and outside the one before, flying straight on
        /// for its share of the interval (the leader at once, the second after one interval...), then its circuit, as the
        /// leader (the overhead's break: each one breaks after the one before).
        /// </summary>
        private void Split(SimAircraft lead)
        {
            for (int i = 0; i < lead.Wingmen.Count; i++)
            {
                int n = i + 1;
                SimAircraft wingman = Member(lead, lead.Wingmen[i]);
                Echelon(wingman, lead, n);
                Delay(wingman, lead.SplitSeconds * n);
                Spacing(wingman, n, lead.SplitSeconds);
                spawned.Add(wingman);
            }
            lead.Wingmen = [];
            lead.Lands = [];
        }

        /// <summary>
        /// The n-th member after the split lengthens its downwind (the user, 4 Nov: as in reality, to land at least a third
        /// of the runway after the one before): the end of its downwind and its turn to the base (or the final) further
        /// out, by what the interval does not give: the time of a third of the runway at the final speed, less the
        /// interval, flown out and back along the downwind.
        /// </summary>
        private void Spacing(SimAircraft member, int n, double intervalSeconds)
        {
            if (n <= 0 || member.Circuit == null || member.Route == null || double.IsNaN(glidePathCourse)) return;
            CircuitSide side = member.Circuit.Side;
            double third = Math.Max(runwayLength, 0.3) / 3;
            double needed = third / Math.Max(30, member.Class.FinalSpeedKt) * 3600;
            // Both landing to a full stop (the user, 4 Nov): the one ahead brakes, so more: reduced rwy separation (it has rolled
            // a third of the runway, or reached its exit before) or full (it has left the runway).
            if (member.Circuit.End == EndAction.FullStop) needed = Math.Max(needed, RollSeconds(member.Class, aerodrome?.FullRunwaySeparation == true));
            double extra = Math.Max(0, needed - intervalSeconds) * Math.Max(30, member.Class.CircuitSpeedKt) / 3600 / 2;
            if (extra <= 0.01) return;
            double shift = extra * n + SpacingMarginNM * n;
            List<RoutePoint> route = member.Route.ToList();
            int end = route.FindLastIndex(p => p.Name == "DOWNWIND" && !p.Turn180);
            if (end < 0 || end + 1 >= route.Count || end < member.RouteNext - 1) return;
            for (int i = end; i <= end + 1; i++)
            {
                (double along, double lateral) = RunwayFrame(route[i].X, route[i].Y, side);
                (double x, double y) = FromRunway(along - shift, lateral, side);
                route[i] = route[i] with { X = x, Y = y };
            }
            member.Route = route.ToArray();
        }

        /// <summary>
        /// The seconds the one ahead needs after its touchdown (on the aim point, at the final speed): to roll a third of the
        /// runway (or to its exit, if it leaves before), or to be off the runway (full: to its exit and the turn off).
        /// </summary>
        private double RollSeconds(PerformanceClass kind, bool full)
        {
            double course = double.IsNaN(glidePathCourse) ? 0 : glidePathCourse * Math.PI / 180;
            double length = Math.Max(runwayLength, 0.3);
            double braking = Math.Max(0.5, kind.BrakingKtPerSecond);
            double speed = Math.Max(kind.FinalSpeedKt, ExitSpeed);
            double slow = TouchdownAim + (speed * speed - ExitSpeed * ExitSpeed) / (2 * braking) / 3600;
            var exits = runwayExits.Select(e => (e.Name, Along: e.X * Math.Sin(course) + e.Y * Math.Cos(course), Lateral: e.X * Math.Cos(course) - e.Y * Math.Sin(course)))
                .Where(e => e.Along > TouchdownAim && e.Along <= length + 0.05 && Math.Abs(e.Lateral) < 0.3).OrderBy(e => e.Along).ToList();
            var chosen = exits.FirstOrDefault(e => e.Along >= slow - 0.01);
            if (chosen.Name == null && exits.Count > 0) chosen = exits[^1];
            double exitAlong = chosen.Name != null ? chosen.Along : Math.Min(slow, length);
            double target = full ? exitAlong : Math.Min(TouchdownAim + length / 3, exitAlong);
            // Its roll, step by step (as LandingRoll: braking to the taxi speed, then on at it).
            double s = TouchdownAim, v = speed, t = 0;
            while (s < target && t < 300)
            {
                v = Math.Max(ExitSpeed, v - braking * Step);
                s += v * Step / 3600;
                t += Step;
            }
            return t + (full && chosen.Name != null ? ExitTurnSeconds : 0);
        }

        /// <summary>A little more than needed (NM per member): the turns and the slowing down on the final.</summary>
        private const double SpacingMarginNM = 0.05;

        /// <summary>The n-th place of the echelon: behind and outside the leader (away from the side of the turns: a left-hand circuit, to the right).</summary>
        private static void Echelon(SimAircraft member, SimAircraft lead, int n)
        {
            double h = lead.HeadingTrue * Math.PI / 180;
            double s = (lead.Circuit?.Side ?? CircuitSide.Left) == CircuitSide.Left ? 1 : -1;
            member.X = lead.X - EchelonNM * n * Math.Sin(h) + s * EchelonNM * n * Math.Cos(h);
            member.Y = lead.Y - EchelonNM * n * Math.Cos(h) - s * EchelonNM * n * Math.Sin(h);
        }

        /// <summary>A member of a formation as an aircraft of its own: the leader's state, its own callsign.</summary>
        private static SimAircraft Member(SimAircraft lead, string callsign)
        {
            SimAircraft member = lead.Copy();
            member.Callsign = callsign;
            if (member.Circuit != null)
            {
                // Its own legs (a 180° turn of its own), no orbit of the leader's.
                member.Circuit.LegIndex = -1;
                member.Circuit.Orbit = 0;
                member.Circuit.OrbitForever = false;
                member.Circuit.Turning = false;
                member.Circuit.Spiral = false;
            }
            return member;
        }

        /// <summary>Straight on (heading and height kept) for these seconds, then its circuit again.</summary>
        private void Delay(SimAircraft plane, double seconds)
        {
            if (plane.Circuit == null || seconds <= 0) return;
            plane.Circuit.DelayUntilStep = steps + (long)Math.Round(seconds / Step);
            plane.Circuit.DelayHeading = plane.HeadingTrue;
            plane.Navigation = NavigationMode.Manual;
            plane.TargetHeading = plane.HeadingTrue;
            plane.TurnDirection = TurnDirection.Shortest;
        }

        /// <summary>In CircuitStep: straight on while delayed (true); at its end back onto its circuit.</summary>
        private bool Delayed(SimAircraft plane, CircuitState circuit)
        {
            if (circuit.DelayUntilStep <= 0) return false;
            if (circuit.Suspended)
            {
                // The instructor took it meanwhile (a heading): his.
                circuit.DelayUntilStep = 0;
                return false;
            }
            if (steps < circuit.DelayUntilStep)
            {
                plane.Navigation = NavigationMode.Manual;
                plane.TargetHeading = circuit.DelayHeading;
                return true;
            }
            circuit.DelayUntilStep = 0;
            if (plane.Route != null && !circuit.Suspended && !circuit.Departed)
            {
                plane.Navigation = NavigationMode.Route;
                plane.LegStartX = plane.X;
                plane.LegStartY = plane.Y;
                plane.RouteApproached = false;
                circuit.LegIndex = -1;
            }
            else if (circuit.Departed && !circuit.Suspended && circuit.DepartPhase < 3)
            {
                // A departure (a wingman after the split): on along its way again (to the radial, or along it).
                if (circuit.DepartPhase == 1)
                {
                    plane.Navigation = NavigationMode.Manual;
                    plane.TargetHeading = circuit.DepartIntercept;
                    // (To a gate or a point its turn on its side, also the long way, the review of 7 Nov.)
                    plane.TurnDirection = double.IsFinite(circuit.ExitX) ? circuit.DepartTurn : TurnDirection.Shortest;
                }
                else if (plane.Route != null)
                {
                    plane.Navigation = NavigationMode.Route;
                    plane.LegStartX = plane.X;
                    plane.LegStartY = plane.Y;
                    plane.RouteApproached = false;
                }
            }
            return false;
        }

        /// <summary>
        /// On the short final: the members chosen land together (from a circuit at most 2), the others go around alone into
        /// the close pattern, one after the other. True: this aircraft (the leader) went around.
        /// </summary>
        private bool LandingSplit(SimAircraft plane, CircuitState circuit)
        {
            List<string> members = [plane.Callsign, .. plane.Wingmen];
            int cap = circuit.StraightIn ? members.Count : MaxCircuitFormationLanding;
            List<int> landing = Enumerable.Range(0, members.Count).Where(i => i >= plane.Lands.Count || plane.Lands[i]).Take(cap).ToList();
            List<int> going = Enumerable.Range(0, members.Count).Where(i => !landing.Contains(i)).ToList();
            if (going.Count == 0) return false;
            List<SimAircraft> made = members.Select((name, i) => i == 0 ? plane : Member(plane, name)).ToList();
            // (Going around one after the other: at least 5 s apart, room on the downwind.)
            double interval = Math.Max(plane.SplitSeconds, 5);
            // The landing ones: a formation led by the first of them.
            if (landing.Count > 0)
            {
                SimAircraft lead = made[landing[0]];
                lead.Wingmen = landing.Skip(1).Select(i => members[i]).ToList();
                lead.Lands = landing.Select(_ => true).ToList();
            }
            for (int k = 0; k < going.Count; k++)
            {
                SimAircraft goer = made[going[k]];
                goer.Wingmen = [];
                goer.Lands = [];
                CircuitState mine = goer.Circuit!;
                mine.NextType = CircuitType.Close;
                mine.NextSide = aerodrome?.SideOf(aerodromeRunway, CircuitType.Close) ?? mine.Side;
                StartGoAround(goer);
                goer.VerticalMode = VerticalMode.Altitude;
                goer.TargetAltitudeFt = runwayElevation + CircuitHeight(CircuitType.Close, goer.Class);
                goer.TargetSpeedKt = Math.Clamp(goer.Class.CircuitSpeedKt, goer.Class.MinSpeedKt, goer.Class.MaxSpeedKt);
                // Side by side on the climb out, each turning to the downwind one interval after the one before.
                if (going[k] != 0) Echelon(goer, plane, k + 1);
                mine.TurnWaitSeconds = interval * k;
                Spacing(goer, k, interval);
            }
            // Into the traffic: the ones going around and the leader of the landing ones (the others stay its wingmen).
            for (int i = 1; i < made.Count; i++) if (going.Contains(i) || (landing.Count > 0 && i == landing[0])) spawned.Add(made[i]);
            return going.Contains(0);
        }
    }
}
