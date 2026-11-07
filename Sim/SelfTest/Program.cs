using System.Globalization;
using System.Text.Json;
using ATCRoom.Sim;
using AuroraPAR;

// Self-test of the simulation engine (ATCRoom.Sim). Run by the GitHub build: any failure gives exit code 1.
// Times in the checks follow from the fixed steps of 0.1 s: an instruction is carried out at the start of the next step,
// the turn rate changes by 1.5°/s per second (rate one in 2 s), the vertical speed by the class's vertical acceleration
// (Turboprop, the default class: 400 ft/min per second).

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
int failures = 0;
int checks = 0;

void Check(bool ok, string name, string detail = "")
{
    checks++;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")}  {name}{(detail.Length > 0 ? "  (" + detail + ")" : "")}");
}

bool Near(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance;
bool Between(double value, double low, double high) => value >= low && value <= high;

// A runway like LIBN 32 (threshold of runways.par, heading for AuroraPAR).
const double ThresholdLatitude = 40.23227088;
const double ThresholdLongitude = 18.13907997;
const double RunwayHeading = 325.80;

// --- Turns -------------------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 90);
    engine.RunFor(1);
    AircraftState s = engine.Get(a)!;
    Check(Near(s.TurnRateDps, 1.5, 1e-9), "Roll-in: half of rate one after 1 s", $"rate {s.TurnRateDps:F3}");
    double highest = 0;
    for (int i = 0; i < 285; i++)
    {
        engine.RunFor(0.1);
        highest = Math.Max(highest, engine.Get(a)!.HeadingTrue);
    }
    s = engine.Get(a)!;
    Check(Near(s.TurnRateDps, 3, 1e-9) && Between(s.HeadingTrue, 85, 86), "Turn right 90° at 3°/s: full rate at 29.5 s", $"heading {s.HeadingTrue:F2}");
    for (int i = 0; i < 15; i++)
    {
        engine.RunFor(0.1);
        highest = Math.Max(highest, engine.Get(a)!.HeadingTrue);
    }
    s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 88, 90) && s.TurnRateDps > 0 && s.TurnRateDps < 3, "Turn right 90°: rolling out at 31 s", $"heading {s.HeadingTrue:F2} rate {s.TurnRateDps:F2}");
    for (int i = 0; i < 20; i++)
    {
        engine.RunFor(0.1);
        highest = Math.Max(highest, engine.Get(a)!.HeadingTrue);
    }
    s = engine.Get(a)!;
    Check(s.HeadingTrue == 90 && s.TurnRateDps == 0 && highest <= 90, "Turn right 90°: exactly on 090 by 33 s, never past it", $"heading {s.HeadingTrue:R} highest {highest:R}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 90, TurnDirection.Left);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 330, 336) && s.TurnRateDps < 0, "Turn to 090 forced left: through 333 after 10 s", $"heading {s.HeadingTrue:F2}");
    engine.RunFor(85);
    s = engine.Get(a)!;
    Check(s.HeadingTrue == 90 && s.TurnRateDps == 0 && s.TurnDirection == TurnDirection.Shortest, "Turn to 090 forced left: on 090 by 95 s, side back to shortest", $"heading {s.HeadingTrue:R}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 350, 150);
    engine.SetHeading(a, 10);
    engine.RunFor(3);
    AircraftState s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 352, 358) && s.TurnRateDps > 0, "Shortest turn across north: 350 to 010 turns right", $"heading {s.HeadingTrue:F2}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetTurnRateMode(a, TurnRateMode.Half);
    engine.SetHeading(a, 30);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 13.5, 15) && Near(s.TurnRateDps, 1.5, 1e-9), "Half rate turn: about 14° in 10 s at 1.5°/s", $"heading {s.HeadingTrue:F2}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 10000, 0, 250, null, "Jet");
    engine.SetHeading(a, 120);
    engine.RunFor(20);
    AircraftState s = engine.Get(a)!;
    double expected = SimEngine.RateForBank(30, 250);
    Check(Near(s.TurnRateDps, expected, 1e-6) && Near(expected, 2.52, 0.01), "Jet at 250 kt asked rate one: bank limit 30°, about 2.5°/s", $"rate {s.TurnRateDps:F3}");
}

// --- Altitude and speed ------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 2000, 0, 150);
    engine.SetAltitude(a, 3000, 1000);
    double highest = 0;
    for (int i = 0; i < 590; i++)
    {
        engine.RunFor(0.1);
        highest = Math.Max(highest, engine.Get(a)!.AltitudeFt);
    }
    AircraftState s = engine.Get(a)!;
    Check(s.AltitudeFt < 3000 && Near(s.VerticalSpeedFpm, 1000, 1e-9), "Climb 1000 ft at 1000 ft/min: still climbing at 59 s", $"altitude {s.AltitudeFt:F1}");
    for (int i = 0; i < 60; i++)
    {
        engine.RunFor(0.1);
        highest = Math.Max(highest, engine.Get(a)!.AltitudeFt);
    }
    s = engine.Get(a)!;
    Check(s.AltitudeFt == 3000 && s.VerticalSpeedFpm == 0 && highest <= 3000, "Climb 1000 ft: smooth level-off, level at 3000 by 65 s, never above", $"highest {highest:R}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 180);
    engine.SetSpeed(a, 140);
    engine.RunFor(39.5);
    AircraftState s = engine.Get(a)!;
    Check(s.SpeedKt > 140 && s.SpeedKt < 141, "Slow down 180 to 140 kt at 1 kt/s: 140.5 at 39.5 s", $"speed {s.SpeedKt:F3}");
    engine.RunFor(0.5);
    s = engine.Get(a)!;
    Check(s.SpeedKt == 140, "Slow down 180 to 140 kt at 1 kt/s: 140 at 40 s", $"speed {s.SpeedKt:R}");
    engine.SetSpeed(a, 170);
    engine.RunFor(20);
    s = engine.Get(a)!;
    Check(s.SpeedKt == 170, "Speed up 140 to 170 kt at 1.5 kt/s: 170 at 20 s", $"speed {s.SpeedKt:R}");
}

// --- Movement and wind -------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string north = engine.Add(0, 0, 3000, 0, 120);
    string east = engine.Add(0, 0, 3000, 90, 120);
    engine.RunFor(60);
    AircraftState n = engine.Get(north)!;
    AircraftState e = engine.Get(east)!;
    Check(Near(n.Y, 2, 1e-9) && Near(n.X, 0, 1e-9), "Straight north at 120 kt: 2 NM in 1 min", $"x {n.X:G6} y {n.Y:G10}");
    Check(Near(e.X, 2, 1e-9) && Near(e.Y, 0, 1e-9), "Straight east at 120 kt: 2 NM in 1 min", $"x {e.X:G10} y {e.Y:G6}");
}

{
    SimEngine engine = new();
    engine.SetWind(270, 20, 0);
    string a = engine.Add(0, 0, 3000, 0, 140);
    engine.RunFor(60);
    AircraftState s = engine.Get(a)!;
    double drift = Math.Atan2(20, 140) * 180 / Math.PI;
    Check(Near(s.DriftDegrees, drift, 1e-6) && Near(s.GroundSpeedKt, Math.Sqrt(140 * 140 + 20 * 20), 1e-6) && Near(s.X, 20.0 / 60, 1e-6) && Near(s.Y, 140.0 / 60, 1e-6),
        "Wind from 270 at 20 kt, heading 360: drifts 8.1° right, 1/3 NM east in 1 min", $"drift {s.DriftDegrees:F3} x {s.X:F4}");
}

{
    SimEngine engine = new();
    engine.SetGlidePath(0, 3);
    engine.SetWind(0, 20, 0);
    string a = engine.Add(0, -10, 3000, 0, 140);
    engine.SetGlidePathRate(a);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    double headwind = -120 * SimEngine.FeetPerNM / 60 * Math.Tan(3 * Math.PI / 180);
    Check(Near(s.VerticalSpeedFpm, headwind, 1e-6) && Near(s.GroundSpeedKt, 120, 1e-6), "Normal rate with 20 kt of headwind: as at 120 kt over the ground", $"VS {s.VerticalSpeedFpm:F1}");
}

{
    IReadOnlyList<AircraftState> Gusty()
    {
        SimEngine engine = new();
        engine.SetWind(250, 15, 10);
        string a = engine.Add(0, 0, 3000, 0, 140);
        engine.RunFor(30);
        return engine.Snapshot();
    }
    IReadOnlyList<AircraftState> first = Gusty();
    IReadOnlyList<AircraftState> second = Gusty();
    Check(first.SequenceEqual(second) && first[0].GroundSpeedKt != 140, "Gusts: random but the same in two runs");
}

// --- Ground, landing ---------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    engine.SetGlidePath(0, 3, 500, 1.5);
    double onPath = 500 + 3 * SimEngine.FeetPerNM * Math.Tan(3 * Math.PI / 180);
    string a = engine.Add(0, -3, onPath, 0, 140);
    engine.SetGlidePathRate(a);
    bool landed = false;
    for (int i = 0; i < 2400 && engine.Get(a) is AircraftState state; i++)
    {
        if (state.Contact == GroundContact.Landed) landed = true;
        engine.RunFor(0.1);
    }
    Check(landed && engine.Get(a) == null, "Normal rate from 3 NM on the glide path: lands on the runway, removed a few seconds later");

    string b = engine.Add(3, -5, 600, 0, 140);
    engine.SetVerticalSpeed(b, -1000);
    engine.RunFor(20);
    AircraftState s = engine.Get(b)!;
    Check(s.Contact == GroundContact.OnGround && s.AltitudeFt == 500 && s.GroundSpeedKt == 0, "Descending 3 NM beside the runway: stops on the ground at the runway elevation (500 ft)");
    engine.RunFor(20);
    Check(engine.Get(b) is AircraftState still && still.X == s.X && still.Y == s.Y, "On the ground outside the runway: stays there until removed");

    string c = engine.Add(-5, 0, 2000, 0, 140);
    engine.SetAltitude(c, 300);
    engine.RunFor(0.1);
    Check(engine.Get(c)!.TargetAltitudeFt == 500, "Altitude below the runway: kept at the runway elevation");
}

{
    SimEngine engine = new();
    HashSet<string> flying = new(StringComparer.OrdinalIgnoreCase) { "SIM1", "AZA123" };
    string a = engine.Add(0, 0, 3000, 0, 140, null, null, flying.Contains);
    string b = engine.Add(0, 0, 3000, 0, 140, "aza123", null, flying.Contains);
    Check(a == "SIM2" && b == "AZA123A", "Callsigns already flying outside the simulation are skipped (SIM2) or get a letter (AZA123A)", $"{a} {b}");
    engine.Clear();
    Check(engine.Add(0, 0, 3000, 0, 140) == "SIM1", "After Remove all the callsigns start again from SIM1");
}

// --- Performance classes -----------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string light = engine.Add(0, 0, 3000, 0, 40, null, "Light");
    AircraftState s = engine.Get(light)!;
    Check(s.SpeedKt == 60 && s.PerformanceClass == "Light", "Light aircraft added at 40 kt: kept at its minimum, 60 kt", $"speed {s.SpeedKt:R}");
    engine.SetSpeed(light, 250);
    engine.SetVerticalSpeed(light, -3000);
    engine.RunFor(0.1);
    s = engine.Get(light)!;
    Check(s.TargetSpeedKt == 160 && s.CommandedVerticalSpeedFpm == -1500, "Light aircraft: speed and descent kept within the class (160 kt, 1500 ft/min)",
        $"speed {s.TargetSpeedKt:R} VS {s.CommandedVerticalSpeedFpm:R}");
    string other = engine.Add(0, 0, 3000, 0, 150, null, "Glider");
    Check(engine.Get(other)!.PerformanceClass == PerformanceClass.DefaultName, "Unknown class: the Turboprop");
}

{
    string folder = Path.Combine(Path.GetTempPath(), "atcroom-sim-selftest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    string path = Path.Combine(folder, PerformanceFile.FileName);
    try
    {
        var created = PerformanceFile.Load(path);
        Check(File.Exists(path) && created.Classes.Count == 4 && !created.ReadOnly && created.Message == null, "performance.json missing: written with the 4 default classes");
        var again = PerformanceFile.Load(path);
        Check(again.Classes.Select(c => c.Name).SequenceEqual(new[] { "Light", "Turboprop", "Jet", "Helicopter" }), "performance.json read back: the same classes");

        List<PerformanceClass> defaults = PerformanceClass.Defaults();
        Check(defaults[1].ApproachSpeedKt == 140 && defaults[1].FinalSpeedKt == 110 && defaults[2].ApproachSpeedKt == 180 && defaults[2].FinalSpeedKt == 140,
            "Default speeds: Turboprop 140 / final 110, Jet 180 / final 140");

        string old = "{ \"FormatVersion\": 1, \"Classes\": [ { \"Name\": \"Light\", \"MinSpeedKt\": 60, \"MaxSpeedKt\": 160, \"ApproachSpeedKt\": 90 }, "
            + "{ \"Name\": \"Turboprop\", \"MinSpeedKt\": 90, \"MaxSpeedKt\": 280, \"ApproachSpeedKt\": 120 }, "
            + "{ \"Name\": \"Jet\", \"MinSpeedKt\": 120, \"MaxSpeedKt\": 350, \"ApproachSpeedKt\": 150 }, "
            + "{ \"Name\": \"Heavy\", \"MinSpeedKt\": 130, \"MaxSpeedKt\": 340, \"ApproachSpeedKt\": 160 } ] }";
        File.WriteAllText(path, old);
        var upgraded = PerformanceFile.Load(path);
        var reread = PerformanceFile.Load(path);
        Check(upgraded.Classes[0].FinalSpeedKt == 70 && upgraded.Classes[1].ApproachSpeedKt == 140 && upgraded.Classes[1].FinalSpeedKt == 110
            && upgraded.Classes[2].ApproachSpeedKt == 150 && upgraded.Classes[2].FinalSpeedKt == 140
            && Near(upgraded.Classes[3].FinalSpeedKt, 140, 1e-9) && File.ReadAllText(path).Contains("FinalSpeedKt")
            && reread.Classes[1].ApproachSpeedKt == 140 && reread.Classes[2].ApproachSpeedKt == 150,
            "performance.json before the final speed: final speeds added, initial speeds raised only where never changed, file written once",
            string.Join(" ", upgraded.Classes.Select(c => $"{c.Name} {c.ApproachSpeedKt}/{c.FinalSpeedKt}")));

        string newer = "{ \"FormatVersion\": 2, \"Future\": true, \"Classes\": [ { \"Name\": \"Heavy\", \"MinSpeedKt\": 140, \"MaxSpeedKt\": 340, \"ApproachSpeedKt\": 150, \"Wake\": \"H\" } ] }";
        File.WriteAllText(path, newer);
        var future = PerformanceFile.Load(path);
        Check(future.ReadOnly && future.Message != null && future.Classes.Count == 1 && future.Classes[0].Name == "Heavy" && File.ReadAllText(path) == newer,
            "performance.json of a newer version: used, never written");

        PerformanceFile? parsed = JsonSerializer.Deserialize<PerformanceFile>(newer, PerformanceFile.JsonOptions);
        string written = JsonSerializer.Serialize(parsed, PerformanceFile.JsonOptions);
        Check(written.Contains("\"Future\"") && written.Contains("\"Wake\""), "Unknown fields kept when the file is written again");

        File.WriteAllText(path, "{ not json");
        var damaged = PerformanceFile.Load(path);
        Check(File.Exists(path + ".bad") && damaged.Classes.Count == 4 && damaged.Message != null, "Damaged performance.json: kept as .bad, defaults written");
    }
    finally
    {
        try { Directory.Delete(folder, true); } catch (Exception) { }
    }
}

// --- METAR wind ----------------------------------------------------------------------------------------------------

{
    bool ok1 = MetarWind.TryParse("LIBN 181650Z 27015G25KT 9999 FEW030 18/12 Q1015", out double f1, out double s1, out double g1);
    bool ok2 = MetarWind.TryParse("LIRF 181650Z VRB03KT CAVOK", out double f2, out double s2, out double g2);
    bool ok3 = MetarWind.TryParse("UUEE 181650Z 27008MPS 9999", out double f3, out double s3, out double g3);
    bool ok4 = MetarWind.TryParse("LIBN 181650Z 9999 CAVOK", out _, out _, out _);
    Check(ok1 && f1 == 270 && s1 == 15 && g1 == 10 && ok2 && double.IsNaN(f2) && s2 == 3 && g2 == 0 && ok3 && f3 == 270 && Near(s3, 15.55, 0.01) && g3 == 0 && !ok4,
        "METAR wind: knots with gusts, variable, metres per second, none");
    Check(MetarWind.WindAndQnh("METAR LIPI 031250Z 06012G22KT 020V100 9999 FEW030 15/08 Q1018 NOSIG") == "LIPI 031250Z 06012G22KT 020V100 Q1018"
        && MetarWind.WindAndQnh("KJFK 031251Z 27008KT 10SM CLR 12/03 A2992") == "KJFK 031251Z 27008KT A2992" && MetarWind.WindAndQnh("  ") == "",
        "METAR: only the wind and the QNH (station, time, wind, its variable directions, QNH)");
    SimEngine vrb = new();
    vrb.SetWind(double.NaN, 4, 0);
    double vrbFirst = vrb.Wind.FromTrue, vrbMost = 0, vrbStep = 0;
    for (int i = 0; i < 300; i++)
    {
        double before = vrb.Wind.FromTrue;
        vrb.RunFor(1);
        vrbStep = Math.Max(vrbStep, Math.Abs(SimEngine.Wrap(vrb.Wind.FromTrue - before)));
        vrbMost = Math.Max(vrbMost, Math.Abs(SimEngine.Wrap(vrb.Wind.FromTrue - vrbFirst)));
    }
    Check(vrb.WindVariable && vrb.Wind.SpeedKt == 4 && vrbMost > 20 && vrbStep <= 2.01,
        "Variable wind (VRB): its direction changes slowly (at most 2° a second)", $"{vrbMost:F0}° from the first in 5 minutes, {vrbStep:F1}°/s at most");
}

// --- Clock, pause, freeze, instructions -----------------------------------------------------------------------------

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 120);
    engine.Advance(0.25);
    Check(Near(engine.Time, 0.2, 1e-12), "Advance 0.25 s: two whole steps", $"time {engine.Time:R}");
    engine.Advance(0.05);
    Check(Near(engine.Time, 0.3, 1e-12), "Advance 0.05 s more: the rest makes a third step", $"time {engine.Time:R}");
    engine.Advance(10);
    Check(Near(engine.Time, 2.3, 1e-9), "Advance 10 s at once: at most 2 s made up", $"time {engine.Time:R}");

    engine.Paused = true;
    AircraftState before = engine.Get(a)!;
    engine.SetHeading(a, 180);
    engine.Advance(1);
    AircraftState after = engine.Get(a)!;
    Check(after.X == before.X && after.Y == before.Y && Near(engine.Time, 2.3, 1e-9) && after.TargetHeading == 180,
        "Paused: nothing moves, the instruction is taken", $"time {engine.Time:R} target {after.TargetHeading:R}");
    engine.Paused = false;

    engine.SetFrozen(a, true);
    before = engine.Get(a)!;
    engine.RunFor(5);
    after = engine.Get(a)!;
    Check(after.X == before.X && after.Y == before.Y && after.HeadingTrue == before.HeadingTrue && after.Frozen, "Frozen aircraft: holds position and heading");
    engine.SetFrozen(a, false);
    engine.RunFor(1);
    Check(engine.Get(a)!.HeadingTrue != before.HeadingTrue, "Unfrozen aircraft: goes on with its instruction");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 120);
    engine.SetHeading(a, 90);
    AircraftState s = engine.Get(a)!;
    Check(s.TargetHeading == 0, "Instruction queued: not carried out before the next step");
    engine.RunFor(0.1);
    s = engine.Get(a)!;
    Check(s.TargetHeading == 90 && s.HeadingTrue > 0 && s.HeadingTrue < 0.1, "Instruction queued: carried out at the start of the next step", $"heading {s.HeadingTrue:R}");
}

// --- Callsigns ---------------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 120);
    string b = engine.Add(0, 0, 3000, 0, 120);
    engine.CallsignPrefix = "atc";
    string c = engine.Add(0, 0, 3000, 0, 120);
    Check(a == "SIM1" && b == "SIM2" && c == "ATC3", "Callsigns given in order, prefix configurable", $"{a} {b} {c}");
    engine.SetSpeed(b, 200);
    bool renamed = engine.Rename(b, "TST22");
    engine.RunFor(0.1);
    AircraftState? renamedState = engine.Get("TST22");
    Check(renamed && renamedState != null && renamedState.TargetSpeedKt == 200 && engine.Get(b) == null,
        "Rename: new callsign, queued instruction follows the aircraft");
    Check(!engine.Rename(a, "ATC3"), "Rename refused when the callsign is taken");
    bool refused = false;
    try { engine.Add(0, 0, 3000, 0, 120, "sim1"); } catch (InvalidOperationException) { refused = true; }
    Check(refused, "Add refuses a callsign already in the simulation (not case sensitive)");
    engine.Remove(a);
    Check(engine.Count == 2 && !engine.IsCallsignTaken("SIM1"), "Remove");
}

// --- Pilot reaction ----------------------------------------------------------------------------------------------

{
    SimEngine engine = new() { ReactionSeconds = 1 };
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 90);
    engine.RunFor(0.9);
    AircraftState s = engine.Get(a)!;
    Check(s.TargetHeading == 0 && s.HeadingTrue == 0 && s.PendingCommands == 1, "Reaction 1 s: nothing carried out after 0.9 s", $"pending {s.PendingCommands}");
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.TargetHeading == 90 && s.HeadingTrue > 0 && s.PendingCommands == 0, "Reaction 1 s: carried out by 1.1 s", $"heading {s.HeadingTrue:F2}");
}

{
    SimEngine engine = new() { ReactionSeconds = 1 };
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 10);
    engine.RunFor(0.5);
    engine.SetHeading(a, 20);
    engine.SetSpeed(a, 160);
    engine.RunFor(0.9);
    AircraftState s = engine.Get(a)!;
    Check(s.TargetHeading == 0 && s.PendingCommands == 2, "Reaction: a new heading replaces the one still waiting (wheel steps)", $"target {s.TargetHeading:R} pending {s.PendingCommands}");
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.TargetHeading == 20 && s.TargetSpeedKt == 160 && s.PendingCommands == 0, "Reaction: the last heading and the speed carried out 1 s after they were given");
}

// --- Vertical modes ----------------------------------------------------------------------------------------------

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetVerticalSpeed(a, -700);
    engine.RunFor(60);
    AircraftState s = engine.Get(a)!;
    Check(Between(s.AltitudeFt, 2305, 2315) && s.VerticalMode == VerticalMode.Rate && s.VerticalSpeedFpm == -700, "Rate −700 ft/min kept: about 690 ft lower after 1 min (2 s to reach the rate)", $"altitude {s.AltitudeFt:F1}");
    engine.SetAltitude(a, 2500, 500);
    engine.RunFor(40);
    s = engine.Get(a)!;
    Check(s.AltitudeFt == 2500 && s.VerticalSpeedFpm == 0 && s.VerticalMode == VerticalMode.Altitude, "Then altitude 2500 at 500 ft/min: level by 40 s", $"altitude {s.AltitudeFt:R}");
}

{
    SimEngine engine = new();
    engine.SetGlidePath(0, 3);
    string a = engine.Add(0, -10, 3000, 0, 140);
    double best = -140 * SimEngine.FeetPerNM / 60 * Math.Tan(3 * Math.PI / 180);
    engine.SetGlidePathRate(a);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    Check(Near(s.VerticalSpeedFpm, best, 1e-9) && Near(s.BestVerticalSpeedFpm, best, 1e-9) && s.VerticalMode == VerticalMode.GlidePath,
        "Normal rate on a 3° glide path at 140 kt: about −743 ft/min", $"VS {s.VerticalSpeedFpm:F1}");
    engine.SetSpeed(a, 120);
    engine.RunFor(30);
    s = engine.Get(a)!;
    double slower = -120 * SimEngine.FeetPerNM / 60 * Math.Tan(3 * Math.PI / 180);
    Check(Near(s.VerticalSpeedFpm, slower, 1e-9), "Normal rate follows the speed: about −637 ft/min at 120 kt", $"VS {s.VerticalSpeedFpm:F1}");
    engine.SetHeading(a, 90);
    engine.RunFor(40);
    s = engine.Get(a)!;
    Check(Near(s.BestVerticalSpeedFpm, 0, 1e-6), "Normal rate flying across the final: no descent needed", $"best {s.BestVerticalSpeedFpm:F3}");
}

// --- Stick -------------------------------------------------------------------------------------------------------

{
    SimEngine engine = new() { ReactionSeconds = 2 };
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 300);
    engine.SetStick(a, 1, 0);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 26, 28) && s.StickActive && s.PendingCommands == 0, "Stick right for 10 s: about 27° right (2 s to bank), the waiting heading cancelled", $"heading {s.HeadingTrue:F3}");
    engine.SetStick(a, 0, 0);
    engine.RunFor(10);
    s = engine.Get(a)!;
    Check(Between(s.HeadingTrue, 29.5, 30.5) && s.TurnRateDps == 0 && !s.StickActive, "Stick released: rolls out about 3° further and stays", $"heading {s.HeadingTrue:F3}");
    engine.SetStick(a, 0, -1);
    engine.RunFor(1);
    engine.SetStick(a, 0, 0);
    engine.RunFor(5);
    s = engine.Get(a)!;
    Check(Near(s.VerticalSpeedFpm, -600, 1e-6) && s.VerticalMode == VerticalMode.Rate, "Stick down 1 s: −600 ft/min, kept when released", $"VS {s.VerticalSpeedFpm:F3}");
}

{
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetTurnRateMode(a, TurnRateMode.Free);
    engine.SetStick(a, 1, 0);
    engine.RunFor(4);
    AircraftState s = engine.Get(a)!;
    Check(Near(s.TurnRateDps, 6, 0.16), "Free turn rate: 6°/s after 4 s held at the edge (more than rate one)", $"rate {s.TurnRateDps:F2}");
    engine.SetTurnRateMode(a, TurnRateMode.Half);
    Check(engine.Get(a)!.SelectedTurnRateDps == 1.5, "Half rate selected: 1.5°/s for the instructions");
}

// --- Same instructions, same flight --------------------------------------------------------------------------------

{
    IReadOnlyList<AircraftState> Run()
    {
        SimEngine engine = new() { ReactionSeconds = 0.7 };
        engine.SetGlidePath(325, 3);
        engine.SetWind(290, 12, 8);
        string a = engine.Add(-3, -8, 2500, 45, 160);
        string b = engine.Add(4, -6, 3000, 300, 140, null, "Jet");
        engine.RunFor(12.3);
        engine.SetHeading(a, 200, TurnDirection.Right);
        engine.SetAltitude(b, 1800, 700);
        engine.RunFor(40);
        engine.SetSpeed(a, 120);
        engine.SetTurnRate(b, 1.5);
        engine.SetHeading(b, 150);
        engine.SetGlidePathRate(a);
        for (int i = 0; i < 300; i++) engine.Advance(0.137);
        return engine.Snapshot();
    }
    IReadOnlyList<AircraftState> first = Run();
    IReadOnlyList<AircraftState> second = Run();
    Check(first.SequenceEqual(second), "Same instructions twice (with wind and gusts): identical result, bit for bit");
}

// --- Local plane on the radar's Earth ----------------------------------------------------------------------------

{
    LocalPlane plane = new(ThresholdLatitude, ThresholdLongitude, RunwayHeading);
    double worst = 0;
    for (int i = 0; i < 16; i++)
    {
        double bearing = i * 22.5 * Math.PI / 180;
        foreach (double distance in new[] { 0.5, 5, 15, 30 })
        {
            double x = distance * Math.Sin(bearing);
            double y = distance * Math.Cos(bearing);
            (double latitude, double longitude) = plane.ToGeo(x, y);
            (double x2, double y2) = plane.FromGeo(latitude, longitude);
            worst = Math.Max(worst, Math.Sqrt((x2 - x) * (x2 - x) + (y2 - y) * (y2 - y)) * 1852);
        }
    }
    Check(worst < 0.01, "Plane to latitude/longitude and back: same point within 1 cm up to 30 NM", $"worst {worst:G3} m");

    // On the extended centreline of the plane: what the radar measures (Aircraft.LateralOffset and Distance in Data.cs).
    double approach = (RunwayHeading + 180) * Math.PI / 180;
    double worstLateral = 0;
    double worstAlong = 0;
    foreach (double distance in new[] { 0.5, 2, 5, 10, 20, 30 })
    {
        (double latitude, double longitude) = plane.ToGeo(distance * Math.Sin(approach), distance * Math.Cos(approach));
        double radius = Earth.Radius(ThresholdLatitude, RunwayHeading);
        double bearing = Earth.Bearing(ThresholdLatitude, ThresholdLongitude, latitude, longitude) * Math.PI / 180;
        double measured = Earth.DistanceNM(ThresholdLatitude, ThresholdLongitude, latitude, longitude, radius);
        double lateral = Math.Asin(Math.Sin(measured * 1852 / radius) * Math.Sin(bearing - RunwayHeading * Math.PI / 180)) * radius;
        double along = Math.Sqrt(Math.Max(0, measured * measured - lateral / 1852 * lateral / 1852));
        worstLateral = Math.Max(worstLateral, Math.Abs(lateral));
        worstAlong = Math.Max(worstAlong, Math.Abs(along - distance) * 1852);
    }
    Check(worstLateral < 0.001, "On the extended centreline: lateral offset measured by the radar is zero", $"worst {worstLateral:G3} m");
    Check(worstAlong < 0.01, "On the extended centreline: distance measured by the radar is the same", $"worst {worstAlong:G3} m");
}

// --- Routes ------------------------------------------------------------------------------------------------------

double Distance(AircraftState s, double x, double y) => Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));

{
    // Three points: north 5 NM, then east 5 NM descending and slowing, then south 5 NM.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 120);
    engine.SetRoute(a, [new RoutePoint(0, 5, 3000, 140, "ONE"), new RoutePoint(5, 5, 2000, 120, "TWO"), new RoutePoint(5, 0, 2000, 120, "THREE")]);
    AircraftState s = engine.Get(a)!;
    Check(s.Route?.Count == 3 && s.RouteNext == 0 && s.RouteAltitude && s.RouteSpeed && s.TargetSpeedKt == 140 && s.TargetAltitudeFt == 3000,
        "Route given: three points, flying to the first, its speed at once", $"next {s.RouteNext} speed {s.TargetSpeedKt}");
    double[] nearest = [double.MaxValue, double.MaxValue, double.MaxValue];
    List<int> order = [0];
    bool descendingAtOnce = false;
    double secondsToTwo = double.NaN;
    for (int i = 0; i < 6000 && engine.Get(a)!.Navigation == NavigationMode.Route; i++)
    {
        engine.RunFor(0.1);
        s = engine.Get(a)!;
        nearest[0] = Math.Min(nearest[0], Distance(s, 0, 5));
        nearest[1] = Math.Min(nearest[1], Distance(s, 5, 5));
        nearest[2] = Math.Min(nearest[2], Distance(s, 5, 0));
        if (s.Navigation == NavigationMode.Route && s.RouteNext != order[^1]) order.Add(s.RouteNext);
        if (s.Navigation == NavigationMode.Route && s.RouteNext == 1 && double.IsNaN(secondsToTwo)) secondsToTwo = 0;
        if (!double.IsNaN(secondsToTwo) && secondsToTwo < 5)
        {
            secondsToTwo += 0.1;
            if (s.TargetAltitudeFt == 2000 && s.VerticalSpeedFpm < -100 && s.TargetSpeedKt == 120) descendingAtOnce = true;
        }
    }
    s = engine.Get(a)!;
    Check(string.Join(",", order) == "0,1,2" && s.Navigation == NavigationMode.Manual && s.RouteNext == 3, "Route: points flown in order, then MANUAL (the route kept)", $"order {string.Join(",", order)}");
    Check(nearest.All(d => d < 0.1), "Route: every point flown over (within 0.1 NM)", $"nearest {string.Join(" ", nearest.Select(d => d.ToString("F3")))}");
    Check(descendingAtOnce, "Route: past a point, descent and new speed for the next one start at once");
    engine.RunFor(120);
    s = engine.Get(a)!;
    Check(Near(s.HeadingTrue, 180, 0.01) && Near(s.AltitudeFt, 2000, 0.01) && Near(s.SpeedKt, 120, 0.01) && Near(s.X, 5, 0.05) && s.Y < -1,
        "After the last point: course of the last leg (180) kept, with its altitude and speed", $"heading {s.HeadingTrue:F2} x {s.X:F3} y {s.Y:F2} alt {s.AltitudeFt:F0}");
}

{
    // A point behind: it turns back to it, passes it (no circling) and keeps the course from where the route was given.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetRoute(a, [new RoutePoint(0, -3, 3000, 150)]);
    double nearest = double.MaxValue;
    double seconds = 0;
    while (engine.Get(a)!.Navigation == NavigationMode.Route && seconds < 600)
    {
        engine.RunFor(0.1);
        seconds += 0.1;
        nearest = Math.Min(nearest, Distance(engine.Get(a)!, 0, -3));
    }
    engine.RunFor(60);
    AircraftState s = engine.Get(a)!;
    Check(seconds < 300 && nearest < 0.1 && Near(s.HeadingTrue, 180, 0.01), "Route: a point behind is reached after turning back, then course 180 kept",
        $"{seconds:F0} s nearest {nearest:F3} heading {s.HeadingTrue:F2}");
}

{
    // A point inside the turn: left behind flying straight, then turned to and flown over (never circled).
    SimEngine engine = new();
    string a = engine.Add(0, 0, 5000, 0, 250, null, "Jet");
    engine.SetRoute(a, [new RoutePoint(0.6, 0.2, 5000, 250)]);
    double seconds = 0;
    double nearest = double.MaxValue;
    while (engine.Get(a)!.Navigation == NavigationMode.Route && seconds < 600)
    {
        engine.RunFor(0.1);
        seconds += 0.1;
        nearest = Math.Min(nearest, Distance(engine.Get(a)!, 0.6, 0.2));
    }
    Check(seconds < 200 && nearest < 0.1, "Route: a point inside the turn radius is flown over, not circled", $"{seconds:F0} s nearest {nearest:F3}");
}

{
    // Crosswind from the west: the track is pointed at the point, then kept along the leg.
    SimEngine engine = new();
    engine.SetWind(270, 20, 0);
    string a = engine.Add(0, 0, 3000, 0, 120);
    engine.SetRoute(a, [new RoutePoint(0, 10, 3000, 120)]);
    double nearest = double.MaxValue;
    for (int i = 0; i < 4000 && engine.Get(a)!.Navigation == NavigationMode.Route; i++)
    {
        engine.RunFor(0.1);
        nearest = Math.Min(nearest, Distance(engine.Get(a)!, 0, 10));
    }
    engine.RunFor(120);
    AircraftState s = engine.Get(a)!;
    double expected = SimEngine.Normalize(-Math.Asin(20.0 / 120) * 180 / Math.PI);
    Check(nearest < 0.1 && Near(s.HeadingTrue, expected, 0.05) && Near(SimEngine.Wrap(s.TrackTrue), 0, 0.05) && Near(s.X, 0, 0.1),
        "Route in a crosswind: point reached, then track 360 kept heading into the wind", $"nearest {nearest:F3} heading {s.HeadingTrue:F2} track {s.TrackTrue:F2} x {s.X:F3}");
}

{
    // Last two points on the extended centreline (final course 360): the aircraft goes on along it, no other capture.
    SimEngine engine = new();
    engine.SetGlidePath(360, 3, 0, 1.5);
    string a = engine.Add(-4, -12, 3000, 90, 140);
    engine.SetRoute(a, [new RoutePoint(0, -10, 3000, 140), new RoutePoint(0, -6, 2000, 130)]);
    for (int i = 0; i < 4000 && engine.Get(a)!.Navigation == NavigationMode.Route; i++) engine.RunFor(0.1);
    engine.RunFor(60);
    AircraftState s = engine.Get(a)!;
    Check(Near(s.HeadingTrue, 0, 0.01) || Near(s.HeadingTrue, 360, 0.01), "Route ending on the extended centreline: on along the final course", $"heading {s.HeadingTrue:F2}");
    Check(Near(s.X, 0, 0.1) && Near(s.AltitudeFt, 2000, 0.01) && s.VerticalMode == VerticalMode.Altitude,
        "Route ending on the extended centreline: on the centreline, level at the last altitude (no glide path capture)", $"x {s.X:F3} alt {s.AltitudeFt:F0}");
}

{
    // The route edited while flown, the same point next: the leg keeps its start, so the last course is the one of the leg.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 45, 120);
    engine.SetRoute(a, [new RoutePoint(0, 10, 3000, 120, "END")]);
    engine.RunFor(20);
    engine.SetRoute(a, [engine.Get(a)!.Route![0]]);
    for (int i = 0; i < 4000 && engine.Get(a)!.Navigation == NavigationMode.Route; i++) engine.RunFor(0.1);
    engine.RunFor(60);
    AircraftState s = engine.Get(a)!;
    Check(Near(s.HeadingTrue, 0, 0.01) || Near(s.HeadingTrue, 360, 0.01), "Route edited while flown, same point next: course of the leg from where it started", $"heading {s.HeadingTrue:F2}");

    // Values of the next point changed while flown: same leg; a point passed meanwhile and given again is not flown again.
    string b = engine.Add(10, 0, 3000, 0, 120);
    RoutePoint one = new(10, 2, 3000, 120, "ONE");
    RoutePoint two = new(10, 6, 3000, 120, "TWO");
    engine.SetRoute(b, [one, two]);
    engine.RunFor(10);
    engine.SetRoute(b, [one with { AltitudeFt = 2500 }, two]);
    s = engine.Get(b)!;
    Check(s.Route != null && s.RouteNext == 0 && s.TargetAltitudeFt == 2500, "Next point's altitude changed while flown: given at once", $"target {s.TargetAltitudeFt:F0}");
    for (int i = 0; i < 2000 && engine.Get(b)!.RouteNext == 0; i++) engine.RunFor(0.1);
    IReadOnlyList<RoutePoint> flown = engine.Get(b)!.Route!;
    engine.SetRoute(b, flown);
    s = engine.Get(b)!;
    Check(s.Route?.Count == 1 && s.Route[0].Name == "TWO", "Route given again with a point already passed: that point is left out", $"points {s.Route?.Count}");
    engine.SetHeading(b, 90);
    engine.SetRoute(b, [s.Route![0]]);
    engine.RunFor(0.2);
    Check(engine.Get(b)!.Navigation == NavigationMode.Manual && engine.Get(b)!.Route != null, "Route edited with the same place next: an instruction still waiting is kept (here a heading: MANUAL)");
}

{
    // Instructions and the route.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 120);
    engine.SetRoute(a, [new RoutePoint(0, 3, 4000, 150), new RoutePoint(3, 3, 5000, 160)]);
    engine.SetAltitude(a, 2500);
    engine.SetSpeed(a, 110);
    engine.RunFor(0.2);
    AircraftState s = engine.Get(a)!;
    Check(s.Route != null && !s.RouteAltitude && !s.RouteSpeed && s.TargetAltitudeFt == 2500 && s.TargetSpeedKt == 110,
        "Altitude and speed instructions take them over, the route goes on", $"target {s.TargetAltitudeFt:F0} ft {s.TargetSpeedKt:F0} kt");
    for (int i = 0; i < 3000 && engine.Get(a)!.RouteNext == 0; i++) engine.RunFor(0.1);
    engine.RunFor(1);
    s = engine.Get(a)!;
    Check(s.Route != null && s.RouteNext == 1 && s.TargetAltitudeFt == 2500 && s.TargetSpeedKt == 110,
        "Past a point, an altitude and speed taken over are kept", $"target {s.TargetAltitudeFt:F0} ft {s.TargetSpeedKt:F0} kt");
    engine.SetHeading(a, 270);
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.Navigation == NavigationMode.Manual && s.Route != null && s.TargetHeading == 270, "A heading instruction: MANUAL, the route kept", $"target {s.TargetHeading:F0}");

    string b = engine.Add(10, 0, 3000, 0, 120);
    engine.SetRoute(b, [new RoutePoint(10, 5, double.NaN, double.NaN)]);
    s = engine.Get(b)!;
    Check(s.TargetAltitudeFt == 3000 && s.TargetSpeedKt == 120, "Route point without altitude and speed: they are kept");
    engine.SetStick(b, 0.5, 0);
    engine.RunFor(0.1);
    engine.SetStick(b, 0, 0);
    Check(engine.Get(b)!.Navigation == NavigationMode.Manual && engine.Get(b)!.Route != null, "The stick sideways: MANUAL, the route kept");

    string c = engine.Add(20, 0, 3000, 0, 120);
    engine.SetHeading(c, 90);
    engine.SetRoute(c, [new RoutePoint(20, 5, 3000, 120)]);
    engine.RunFor(5);
    s = engine.Get(c)!;
    Check(s.Route != null && s.PendingCommands == 0 && Near(s.HeadingTrue, 0, 0.01), "A route drops the instructions still waiting", $"heading {s.HeadingTrue:F2}");
    engine.SetFrozen(c, true);
    double y = engine.Get(c)!.Y;
    engine.RunFor(10);
    Check(engine.Get(c)!.Y == y && engine.Get(c)!.Route != null, "Frozen: the route waits");
    engine.SetRoute(c, []);
    Check(engine.Get(c)!.Route == null && engine.Get(c)!.RouteNext == -1, "Empty route: no route");
}

{
    // MANUAL / ROUTE, direct to, next point.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    RoutePoint p1 = new(0, 5, 3000, 150, "P1");
    RoutePoint p2 = new(5, 10, 3000, 150, "P2");
    RoutePoint p3 = new(5, 15, 3000, 150, "P3");
    engine.SetRoute(a, [p1, p2, p3], follow: null);
    AircraftState s = engine.Get(a)!;
    Check(s.Navigation == NavigationMode.Manual && s.Route?.Count == 3, "A route given with follow null to an aircraft without a route: MANUAL, route kept", $"{s.Navigation}");
    Check(engine.SetNavigation(a, true) && engine.Get(a)!.Navigation == NavigationMode.RouteArmed, "ROUTE chosen: waiting for a point");
    engine.SetHeading(a, 0);
    engine.RunFor(0.2);
    Check(engine.Get(a)!.Navigation == NavigationMode.Manual, "A heading in ROUTE waiting: MANUAL");
    engine.SetNavigation(a, true);
    engine.RunFor(20);
    s = engine.Get(a)!;
    Check(s.Navigation == NavigationMode.RouteArmed && Near(s.HeadingTrue, 0, 1e-9), "ROUTE waiting for a point: the heading is kept", $"heading {s.HeadingTrue:F2}");
    engine.NextWaypoint(a);
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.Navigation == NavigationMode.Route && s.RouteNext == 0, "Next WPT while waiting: the next point not passed (P1)", $"next {s.RouteNext}");
    engine.NextWaypoint(a);
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.RouteNext == 1, "Next WPT while flying the route: the point after (P2)", $"next {s.RouteNext}");
    engine.DirectTo(a, 2);
    engine.RunFor(0.2);
    Check(engine.Get(a)!.RouteNext == 2, "Direct to a point of the route: P3");
    RoutePoint off = new(-5, 10, double.NaN, double.NaN, "OFF");
    engine.DirectToPoint(a, off);
    engine.RunFor(0.2);
    s = engine.Get(a)!;
    Check(s.Route?.Count == 4 && s.RouteNext == 2 && s.Route[2].Name == "OFF" && s.Route[3].Name == "P3", "Direct to a point off the route: it becomes the next point, then the rest", $"next {s.RouteNext}");
    double nearest = double.MaxValue;
    for (int i = 0; i < 9000 && engine.Get(a)!.Navigation == NavigationMode.Route; i++)
    {
        engine.RunFor(0.1);
        nearest = Math.Min(nearest, Distance(engine.Get(a)!, -5, 10));
    }
    s = engine.Get(a)!;
    Check(nearest < 0.1 && s.Navigation == NavigationMode.Manual && s.RouteNext == 4, "Off-route point flown over, then the rest, then MANUAL", $"nearest {nearest:F3} next {s.RouteNext}");
    engine.DirectTo(a, 0);
    engine.RunFor(0.2);
    Check(engine.Get(a)!.Navigation == NavigationMode.Route && engine.Get(a)!.RouteNext == 0, "Direct to a point already passed: flown again");
    Check(engine.SetNavigation(a, false) && engine.Get(a)!.Navigation == NavigationMode.Manual && engine.Get(a)!.Route?.Count == 4, "MANUAL chosen: the route kept");

    // The instructor's altitude rules from then on, also when the route is edited.
    string b = engine.Add(20, 0, 3000, 0, 150);
    engine.SetRoute(b, [new RoutePoint(20, 5, 5000, 150), new RoutePoint(20, 10, 6000, 150)]);
    engine.SetAltitude(b, 4000);
    engine.RunFor(0.2);
    engine.SetRoute(b, engine.Get(b)!.Route!.Select(p => p with { AltitudeFt = 7000 }).ToList());
    s = engine.Get(b)!;
    Check(!s.RouteAltitude && s.TargetAltitudeFt == 4000, "Altitude given: the route's altitudes no more, also after the route is changed", $"target {s.TargetAltitudeFt:F0}");
    Check(!engine.SetNavigation(engine.Add(30, 0, 3000, 0, 150), true), "ROUTE without a route: not possible");
}

{
    // Stop turn, stop climb / descent.
    SimEngine engine = new();
    string a = engine.Add(0, 0, 3000, 0, 150);
    engine.SetHeading(a, 90);
    engine.RunFor(10);
    double atStop = engine.Get(a)!.HeadingTrue;
    engine.StopTurn(a);
    engine.RunFor(10);
    AircraftState s = engine.Get(a)!;
    double settled = s.HeadingTrue;
    engine.RunFor(10);
    Check(s.TurnRateDps == 0 && settled > atStop && settled - atStop < 5 && engine.Get(a)!.HeadingTrue == settled && s.Navigation == NavigationMode.Manual,
        "Stop turn: rolls out a few degrees after, then holds that heading", $"stop at {atStop:F1}, holds {settled:F1}");

    string b = engine.Add(10, 0, 3000, 0, 150);
    engine.SetAltitude(b, 6000, 1000);
    engine.RunFor(30);
    double altitudeAtStop = engine.Get(b)!.AltitudeFt;
    engine.StopVertical(b);
    engine.RunFor(20);
    s = engine.Get(b)!;
    double level = s.AltitudeFt;
    engine.RunFor(20);
    Check(s.VerticalSpeedFpm == 0 && level > altitudeAtStop && level - altitudeAtStop < 60 && engine.Get(b)!.AltitudeFt == level,
        "Stop climb: levels off just above where it was, then holds it", $"stop at {altitudeAtStop:F0}, holds {level:F0}");
}

// --- Circuits (TWR Trainer): flown by the engine, checked, and drawn into circuits.html for the user ---------------

{
    // (Elevation 0: the heights of the classes are altitudes QNH since 7 Nov, the same as heights at sea level; test 60: higher.)
    const double CircuitCourse = 140, CircuitElevation = 0, CircuitLength = 1.0;
    List<(string Title, List<AircraftState> Track)> pictures = [];

    SimEngine NewCircuitEngine()
    {
        SimEngine created = new();
        created.SetGlidePath(CircuitCourse, 3, CircuitElevation, CircuitLength);
        return created;
    }
    (double Along, double Lateral) Frame(AircraftState st)
    {
        double c = CircuitCourse * Math.PI / 180;
        double sign = st.CircuitSide == CircuitSide.Right ? 1 : -1;
        return (st.X * Math.Sin(c) + st.Y * Math.Cos(c), sign * (st.X * Math.Cos(c) - st.Y * Math.Sin(c)));
    }
    List<AircraftState> Fly(SimEngine sim, string callsign, double seconds, Action<AircraftState>? each = null)
    {
        List<AircraftState> track = [];
        for (int t = 0; t < seconds; t++)
        {
            sim.RunFor(1);
            AircraftState? now = sim.Get(callsign);
            if (now == null) break;
            track.Add(now);
            each?.Invoke(now);
        }
        return track;
    }
    double Height(AircraftState st) => st.AltitudeFt - CircuitElevation;
    // The legs flown, for the details of a failure: each leg once, with its second, height and place.
    string Legs(List<AircraftState> track) => string.Join(" ", track.Select((st, i) => (st, i))
        .Where(q => q.i == 0 || q.st.Leg != track[q.i - 1].Leg || q.i == track.Count - 1)
        .Select(q => $"{q.st.Leg}@{q.i}s/{Height(q.st):F0}ft/{Frame(q.st).Along:F1},{Frame(q.st).Lateral:F1}{(q.st.Contact == GroundContact.Airborne ? "" : "/gnd")}").Take(40));
    // From the Low Key on, every 4 s: along, lateral, height, vertical speed, heading, ground speed (for the details).
    string FromLowKey(List<AircraftState> track) => From(track, "LOW KEY", 4);
    string From(List<AircraftState> track, string leg, int every) => string.Join(" ", track.SkipWhile(st => st.Leg != leg).Where((st, i) => i % every == 0).Take(45)
        .Select(st => $"[{st.Leg[0]}{Frame(st).Along:F2},{Frame(st).Lateral:F2},{Height(st):F0},{st.VerticalSpeedFpm:F0},{st.HeadingTrue:F0},{st.GroundSpeedKt:F0},{st.Contact}]"));
    List<AircraftState> OnLeg(List<AircraftState> track, string leg) => track.Where(st => st.Leg == leg).ToList();
    bool LandedOnRunway(List<AircraftState> track)
    {
        AircraftState? first = track.FirstOrDefault(st => st.Contact == GroundContact.Landed);
        return first != null && Between(Frame(first).Along, -0.2, CircuitLength) && Math.Abs(Frame(first).Lateral) < 0.1;
    }

    // The straight part of the downwind (heading within 10° of the opposite of the runway): its distance from the runway.
    List<double> StraightDownwind(List<AircraftState> track) => track
        .Where(st => st.Leg == "DOWNWIND" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse - 180)) < 10).Select(st => Frame(st).Lateral).ToList();
    bool Parallel(List<double> laterals, double width) => laterals.Count > 5 && Near(laterals.Average(), width, 0.2) && laterals.Max() - laterals.Min() < 0.25;
    string Describe(List<double> laterals) => laterals.Count > 0 ? $"{laterals.Average():F2} NM, from {laterals.Min():F2} to {laterals.Max():F2}" : "no straight downwind";
    (double X, double Y) Place(double along, double lateral, CircuitSide side)
    {
        double c = CircuitCourse * Math.PI / 180;
        double sign = side == CircuitSide.Right ? 1 : -1;
        return (along * Math.Sin(c) + sign * lateral * Math.Cos(c), along * Math.Cos(c) - sign * lateral * Math.Sin(c));
    }

    // Values of the classes: a class without circuit values gets those of its name.
    PerformanceClass bare = new() { Name = "Light", MinSpeedKt = 60, MaxSpeedKt = 160, ApproachSpeedKt = 90, FinalSpeedKt = 70 };
    bare.Normalize();
    Check(bare.CircuitHeightFt == 1000 && bare.CircuitSpeedKt == 90 && bare.DownwindNM == 1.0 && bare.CloseDownwindNM == 0.6 && bare.WideDownwindNM == 2.5
        && bare.RollOutNM == 0.5 && bare.RollOutHeightFt == 250,
        "Circuit values: a class written before them gets those of its name");
    PerformanceClass odd = new() { Name = "Light", CloseDownwindNM = 3, WideDownwindNM = 0.5 };
    odd.Normalize();
    Check(odd.CloseDownwindNM == 1.5 && odd.WideDownwindNM >= odd.DownwindNM, "Circuit values: the close downwind at most 1.5 NM, the wide one not inside the conventional");
    Check(PerformanceClass.Defaults().First(k => k.Name == "Jet") is { CloseDownwindNM: 1.2, RollOutNM: 1.0, RollOutHeightFt: 450 }, "Circuit values: the Jet's close downwind 1.2 NM, roll out 1 NM at 450 ft");

    // 1. Conventional, Light, left, from the downwind, full stop.
    SimEngine e1 = NewCircuitEngine();
    string c1 = e1.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
    List<AircraftState> t1 = Fly(e1, c1, 900);
    List<AircraftState> d1 = OnLeg(t1, "DOWNWIND");
    Check(d1.Count > 10 && Near(d1.Average(st => Frame(st).Lateral), 1.0, 0.2) && Near(Height(d1[0]), 1000, 60),
        "Conventional (Light, left): downwind about 1 NM to the left, at 1000 ft above the threshold", d1.Count > 0 ? $"lateral {d1.Average(st => Frame(st).Lateral):F2}, height {Height(d1[0]):F0}" : "no downwind");
    Check(OnLeg(t1, "BASE").Count > 0 && OnLeg(t1, "FINAL").Count > 0 && LandedOnRunway(t1) && e1.Get(c1) == null,
        "Conventional: base, final, landed on the runway, then removed (full stop)");
    Check(d1.Count > 10 && d1.Max(st => Frame(st).Lateral) < 1.15, "Conventional: the turn to the downwind is anticipated (not wide of it)", d1.Count > 0 ? $"widest {d1.Max(st => Frame(st).Lateral):F2}" : "");
    pictures.Add(("Conventional circuit, Light, left, full stop", t1));

    // 2. Close pattern, Jet, right, from the runway, touch and go then full stop.
    SimEngine e2 = NewCircuitEngine();
    // (After a take-off only the conventional or the wide: the close pattern ordered while rolling, flown from the lift-off.)
    string c2 = e2.AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, CircuitSide.Right, EndAction.TouchAndGo, performanceClass: "Jet");
    e2.SetCircuit(c2, CircuitType.Close, CircuitSide.Right);
    int rolls = 0;
    GroundContact last2 = GroundContact.Rolling;
    List<AircraftState> t2 = Fly(e2, c2, 1200, st =>
    {
        if (st.Contact == GroundContact.Rolling && last2 != GroundContact.Rolling) { rolls++; e2.SetEndAction(c2, EndAction.FullStop); }
        last2 = st.Contact;
    });
    List<AircraftState> d2 = OnLeg(t2, "DOWNWIND");
    Check(d2.Count > 5 && StraightDownwind(t2) is { Count: > 5 } straight2 && Near(straight2.Average(), 1.2, 0.3) && d2.All(st => Frame(st).Lateral < 1.7),
        "Close pattern (Jet, right): downwind about 1.2 NM to the right, never beyond 1.7", d2.Count > 0 ? $"max {d2.Max(st => Frame(st).Lateral):F2}" : "no downwind");
    Check(t2.All(st => st.Leg is not ("CROSSWIND" or "BASE")), "Close pattern: no crosswind and no base (180° turns to the downwind and to the final)");
    List<double> s2 = StraightDownwind(t2);
    Check(Parallel(s2, 1.2), "Close pattern (Jet): the downwind parallel to the runway at 1.2 NM, also while it accelerates after the take-off", Describe(s2));
    AircraftState? rollOut2 = t2.FirstOrDefault(st => st.Leg == "FINAL" && Math.Abs(Frame(st).Lateral) < 0.15 && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) < 15);
    Check(rollOut2 != null && Near(Frame(rollOut2).Along, -1.0, 0.35) && Near(Height(rollOut2), 450, 100),
        "Close pattern: rolls out on the final at about 1 NM and 450 ft (Jet)", rollOut2 != null ? $"{-Frame(rollOut2).Along:F2} NM, {Height(rollOut2):F0} ft" : "never on the final");
    Check(t2[0].Contact == GroundContact.Rolling && rolls == 1 && LandedOnRunway(t2) && e2.Get(c2) == null,
        "Close pattern: take-off from the runway, one touch and go, then a full stop", $"touch and go {rolls}");
    Check(t2.Where(st => st.Contact == GroundContact.Airborne).Max(Height) is double top2 && Near(top2, 1500, 80), "Close pattern: circuit height 1500 ft");
    pictures.Add(("Close pattern, Jet, right, take-off, touch and go, full stop", t2));

    // 3. Wide circuit, Turboprop, left, from the initial point.
    SimEngine e3 = NewCircuitEngine();
    string c3 = e3.AddInCircuit(CircuitStart.InitialPoint, CircuitType.Wide, CircuitSide.Left, EndAction.FullStop, performanceClass: "Turboprop");
    List<AircraftState> t3 = Fly(e3, c3, 1200);
    List<AircraftState> d3 = OnLeg(t3, "DOWNWIND");
    Check(Near(Frame(t3[0]).Along, -3, 0.1) && OnLeg(t3, "INITIAL").Count > 0 && OnLeg(t3, "UPWIND").Count > 0 && OnLeg(t3, "CROSSWIND").Count > 0,
        "Wide (Turboprop): from the initial point at 3 NM, along the runway, upwind, crosswind");
    Check(d3.Count > 10 && Near(d3.Average(st => Frame(st).Lateral), 3.0, 0.35) && LandedOnRunway(t3),
        "Wide: downwind about 3 NM out, then landed", d3.Count > 0 ? $"lateral {d3.Average(st => Frame(st).Lateral):F2}" : "no downwind");
    pictures.Add(("Wide circuit (outside downwind), Turboprop, left, from the initial point", t3));

    // 4. Long final, Jet.
    SimEngine e4 = NewCircuitEngine();
    string c4 = e4.AddInCircuit(CircuitStart.LongFinal, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
    AircraftState start4 = e4.Get(c4)!;
    List<AircraftState> t4 = Fly(e4, c4, 600);
    AircraftState? short4 = t4.LastOrDefault(st => st.Contact == GroundContact.Airborne);
    Check(Near(Frame(start4).Along, -10, 0.05) && Near(Height(start4), 10 * 318.4, 20) && LandedOnRunway(t4),
        "Long final (Jet): starts at 10 NM on the glide path, lands on the runway", $"height {Height(start4):F0}");
    Check(short4 != null && Near(short4.SpeedKt, 140, 8) && t4.Where(st => Frame(st).Along < -5).All(st => st.SpeedKt > 170),
        "Long final: approach speed far out, final speed near the threshold", short4 != null ? $"{short4.SpeedKt:F0} kt" : "");
    pictures.Add(("Long final, Jet", t4));

    // 5. Go-around at the end of the final, then a full stop.
    SimEngine e5 = NewCircuitEngine();
    string c5 = e5.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.GoAround, performanceClass: "Light");
    bool wentAround = false;
    List<AircraftState> t5 = Fly(e5, c5, 1500, st =>
    {
        if (st.Leg == "GO AROUND" && !wentAround) { wentAround = true; e5.SetEndAction(c5, EndAction.FullStop); }
    });
    int ga = t5.FindIndex(st => st.Leg == "GO AROUND");
    Check(ga > 0 && t5.Take(ga).All(st => st.Contact == GroundContact.Airborne) && t5.Take(ga).Min(Height) > 150 && t5.Skip(ga).Max(Height) > 950,
        "Go-around: no touchdown, goes around at about 200 ft, climbs back to circuit height", ga > 0 ? $"lowest {t5.Take(ga).Min(Height):F0} ft" : "no go-around");
    Check(LandedOnRunway(t5) && e5.Get(c5) == null, "Go-around: the next circuit ends with the full stop");
    pictures.Add(("Go-around, then full stop, Light", t5));

    // 6. Low pass, close pattern, Turboprop.
    SimEngine e6 = NewCircuitEngine();
    string c6 = e6.AddInCircuit(CircuitStart.Downwind, CircuitType.Close, CircuitSide.Left, EndAction.LowPass, performanceClass: "Turboprop");
    bool passed = false;
    string legBefore6 = "";
    // (The final left at the far end of the runway: the low pass done; at 400 ft the close pattern turns at once, its GO
    // AROUND leg is short.)
    List<AircraftState> t6 = Fly(e6, c6, 1200, st =>
    {
        if (!passed && legBefore6 == "FINAL" && st.Leg != "FINAL" && Frame(st).Along > CircuitLength - 0.1) { passed = true; e6.SetEndAction(c6, EndAction.FullStop); }
        legBefore6 = st.Leg;
    });
    int lp = Enumerable.Range(1, Math.Max(0, t6.Count - 1)).FirstOrDefault(i => t6[i - 1].Leg == "FINAL" && t6[i].Leg != "FINAL" && Frame(t6[i]).Along > CircuitLength - 0.1, -1);
    List<AircraftState> over = lp < 0 ? [] : t6.Take(lp).Where(st => Between(Frame(st).Along, 0, CircuitLength) && Math.Abs(Frame(st).Lateral) < 0.2).ToList();
    Check(lp > 0 && t6.Take(lp).All(st => st.Contact == GroundContact.Airborne) && over.Count > 10 && Between(over.Min(Height), 360, 460) && over.Max(Height) < 460,
        "Low pass: at about 400 ft all along the runway (the user, 5 Nov), no touchdown",
        over.Count > 0 ? $"{over.Min(Height):F0} to {over.Max(Height):F0} ft over the runway" : "not over the runway");
    Check(LandedOnRunway(t6), "Low pass: then the full stop");
    pictures.Add(("Low pass on a close pattern, then full stop, Turboprop", t6));

    // 7a. The close pattern ordered on the final of a conventional circuit: flown after the go-around.
    SimEngine e7 = NewCircuitEngine();
    string c7 = e7.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.GoAround, performanceClass: "Light");
    bool ordered = false, aroundSeen = false;
    AircraftState? waiting = null;
    List<AircraftState> t7 = Fly(e7, c7, 1500, st =>
    {
        if (!ordered && st.Leg == "FINAL") { ordered = true; e7.SetCircuit(c7, CircuitType.Close, CircuitSide.Left); }
        else if (ordered && waiting == null) waiting = st;
        if (st.Leg == "GO AROUND" && !aroundSeen) { aroundSeen = true; e7.SetEndAction(c7, EndAction.FullStop); }
    });
    List<double> s7 = StraightDownwind(t7.Where(st => st.Circuit == CircuitType.Close).ToList());
    Check(waiting != null && waiting.Circuit == CircuitType.Conventional && waiting.NextCircuit == CircuitType.Close,
        "Close pattern ordered while in a circuit: kept for after the go-around (shown as the next circuit)");
    Check(aroundSeen && Parallel(s7, 0.6) && LandedOnRunway(t7), "After the go-around: the close pattern (180° to the downwind at 0.6 NM), then landed", Describe(s7));
    pictures.Add(("Conventional; on the final the instructor orders the close pattern: go-around, close pattern, full stop (Light)", t7));

    // 7b. From outside the circuits (arriving 4 NM abeam, 3 NM before the threshold): into the close pattern at once.
    SimEngine e7b = NewCircuitEngine();
    (double ox, double oy) = Place(-3, 4, CircuitSide.Left);
    string c7b = e7b.Add(ox, oy, CircuitElevation + 1500, CircuitCourse, 150, performanceClass: "Turboprop");
    e7b.SetCircuit(c7b, CircuitType.Close, CircuitSide.Left);
    e7b.RunFor(2);
    AircraftState joining = e7b.Get(c7b)!;
    List<AircraftState> t7b = Fly(e7b, c7b, 1200);
    List<AircraftState> d7b = OnLeg(t7b, "DOWNWIND");
    Check(joining.Circuit == CircuitType.Close && joining.Leg == "JOIN" && d7b.Count > 3 && Near(Frame(d7b.Last()).Lateral, 0.8, 0.25) && LandedOnRunway(t7b),
        "From outside the circuits: joins the close pattern at once, then lands", d7b.Count > 0 ? $"{joining.Leg}, downwind end {Frame(d7b.Last()).Lateral:F2} NM" : joining.Leg);
    List<double> s7b = StraightDownwind(t7b);
    List<AircraftState> j7b = OnLeg(t7b, "JOIN");
    Check(Parallel(s7b, 0.8) && j7b.Count > 0 && j7b.Min(st => Frame(st).Lateral) > 0.65,
        "From outside: a 45° entry from the outside onto the downwind, parallel at 0.8 NM, never nearer the runway", Describe(s7b) + (j7b.Count > 0 ? $", nearest while joining {j7b.Min(st => Frame(st).Lateral):F2}" : ""));
    pictures.Add(("Arriving from outside the circuits, 45° entry into the close pattern (Turboprop)", t7b));

    // 8. Crosswind: 15 kt from the left of the runway.
    SimEngine e8 = NewCircuitEngine();
    e8.SetWind(CircuitCourse + 90, 15, 0);
    string c8 = e8.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
    List<AircraftState> t8 = Fly(e8, c8, 900);
    List<AircraftState> d8 = OnLeg(t8, "DOWNWIND");
    Check(LandedOnRunway(t8), "Crosswind 15 kt: the Light still lands on the runway");
    Check(d8.Count > 10 && Near(d8.Average(st => Frame(st).Lateral), 0.85, 0.2), "Crosswind 15 kt blowing away from the runway on the downwind side: downwind 0.15 NM closer",
        d8.Count > 0 ? $"lateral {d8.Average(st => Frame(st).Lateral):F2}" : "no downwind");
    pictures.Add(("Conventional with 15 kt crosswind, Light", t8));

    // 9. Departure: take-off and away.
    SimEngine e9 = NewCircuitEngine();
    string c9 = e9.AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, CircuitSide.Left, EndAction.Departure, performanceClass: "Turboprop");
    List<AircraftState> t9 = Fly(e9, c9, 300);
    AircraftState end9 = t9[^1];
    Check(end9.Leg == "DEPARTURE" && Frame(end9).Along > 4 && Near(Height(end9), 1500, 100) && Math.Abs(Frame(end9).Lateral) < 0.3,
        "Departure: take-off, straight out at circuit height", $"{end9.Leg} at {Frame(end9).Along:F1} NM, {Height(end9):F0} ft");
    pictures.Add(("Departure, Turboprop", t9));

    // 11. Waveoff of the jet circuit: above 400 ft it turns to the downwind at its break point (the middle of the runway).
    SimEngine e11 = NewCircuitEngine();
    string c11 = e11.AddInCircuit(CircuitStart.Downwind, CircuitType.Close, CircuitSide.Left, EndAction.GoAround, performanceClass: "Jet");
    bool waved = false;
    List<AircraftState> t11 = Fly(e11, c11, 1200, st =>
    {
        if (st.Leg == "GO AROUND" && !waved) { waved = true; e11.SetEndAction(c11, EndAction.FullStop); }
    });
    int wo = t11.FindIndex(st => st.Leg == "GO AROUND");
    int turnDownwind = wo < 0 ? -1 : t11.FindIndex(wo, st => st.Leg == "DOWNWIND");
    Check(turnDownwind > 0 && Height(t11[turnDownwind]) > 380 && Between(Frame(t11[turnDownwind]).Along, CircuitLength / 2 - 0.05, CircuitLength) && LandedOnRunway(t11),
        "Waveoff (Jet, close pattern): turns to the downwind above 400 ft over the middle of the runway (its break), then lands", turnDownwind > 0 ? $"{Height(t11[turnDownwind]):F0} ft at {Frame(t11[turnDownwind]).Along:F2} NM" : "no turn");
    List<double> s11 = StraightDownwind(t11);
    Check(Parallel(s11, 1.2), "Waveoff: the downwind parallel to the runway at 1.2 NM", Describe(s11));
    pictures.Add(("Waveoff in the close pattern, then full stop, Jet", t11));

    // 12. Overhead pattern (Jet) from the initial point, break over the middle of the runway.
    (List<AircraftState> Track, double BreakAlong) Overhead(BreakPoint breakAt, EndAction end, Action<SimEngine, string, AircraftState>? each = null)
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.InitialPoint, CircuitType.Overhead, CircuitSide.Left, end, performanceClass: "Jet");
        sim.SetBreak(cs, breakAt);
        List<AircraftState> flown = [sim.Get(cs)!];
        flown.AddRange(Fly(sim, cs, 1200, st => each?.Invoke(sim, cs, st)));
        int lastInitial = flown.FindLastIndex(st => st.Leg == "INITIAL");
        return (flown, lastInitial >= 0 ? Frame(flown[lastInitial]).Along : double.NaN);
    }
    (List<AircraftState> t12, double b12) = Overhead(BreakPoint.Middle, EndAction.FullStop);
    List<double> s12 = StraightDownwind(t12);
    AircraftState? roll12 = t12.FirstOrDefault(st => st.Leg == "FINAL" && Math.Abs(Frame(st).Lateral) < 0.15 && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) < 15);
    Check(Near(Frame(t12[0]).Along, -3, 0.05) && Near(Height(t12[0]), 1500, 5) && t12[0].SpeedKt == 300 && t12[0].Leg == "INITIAL",
        "Overhead (Jet): from the initial point at 3 NM, 1500 ft, 300 kt, along the runway");
    Check(Near(b12, CircuitLength / 2, 0.15) && t12.All(st => st.Leg is not ("CROSSWIND" or "BASE" or "UPWIND")),
        "Overhead: the break over the middle of the runway, then straight to the downwind (no crosswind, no base)", $"break at {b12:F2} NM");
    Check(Parallel(s12, 1.2) && s12.Max() < 1.5, "Overhead: after the break the downwind is parallel at 1.2 NM (never beyond 1.5)", Describe(s12));
    Check(roll12 != null && Near(Frame(roll12).Along, -1.0, 0.35) && LandedOnRunway(t12), "Overhead: rolls out at about 1 NM, then lands",
        roll12 != null ? $"{-Frame(roll12).Along:F2} NM, {Height(roll12):F0} ft" : "never on the final");
    AircraftState? touch12 = t12.LastOrDefault(st => st.Contact == GroundContact.Airborne);
    Check(touch12 != null && touch12.SpeedKt <= 160, "Overhead: slows down from 300 kt in the turns, lands at about the final speed", touch12 != null ? $"{touch12.SpeedKt:F0} kt" : "");
    pictures.Add(("Overhead pattern (Jet, left), break over the middle of the runway, full stop", t12));

    // 13. Break over the end and over the beginning of the runway.
    (List<AircraftState> t13, double b13) = Overhead(BreakPoint.End, EndAction.FullStop);
    (List<AircraftState> t13b, double b13b) = Overhead(BreakPoint.Beginning, EndAction.FullStop);
    Check(Near(b13, CircuitLength, 0.15) && Near(b13b, 0.05, 0.15) && LandedOnRunway(t13) && LandedOnRunway(t13b),
        "Overhead: break at the end of the runway, or at its beginning", $"end {b13:F2} NM, beginning {b13b:F2} NM");
    pictures.Add(("Overhead, break at the end of the runway (Jet)", t13));
    pictures.Add(("Overhead, break at the beginning of the runway (Jet)", t13b));

    // 14. "Standby for break", then "break now" 1.5 NM past the threshold.
    bool broke = false;
    (List<AircraftState> t14, double b14) = Overhead(BreakPoint.Standby, EndAction.FullStop, (sim, cs, st) =>
    {
        if (!broke && st.Leg == "INITIAL" && Frame(st).Along > 1.5) { broke = true; sim.BreakNow(cs); }
    });
    Check(broke && Between(b14, 1.45, 1.75) && LandedOnRunway(t14), "Overhead: standby for break (no break over the runway), then break now", $"break at {b14:F2} NM");
    pictures.Add(("Overhead, standby for break, break now past the runway (Jet)", t14));

    // 15. Overhead with a touch and go: then the close pattern, not the initial point again.
    bool touched = false;
    (List<AircraftState> t15, _) = Overhead(BreakPoint.Middle, EndAction.TouchAndGo, (sim, cs, st) =>
    {
        if (!touched && st.Contact == GroundContact.Rolling) { touched = true; sim.SetEndAction(cs, EndAction.FullStop); }
    });
    int roll15 = t15.FindIndex(st => st.Contact == GroundContact.Rolling);
    Check(roll15 > 0 && t15.Skip(roll15).All(st => st.Leg != "INITIAL") && t15.Skip(roll15 + 5).All(st => st.Circuit == CircuitType.Close) && LandedOnRunway(t15),
        "Overhead with touch and go: then the close pattern (not the initial point again), then landed");
    pictures.Add(("Overhead with touch and go, then close pattern, full stop (Jet)", t15));

    // 16-18. An orbit on the downwind (away from the runway) in the conventional, close and overhead patterns.
    void OrbitTest(string title, CircuitStart start, CircuitType type, string kind, double width, Func<AircraftState, bool> when)
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(start, type, CircuitSide.Left, EndAction.FullStop, performanceClass: kind);
        bool asked = false;
        List<AircraftState> flown = Fly(sim, cs, 1500, st =>
        {
            if (!asked && when(st)) { asked = true; sim.Orbit(cs); }
        });
        List<AircraftState> orbit = OnLeg(flown, "ORBIT");
        double turned = 0;
        for (int n = 1; n < orbit.Count; n++) turned += Math.Abs(SimEngine.Wrap(orbit[n].HeadingTrue - orbit[n - 1].HeadingTrue));
        int end = flown.FindLastIndex(st => st.Leg == "ORBIT");
        List<double> after = StraightDownwind(flown.Skip(end + 1).ToList());
        Check(orbit.Count > 10 && turned > 300 && orbit.Min(st => Frame(st).Lateral) > width - 0.15 && after.Count > 2 && Near(after.Average(), width, 0.2) && LandedOnRunway(flown),
            $"Orbit on the downwind ({title}): a 360° turn away from the runway, then on along the downwind, then landed",
            orbit.Count > 0 ? $"turned {turned:F0}°, nearest {orbit.Min(st => Frame(st).Lateral):F2} NM, downwind after {(after.Count > 0 ? after.Average() : double.NaN):F2} NM" : "no orbit");
        pictures.Add(($"Orbit on the downwind, {title}", flown));
    }
    bool StraightOn(AircraftState st) => st.Leg == "DOWNWIND" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse - 180)) < 10;
    OrbitTest("conventional circuit, Light", CircuitStart.Downwind, CircuitType.Conventional, "Light", 1.0, st => StraightOn(st) && Frame(st).Along < 0.8);
    OrbitTest("close pattern, Jet", CircuitStart.Downwind, CircuitType.Close, "Jet", 1.2, st => StraightOn(st) && Frame(st).Along < 0.6);
    OrbitTest("overhead pattern, Jet", CircuitStart.InitialPoint, CircuitType.Overhead, "Jet", 1.2, st => StraightOn(st) && Frame(st).Along < 0.3);

    // 19. The circuits of the aerodrome: the side of the runway, the heights of the Jet (initial 2500, break 2000, circuit 2000).
    {
        string json = "{ \"Airport\": \"lixx\", \"Runways\": [ { \"Runway\": \"14\", \"Side\": \"Right\" }, { \"Runway\": \"32\", \"Side\": \"Left\" } ], "
            + "\"Classes\": [ { \"Class\": \"jet\", \"CircuitHeightFt\": 2000, \"InitialHeightFt\": 2500, \"BreakHeightFt\": 2000, \"Tower\": \"kept\" } ], \"Future\": 1 }";
        AerodromeCircuits here = AerodromeCircuits.Parse(json);
        string again = JsonSerializer.Serialize(here, AerodromeCircuits.JsonOptions);
        Check(here.Airport == "LIXX" && here.SideOf("14") == CircuitSide.Right && here.SideOf("32") == CircuitSide.Left && here.SideOf("05") == null
            && again.Contains("\"Future\"") && again.Contains("\"Tower\""), "Aerodrome file: the side of each runway, unknown fields kept");
        PerformanceClass jet = here.Apply(PerformanceClass.Defaults().First(k => k.Name == "Jet"));
        PerformanceClass light = here.Apply(PerformanceClass.Defaults().First(k => k.Name == "Light"));
        Check(jet is { CircuitHeightFt: 2000, OverheadHeightFt: 2500, BreakHeightFt: 2000, HighKeyFt: 7000 } && light.CircuitHeightFt == 1000,
            "Aerodrome file: the heights it gives (Jet), the class's for the others (Light)");
        bool bad = false;
        try { AerodromeCircuits.Parse("{ not json"); } catch (InvalidDataException) { bad = true; }
        Check(bad, "Aerodrome file: a damaged one is refused with a readable message");

        SimEngine sim = NewCircuitEngine();
        sim.SetAerodrome(here, "14");
        string cs = sim.AddInCircuit(CircuitStart.InitialPoint, CircuitType.Overhead, null, EndAction.FullStop, performanceClass: "Jet");
        List<AircraftState> flown = [sim.Get(cs)!];
        flown.AddRange(Fly(sim, cs, 1200));
        int lastInitial = flown.FindLastIndex(st => st.Leg == "INITIAL");
        List<AircraftState> downwind = flown.Where(StraightOn).ToList();
        Check(flown[0].CircuitSide == CircuitSide.Right && Near(Height(flown[0]), 2500, 5) && lastInitial > 0 && Near(Height(flown[lastInitial]), 2000, 120)
            && downwind.Count > 5 && Near(downwind.Average(Height), 2000, 60) && LandedOnRunway(flown),
            "Aerodrome (runway 14, right; Jet initial 2500, break 2000): right-hand, descends along the initial to the break, downwind at 2000, lands",
            lastInitial > 0 ? $"side {flown[0].CircuitSide}, at the break {Height(flown[lastInitial]):F0} ft, downwind {(downwind.Count > 0 ? downwind.Average(Height) : double.NaN):F0} ft" : "no initial");
        pictures.Add(("Aerodrome file: overhead right-hand, initial 2500 ft, break 2000 ft, circuit 2000 ft (Jet)", flown));

        // The side changed in flight by the instructor (the default stays the aerodrome's): on the downwind an S over the
        // aerodrome onto the new downwind (it does not leave the circuit); on the final a go-around, then the new downwind.
        void SideChange(string title, Func<AircraftState, bool> when, bool goAround)
        {
            SimEngine sim2 = NewCircuitEngine();
            sim2.SetAerodrome(here, "14");
            string cs2 = sim2.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, null, EndAction.FullStop, performanceClass: "Light");
            AircraftState first2 = sim2.Get(cs2)!;
            bool changed = false;
            int changedAt = -1, seen = 0;
            List<AircraftState> flown2 = [first2];
            flown2.AddRange(Fly(sim2, cs2, 1500, st =>
            {
                seen++;
                if (!changed && when(st)) { changed = true; changedAt = seen; sim2.SetCircuit(cs2, CircuitType.Conventional, CircuitSide.Left); }
            }));
            List<AircraftState> after2 = changedAt < 0 ? [] : flown2.Skip(changedAt + 1).ToList();
            List<AircraftState> downwind2 = after2.Where(StraightOn).ToList();
            // Where it crossed the runway track before landing: over the aerodrome.
            List<double> crossings = [];
            for (int n = 1; n < after2.Count; n++)
            {
                if (after2[n].Contact != GroundContact.Airborne || after2[n].Leg == "FINAL") break;
                double l0 = Frame(after2[n - 1] with { CircuitSide = CircuitSide.Left }).Lateral, l1 = Frame(after2[n] with { CircuitSide = CircuitSide.Left }).Lateral;
                if (after2[n].Leg == "JOIN" && Math.Sign(l0) != Math.Sign(l1) && Math.Abs(l0) < 0.5) crossings.Add(Frame(after2[n]).Along);
            }
            // No loop: the heading turned one way and back (an S), never round (a loop: one way 270° or more).
            int onDownwind = after2.FindIndex(StraightOn);
            double sum = 0, top = 0, bottom = 0;
            for (int n = 1; n < (onDownwind < 0 ? after2.Count : onDownwind); n++)
            {
                sum += SimEngine.Wrap(after2[n].HeadingTrue - after2[n - 1].HeadingTrue);
                top = Math.Max(top, sum);
                bottom = Math.Min(bottom, sum);
            }
            bool noLoop = onDownwind > 0 && top - bottom < 220;
            bool overField = noLoop && goAround ? after2.Any(st => st.Leg == "GO AROUND") : crossings.Count > 0 && crossings.All(c => Between(c, -0.2, CircuitLength + 0.3));
            Check(first2.CircuitSide == CircuitSide.Right && sim2.DefaultSide == CircuitSide.Right && changed && after2.All(st => st.Leg != "MANUAL") && overField
                && downwind2.Count > 5 && Near(downwind2.Average(st => Frame(st).Lateral), 1.0, 0.25) && LandedOnRunway(flown2),
                $"Aerodrome right-hand, the instructor changes to the left {title}",
$"turned at most {top - bottom:F0}° one way, " + (crossings.Count > 0 ? $"crossed at {string.Join(", ", crossings.Select(c => c.ToString("F2", CultureInfo.InvariantCulture)))} NM, " : "")
                + (downwind2.Count > 0 ? $"downwind {downwind2.Average(st => Frame(st).Lateral):F2} NM to the left" : "no downwind"));
            pictures.Add(($"Aerodrome right-hand, changed to left {title} (Light)", flown2));
        }
        SideChange("at the start of the downwind: S over the aerodrome", st => StraightOn(st) && Frame(st).Along < 1.2, goAround: false);
        SideChange("near the end of the downwind: S over the aerodrome", st => StraightOn(st) && Frame(st).Along < -0.5, goAround: false);
        SideChange("on the final: go-around, new downwind", st => st.Leg == "FINAL" && Frame(st).Along > -0.8, goAround: true);
    }

    // 19b. A side for each group of circuits at a runway: conventional right, jet (overhead, close) left, SFO right; the
    // SFO after its touch and go: the close pattern on the jet side.
    {
        AerodromeCircuits sides = AerodromeCircuits.Parse("{ \"Airport\": \"LIXX\", \"Runways\": [ { \"Runway\": \"14\", \"Side\": \"Left\", "
            + "\"ConventionalSide\": \"Right\", \"JetSide\": \"Left\", \"SfoSide\": \"Right\" } ] }");
        SimEngine sim = NewCircuitEngine();
        sim.SetAerodrome(sides, "14");
        CircuitSide SideOf(CircuitStart start, CircuitType type) => sim.Get(sim.AddInCircuit(start, type, null, EndAction.FullStop, performanceClass: "Jet"))!.CircuitSide;
        bool groups = SideOf(CircuitStart.Downwind, CircuitType.Conventional) == CircuitSide.Right && SideOf(CircuitStart.Downwind, CircuitType.Wide) == CircuitSide.Right
            && SideOf(CircuitStart.InitialPoint, CircuitType.Overhead) == CircuitSide.Left && SideOf(CircuitStart.Downwind, CircuitType.Close) == CircuitSide.Left
            && SideOf(CircuitStart.HighKey, CircuitType.Sfo) == CircuitSide.Right && sim.SideFor(CircuitType.Sfo) == CircuitSide.Right;
        Check(groups && sides.SideOf("14") == CircuitSide.Left, "Aerodrome: a side for each group of circuits (conventional and wide right, overhead and close left, SFO right)");

        SimEngine sfo = NewCircuitEngine();
        sfo.SetAerodrome(sides, "14");
        string cs = sfo.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, null, EndAction.TouchAndGo, performanceClass: "Jet");
        sfo.HoldAtHighKey(cs, false);
        bool rolled = false;
        List<AircraftState> flown = [sfo.Get(cs)!];
        flown.AddRange(Fly(sfo, cs, 1500, st =>
        {
            if (!rolled && st.Contact == GroundContact.Rolling) { rolled = true; sfo.SetEndAction(cs, EndAction.FullStop); }
        }));
        int roll = flown.FindIndex(st => st.Contact == GroundContact.Rolling);
        List<double> after = StraightDownwind(flown.Skip(Math.Max(0, roll)).ToList());
        Check(flown[0].CircuitSide == CircuitSide.Right && roll > 0 && flown.Skip(roll + 5).All(st => st.Circuit == CircuitType.Close && st.CircuitSide == CircuitSide.Left)
            && Parallel(after, 1.2) && LandedOnRunway(flown),
            "SFO on the right, after the touch and go the close pattern on the jet side (left), then lands", Describe(after));
        pictures.Add(("SFO right-hand, touch and go, then the close pattern left-hand (aerodrome sides per circuit), Jet", flown));
    }

    // 19c. A height for each circuit at the aerodrome: Jet conventional 2000 and wide 2500, Turboprop conventional 1500;
    // a circuit changed in flight (conventional → wide) flies the new height.
    {
        AerodromeCircuits heights = AerodromeCircuits.Parse("{ \"Airport\": \"LIXX\", \"Runways\": [ { \"Runway\": \"14\", \"Side\": \"Left\" } ], \"Classes\": [ "
            + "{ \"Class\": \"Jet\", \"ConventionalHeightFt\": 2000, \"WideHeightFt\": 2500 }, { \"Class\": \"Turboprop\", \"ConventionalHeightFt\": 1500 } ] }");
        double DownwindHeight(string kind, CircuitType type)
        {
            SimEngine sim = NewCircuitEngine();
            sim.SetAerodrome(heights, "14");
            string cs = sim.AddInCircuit(CircuitStart.Downwind, type, null, EndAction.FullStop, performanceClass: kind);
            List<AircraftState> downwind = Fly(sim, cs, 20).Where(st => st.Leg == "DOWNWIND").ToList();
            return downwind.Count > 0 ? downwind.Max(Height) : double.NaN;
        }
        double jetConventional = DownwindHeight("Jet", CircuitType.Conventional), jetWide = DownwindHeight("Jet", CircuitType.Wide);
        double turboprop = DownwindHeight("Turboprop", CircuitType.Conventional), jetClose = DownwindHeight("Jet", CircuitType.Close);
        Check(Near(jetConventional, 2000, 30) && Near(jetWide, 2500, 30) && Near(turboprop, 1500, 30) && Near(jetClose, 1500, 30),
            "Aerodrome heights of each circuit: Jet conventional 2000, Jet wide 2500, Turboprop conventional 1500, Jet close (not given) 1500",
            $"{jetConventional:F0} / {jetWide:F0} / {turboprop:F0} / {jetClose:F0} ft");

        SimEngine change = NewCircuitEngine();
        change.SetAerodrome(heights, "14");
        string jet = change.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, null, EndAction.FullStop, performanceClass: "Jet");
        change.RunFor(20);
        change.SetCircuit(jet, CircuitType.Wide, CircuitSide.Left);
        List<AircraftState> after = Fly(change, jet, 400);
        List<AircraftState> wideDownwind = after.Where(st => st.Leg == "DOWNWIND" && st.Circuit == CircuitType.Wide).ToList();
        Check(wideDownwind.Count > 5 && Near(wideDownwind.Max(Height), 2500, 60),
            "A circuit changed in flight (conventional → wide): its downwind at the height of the wide (2500)", wideDownwind.Count > 0 ? $"{wideDownwind.Max(Height):F0} ft" : "no wide downwind");
    }

    // 20-22. Simulated flame out: High Key over the runway, 180° to the Low Key, gliding down to the final.
    void Sfo(string kind, CircuitSide side, double highKey, double lowFrom, double lowTo)
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.HighKey, CircuitType.Conventional, side, EndAction.FullStop, performanceClass: kind);
        sim.HoldAtHighKey(cs, false);
        List<AircraftState> flown = [sim.Get(cs)!];
        flown.AddRange(Fly(sim, cs, 1200));
        AircraftState? atHigh = flown.LastOrDefault(st => st.Leg == "HIGH KEY");
        // The Low Key: the first moment on the downwind heading after the turn.
        AircraftState? atLow = flown.FirstOrDefault(st => st.Leg == "LOW KEY" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse - 180)) < 10);
        // The roll out on the final: nearly on the runway.
        AircraftState? rolled = flown.FirstOrDefault(st => st.Leg == "FINAL" && st.Contact == GroundContact.Airborne && Math.Abs(Frame(st).Lateral) < 0.15
            && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) < 15);
        Check(flown[0].Circuit == CircuitType.Sfo && atHigh != null && Near(Height(atHigh), highKey, 100) && Between(Frame(atHigh).Along, 0, CircuitLength)
            && atLow != null && Between(Height(atLow), lowFrom, lowTo) && Frame(atLow).Lateral > 0.3 && flown.All(st => st.Leg is not ("CROSSWIND" or "BASE"))
            && rolled != null && Between(Frame(rolled).Along, -0.5, 0.1) && Height(rolled) < 400 && LandedOnRunway(flown),
            $"SFO ({kind}, {side}): High Key over the runway at about {highKey} ft, Low Key abeam on the {side.ToString().ToLowerInvariant()}, out of the last turn nearly on the runway, lands",
            atHigh != null && atLow != null ? $"High Key {Height(atHigh):F0} ft at {Frame(atHigh).Along:F2} NM, Low Key {Height(atLow):F0} ft {Frame(atLow).Lateral:F2} NM out"
                + (rolled != null ? $", on the final {-Frame(rolled).Along:F2} NM before the threshold at {Height(rolled):F0} ft" : ", never on the final") + (kind == "Jet" && side == CircuitSide.Left ? " " + FromLowKey(flown) : "") : "no High or Low Key");
        pictures.Add(($"SFO, {kind}, {side.ToString().ToLowerInvariant()}", flown));
    }
    Sfo("Jet", CircuitSide.Left, 7000, 3000, 5600);
    Sfo("Jet", CircuitSide.Right, 7000, 3000, 5600);
    Sfo("Light", CircuitSide.Left, 2500, 900, 1800);
    Sfo("Turboprop", CircuitSide.Right, 4000, 1500, 2800);

    // 22b. SFO again after a go-around: straight to the High Key, climbing on the circle over the field (no wide loop).
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.GoAround, performanceClass: "Jet");
        bool sfoOrdered = false, around = false;
        List<AircraftState> flown = [sim.Get(cs)!];
        flown.AddRange(Fly(sim, cs, 2400, st =>
        {
            if (!sfoOrdered && st.Leg == "FINAL") { sfoOrdered = true; sim.SetCircuit(cs, CircuitType.Sfo, CircuitSide.Left); }
            // (On hold at the High Key by default: started.)
            if (st.Leg == "HOLD") sim.HoldAtHighKey(cs, false);
            if (!around && st.Leg == "GO AROUND") { around = true; sim.SetEndAction(cs, EndAction.FullStop); }
        }));
        List<AircraftState> climb = OnLeg(flown, "CLIMB");
        (double hx, double hy) = Place(0.5, 0, CircuitSide.Left);
        double far = climb.Count > 0 ? climb.Max(st => Math.Sqrt((st.X - hx) * (st.X - hx) + (st.Y - hy) * (st.Y - hy))) : double.NaN;
        int second = flown.FindLastIndex(st => st.Leg == "CLIMB");
        AircraftState? lowKey2 = second < 0 ? null : flown.Skip(second).FirstOrDefault(st => st.Leg == "LOW KEY");
        // Turning to the side of the SFO (left), a little the other way at most while it joins the circle: no loops.
        double backwards = 0;
        for (int n = 1; n < climb.Count; n++) backwards += Math.Max(0, SimEngine.Wrap(climb[n].HeadingTrue - climb[n - 1].HeadingTrue));
        List<AircraftState> short2 = second < 0 ? [] : flown.Skip(second).Where(st => st.Leg == "FINAL" && st.Contact == GroundContact.Airborne && Frame(st).Along > -0.15).ToList();
        Check(around && climb.Count > 10 && far < 1.6 && backwards < 90 && climb.Max(Height) > 6800 && flown.All(st => st.Leg != "JOIN") && lowKey2 != null
            && short2.All(st => Math.Abs(Frame(st).Lateral) < 0.1) && LandedOnRunway(flown),
            "SFO again after the go-around: a climbing spiral over the field to the High Key (7000 ft, turning left, no loops), the SFO, an aligned final, lands",
            (climb.Count > 0 ? $"climbed to {climb.Max(Height):F0} ft, at most {far:F2} NM from the High Key, turned right {backwards:F0}° in all" : "no climb")
            + string.Concat(climb.Where((st, n) => n > 0 && SimEngine.Wrap(st.HeadingTrue - climb[n - 1].HeadingTrue) > 1).Take(12)
                .Select(st => $" [{Frame(st).Along:F2},{Frame(st).Lateral:F2},h{SimEngine.Wrap(st.HeadingTrue - CircuitCourse):F0},{Height(st):F0}]")) + " " + Legs(flown));
        pictures.Add(("SFO, go-around, SFO again: climbing spiral over the field to the High Key (Jet)", flown));
    }

    // 22c. Hold at the High Key: three turns there, then "start SFO".
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Right, EndAction.FullStop, performanceClass: "Jet", hold: true);
        double turned = 0;
        double? lastHeading = null;
        bool released = false;
        List<AircraftState> flown = [sim.Get(cs)!];
        flown.AddRange(Fly(sim, cs, 1500, st =>
        {
            if (st.Leg == "HOLD")
            {
                if (lastHeading is double before) turned += Math.Abs(SimEngine.Wrap(st.HeadingTrue - before));
                lastHeading = st.HeadingTrue;
                if (!released && turned >= 3 * 360) { released = true; sim.HoldAtHighKey(cs, false); }
            }
        }));
        List<AircraftState> hold = OnLeg(flown, "HOLD");
        (double hx, double hy) = Place(0.5, 0, CircuitSide.Right);
        double far = hold.Count > 0 ? hold.Max(st => Math.Sqrt((st.X - hx) * (st.X - hx) + (st.Y - hy) * (st.Y - hy))) : double.NaN;
        List<AircraftState> late = hold.Skip(hold.Count / 3).ToList();
        // On the circle of the High Key: over it, on the right side, 0.6 NM radius.
        (double cx, double cy) = Place(0.5, 0.6, CircuitSide.Right);
        double offCircle = late.Count > 0 ? late.Max(st => Math.Abs(Math.Sqrt((st.X - cx) * (st.X - cx) + (st.Y - cy) * (st.Y - cy)) - 0.6)) : double.NaN;
        int started = flown.FindIndex(st => st.Leg == "LOW KEY");
        List<AircraftState> shortFinal = started < 0 ? [] : flown.Skip(started).Where(st => st.Leg == "FINAL" && st.Contact == GroundContact.Airborne && Frame(st).Along > -0.15).ToList();
        Check(released && late.Count > 10 && late.All(st => Near(Height(st), 7000, 150)) && offCircle < 0.2 && started > 0
            && shortFinal.All(st => Math.Abs(Frame(st).Lateral) < 0.1) && LandedOnRunway(flown),
            "SFO hold at the High Key: on its circle over the field (within 0.2 NM) at 7000 ft, three turns, then start SFO, an aligned final, lands",
            (hold.Count > 0 ? $"turned {turned:F0}°, at most {offCircle:F2} NM off the circle, heights {late.Min(Height):F0}-{late.Max(Height):F0} ft" : "no hold")
            + "; legs " + string.Join(">", flown.Select(st => st.Leg).Where((leg, i) => i == 0 || leg != flown[i - 1].Leg)) + $"; last {flown[^1].Contact} {Height(flown[^1]):F0} ft"
            + string.Concat(flown.Where((st, i) => i > 0 && st.Leg == "LOW KEY" && (flown[i - 1].Leg != "LOW KEY" || i % 20 == 0)).Take(12)
                .Select(st => $" [{Frame(st).Along:F2},{Frame(st).Lateral:F2},h{SimEngine.Wrap(st.HeadingTrue - CircuitCourse):F0},r{st.TurnRateDps:F1},{st.Navigation},{st.RouteNext}]")));
        pictures.Add(("SFO hold at the High Key, three turns, then start (Jet, right)", flown));
    }

    // 24. Extend downwind, then turn base (the user, 3 Nov: A): straight on beyond the base turn, then the base from there.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        bool extended = false, based = false;
        List<AircraftState> flown = Fly(sim, cs, 1200, st =>
        {
            if (!extended && st.Leg == "DOWNWIND" && Frame(st).Along < 0.5) { extended = true; sim.ExtendDownwind(cs); }
            if (extended && !based && st.Leg == "EXT DOWNWIND" && Frame(st).Along < -3) { based = true; sim.TurnBase(cs); }
        });
        List<AircraftState> ext = OnLeg(flown, "EXT DOWNWIND");
        List<AircraftState> baseLeg = OnLeg(flown, "BASE");
        Check(based && ext.Count > 10 && ext.All(st => Near(Height(st), 1000, 60) && Near(Frame(st).Lateral, 1.0, 0.25)) && baseLeg.Count > 0
            && baseLeg.All(st => Frame(st).Along < -2.7) && LandedOnRunway(flown),
            "Extend downwind: straight on at circuit height beyond the base turn (to 3 NM), turn base there, then lands",
            baseLeg.Count > 0 ? $"base at {baseLeg.Average(st => Frame(st).Along):F2} NM" : "no base");
        pictures.Add(("Extend downwind to 3 NM, then turn base (Light)", flown));
    }

    // 25. The next circuit (the plan): ordered on the downwind, flown after the touch and go; the SFO too (not at once).
    void NextCircuitTest(CircuitType next, string title)
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Jet");
        bool nextOrdered = false, rolled = false;
        AircraftState? shown = null;
        List<AircraftState> flown = Fly(sim, cs, 2400, st =>
        {
            if (!nextOrdered && st.Leg == "DOWNWIND") { nextOrdered = true; sim.SetNextCircuit(cs, next, CircuitSide.Left); }
            else if (nextOrdered && shown == null) shown = st;
            if (!rolled && st.Contact == GroundContact.Rolling) { rolled = true; sim.SetEndAction(cs, EndAction.FullStop); }
            if (st.Leg == "HOLD") sim.HoldAtHighKey(cs, false);
        });
        int roll = flown.FindIndex(st => st.Contact == GroundContact.Rolling);
        Check(shown?.NextCircuit == next && roll > 0 && flown.Take(roll).All(st => st.Circuit == CircuitType.Conventional && st.Leg is not ("CLIMB" or "HOLD"))
            && flown.Skip(roll + 30).Any(st => st.Circuit == next) && LandedOnRunway(flown),
            $"Next circuit {title}: the conventional finished first (touch and go), then the {title}, then lands",
            $"next shown {shown?.NextCircuit}, touch and go at {roll} s");
        pictures.Add(($"Next circuit {title} ordered on the downwind: flown after the touch and go (Jet)", flown));
    }
    NextCircuitTest(CircuitType.Close, "close pattern");
    NextCircuitTest(CircuitType.Sfo, "SFO");

    // 26. To the initial point from the wide downwind: straight to the IP, the turn onto the initial (no loops).
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Wide, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        sim.RunFor(20);
        sim.SetCircuit(cs, CircuitType.Overhead, CircuitSide.Left);
        List<AircraftState> flown = Fly(sim, cs, 1500);
        int initial = flown.FindIndex(st => st.Leg == "INITIAL");
        double turned = 0;
        for (int n = 1; n <= initial && n < flown.Count; n++) turned += Math.Abs(SimEngine.Wrap(flown[n].HeadingTrue - flown[n - 1].HeadingTrue));
        AircraftState? onInitial = initial < 0 ? null : flown.Skip(initial).FirstOrDefault(st => Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) < 10);
        Check(initial > 0 && turned < 240 && onInitial != null && Math.Abs(Frame(onInitial).Lateral) < 0.3 && Frame(onInitial).Along < -2.0 && LandedOnRunway(flown),
            "From the wide downwind to the initial point: straight to the IP, one turn onto the initial (no loops), then the overhead",
            initial > 0 ? $"turned {turned:F0}° before the initial, on it at {Frame(onInitial ?? flown[initial]).Along:F2} NM, {Frame(onInitial ?? flown[initial]).Lateral:F2} NM off the track" : "never on the initial");
        pictures.Add(("Wide downwind, then the overhead ordered: straight to the IP (Jet)", flown));
    }

    // 27. SFO on hold at the High Key by default (the user, 3 Nov): it circles there until START SFO.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        List<AircraftState> flown = Fly(sim, cs, 300);
        Check(flown.Count > 0 && flown[^1].Leg == "HOLD" && flown[^1].SfoHold && flown.All(st => st.Leg != "LOW KEY"),
            "SFO: on hold at the High Key by default (no Low Key in 5 minutes without START SFO)", flown.Count > 0 ? flown[^1].Leg : "");
    }

    // 28. Orbits until "continue" (ORBIT ∞), then on along the downwind; an orbit ended by "turn base".
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        bool asked = false, continued = false;
        double turned = 0;
        double? last = null;
        List<AircraftState> flown = Fly(sim, cs, 1500, st =>
        {
            if (!asked && StraightOn(st) && Frame(st).Along < 1.0) { asked = true; sim.Orbit(cs, forever: true); }
            if (st.Leg == "ORBITS")
            {
                if (last is double h) turned += Math.Abs(SimEngine.Wrap(st.HeadingTrue - h));
                last = st.HeadingTrue;
                if (!continued && turned > 2.5 * 360) { continued = true; sim.ContinueAfterOrbit(cs); }
            }
        });
        int end = flown.FindLastIndex(st => st.Leg == "ORBITS");
        Check(continued && turned > 900 && end > 0 && flown.Skip(end + 1).Any(StraightOn) && LandedOnRunway(flown),
            "ORBIT ∞: orbits until CONTINUE (two and a half here), then on along the downwind, lands", $"turned {turned:F0}°");
        pictures.Add(("Orbits until continue, then on along the downwind (Light)", flown));

        SimEngine sim2 = NewCircuitEngine();
        string cs2 = sim2.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        bool orbited = false, based = false;
        double turned2 = 0;
        double? lastHeading2 = null;
        List<AircraftState> flown2 = Fly(sim2, cs2, 1200, st =>
        {
            if (!orbited && StraightOn(st) && Frame(st).Along < 0.5) { orbited = true; sim2.Orbit(cs2); }
            if (st.Leg == "ORBIT")
            {
                if (lastHeading2 is double h) turned2 += Math.Abs(SimEngine.Wrap(st.HeadingTrue - h));
                lastHeading2 = st.HeadingTrue;
                if (!based && turned2 > 120) { based = true; sim2.TurnBase(cs2); }
            }
        });
        Check(based && turned2 < 300 && flown2.Any(st => st.Leg == "BASE") && LandedOnRunway(flown2),
            "TURN BASE during an orbit: the orbit ends at once, the base from there, lands", $"orbit stopped after {turned2:F0}°");
    }

    // 29. GO AROUND (the button): only on the final; level, to the threshold, then the runway heading until a new order.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        bool early = false, around = false;
        string legAfterEarly = "";
        double heightAtGa = double.NaN;
        List<AircraftState> flown = Fly(sim, cs, 400, st =>
        {
            if (!early && st.Leg == "DOWNWIND") { early = true; sim.GoAround(cs); }
            else if (early && legAfterEarly.Length == 0) legAfterEarly = st.Leg;
            if (!around && st.Leg == "FINAL" && Frame(st).Along > -0.8) { around = true; heightAtGa = Height(st); sim.GoAround(cs); }
        });
        int gaIndex = flown.FindIndex(st => st.Leg == "GO AROUND");
        List<AircraftState> after = gaIndex < 0 ? [] : flown.Skip(gaIndex).ToList();
        AircraftState? lastState = after.LastOrDefault();
        Check(legAfterEarly == "DOWNWIND" && gaIndex > 0 && after.All(st => st.Contact == GroundContact.Airborne && Height(st) > heightAtGa - 40)
            && lastState != null && Math.Abs(SimEngine.Wrap(lastState.HeadingTrue - CircuitCourse)) < 5 && Math.Abs(Frame(lastState).Lateral) < 0.2
            && lastState.Leg is "GO AROUND" or "MANUAL" && after.All(st => st.Leg is not ("UPWIND" or "CROSSWIND" or "DOWNWIND")),
            "GO AROUND: not on the downwind; on the final it stops descending, passes the threshold and keeps the runway heading (no circuit by itself)",
            lastState != null ? $"{lastState.Leg}, {Height(lastState):F0} ft (GA at {heightAtGa:F0}), heading {SimEngine.Wrap(lastState.HeadingTrue - CircuitCourse):F0}° off the runway" : "no go-around");
        pictures.Add(("GO AROUND on the final: level, runway heading until a new order (Light)", flown));
    }

    // 30. BREAKUP: on the downwind out to 90°, away from the runway; on the base on turning past the final to its reciprocal.
    {
        double Breakup(Func<AircraftState, bool> when, out string leg)
        {
            SimEngine sim = NewCircuitEngine();
            string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
            bool given = false;
            List<AircraftState> flown = Fly(sim, cs, 600, st =>
            {
                if (!given && when(st)) { given = true; sim.Breakup(cs); }
            });
            leg = flown.Count > 0 ? flown[^1].Leg : "";
            return flown.Count > 0 ? SimEngine.Wrap(flown[^1].HeadingTrue - CircuitCourse) : double.NaN;
        }
        double fromDownwind = Breakup(st => StraightOn(st) && Frame(st).Along < 0.5, out string leg1);
        double fromBase = Breakup(st => st.Leg == "BASE" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse - 90)) < 10, out string leg2);
        // Left-hand circuit, runway 140: downwind 320, away from the runway 050 (-90 from the runway); base 230 (+90) to 050 (-90).
        Check(Near(Math.Abs(fromDownwind), 90, 5) && leg1 == "MANUAL" && Near(fromBase, -90, 5) && leg2 == "MANUAL",
            "BREAKUP: out of the circuit, on the downwind to 90° away from the runway, on the base turning on to the reciprocal of the base",
            $"downwind → {fromDownwind:F0}°, base → {fromBase:F0}° from the runway course");
    }

    // 31. From an entry gate 6 NM out: into the circuit by each entry, at the arrival height of the class.
    {
        (double gx, double gy) = Place(-4, 4.5, CircuitSide.Left);
        List<AircraftState> FromGate(CircuitEntry entry, CircuitType type, string kind, out SimEngine sim, out string cs)
        {
            sim = NewCircuitEngine();
            cs = sim.AddAtGate(gx, gy, entry, type, CircuitSide.Left, EndAction.FullStop, performanceClass: kind);
            SimEngine me = sim;
            string callsign = cs;
            return Fly(sim, cs, 1800, st => { if (st.Leg == "HOLD") me.HoldAtHighKey(callsign, false); });
        }
        List<AircraftState> dw = FromGate(CircuitEntry.Downwind, CircuitType.Conventional, "Light", out _, out _);
        List<AircraftState> bs = FromGate(CircuitEntry.Base, CircuitType.Conventional, "Light", out _, out _);
        List<AircraftState> fin = FromGate(CircuitEntry.Final, CircuitType.Conventional, "Turboprop", out _, out _);
        List<AircraftState> ip = FromGate(CircuitEntry.InitialPoint, CircuitType.Conventional, "Jet", out _, out _);
        List<AircraftState> hk = FromGate(CircuitEntry.HighKey, CircuitType.Conventional, "Jet", out _, out _);
        Check(dw.Count > 0 && Near(Height(dw[0]), 1500, 30) && dw.Any(StraightOn) && LandedOnRunway(dw),
            "From a gate 6 NM out (Light, 1500 ft: 500 above its circuit): 45° onto the downwind, lands", dw.Count > 0 ? $"{Height(dw[0]):F0} ft" : "");
        Check(bs.Any(st => st.Leg == "BASE") && bs.All(st => st.Leg != "DOWNWIND") && LandedOnRunway(bs), "From a gate: straight to the base, lands");
        Check(fin.All(st => st.Leg is not ("DOWNWIND" or "BASE")) && LandedOnRunway(fin), "From a gate: straight in to the final, lands");
        Check(ip.Any(st => st.Leg == "INITIAL") && ip.All(st => st.Circuit is CircuitType.Overhead or CircuitType.Close) && LandedOnRunway(ip),
            "From a gate to the IP: the overhead, lands");
        Check(hk.Any(st => st.Leg == "HOLD") && hk.Any(st => st.Leg == "LOW KEY") && LandedOnRunway(hk), "From a gate to the High Key: on hold, started, the SFO, lands", Legs(hk) + " " + From(hk, "HIGH KEY", 5) + " ROUTE " + string.Join(" ", hk.FirstOrDefault(st => st.Leg == "HIGH KEY")?.Route?.Select(rp =>
            { AircraftState at = hk[0] with { X = rp.X, Y = rp.Y }; return $"<{Frame(at).Along:F2},{Frame(at).Lateral:F2}>"; }) ?? []));
        pictures.Add(("From a gate 6 NM out: 45° onto the downwind (Light)", dw));
        pictures.Add(("From a gate: straight to the base (Light)", bs));
        pictures.Add(("From a gate: straight in (Turboprop)", fin));
        pictures.Add(("From a gate: to the IP, overhead (Jet)", ip));
        pictures.Add(("From a gate: to the High Key, hold, SFO (Jet)", hk));
    }

    // 31b. From a gate at a height given for this aircraft (3000 ft): down to the circuit height before the circuit.
    {
        (double gx, double gy) = Place(-4, 4.5, CircuitSide.Left);
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddAtGate(gx, gy, CircuitEntry.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light", arrivalHeightFt: 3000);
        AircraftState first = sim.Get(cs)!;
        List<AircraftState> flown = Fly(sim, cs, 1200);
        AircraftState? onDownwind = flown.FirstOrDefault(StraightOn);
        Check(Near(Height(first), 3000, 5) && onDownwind != null && Near(Height(onDownwind), 1000, 60) && LandedOnRunway(flown),
            "From a gate at 3000 ft (given): at the circuit height (1000) before the downwind, lands",
            onDownwind != null ? $"{Height(first):F0} ft at the gate, {Height(onDownwind):F0} ft on the downwind" : "no downwind");
    }

    // 35-38. Formations (the user, 4 Nov). Every second, the traffic of an engine for a while.
    List<List<AircraftState>> FlyAll(SimEngine sim, double seconds, Action<int, List<AircraftState>>? each = null)
    {
        List<List<AircraftState>> all = [];
        for (int t = 0; t < seconds; t++)
        {
            sim.RunFor(1);
            List<AircraftState> now = sim.Snapshot().ToList();
            all.Add(now);
            each?.Invoke(t, now);
            if (now.Count == 0) break;
        }
        return all;
    }
    List<AircraftState> TrackOf(List<List<AircraftState>> all, string callsign) => all.SelectMany(n => n.Where(st => st.Callsign == callsign)).ToList();

    // 35. The callsigns of a formation: in sequence after the leader's number, reserved; a leader without a number refused.
    {
        SimEngine sim = NewCircuitEngine();
        string lead = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, "VIPER02", "Light");
        string? made = sim.MakeFormation(lead, 3);
        bool refused = false;
        try { sim.Add(0, 0, 3000, 0, 100, "VIPER04"); } catch (InvalidOperationException) { refused = true; }
        string other = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, "HAWK", "Light");
        string? noNumber = sim.MakeFormation(other, 2);
        AircraftState? shown = sim.Get(lead);
        Check(made == null && shown?.Formation is ["VIPER02", "VIPER03", "VIPER04"] && refused && noNumber != null && sim.Get("HAWK")?.Formation == null
            && SimEngine.FormationNames("VIPER09", 3) is ["VIPER10", "VIPER11"],
            "Formation: VIPER02 leads VIPER03 and VIPER04 (reserved); a leader without a number refused", made ?? noNumber ?? "");
    }

    // 36. Overhead pattern in formation of 4: one aircraft until the break, then each one breaks 3 s after the one before.
    {
        SimEngine sim = NewCircuitEngine();
        string lead = sim.AddInCircuit(CircuitStart.InitialPoint, CircuitType.Overhead, CircuitSide.Left, EndAction.FullStop, "VIPER01", "Jet");
        sim.MakeFormation(lead, 4);
        string[] names = ["VIPER01", "VIPER02", "VIPER03", "VIPER04"];
        Dictionary<string, int> brokeAt = [];
        int alone = 0;
        bool splitEarly = false;
        List<List<AircraftState>> all = FlyAll(sim, 900, (t, now) =>
        {
            if (now.Count == 1 && now[0].Formation?.Count == 4) alone++;
            if (now.Count == 1 && now[0].Leg == "INITIAL" && now[0].SplitAllowed) splitEarly = true;
            foreach (AircraftState st in now)
            {
                if (!brokeAt.ContainsKey(st.Callsign) && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) > 30) brokeAt[st.Callsign] = t;
            }
        });
        bool allBroke = names.All(brokeAt.ContainsKey);
        List<int> gaps = allBroke ? names.Skip(1).Select((n, i) => brokeAt[n] - brokeAt[names[i]]).ToList() : [];
        bool allLanded = names.All(n => LandedOnRunway(TrackOf(all, n)));
        bool downwinds = names.All(n => TrackOf(all, n).Any(st => st.Leg == "DOWNWIND"));
        // (The user, 4 Nov: on the runway at least a third of it after the one before: the downwinds lengthened.)
        Dictionary<string, int> down = [];
        for (int t = 0; t < all.Count; t++)
            foreach (AircraftState st in all[t]) if (st.Contact != GroundContact.Airborne && !down.ContainsKey(st.Callsign)) down[st.Callsign] = t;
        double finalKt = PerformanceClass.Defaults().First(k => k.Name == "Jet").FinalSpeedKt;
        List<double> spacing = names.All(down.ContainsKey) ? names.Skip(1).Select((n, i) => (down[n] - down[names[i]]) * finalKt / 3600).ToList() : [];
        Check(alone > 10 && !splitEarly && allBroke && gaps.All(g => g >= 2 && g <= 4) && downwinds && allLanded && spacing.Count == 3 && spacing.All(d => d >= CircuitLength / 3 * 0.9),
            "Formation (overhead, 4 jets): one aircraft until the break, each breaks about 3 s after the one before, each lands a third of the runway after the one before",
            $"together {alone} s, gaps {string.Join(" ", gaps)} s, landed {names.Count(n => LandedOnRunway(TrackOf(all, n)))} of 4, apart on the runway {string.Join(" ", spacing.Select(d => d.ToString("F2")))} NM");
        foreach (string n in names) pictures.Add(($"Formation overhead: {n}", TrackOf(all, n)));
    }

    // 37. From the long final in formation of 4: VIPER01 and VIPER03 land together, VIPER02 and VIPER04 go around (close pattern).
    {
        SimEngine sim = NewCircuitEngine();
        string lead = sim.AddInCircuit(CircuitStart.LongFinal, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, "VIPER01", "Light");
        sim.MakeFormation(lead, 4, [true, false, true, false]);
        bool landersTogether = false;
        List<List<AircraftState>> all = FlyAll(sim, 600, (t, now) =>
        {
            if (now.Any(st => st.Callsign == "VIPER01" && st.Formation is ["VIPER01", "VIPER03"])) landersTogether = true;
        });
        List<AircraftState> two = TrackOf(all, "VIPER02"), four = TrackOf(all, "VIPER04");
        bool goneAround = two.Any(st => st.Leg == "GO AROUND") && four.Any(st => st.Leg == "GO AROUND")
            && two.Any(st => st.Circuit == CircuitType.Close && st.Leg == "DOWNWIND") && four.Any(st => st.Circuit == CircuitType.Close && st.Leg == "DOWNWIND");
        bool landed = LandedOnRunway(TrackOf(all, "VIPER01")) && TrackOf(all, "VIPER03").Count == 0;
        // The two going around not on top of each other: apart on the straight part of their downwind.
        bool Straight(AircraftState st) => st.Leg == "DOWNWIND" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse - 180)) < 10;
        List<double> apart = all.Where(n => n.Any(st => st.Callsign == "VIPER02" && Straight(st)) && n.Any(st => st.Callsign == "VIPER04" && Straight(st)))
            .Select(n => { AircraftState a2 = n.First(st => st.Callsign == "VIPER02"), a4 = n.First(st => st.Callsign == "VIPER04"); return Math.Sqrt(Math.Pow(a2.X - a4.X, 2) + Math.Pow(a2.Y - a4.Y, 2)); }).ToList();
        Check(landersTogether && landed && goneAround && apart.Count > 5 && apart.Min() > 0.08,
            "Formation (long final, 4): two chosen land together, the other two go around alone into the close pattern, apart",
            $"apart on the downwind {(apart.Count > 0 ? apart.Min() : double.NaN):F2} NM at least");
        pictures.Add(("Formation from the long final: VIPER02 going around", two));
    }

    // 38. Conventional circuit in formation of 3: at most 2 land together (VIPER01, VIPER02), VIPER03 goes around; a split
    // by command on the downwind: two aircraft of their own.
    {
        SimEngine sim = NewCircuitEngine();
        string lead = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, "VIPER01", "Light");
        sim.MakeFormation(lead, 3);
        bool pairLanding = false;
        List<List<AircraftState>> all = FlyAll(sim, 600, (t, now) =>
        {
            if (now.Any(st => st.Callsign == "VIPER01" && st.Formation is ["VIPER01", "VIPER02"])) pairLanding = true;
        });
        bool threeAround = TrackOf(all, "VIPER03").Any(st => st.Leg == "GO AROUND");
        SimEngine sim2 = NewCircuitEngine();
        string lead2 = sim2.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, "COBRA1", "Light");
        sim2.MakeFormation(lead2, 2);
        bool allowed = sim2.Get(lead2)?.SplitAllowed == true;
        sim2.RunFor(5);
        sim2.SplitFormation(lead2);
        sim2.RunFor(10);
        AircraftState? cobraA = sim2.Get("COBRA1"), cobraB = sim2.Get("COBRA2");
        Check(pairLanding && threeAround && LandedOnRunway(TrackOf(all, "VIPER01")) && allowed && cobraA != null && cobraB != null && cobraA.Formation == null
            && cobraB.Circuit == CircuitType.Conventional && cobraB.Leg == "DOWNWIND",
            "Formation (conventional, 3): two land together, the third goes around; SPLIT on the downwind: two aircraft in the circuit");
    }

    // 39. The plan of its circuits (the user, 4 Nov): 3 circuits, the first two with a touch and go, the last a full stop.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Light");
        sim.SetLaps(cs, 3, EndAction.FullStop);
        List<AircraftState> flown = Fly(sim, cs, 2400);
        int touches = 0;
        for (int i = 1; i < flown.Count; i++) if (flown[i].Contact == GroundContact.Rolling && flown[i - 1].Contact == GroundContact.Airborne) touches++;
        Check(touches == 2 && LandedOnRunway(flown) && sim.Get(cs) == null && flown[0].LapsLeft == 3,
            "Circuits of the plan: 3 circuits, two touch and go, then a full stop", $"{touches} touch and go");
    }

    // 39b. On Final (the user, 5 Nov): 3 circuits, the first two with a low approach (L/A), the last a full stop: never a
    // touch and go, two go-arounds.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Light");
        sim.SetLaps(cs, 3, EndAction.FullStop, EndAction.GoAround);
        List<AircraftState> flown = Fly(sim, cs, 2400);
        int touches = 0, arounds = 0;
        for (int i = 1; i < flown.Count; i++)
        {
            if (flown[i].Contact == GroundContact.Rolling && flown[i - 1].Contact == GroundContact.Airborne) touches++;
            if (flown[i].Leg == "GO AROUND" && flown[i - 1].Leg != "GO AROUND") arounds++;
        }
        Check(touches == 0 && arounds == 2 && LandedOnRunway(flown) && sim.Get(cs) == null,
            "On Final L/A: 3 circuits, two low approaches, then a full stop", $"{touches} touch and go, {arounds} go-arounds");
    }

    // 40. The landing roll (the user, 4 Nov): braking with its class to the first runway exit it reaches slow, turning off
    // there, then gone; a Light takes an earlier exit than a Jet; no exits: gone where it is slow.
    {
        (double ax, double ay) = Place(0.3, 0.03, CircuitSide.Right);
        (double bx, double by) = Place(0.45, 0.03, CircuitSide.Left);
        (double cx, double cy) = Place(0.9, 0.03, CircuitSide.Right);
        string Exit(string kind, bool withExits, out double lastAlong, out double slowest)
        {
            SimEngine sim = NewCircuitEngine();
            if (withExits) sim.SetRunwayExits([("A", ax, ay), ("B", bx, by), ("C", cx, cy)]);
            string cs = sim.AddInCircuit(CircuitStart.LongFinal, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: kind);
            List<AircraftState> flown = Fly(sim, cs, 900);
            List<AircraftState> rolling = flown.Where(st => st.Contact == GroundContact.Landed).ToList();
            lastAlong = rolling.Count > 0 ? Frame(rolling[^1]).Along : double.NaN;
            slowest = rolling.Count > 0 ? rolling.Min(st => st.SpeedKt) : double.NaN;
            return sim.Get(cs) == null && rolling.Count > 0 ? rolling[^1].Exit : "(not gone)";
        }
        string light = Exit("Light", true, out double lightAlong, out double lightSlow);
        string jet = Exit("Jet", true, out double jetAlong, out double jetSlow);
        string noExit = Exit("Light", false, out double bareAlong, out _);
        Check(light == "B" && jet == "C" && Near(lightAlong, 0.45, 0.05) && Near(jetAlong, 0.9, 0.05) && Near(lightSlow, 15, 1) && noExit == "" && bareAlong > 0.2 && bareAlong < 0.6,
            "Landing roll: a Light leaves the runway at B, a Jet at C (the first exit each reaches slow); without exits gone where slow",
            $"Light {light} at {lightAlong:F2} NM, Jet {jet} at {jetAlong:F2} NM, no exits gone at {bareAlong:F2} NM");
    }

    // 41. The instructor's end of the final in a plan of circuits (the user, 4 Nov): for that circuit only, then the plan.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Light");
        sim.SetLaps(cs, 3, EndAction.FullStop);
        sim.SetEndAction(cs, EndAction.LowPass);
        List<AircraftState> flown = Fly(sim, cs, 2400);
        int touchAndGo = 0;
        for (int i = 1; i < flown.Count; i++) if (flown[i].Contact == GroundContact.Rolling && flown[i - 1].Contact == GroundContact.Airborne) touchAndGo++;
        bool lowPass = flown.Any(st => st.EndAction == EndAction.LowPass) && flown.Any(st => st.EndAction == EndAction.TouchAndGo && st.LapsLeft == 2);
        Check(lowPass && touchAndGo == 1 && LandedOnRunway(flown) && sim.Get(cs) == null,
            "Plan of circuits: a low pass given for one circuit, then the plan again (one touch and go, then the full stop)", $"{touchAndGo} touch and go");
    }

    // 42. Formation landing, both full stop (the user, 4 Nov): reduced rwy separation (the one ahead has rolled a third of
    // the runway when the one behind touches down) and full (the one ahead has left the runway).
    {
        (double sepFirst, bool sepGone) Separation(bool full)
        {
            SimEngine sim = NewCircuitEngine();
            if (full) sim.SetAerodrome(new AerodromeCircuits { FullRunwaySeparation = true }, "");
            string lead = sim.AddInCircuit(CircuitStart.InitialPoint, CircuitType.Overhead, CircuitSide.Left, EndAction.FullStop, "EAGLE1", "Jet");
            sim.MakeFormation(lead, 2);
            double ahead = double.NaN;
            bool gone = false;
            FlyAll(sim, 900, (t, now) =>
            {
                AircraftState? two = now.FirstOrDefault(st => st.Callsign == "EAGLE2");
                if (!double.IsNaN(ahead) || gone || two == null || two.Contact == GroundContact.Airborne) return;
                AircraftState? one = now.FirstOrDefault(st => st.Callsign == "EAGLE1");
                if (one == null) gone = true; else ahead = Frame(one).Along;
            });
            return (ahead, gone);
        }
        (double reducedAhead, bool reducedGone) = Separation(false);
        (double fullAhead, bool fullGone) = Separation(true);
        Check((reducedGone || reducedAhead >= 0.15 + CircuitLength / 3 - 0.03) && fullGone,
            "Formation landing, both full stop: reduced rwy separation (a third of the runway ahead), full (the one ahead off the runway)",
            $"reduced: the one ahead {(reducedGone ? "gone" : $"at {reducedAhead:F2} NM")}; full: {(fullGone ? "gone" : $"still on the runway at {fullAhead:F2} NM")}");
    }

    // 43. TURN BASE before the threshold (the user, 4 Nov): on along the downwind to 0.3 NM past the threshold, a short base,
    // never across the field; a conventional circuit and a close pattern.
    {
        string BaseEarly(CircuitType type, string kind, out double closest, out double turnedAt)
        {
            SimEngine sim = NewCircuitEngine();
            string cs = sim.AddInCircuit(CircuitStart.Downwind, type, CircuitSide.Left, EndAction.FullStop, performanceClass: kind);
            bool given = false;
            double near = double.PositiveInfinity, turned = double.NaN;
            List<AircraftState> flown = Fly(sim, cs, 1200, st =>
            {
                (double along, double lateral) = Frame(st);
                if (!given && st.Leg == "DOWNWIND" && along < CircuitLength * 0.7 && along > 0.1) { given = true; sim.TurnBase(cs); return; }
                if (!given || st.Contact != GroundContact.Airborne) return;
                // Before the threshold (along > 0.05, not yet on the final): not towards the runway.
                if (along > 0.05 && double.IsNaN(turned)) near = Math.Min(near, lateral);
                // Where it leaves the downwind (halfway to the runway): past the threshold.
                if (double.IsNaN(turned) && lateral < 0.5) turned = along;
            });
            closest = near;
            turnedAt = turned;
            return given && LandedOnRunway(flown) ? "landed" : given ? "not landed" : "never given";
        }
        string conventional = BaseEarly(CircuitType.Conventional, "Light", out double convClosest, out double convTurned);
        string close = BaseEarly(CircuitType.Close, "Jet", out double closeClosest, out _);
        Check(conventional == "landed" && close == "landed" && convClosest > 0.6 && closeClosest > 0.6 && convTurned < -0.1,
            "TURN BASE before the threshold: on along the downwind, a short base past the threshold, lands (conventional and close)",
            $"conventional {conventional}, nearest the runway before the threshold {convClosest:F2} NM, base at {convTurned:F2}; close {close}, nearest {closeClosest:F2} NM");
    }

    // 44. The break of the close pattern (the user, 5 Nov: an overhead without the initial): END turns over the runway end;
    // standby on along the runway track (past where the upwind used to end) until BREAK NOW.
    {
        (List<AircraftState> Track, double TurnAlong, bool Straight) CloseBreak(BreakPoint where)
        {
            SimEngine sim = NewCircuitEngine();
            string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Close, CircuitSide.Left, EndAction.GoAround, performanceClass: "Jet");
            sim.SetBreak(cs, where);
            bool around = false, breakOrdered = false, straight = true;
            List<AircraftState> flown = Fly(sim, cs, 1500, st =>
            {
                if (st.Leg == "GO AROUND" && !around) { around = true; sim.SetEndAction(cs, EndAction.FullStop); }
                if (where == BreakPoint.Standby && around && !breakOrdered && st.Leg == "GO AROUND")
                {
                    if (Math.Abs(Frame(st).Lateral) > 0.3) straight = false;
                    if (Frame(st).Along > CircuitLength + 3) { breakOrdered = true; sim.BreakNow(cs); }
                }
            });
            int goneAround = flown.FindIndex(st => st.Leg == "GO AROUND");
            int turn = goneAround < 0 ? -1 : flown.FindIndex(goneAround, st => st.Leg == "DOWNWIND");
            return (flown, turn > 0 ? Frame(flown[turn]).Along : double.NaN, straight && (where != BreakPoint.Standby || breakOrdered));
        }
        (List<AircraftState> endTrack, double endTurn, _) = CloseBreak(BreakPoint.End);
        (List<AircraftState> stbyTrack, double stbyTurn, bool stbyStraight) = CloseBreak(BreakPoint.Standby);
        Check(Between(endTurn, CircuitLength - 0.05, CircuitLength + 0.3) && LandedOnRunway(endTrack),
            "Close pattern, break END: turns to the downwind over the runway end, then lands", $"turn at {endTurn:F2} NM");
        Check(stbyStraight && stbyTurn > CircuitLength + 3 && LandedOnRunway(stbyTrack),
            "Close pattern, standby for break: on along the runway track until BREAK NOW, then the downwind, lands", $"turn at {stbyTurn:F2} NM, straight {stbyStraight}");
        pictures.Add(("Close pattern, standby for break until BREAK NOW (Jet)", stbyTrack));
    }

    // 45. TURN BASE while still joining (the user, 4 Nov, with a drawing): straight to the start of the base, never up to
    // the downwind start; then the base and the final, lands.
    {
        SimEngine sim = NewCircuitEngine();
        (double jx, double jy) = Place(-3, 4, CircuitSide.Left);
        string cs = sim.Add(jx, jy, CircuitElevation + 1000, CircuitCourse, 100, performanceClass: "Light");
        sim.SetCircuit(cs, CircuitType.Conventional, CircuitSide.Left);
        sim.RunFor(2);
        string legBefore = sim.Get(cs)?.Leg ?? "";
        sim.TurnBase(cs);
        List<AircraftState> flown = Fly(sim, cs, 1200);
        int onBase = flown.FindIndex(st => st.Leg == "BASE");
        // (Before the base: on the runway it rolls on past the threshold.)
        double farthest = onBase > 0 ? flown.Take(onBase).Max(st => Frame(st).Along) : double.NaN;
        double closestBefore = onBase > 0 ? flown.Take(onBase).Min(st => Frame(st).Lateral) : double.NaN;
        Check(legBefore == "JOIN" && onBase > 0 && farthest < 0.2 && closestBefore > 0.6 && LandedOnRunway(flown),
            "TURN BASE while joining: straight to the start of the base (not the downwind), then the base, lands",
            $"leg before {legBefore}, farthest along {farthest:F2} NM, nearest the runway before the base {closestBefore:F2} NM");
        pictures.Add(("TURN BASE while joining: straight to the base (Light)", flown));
    }

    // 46. Departures with no circuit (the user, 5 Nov): along the runway heading, or onto a radial (from the middle of the
    // runway) turning right or left above 400 ft; gone 10 NM out.
    {
        (double midX, double midY) = Place(CircuitLength / 2, 0, CircuitSide.Left);
        (List<AircraftState> Track, bool Gone) Depart(double radial, bool? turnRight)
        {
            SimEngine sim = NewCircuitEngine();
            string cs = sim.AddDeparture(radial, turnRight, null, null, null, performanceClass: "Turboprop");
            List<AircraftState> flown = Fly(sim, cs, 1500);
            return (flown, sim.Get(cs) == null);
        }
        double FromField(AircraftState st) => Math.Sqrt((st.X - midX) * (st.X - midX) + (st.Y - midY) * (st.Y - midY));
        double OffRadial(AircraftState st, double radial) =>
            (st.X - midX) * Math.Cos(radial * Math.PI / 180) - (st.Y - midY) * Math.Sin(radial * Math.PI / 180);
        // (The first turn: its side and its height.)
        (int Side, double Height, double Along) FirstTurn(List<AircraftState> track)
        {
            for (int i = 1; i < track.Count; i++)
            {
                double change = SimEngine.Wrap(track[i].HeadingTrue - track[0].HeadingTrue);
                if (track[i].Contact == GroundContact.Airborne && Math.Abs(change) > 3) return (Math.Sign(change), Height(track[i]), Frame(track[i]).Along);
            }
            return (0, double.NaN, double.NaN);
        }
        (List<AircraftState> straight, bool straightGone) = Depart(double.NaN, null);
        Check(straightGone && straight.Count > 10 && straight.All(st => Math.Abs(Frame(st).Lateral) < 0.3) && Between(FromField(straight[^1]), 9, 10.5)
            && straight.Max(st => Height(st)) > 2800,
            "Departure RWY HDG: along the runway heading, up to 3000 ft, gone 10 NM out",
            $"gone {straightGone}, last {FromField(straight[^1]):F1} NM, highest {straight.Max(st => Height(st)):F0} ft");
        double radialRight = SimEngine.Normalize(CircuitCourse + 90);
        (List<AircraftState> right, bool rightGone) = Depart(radialRight, true);
        (int rightSide, double rightHeight, double rightAlong) = FirstTurn(right);
        AircraftState rightOut = right[^1];
        // (The user, 5 Nov: past the far end of the runway.)
        Check(rightGone && rightSide > 0 && rightHeight > 380 && rightAlong > CircuitLength - 0.02 && Math.Abs(OffRadial(rightOut, radialRight)) < 0.3
            && Math.Abs(SimEngine.Wrap(rightOut.HeadingTrue - radialRight)) < 15,
            "Departure onto a radial, turning right above 400 ft: along the radial, gone 10 NM out",
            $"turn {rightSide} at {rightHeight:F0} ft {rightAlong:F2} NM along, off the radial {OffRadial(rightOut, radialRight):F2} NM, heading {rightOut.HeadingTrue:F0}");
        (List<AircraftState> left, bool leftGone) = Depart(radialRight, false);
        (int leftSide, _, _) = FirstTurn(left);
        AircraftState leftOut = left[^1];
        Check(leftGone && leftSide < 0 && Math.Abs(OffRadial(leftOut, radialRight)) < 0.3 && Math.Abs(SimEngine.Wrap(leftOut.HeadingTrue - radialRight)) < 15,
            "Departure onto the same radial turning left (the long way): along the radial, gone",
            $"turn {leftSide}, off the radial {OffRadial(leftOut, radialRight):F2} NM, heading {leftOut.HeadingTrue:F0}");
        pictures.Add(("Departure onto a radial, turning right (Turboprop)", right));
        pictures.Add(("Departure onto the same radial, turning left (Turboprop)", left));
    }

    // 47. Away after the circuits (the user, 5 Nov): a departure with 2 circuits, each final a low pass, then onto its radial,
    // never down, gone 10 NM out; a crossing with a circuit (a touch and go), then to its gate To, gone there.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, CircuitSide.Left, EndAction.LowPass, performanceClass: "Light");
        sim.SetLaps(cs, 2, EndAction.LowPass, EndAction.LowPass);
        double radialOut = SimEngine.Normalize(CircuitCourse + 90);
        sim.LeaveAfterCircuits(cs, radialOut, true, null, null, null);
        List<AircraftState> flown = Fly(sim, cs, 2400);
        int touches = 0, finals = 0;
        for (int i = 1; i < flown.Count; i++)
        {
            if (flown[i].Contact == GroundContact.Rolling && flown[i - 1].Contact == GroundContact.Airborne) touches++;
            if (flown[i - 1].Leg == "FINAL" && flown[i].Leg != "FINAL" && Frame(flown[i]).Along > CircuitLength - 0.1) finals++;
        }
        bool departed = flown.Any(st => st.Leg == "DEPARTURE");
        Check(touches == 0 && finals == 2 && departed && sim.Get(cs) == null,
            "Departure with circuits: two low passes, then away on its radial, gone", $"{touches} touch and go, {finals} low passes, departed {departed}, gone {sim.Get(cs) == null}");

        (double gx, double gy) = Place(-3, 5, CircuitSide.Left);
        (double ex, double ey) = Place(5, -5, CircuitSide.Left);
        SimEngine crossing = NewCircuitEngine();
        string cross = crossing.AddAtGate(gx, gy, CircuitEntry.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Light");
        crossing.SetLaps(cross, 1, EndAction.TouchAndGo, EndAction.TouchAndGo);
        crossing.LeaveAfterCircuits(cross, double.NaN, null, null, null, null, ex, ey);
        List<AircraftState> crossed = Fly(crossing, cross, 2400);
        int crossTouches = 0;
        for (int i = 1; i < crossed.Count; i++) if (crossed[i].Contact == GroundContact.Rolling && crossed[i - 1].Contact == GroundContact.Airborne) crossTouches++;
        double lastFromGate = crossed.Count > 0 ? Math.Sqrt((crossed[^1].X - ex) * (crossed[^1].X - ex) + (crossed[^1].Y - ey) * (crossed[^1].Y - ey)) : double.NaN;
        Check(crossTouches == 1 && crossing.Get(cross) == null && lastFromGate < 0.5,
            "Crossing with a circuit: one touch and go, then out to its gate To, gone there", $"{crossTouches} touch and go, last {lastFromGate:F2} NM from the gate");
        pictures.Add(("Departure with two low passes, then its radial (Light)", flown));
        pictures.Add(("Crossing with a circuit, then out to its gate (Light)", crossed));
    }

    // 48. The take-off keeps its conventional circuit (the user, 5 Nov: OVH chosen while taking off made it turn to the IP at
    // the lift-off): its first circuit conventional, the overhead after the touch and go.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Light");
        sim.RunFor(1);
        bool rolling = sim.Get(cs)?.Contact == GroundContact.Rolling;
        sim.SetNextCircuit(cs, CircuitType.Overhead, CircuitSide.Left);
        List<AircraftState> flown = Fly(sim, cs, 600);
        int firstDownwind = flown.FindIndex(st => st.Leg == "DOWNWIND");
        bool conventionalFirst = firstDownwind > 0 && flown.Take(firstDownwind).All(st => st.Circuit == CircuitType.Conventional);
        bool overheadLater = flown.Any(st => st.Circuit == CircuitType.Overhead);
        Check(rolling && conventionalFirst && overheadLater, "Take-off with OVH chosen: the first circuit conventional, the overhead after the touch and go",
            $"first downwind at {firstDownwind} s, conventional {conventionalFirst}, overhead later {overheadLater}");
    }

    // 49. DCT TO (the user, 5 Nov): on the WIDE downwind, BASE straight to the start of the conventional base (never farther
    // than the wide's downwind line again), then lands; IP: to the initial point, then the overhead.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Wide, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        sim.RunFor(20);
        double wideLateral = Frame(sim.Get(cs)!).Lateral;
        sim.DirectTo(cs, DirectPoint.Base, CircuitSide.Left);
        List<AircraftState> flown = Fly(sim, cs, 900);
        int onBase = flown.FindIndex(st => st.Leg == "BASE");
        bool conventional = flown.Count > 0 && flown[0].Circuit == CircuitType.Conventional;
        Check(conventional && onBase > 0 && onBase < 300 && LandedOnRunway(flown),
            "DCT TO BASE from the WIDE: the conventional base at once, then lands", $"conventional {conventional}, base at {onBase} s, wide at {wideLateral:F2} NM");

        SimEngine sim2 = NewCircuitEngine();
        string cs2 = sim2.AddInCircuit(CircuitStart.Downwind, CircuitType.Wide, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        sim2.RunFor(20);
        sim2.DirectTo(cs2, DirectPoint.InitialPoint, CircuitSide.Left);
        List<AircraftState> toIp = Fly(sim2, cs2, 900);
        bool initial = toIp.Any(st => st.Leg == "INITIAL");
        Check(toIp.Count > 0 && toIp[0].Circuit == CircuitType.Overhead && initial && LandedOnRunway(toIp),
            "DCT TO IP from the WIDE: to the initial point, the overhead, lands", $"initial {initial}");
        pictures.Add(("DCT TO BASE from the WIDE (Light)", flown));
        pictures.Add(("DCT TO IP from the WIDE (Jet)", toIp));
    }

    // 50. DCT TO IP after a take-off on the right (the overhead's side the left): out to the IP on its own side, never across
    // the runway (the user, 5 Nov).
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Runway, CircuitType.Conventional, CircuitSide.Right, EndAction.FullStop, performanceClass: "Jet");
        for (int i = 0; i < 300 && sim.Get(cs) is { } st && (st.Contact != GroundContact.Airborne || Height(st) < 500); i++) sim.RunFor(1);
        sim.DirectTo(cs, DirectPoint.InitialPoint, CircuitSide.Left);
        List<AircraftState> flown = Fly(sim, cs, 900);
        int initialAt = flown.FindIndex(st => st.Leg == "INITIAL");
        double c = CircuitCourse * Math.PI / 180;
        double leftMost = initialAt > 0 ? flown.Take(initialAt).Min(st => st.X * Math.Cos(c) - st.Y * Math.Sin(c)) : double.NaN;
        Check(initialAt > 0 && leftMost > -0.3, "DCT TO IP after a take-off on the right: to the IP on its own side, not across the runway",
            $"initial at {initialAt} s, farthest left {-leftMost:F2} NM");
        pictures.Add(("DCT TO IP after a take-off on the right (Jet)", flown));
    }

    // 51. DCT TO HPAD (the user, 5 Nov): a helicopter on the downwind straight to a pad beside the runway, never reaching
    // the base, down onto it, removed.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Helicopter");
        sim.RunFor(10);
        (double hx, double hy) = Place(0.5, -0.3, CircuitSide.Left);
        sim.DirectToPad(cs, hx, hy);
        List<AircraftState> flown = Fly(sim, cs, 900);
        AircraftState? landed = flown.FirstOrDefault(st => st.Contact == GroundContact.Landed);
        bool noBase = flown.All(st => st.Leg != "BASE");
        double missed = landed != null ? Math.Sqrt(Math.Pow(landed.X - hx, 2) + Math.Pow(landed.Y - hy, 2)) : double.NaN;
        Check(landed != null && missed < 0.05 && noBase && sim.Get(cs) == null,
            "DCT TO HPAD: the helicopter straight to its pad (no base), down onto it, removed", landed != null ? $"{missed:F3} NM from the pad" : "never landed");
        pictures.Add(("DCT TO HPAD from the downwind (Helicopter)", flown));
    }

    // 52. DCT TO BASE on the way to the final, out on the circuit side (the user, 5 Nov: it went far up and back): straight
    // to the start of the base, never past the threshold before the base, lands.
    {
        SimEngine sim = NewCircuitEngine();
        (double sx, double sy) = Place(-3.5, 1.5, CircuitSide.Left);
        string cs = sim.Add(sx, sy, CircuitElevation + 1000, CircuitCourse, 100, performanceClass: "Light");
        sim.SetCircuit(cs, CircuitType.Conventional, CircuitSide.Left);
        sim.RunFor(2);
        sim.DirectTo(cs, DirectPoint.Base, CircuitSide.Left);
        List<AircraftState> flown = Fly(sim, cs, 900);
        int onBase = flown.FindIndex(st => st.Leg == "BASE");
        double farthest = onBase > 0 ? flown.Take(onBase).Max(st => Frame(st).Along) : double.NaN;
        Check(onBase > 0 && farthest < 0 && LandedOnRunway(flown), "DCT TO BASE on the way in: straight to the start of the base, then lands",
            $"base at {onBase} s, farthest along before it {farthest:F2} NM");
        pictures.Add(("DCT TO BASE on the way in (Light)", flown));
    }

    // 53. A height given in the circuit (the user, 6 Nov): the height of this circuit; down as usual from the base / the Low
    // Key by itself (no AUTO), lands. The conventional downwind at 1500 ft; the SFO from a High Key 1000 ft higher.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        sim.RunFor(5);
        sim.SetAltitude(cs, CircuitElevation + 1500);
        List<AircraftState> flown = Fly(sim, cs, 1200);
        int onBase = flown.FindIndex(st => st.Leg == "BASE");
        double highest = onBase > 0 ? flown.Take(onBase).Max(Height) : double.NaN;
        Check(onBase > 0 && highest > 1400 && LandedOnRunway(flown), "A height given on the downwind: held there, down from the base by itself, lands",
            $"highest before the base {highest:F0} ft");

        SimEngine sfo = NewCircuitEngine();
        string jet = sfo.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        sfo.RunFor(2);
        double given = sfo.Get(jet)!.AltitudeFt + 1000;
        sfo.SetAltitude(jet, given);
        sfo.HoldAtHighKey(jet, false);
        List<AircraftState> glide = Fly(sfo, jet, 1800);
        int lowKey = glide.FindIndex(st => st.Leg == "LOW KEY");
        double top = lowKey > 0 ? glide.Take(lowKey).Max(st => st.AltitudeFt) : double.NaN;
        Check(lowKey > 0 && top > given - 150 && LandedOnRunway(glide), "SFO from a High Key given 1000 ft higher: it starts from there, down by itself, lands",
            $"highest before the Low Key {top - given:+0;-0} ft from the one given");
        pictures.Add(("SFO from a High Key 1000 ft higher (Jet)", glide));
    }

    // 54. SFO (the user, 6 Nov): START SFO on the far side of its circle: off at once the shortest way, never a whole circle
    // more; a High Key 4000 ft higher: one vertical speed from the High Key to the final (a longer downwind), lands.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        sim.HoldAtHighKey(cs, true);
        bool released = false;
        double turned = 0, lastHeading = double.NaN;
        List<AircraftState> flown = Fly(sim, cs, 1800, st =>
        {
            if (!released && st.Leg == "HOLD" && Math.Abs(SimEngine.Wrap(st.HeadingTrue - CircuitCourse)) > 150 && sim.Time > 120)
            {
                released = true;
                sim.HoldAtHighKey(cs, false);
                lastHeading = st.HeadingTrue;
            }
            else if (released && st.Leg != "LOW KEY" && double.IsFinite(lastHeading))
            {
                turned += Math.Abs(SimEngine.Wrap(st.HeadingTrue - lastHeading));
                lastHeading = st.HeadingTrue;
            }
            if (st.Leg == "LOW KEY") lastHeading = double.NaN;
        });
        Check(released && turned < 330 && LandedOnRunway(flown), "START SFO on the far side of the circle: off at once, no whole circle more, lands",
            $"turned {turned:F0}° before the Low Key; {Legs(flown)} {FromLowKey(flown)}");

        // 55. A departure straight to a point (a gate or an R/D point, the user 6 Nov): to it, gone there, also beyond 10 NM.
        {
            (double m55X, double m55Y) = Place(CircuitLength / 2, 0, CircuitSide.Left);
            foreach (double nm55 in new[] { 6.0, 15.0 })
            {
                double b55 = 40 * Math.PI / 180;
                (double p55X, double p55Y) = (m55X + nm55 * Math.Sin(b55), m55Y + nm55 * Math.Cos(b55));
                SimEngine s55 = NewCircuitEngine();
                string c55 = s55.AddDeparture(double.NaN, null, null, null, null, performanceClass: "Turboprop", exitX: p55X, exitY: p55Y);
                List<AircraftState> f55 = Fly(s55, c55, 1800);
                AircraftState l55 = f55[^1];
                double o55 = Math.Sqrt((l55.X - p55X) * (l55.X - p55X) + (l55.Y - p55Y) * (l55.Y - p55Y));
                Check(s55.Get(c55) == null && o55 < 0.6, $"Departure straight to a point {nm55:0} NM out: gone there", $"{o55:F2} NM from it when gone, {Legs(f55)}");
            }
        }

        // 56. Where a plan waiting is drawn = where the aircraft starts (StartPoint, the review of 6 Nov).
        {
            foreach ((CircuitStart st56, CircuitType ty56) in new[] { (CircuitStart.Downwind, CircuitType.Conventional), (CircuitStart.LongFinal, CircuitType.Conventional),
                (CircuitStart.InitialPoint, CircuitType.Overhead), (CircuitStart.HighKey, CircuitType.Sfo), (CircuitStart.Runway, CircuitType.Wide) })
            {
                SimEngine s55 = NewCircuitEngine();
                (double X, double Y)? d56 = s55.StartPoint(st56, ty56, CircuitSide.Right, "Jet");
                string c55 = s55.AddInCircuit(st56, ty56, CircuitSide.Right, EndAction.FullStop, performanceClass: "Jet");
                AircraftState? a56 = s55.Get(c55);
                Check(d56 is (double x56, double y56) && a56 != null && Math.Abs(a56.X - x56) < 1e-6 && Math.Abs(a56.Y - y56) < 1e-6,
                    $"Plan waiting drawn where it starts: {st56} {ty56}", d56 is { } dd56 && a56 != null ? $"{dd56.X:F3},{dd56.Y:F3} / {a56.X:F3},{a56.Y:F3}" : "none");
            }
        }

        // 57. A departure to a point on its turn side (the user, 6 Nov: L / R kept, also the long way), its exit altitude.
        {
            (double m57X, double m57Y) = Place(CircuitLength / 2, 0, CircuitSide.Left);
            double b57 = (CircuitCourse - 70) * Math.PI / 180;
            (double p57X, double p57Y) = (m57X + 8 * Math.Sin(b57), m57Y + 8 * Math.Cos(b57));
            foreach (bool right57 in new[] { false, true })
            {
                SimEngine s57 = NewCircuitEngine();
                string c57 = s57.AddDeparture(double.NaN, right57, null, null, 2000, performanceClass: "Turboprop", exitX: p57X, exitY: p57Y);
                double first57 = 0, high57 = 0;
                List<AircraftState> f57 = Fly(s57, c57, 1800, st =>
                {
                    if (first57 == 0 && st.Contact == GroundContact.Airborne && Math.Abs(st.TurnRateDps) > 0.5) first57 = st.TurnRateDps;
                    high57 = Math.Max(high57, Height(st));
                });
                AircraftState l57 = f57[^1];
                double o57 = Math.Sqrt((l57.X - p57X) * (l57.X - p57X) + (l57.Y - p57Y) * (l57.Y - p57Y));
                Check(s57.Get(c57) == null && o57 < 0.6 && (right57 ? first57 > 0 : first57 < 0) && Between(high57, 1900, 2050),
                    $"Departure to a point on the left, turn {(right57 ? "R (the long way)" : "L")}: turns that way, 2000 ft, gone there",
                    $"first turn rate {first57:F1}, highest {high57:F0} ft, {o57:F2} NM from it, {Legs(f57)}");
            }
        }

        // 58. A departure to a point taken off its way by the instructor (a heading): gone 10 NM out as the others (review of 6 Nov).
        {
            (double m58X, double m58Y) = Place(CircuitLength / 2, 0, CircuitSide.Left);
            double b58 = (CircuitCourse + 60) * Math.PI / 180;
            SimEngine s58 = NewCircuitEngine();
            string c58 = s58.AddDeparture(double.NaN, true, null, null, null, performanceClass: "Turboprop", exitX: m58X + 20 * Math.Sin(b58), exitY: m58Y + 20 * Math.Cos(b58));
            bool given58 = false;
            List<AircraftState> f58 = Fly(s58, c58, 1800, st =>
            {
                if (!given58 && st.Contact == GroundContact.Airborne && Height(st) > 800)
                {
                    given58 = true;
                    s58.SetHeading(c58, SimEngine.Wrap(CircuitCourse - 90) + 360);
                }
            });
            AircraftState l58 = f58[^1];
            double o58 = Math.Sqrt((l58.X - m58X) * (l58.X - m58X) + (l58.Y - m58Y) * (l58.Y - m58Y));
            Check(given58 && s58.Get(c58) == null && Between(o58, 9.8, 11), "Departure to a point, a heading of the instructor's: gone 10 NM out",
                $"{o58:F2} NM out when gone, {f58.Count} s");
        }

        // 59. A helicopter from a helipad (the user, 6 Nov): still on the pad, a hover of 15 ft in 4 s at most, an instruction
        // waits for its end, then away as from the runway.
        {
            SimEngine s59 = NewCircuitEngine();
            (double p59X, double p59Y) = Place(0.3, 0.15, CircuitSide.Left);
            string c59 = s59.AddDeparture(double.NaN, null, null, null, null, performanceClass: "Helicopter");
            s59.TakeOffFromPad(c59, p59X, p59Y);
            bool still59 = true, under59 = true;
            double hover59 = double.NaN, top59 = 0;
            AircraftState? end59 = null;
            for (double t59 = 0.5; t59 <= 3.9 + 1e-9; t59 += 0.1)
            {
                if (Math.Abs(t59 - 1.0) < 1e-9) s59.SetHeading(c59, SimEngine.Wrap(CircuitCourse + 90) + 360);
                s59.RunFor(t59 == 0.5 ? 0.5 : 0.1);
                AircraftState a59 = s59.Get(c59)!;
                still59 &= Math.Abs(a59.X - p59X) < 1e-6 && Math.Abs(a59.Y - p59Y) < 1e-6 && a59.Contact != GroundContact.Airborne;
                under59 &= Height(a59) <= 15 + 1e-6;
                top59 = Math.Max(top59, Height(a59));
                if (double.IsNaN(hover59) && Height(a59) > 15 - 0.01) hover59 = t59;
            }
            s59.RunFor(0.2);
            end59 = s59.Get(c59);
            Check(still59 && under59 && hover59 is >= 3 and <= 3.9 && end59 is { Contact: GroundContact.Airborne },
                "Helicopter from a helipad: still on the pad, 15 ft by 3.5 s, away after 4 s", $"top {top59:F1} ft, 15 ft at {hover59:F1} s, then {end59?.Contact}");
            List<AircraftState> f59 = Fly(s59, c59, 120);
            AircraftState l59 = f59[^1];
            Check(Math.Abs(SimEngine.Wrap(l59.HeadingTrue - (CircuitCourse + 90))) < 3 && Height(l59) > 100,
                "Helicopter from a helipad: the heading given in the hover flown after it", $"heading {l59.HeadingTrue:F0}, {Height(l59):F0} ft");
        }

        // 60. The heights of the classes are altitudes (QNH, the user 7 Nov): at an aerodrome 1200 ft high they become heights
        // above its runway, at least 300 ft; the heights written in the aerodrome stay above the threshold; made once only;
        // the departure climbs to 3000 ft QNH (at least 1000 ft above the runway); Import heights keeps the altitudes.
        {
            AerodromeCircuits a60 = AerodromeCircuits.Parse("{ \"Airport\": \"LIXX\", \"Runways\": [ { \"Runway\": \"14\", \"Side\": \"Left\", \"ElevationFt\": 1200 } ], "
                + "\"Classes\": [ { \"Class\": \"Jet\", \"ConventionalHeightFt\": 1000 } ], \"CallsignPrefix\": \"IBLA\", \"IcaoType\": \"C172\", \"DefaultClass\": \"Light\" }");
            PerformanceClass light60 = a60.Apply(PerformanceClass.Defaults().First(k => k.Name == "Light"), 1200);
            PerformanceClass jet60 = a60.Apply(PerformanceClass.Defaults().First(k => k.Name == "Jet"), 1200);
            PerformanceClass twice60 = a60.Apply(jet60, 1200);
            Check(light60 is { ConventionalHeightFt: 300, OverheadHeightFt: 500, HighKeyFt: 1300 } && jet60 is { ConventionalHeightFt: 1000, WideHeightFt: 300, OverheadHeightFt: 500 }
                && jet60.ArrivalHeightFt >= 1500 && twice60.ConventionalHeightFt == 1000 && twice60.HighKeyFt == jet60.HighKeyFt,
                "Class altitudes at a 1200 ft aerodrome: heights above the runway (at least 300 ft, the initial 500), the aerodrome's kept, made once",
                $"Light {light60.ConventionalHeightFt:F0}/{light60.OverheadHeightFt:F0}/{light60.HighKeyFt:F0}, Jet {jet60.ConventionalHeightFt:F0}/{jet60.WideHeightFt:F0}, arrival {jet60.ArrivalHeightFt:F0}");
            string again60 = JsonSerializer.Serialize(a60, AerodromeCircuits.JsonOptions);
            Check(a60.CallsignPrefix == "IBLA" && a60.IcaoType == "C172" && a60.DefaultClass == "Light" && again60.Contains("\"CallsignPrefix\"")
                && !JsonSerializer.Serialize(new AerodromeCircuits(), AerodromeCircuits.JsonOptions).Contains("CallsignPrefix"),
                "Aerodrome file: the base traffic (callsign, ICAO type, class), written only when given");

            SimEngine s60 = new();
            s60.SetGlidePath(CircuitCourse, 3, 1200, CircuitLength);
            s60.SetAerodrome(a60, "14");
            string d60 = s60.AddDeparture(double.NaN, null, null, null, null, performanceClass: "Light");
            List<AircraftState> f60 = Fly(s60, d60, 400);
            SimEngine h60 = new();
            h60.SetGlidePath(CircuitCourse, 3, 2500, CircuitLength);
            string e60 = h60.AddDeparture(double.NaN, null, null, null, null, performanceClass: "Turboprop");
            List<AircraftState> g60 = Fly(h60, e60, 400);
            Check(f60.Count > 0 && Near(f60.Max(st => st.AltitudeFt), 3000, 60) && g60.Count > 0 && Near(g60.Max(st => st.AltitudeFt), 3500, 60),
                "Departure without an exit altitude: 3000 ft QNH (1200 ft aerodrome), at least 1000 ft above a high one (2500 ft: 3500)",
                $"{(f60.Count > 0 ? f60.Max(st => st.AltitudeFt) : double.NaN):F0} / {(g60.Count > 0 ? g60.Max(st => st.AltitudeFt) : double.NaN):F0} ft");

            AerodromeCircuits other60 = AerodromeCircuits.Parse("{ \"Classes\": [ { \"Class\": \"Light\", \"ConventionalHeightFt\": 1400, \"HighKeyFt\": 2000 } ] }");
            AerodromeCircuits mine60 = AerodromeCircuits.Parse("{ \"Classes\": [ { \"Class\": \"Jet\", \"WideHeightFt\": 900, \"Tower\": \"kept\" }, { \"Class\": \"Light\", \"WideHeightFt\": 900 } ] }");
            mine60.CopyHeightsFrom(other60, 100, 400);
            ClassCircuit? l60 = mine60.Classes.FirstOrDefault(c => c.Class == "Light");
            ClassCircuit? j60 = mine60.Classes.FirstOrDefault(c => c.Class == "Jet");
            Check(l60 is { ConventionalHeightFt: 1100, HighKeyFt: 1700, WideHeightFt: null } && j60 is { WideHeightFt: null } && j60.Extra != null,
                "Import heights: the same altitudes (100 → 400 ft: 300 lower), the others emptied, unknown fields kept",
                $"Light {l60?.ConventionalHeightFt}/{l60?.HighKeyFt}/{l60?.WideHeightFt}, Jet {j60?.WideHeightFt}");
        }

        // 61. Quick INBOUND IP (the user, 7 Nov): from the long final point on the axis, at the initial height of its class, kept
        // to the initial point, then the overhead and a landing.
        {
            SimEngine s61 = NewCircuitEngine();
            string c61 = s61.AddInboundIp(null, EndAction.FullStop, performanceClass: "Light");
            AircraftState first61 = s61.Get(c61)!;
            List<AircraftState> f61 = [first61];
            f61.AddRange(Fly(s61, c61, 1500));
            List<AircraftState> before61 = f61.Where(st => Frame(st).Along < -s61.InitialPointNM - 0.2).ToList();
            Check(Near(Frame(first61).Along, -s61.LongFinalNM, 0.05) && Math.Abs(Frame(first61).Lateral) < 0.05 && Near(Height(first61), 1500, 5)
                && before61.Count > 10 && before61.All(st => Near(Height(st), 1500, 60)) && f61.Any(st => st.Leg == "INITIAL") && LandedOnRunway(f61),
                "Quick INBOUND IP: from the long final at the initial height (1500), kept to the IP, the overhead, lands",
                $"start {Frame(first61).Along:F1} NM {Height(first61):F0} ft; {Legs(f61)}");
            pictures.Add(("Quick INBOUND IP: from the long final at 1500 ft, the overhead (Light)", f61));
        }

        // 62. DCT TO IP near the aerodrome (the user, 7 Nov): out parallel to the final on its side (never wider than the WIDE),
        // the turn in abeam the IP + 2 NM, on the initial at the IP, the overhead, lands. From the conventional downwind, early
        // and late (towards the base), and from the wide one.
        {
            SimEngine w62 = NewCircuitEngine();
            string wc62 = w62.AddInCircuit(CircuitStart.Downwind, CircuitType.Wide, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
            w62.RunFor(20);
            double wide62 = Math.Abs(Frame(w62.Get(wc62)!).Lateral);
            foreach ((CircuitType type62, double after) in new[] { (CircuitType.Conventional, 15.0), (CircuitType.Conventional, 75.0), (CircuitType.Wide, 15.0) })
            {
                SimEngine s62 = NewCircuitEngine();
                string c62 = s62.AddInCircuit(CircuitStart.Downwind, type62, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
                s62.RunFor(after);
                string start = $"{type62} {s62.Get(c62)!.Leg} ({after:0} s)";
                s62.DirectTo(c62, DirectPoint.InitialPoint, CircuitSide.Left);
                List<AircraftState> f62 = Fly(s62, c62, 1200);
                int initial62 = f62.FindIndex(st => st.Leg == "INITIAL");
                double widest62 = initial62 > 0 ? f62.Take(initial62).Max(st => Math.Abs(Frame(st).Lateral)) : double.NaN;
                double along62 = initial62 > 0 ? Frame(f62[initial62]).Along : double.NaN;
                // (The user, 7 Nov: the turn in abeam the IP + 2 NM, not farther out; the turn itself on top.)
                double deepest62 = initial62 > 0 ? f62.Take(initial62).Min(st => Frame(st).Along) : double.NaN;
                // (At the IP: on the runway track.)
                AircraftState? atIp62 = initial62 > 0 ? f62.Skip(initial62).FirstOrDefault(st => Frame(st).Along >= -s62.InitialPointNM) : null;
                double lateral62 = atIp62 != null ? Frame(atIp62).Lateral : double.NaN;
                Check(initial62 > 0 && widest62 <= wide62 + 0.25 && Math.Abs(lateral62) < 0.35 && along62 < -s62.InitialPointNM + 0.4
                    && deepest62 > -s62.InitialPointNM - 3.5 && LandedOnRunway(f62),
                    $"DCT TO IP from the {start} (Jet): out parallel to the final, never wider than the WIDE, turns in near abeam the IP + 2 NM, on the runway track at the IP, lands",
                    $"widest {widest62:F2} NM (WIDE {wide62:F2}), deepest {-deepest62:F2} NM out, initial from {along62:F2} NM, {lateral62:F2} NM off the track at the IP");
                pictures.Add(($"DCT TO IP from the {start}, near the aerodrome (Jet)", f62));
            }
        }

        // START SFO a little before the High Key, on its circle: on round it, not a way back to the runway track.
        SimEngine near = NewCircuitEngine();
        string nc = near.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        near.HoldAtHighKey(nc, true);
        bool nearReleased = false;
        double nearTurned = 0, nearHeading = double.NaN;
        List<AircraftState> nearFlown = Fly(near, nc, 1800, st =>
        {
            if (!nearReleased && st.Leg == "HOLD" && near.Time > 120 && Between(SimEngine.Wrap(st.HeadingTrue - CircuitCourse), 20, 60))
            {
                nearReleased = true;
                near.HoldAtHighKey(nc, false);
                nearHeading = st.HeadingTrue;
            }
            else if (nearReleased && double.IsFinite(nearHeading) && st.Leg != "LOW KEY")
            {
                nearTurned += Math.Abs(SimEngine.Wrap(st.HeadingTrue - nearHeading));
                nearHeading = st.HeadingTrue;
            }
            if (st.Leg == "LOW KEY") nearHeading = double.NaN;
        });
        Check(nearReleased && nearTurned < 90 && LandedOnRunway(nearFlown), "START SFO a little before the High Key: on round the circle to it, lands",
            $"turned {nearTurned:F0}° before the Low Key; {Legs(nearFlown)}");

        SimEngine high = NewCircuitEngine();
        string hk = high.AddInCircuit(CircuitStart.HighKey, CircuitType.Sfo, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        high.HoldAtHighKey(hk, false);
        high.RunFor(2);
        high.SetAltitude(hk, high.Get(hk)!.AltitudeFt + 4000);
        List<AircraftState> glide = Fly(high, hk, 2400);
        int lowKeyAt = glide.FindIndex(st => st.Leg == "LOW KEY");
        List<double> rates = lowKeyAt > 0 ? glide.Skip(lowKeyAt + 10).TakeWhile(st => st.Leg == "LOW KEY").Select(st => -st.VerticalSpeedFpm).ToList() : [];
        double median = rates.Count > 0 ? rates.OrderBy(r => r).ElementAt(rates.Count / 2) : double.NaN;
        double steady = rates.Count > 0 ? rates.Count(r => Math.Abs(r - median) < 0.35 * median) / (double)rates.Count : 0;
        Check(rates.Count > 30 && steady > 0.8 && LandedOnRunway(glide), "SFO from a High Key 4000 ft higher: one vertical speed down to the final, lands",
            $"{steady:P0} of {rates.Count} s within 35% of {median:F0} ft/min; {Legs(glide)}");
        pictures.Add(("SFO from a High Key 4000 ft higher (Jet)", glide));
    }

    // 34. Circling (the user, 3 Nov): from the long final of the opposite runway, at the conventional circuit height, aside
    // by 45° onto the downwind of the runway in use, then the conventional circuit, landing on the runway in use.
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Circling, CircuitType.Close, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
        AircraftState first = sim.Get(cs)!;
        List<AircraftState> flown = Fly(sim, cs, 1500);
        List<AircraftState> circlingLeg = OnLeg(flown, "CIRCLING");
        List<AircraftState> circlingDownwind = OnLeg(flown, "DOWNWIND");
        AircraftState? landing = flown.FirstOrDefault(st => st.Contact == GroundContact.Landed);
        double circlingLowest = circlingLeg.Count > 0 ? circlingLeg.Min(st => Height(st)) : double.NaN;
        Check(first.Circuit == CircuitType.Conventional && Frame(first).Along > CircuitLength + 5 && Math.Abs(SimEngine.Wrap(first.HeadingTrue - CircuitCourse - 180)) < 1
            && circlingLeg.Count > 10 && circlingLeg.All(st => Frame(st).Lateral > -0.3) && circlingLowest > 900
            && circlingDownwind.Count > 10 && Near(Height(circlingDownwind[0]), 1000, 80) && Near(circlingDownwind.Average(st => Frame(st).Lateral), 1.0, 0.25)
            && landing != null && Frame(landing).Along > -0.05 && Frame(landing).Along < CircuitLength && Math.Abs(Frame(landing).Lateral) < 0.05 && sim.Get(cs) == null,
            "Circling: from the opposite long final at circuit height, aside onto the downwind (1000 ft, about 1 NM), lands on the runway in use",
            $"lowest circling {circlingLowest:F0} ft, downwind {(circlingDownwind.Count > 0 ? $"{Height(circlingDownwind[0]):F0} ft {circlingDownwind.Average(st => Frame(st).Lateral):F2} NM" : "none")}, "
            + (landing != null ? $"down at {Frame(landing).Along:F2} NM, {Frame(landing).Lateral:F2} NM aside" : "never down"));
        pictures.Add(("Circling from the opposite long final onto the downwind, landing (Light)", flown));
    }

    // 33. Crossing the zone: from a gate over the field to the opposite gate, at its height, never landing, gone there;
    // orbits until continue on the way, a heading (MANUAL), back onto its way.
    {
        (double ax, double ay) = Place(-4, 4.5, CircuitSide.Left);
        (double bx, double by) = Place(4, -4.5, CircuitSide.Left);
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddTransit(ax, ay, bx, by, overhead: true, heightFt: 2500, performanceClass: "Turboprop");
        bool orbiting = false, continued = false, vectored = false, resumed = false;
        double turned = 0;
        double? before = null;
        int seconds = 0, vectoredAt = 0;
        List<AircraftState> flown = Fly(sim, cs, 1200, st =>
        {
            seconds++;
            if (!orbiting && seconds == 20) { orbiting = true; sim.Orbit(cs, forever: true); }
            if (st.Leg == "ORBITS")
            {
                if (before is double h) turned += Math.Abs(SimEngine.Wrap(st.HeadingTrue - h));
                before = st.HeadingTrue;
                if (!continued && turned > 400) { continued = true; sim.ContinueAfterOrbit(cs); }
            }
            // (Past the field, on the way to the exit gate.)
            if (continued && !vectored && st.Leg == "TRANSIT" && st.Route != null && st.RouteNext >= st.Route.Count - 1 && st.Route.Count > 1)
            {
                vectored = true;
                vectoredAt = seconds;
                sim.SetHeading(cs, CircuitCourse);
            }
            if (vectored && !resumed && st.Leg == "MANUAL" && seconds > vectoredAt + 30) { resumed = true; sim.ResumeTransit(cs); }
        });
        AircraftState last = flown[^1];
        double overField = flown.Min(st => Math.Sqrt(Math.Pow(Frame(st).Along - CircuitLength / 2, 2) + Math.Pow(Frame(st).Lateral, 2)));
        Check(flown.All(st => st.Contact == GroundContact.Airborne && st.Transit) && flown.All(st => Near(Height(st), 2500, 80)) && continued && resumed
            && sim.Get(cs) == null && Math.Sqrt(Math.Pow(last.X - bx, 2) + Math.Pow(last.Y - by, 2)) < 0.3 && overField < 0.5,
            "Crossing: from a gate over the field to the other gate at 2500 ft, orbits until continue, a heading and back, never lands, gone at the gate",
            $"over the field within {overField:F2} NM, orbit {turned:F0}°, last {Math.Sqrt(Math.Pow(last.X - bx, 2) + Math.Pow(last.Y - by, 2)):F2} NM from the exit gate");
        pictures.Add(("Crossing from a gate over the field to the opposite gate, an orbit, a heading, back (Turboprop)", flown));
    }

    // 32. Take-off: only the conventional or the wide after it (a close pattern asked: the conventional).
    {
        SimEngine sim = NewCircuitEngine();
        string cs = sim.AddInCircuit(CircuitStart.Runway, CircuitType.Close, CircuitSide.Left, EndAction.FullStop, performanceClass: "Jet");
        Check(sim.Get(cs)?.Circuit == CircuitType.Conventional, "Take-off: then the conventional (or the wide) circuit only");
    }

    // 23. Helicopter: the conventional circuit, landing on a pad beside the runway, hovering down.
    {
        SimEngine sim = NewCircuitEngine();
        sim.SetWind(CircuitCourse + 60, 10, 0);
        string cs = sim.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.TouchAndGo, performanceClass: "Helicopter");
        (double padX, double padY) = Place(0.3, -0.3, CircuitSide.Left);
        sim.SetHelipad(cs, padX, padY);
        List<AircraftState> flown = Fly(sim, cs, 1200);
        AircraftState? down = flown.FirstOrDefault(st => st.Contact == GroundContact.Landed);
        List<AircraftState> hover = flown.Where(st => st.Contact == GroundContact.Airborne && st.SpeedKt < 1).ToList();
        double drift = hover.Count > 1 ? Math.Sqrt(Math.Pow(hover[^1].X - hover[0].X, 2) + Math.Pow(hover[^1].Y - hover[0].Y, 2)) : double.NaN;
        List<AircraftState> downwind = OnLeg(flown, "DOWNWIND");
        Check(downwind.Count > 10 && Near(Height(downwind[0]), 700, 60) && Near(downwind.Average(st => Frame(st).Lateral), 0.8, 0.25),
            "Helicopter: circuit at 700 ft, downwind about 0.8 NM", downwind.Count > 0 ? $"{Height(downwind[0]):F0} ft, {downwind.Average(st => Frame(st).Lateral):F2} NM" : "no downwind");
        Check(down != null && Math.Sqrt(Math.Pow(down.X - padX, 2) + Math.Pow(down.Y - padY, 2)) < 0.05 && hover.Count > 2 && drift < 0.01
            && flown.All(st => st.Contact != GroundContact.Rolling) && sim.Get(cs) == null,
            "Helicopter: hovers over its pad (not moved by the wind), down to it (no touch and go), then removed",
            down != null ? $"{Math.Sqrt(Math.Pow(down.X - padX, 2) + Math.Pow(down.Y - padY, 2)):F3} NM from the pad, {hover.Count} s of hover, drift {drift:F3} NM" : "never landed");
        pictures.Add(("Helicopter, conventional circuit, landing on a pad beside the runway", flown));
    }

    // 10. The instructor takes it, then gives it back.
    SimEngine e10 = NewCircuitEngine();
    string c10 = e10.AddInCircuit(CircuitStart.Downwind, CircuitType.Conventional, CircuitSide.Left, EndAction.FullStop, performanceClass: "Light");
    e10.RunFor(10);
    e10.SetHeading(c10, CircuitCourse + 90);
    e10.RunFor(3);
    string manual = e10.Get(c10)!.Leg;
    e10.SetCircuit(c10, CircuitType.Conventional, CircuitSide.Left);
    e10.RunFor(3);
    string back = e10.Get(c10)!.Leg;
    List<AircraftState> t10 = Fly(e10, c10, 900);
    Check(manual == "MANUAL" && back == "DOWNWIND" && LandedOnRunway(t10), "Instructor: a heading takes it out of the circuit (MANUAL), the circuit given again brings it back", $"{manual} → {back}");

    // The picture of the circuits flown, for the user (artifact of the build): runway up, a colour per leg, the leg name in its middle.
    try
    {
        Dictionary<string, string> colours = new()
        {
            ["INITIAL"] = "#a0a8ff", ["UPWIND"] = "#d0d0d0", ["CROSSWIND"] = "#ffd24a", ["DOWNWIND"] = "#4ab0ff", ["BASE"] = "#c586c0",
            ["FINAL"] = "#7dff9a", ["GO AROUND"] = "#ff5050", ["JOIN"] = "#ffffff", ["ROLL"] = "#ff9f40", ["LANDED"] = "#ff9f40",
            ["DEPARTURE"] = "#ff80c0", ["MANUAL"] = "#888888", ["ORBIT"] = "#ff6ad5", ["HIGH KEY"] = "#ff8c3a", ["LOW KEY"] = "#ffe08a", ["CLIMB"] = "#9fe1cb", ["HOLD"] = "#5dcaa5", ["TRANSIT"] = "#f0997b", ["CIRCLING"] = "#afa9ec"
        };
        string Colour(string leg) => colours.TryGetValue(leg, out string? found) ? found : "#888888";
        System.Text.StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>TWR Trainer - circuits of the self-test</title>")
            .Append("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#101820;color:#d6e2ec;margin:16px}h1{font-size:18px}")
            .Append(".p{display:inline-block;margin:8px;vertical-align:top}.p div{font-size:13px;margin:4px 0;max-width:420px}")
            .Append(".k{display:inline-block;margin-right:14px;font-size:13px}.k b{display:inline-block;width:18px;height:4px;margin-right:5px;vertical-align:middle}</style></head><body>")
            .Append("<h1>Circuits flown by the self-test - the runway (grey) always points up: the final comes from the bottom; a dot every 10 s; the name of each leg in its middle, with the height above the threshold</h1><p>");
        foreach (KeyValuePair<string, string> pair in colours) html.Append($"<span class=\"k\"><b style=\"background:{pair.Value}\"></b>{pair.Key}</span>");
        html.Append("</p>");
        double c = CircuitCourse * Math.PI / 180;
        (double Right, double Along) Up(AircraftState st) => (st.X * Math.Cos(c) - st.Y * Math.Sin(c), st.X * Math.Sin(c) + st.Y * Math.Cos(c));
        foreach ((string title, List<AircraftState> track) in pictures)
        {
            if (track.Count == 0) continue;
            List<(double Right, double Along)> all = track.Select(Up).Append((0.0, 0.0)).Append((0.0, CircuitLength)).ToList();
            double minX = all.Min(p => p.Right) - 0.4, maxX = all.Max(p => p.Right) + 0.4, minY = all.Min(p => p.Along) - 0.3, maxY = all.Max(p => p.Along) + 0.3;
            double scale = 400 / Math.Max(maxX - minX, maxY - minY);
            double Px(double x) => (x - minX) * scale;
            double Py(double y) => (maxY - y) * scale;
            string Sx(double x) => Px(x).ToString("F1", CultureInfo.InvariantCulture);
            string Sy(double y) => Py(y).ToString("F1", CultureInfo.InvariantCulture);
            List<(double X, double Y)> placed = [];
            html.Append($"<div class=\"p\"><div>{System.Net.WebUtility.HtmlEncode(title)}</div><svg width=\"420\" height=\"420\" style=\"background:#0b1118\">");
            html.Append($"<line x1=\"{Sx(0)}\" y1=\"{Sy(0)}\" x2=\"{Sx(0)}\" y2=\"{Sy(CircuitLength)}\" stroke=\"#8a949c\" stroke-width=\"6\"/>");
            // One polyline per run of the same leg (joined to the next one), its name in the middle.
            int i = 0;
            while (i < track.Count)
            {
                int j = i;
                while (j + 1 < track.Count && track[j + 1].Leg == track[i].Leg) j++;
                List<AircraftState> run = track.Skip(i).Take(j - i + 2).ToList();
                string colour = Colour(track[i].Leg);
                html.Append($"<polyline fill=\"none\" stroke=\"{colour}\" stroke-width=\"2\" points=\"")
                    .Append(string.Join(" ", run.Select(st => Sx(Up(st).Right) + "," + Sy(Up(st).Along)))).Append("\"/>");
                AircraftState middle = track[(i + j) / 2];
                if (track[i].Leg.Length > 0 && j - i >= 3)
                {
                    // Beside the leg, on the outer side; moved down while it would cover a name already written.
                    double lx = Px(Up(middle).Right), ly = Py(Up(middle).Along);
                    bool left = Up(middle).Right < 0;
                    while (placed.Any(q => Math.Abs(q.Y - ly) < 13 && Math.Abs(q.X - lx) < 110)) ly += 13;
                    placed.Add((lx, ly));
                    string anchor = left ? "end" : "start";
                    string dx = left ? "-6" : "6";
                    html.Append($"<text x=\"{lx.ToString("F1", CultureInfo.InvariantCulture)}\" y=\"{ly.ToString("F1", CultureInfo.InvariantCulture)}\" dx=\"{dx}\" text-anchor=\"{anchor}\" fill=\"{colour}\" font-size=\"11\">{track[i].Leg} {Height(middle):F0} ft</text>");
                }
                i = j + 1;
            }
            for (int k = 0; k < track.Count; k += 10) html.Append($"<circle cx=\"{Sx(Up(track[k]).Right)}\" cy=\"{Sy(Up(track[k]).Along)}\" r=\"1.6\" fill=\"#ffffff\"/>");
            html.Append("</svg></div>");
        }
        html.Append("</body></html>");
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "circuits.html"), html.ToString());
        Console.WriteLine("circuits.html written.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("circuits.html not written: " + ex.Message);
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? $"All {checks} checks passed." : $"{failures} of {checks} checks FAILED.");
return failures == 0 ? 0 : 1;
