using System.Globalization;
using System.IO;
using System.Text;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The log of the flights (the user, 6-8 Nov): one CSV file per session (START SESSION ... END SESSION, the training or
    /// the exam; nothing written outside one) in the Logs folder (opened by Excel with any language: "sep=;" first), closed
    /// with the program too, or when another map is opened; an error or a crash written in it. On top
    /// the session: aerodrome, runway, start, network, the instructor's and the student's callsign and VID of the network
    /// (empty when not connected). Then a row per event: the simulation's (the snapshots compared second by second), the
    /// instructor's commands, the photos, the network traffic in range. At the end a summary per aircraft.
    /// </summary>
    internal sealed class FlightLog
    {
        private sealed class Seen
        {
            public GroundContact Contact;
            public string Leg = "";
            public bool InCircuit;
            public bool Flown;
            public bool TouchedDown;
            public string Type = "", Rules = "";
            public int TakeOffs, TouchAndGos, Landings, GoArounds, LowApproaches, LowPasses;
        }

        private StreamWriter? writer;
        private readonly object sync = new();
        private bool inSession;
        private double sessionStart;

        /// <summary>A session open (START SESSION), and the simulated time it started at.</summary>
        public bool InSession => inSession;
        public double SessionStart => sessionStart;
        private string fileName = "";
        private readonly Dictionary<string, Seen> seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Seen> flights = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> network = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The aerodrome and the runway in use now (for the top and the rows).</summary>
        public string Aerodrome { get; set; } = "";
        public string Runway { get; set; } = "";
        /// <summary>Connected to the network (IVAO through Aurora), and who: empty when not known or not connected.</summary>
        public bool Network { get; private set; }
        public string InstructorCallsign { get; private set; } = "";
        public string InstructorVid { get; set; } = "";
        public string StudentCallsign { get; set; } = "";
        public string StudentVid { get; set; } = "";

        /// <summary>The file of this session (empty: none yet).</summary>
        public string FileName => fileName;
        /// <summary>What went wrong writing (said once), or null.</summary>
        public string? Error { get; private set; }

        private static string Field(string text) =>
            text.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

        private string Clock(double seconds)
        {
            seconds -= sessionStart;
            seconds = Math.Max(0, seconds);
            return string.Create(CultureInfo.InvariantCulture, $"{(int)(seconds / 3600)}:{(int)(seconds % 3600 / 60):00}:{(int)(seconds % 60):00}");
        }

        private void Line(params string[] fields)
        {
            lock (sync) LineLocked(fields);
        }

        private void LineLocked(string[] fields)
        {
            if (Error != null || ended || !inSession) return;
            try
            {
                if (writer == null)
                {
                    Directory.CreateDirectory(Paths.LogsFolder);
                    fileName = Path.Combine(Paths.LogsFolder, $"Log_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}.csv");
                    writer = new StreamWriter(fileName, false, new UTF8Encoding(true)) { AutoFlush = true };
                    writer.WriteLine("sep=;");
                    writer.WriteLine("Circuit Trainer;log of the flights");
                    writer.WriteLine($"Aerodrome;{Field(Aerodrome)}");
                    writer.WriteLine($"Runway;{Field(Runway)}");
                    writer.WriteLine($"Session started (UTC);{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"Network;{(Network ? "IVAO" : "")}");
                    writer.WriteLine($"Instructor callsign;{Field(InstructorCallsign)}");
                    writer.WriteLine($"Instructor VID;{Field(InstructorVid)}");
                    writer.WriteLine($"Student callsign;{Field(StudentCallsign)}");
                    writer.WriteLine($"Student VID;{Field(StudentVid)}");
                    writer.WriteLine();
                    writer.WriteLine("Sim time;UTC;Source;Callsign;ICAO type;Rules;Runway;Pattern;Event;Detail");
                }
                writer.WriteLine(string.Join(';', fields.Select(Field)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error = $"The log could not be written ({ex.Message}).";
                try { writer?.Dispose(); } catch (Exception) { }
                writer = null;
            }
        }

        /// <summary>A row: when (simulated seconds), what, of which aircraft (empty: none).</summary>
        public void Event(double time, string what, string callsign = "", string detail = "", string source = "SIM", string type = "", string rules = "", string pattern = "")
        {
            if (callsign.Length > 0 && flights.TryGetValue(callsign, out Seen? known))
            {
                if (type.Length == 0) type = known.Type;
                if (rules.Length == 0) rules = known.Rules;
            }
            Line(Clock(time), DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture), source, callsign, type, rules, callsign.Length > 0 ? Runway : "", pattern, what, detail);
        }

        /// <summary>The network now (Aurora connected to IVAO with this callsign, or not): a row when it changes.</summary>
        public void SetNetwork(bool connected, string callsign, double time, bool row = true)
        {
            if (connected == Network && callsign == InstructorCallsign) return;
            Network = connected;
            InstructorCallsign = connected ? callsign : "";
            if (!connected) InstructorVid = "";
            if (writer != null && row) Event(time, connected ? "NETWORK CONNECTED" : "NETWORK OFF", detail: connected ? $"instructor {callsign}" : "");
        }

        private static string Pattern(AircraftState s) => s.Circuit switch
        {
            CircuitType.Conventional => "CONV",
            CircuitType.Wide => "WIDE",
            CircuitType.Close => "CLS",
            CircuitType.Overhead => "OVH",
            CircuitType.Sfo => "SFO",
            _ => s.Transit ? "TRANSIT" : ""
        };

        /// <summary>
        /// The simulation now, compared with the last time: aircraft added and gone, take-offs, touch and go, landings,
        /// go-arounds, low approaches and low passes, out of the circuit. <paramref name="info"/>: the ICAO type and the rules of an aircraft.
        /// </summary>
        public void Observe(IReadOnlyList<AircraftState> traffic, double time, Func<string, (string Type, string Rules)> info)
        {
            HashSet<string> now = new(StringComparer.OrdinalIgnoreCase);
            foreach (AircraftState s in traffic)
            {
                now.Add(s.Callsign);
                string pattern = Pattern(s);
                if (!seen.TryGetValue(s.Callsign, out Seen? was))
                {
                    (string type, string rules) = info(s.Callsign);
                    // (Removed and added again in the session: its counts go on, the review of 7 Nov.)
                    if (flights.TryGetValue(s.Callsign, out Seen? before))
                    {
                        was = before;
                        was.Contact = s.Contact;
                        was.Leg = s.Leg;
                        was.InCircuit = s.Circuit != null;
                        was.TouchedDown = false;
                        if (type.Length > 0) was.Type = type;
                        if (rules.Length > 0) was.Rules = rules;
                    }
                    else was = new Seen { Contact = s.Contact, Leg = s.Leg, InCircuit = s.Circuit != null, Flown = s.Contact == GroundContact.Airborne, Type = type, Rules = rules };
                    seen[s.Callsign] = was;
                    flights[s.Callsign] = was;
                    Event(time, "ADDED", s.Callsign, $"{s.PerformanceClass}, {Math.Round(s.AltitudeFt):0} ft", pattern: pattern);
                    continue;
                }
                if (s.Contact != was.Contact)
                {
                    if (was.Contact == GroundContact.Airborne && s.Contact == GroundContact.Rolling) { was.TouchedDown = true; was.TouchAndGos++; Event(time, "TOUCH AND GO", s.Callsign, pattern: pattern); }
                    else if (was.Contact == GroundContact.Airborne && s.Contact == GroundContact.Landed) { was.Landings++; Event(time, "LANDING", s.Callsign, pattern: pattern); }
                    else if (was.Contact == GroundContact.Airborne && s.Contact == GroundContact.OnGround) Event(time, "ON THE GROUND (not on the runway)", s.Callsign, pattern: pattern);
                    else if (s.Contact == GroundContact.Airborne && !was.TouchedDown) { was.TakeOffs++; Event(time, "TAKE-OFF", s.Callsign, pattern: pattern); }
                    if (s.Contact == GroundContact.Airborne) was.TouchedDown = false;
                    was.Contact = s.Contact;
                }
                if (s.Leg != was.Leg && s.Leg == "GO AROUND")
                {
                    switch (s.EndAction)
                    {
                        case EndAction.GoAround: was.LowApproaches++; Event(time, "LOW APPROACH", s.Callsign, pattern: pattern); break;
                        case EndAction.LowPass: was.LowPasses++; Event(time, "LOW PASS", s.Callsign, pattern: pattern); break;
                        default: was.GoArounds++; Event(time, "GO AROUND", s.Callsign, pattern: pattern); break;
                    }
                }
                string legBefore = was.Leg;
                was.Leg = s.Leg;
                bool inCircuit = s.Circuit != null;
                if (was.InCircuit && !inCircuit && s.Contact == GroundContact.Airborne) Event(time, "OUT OF THE CIRCUIT", s.Callsign, legBefore.Length > 0 ? $"from {legBefore}" : "");
                was.InCircuit = inCircuit;
            }
            foreach (string gone in seen.Keys.Where(cs => !now.Contains(cs)).ToList())
            {
                Seen was = seen[gone];
                Event(time, "GONE", gone, was.Contact == GroundContact.Landed ? "off the runway after landing" : "out of the simulation");
                seen.Remove(gone);
            }
        }

        /// <summary>The network aircraft near the aerodrome now: a row when one comes into range or goes out of it.</summary>
        public void ObserveNetwork(IEnumerable<IvaoAircraft> traffic, double time)
        {
            HashSet<string> now = new(StringComparer.OrdinalIgnoreCase);
            foreach (IvaoAircraft a in traffic)
            {
                now.Add(a.Callsign);
                if (network.Add(a.Callsign))
                    Event(time, "IN RANGE", a.Callsign, string.Create(CultureInfo.InvariantCulture, $"{Math.Round(a.AltitudeFt):0} ft, {Math.Round(a.SpeedKt):0} kt{(a.Squawk != null ? $", squawk {a.Squawk}" : "")}"), "IVAO");
            }
            foreach (string gone in network.Where(cs => !now.Contains(cs)).ToList())
            {
                network.Remove(gone);
                Event(time, "OUT OF RANGE", gone, source: "IVAO");
            }
        }

        /// <summary>No more rows (the program closing, the review of 7 Nov).</summary>
        private bool ended;

        /// <summary>
        /// START SESSION (the user, 8 Nov: the training or the exam begins; nothing starts flying): a new file, the time from
        /// 0:00:00, the aircraft already in the simulation written AT START (counted from now).
        /// </summary>
        public void StartSession(double time, IReadOnlyList<AircraftState> traffic, Func<string, (string Type, string Rules)> info)
        {
            lock (sync)
            {
                Close(time);
                inSession = true;
                sessionStart = time;
                Event(time, "SESSION START", source: "INSTRUCTOR");
                foreach (AircraftState s in traffic)
                {
                    (string type, string rules) = info(s.Callsign);
                    Seen was = new() { Contact = s.Contact, Leg = s.Leg, InCircuit = s.Circuit != null, Flown = s.Contact == GroundContact.Airborne, Type = type, Rules = rules };
                    seen[s.Callsign] = was;
                    flights[s.Callsign] = was;
                    Event(time, "AT START", s.Callsign, string.Create(CultureInfo.InvariantCulture, $"{s.PerformanceClass}, {s.Leg}, {Math.Round(s.AltitudeFt):0} ft"), pattern: Pattern(s));
                }
            }
        }

        /// <summary>END SESSION (or the program closing, another map): the row, the summary, the file closed.</summary>
        public void EndSession(double time, string why = "")
        {
            lock (sync)
            {
                if (!inSession) return;
                Event(time, "SESSION END", detail: why, source: "INSTRUCTOR");
                Close(time);
            }
        }

        /// <summary>Something went wrong in the program (the user, 8 Nov): a row; <paramref name="fatal"/>: it closes, the session ended.</summary>
        public void Problem(double time, string message, bool fatal)
        {
            lock (sync)
            {
                if (!inSession) return;
                Event(time, fatal ? "CRASH" : "ERROR", detail: message, source: "PROGRAM");
                if (fatal) Close(time, final: true);
            }
        }

        /// <summary>
        /// A log left open by a program that could not close it (a PC switched off, the program killed): "ended unexpectedly"
        /// added at its end, with the time it was last written (the user, 8 Nov). Only the newest log is looked at.
        /// </summary>
        public static void MarkUnfinished()
        {
            try
            {
                if (!Directory.Exists(Paths.LogsFolder)) return;
                FileInfo? newest = new DirectoryInfo(Paths.LogsFolder).GetFiles("Log_*.csv").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (newest == null) return;
                string text = File.ReadAllText(newest.FullName);
                if (text.Contains("\nEnded (UTC);", StringComparison.Ordinal)) return;
                File.AppendAllText(newest.FullName, string.Create(CultureInfo.InvariantCulture,
                    $"\r\nEnded (UTC);UNEXPECTEDLY (the program did not close it);last written {newest.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}\r\n"), new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // (Not readable or writable: left as it is.)
            }
        }

        /// <summary>
        /// The end of the session: the summary per aircraft, the file closed (the next event makes a new one; <paramref
        /// name="final"/>: none, the program closes).
        /// </summary>
        public void Close(double time, bool final = false)
        {
            lock (sync) CloseLocked(time, final);
        }

        private void CloseLocked(double time, bool final)
        {
            if (final) ended = true;
            inSession = false;
            if (writer != null)
            {
                try
                {
                    writer.WriteLine();
                    writer.WriteLine($"Ended (UTC);{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)};sim time {Clock(time)}");
                    writer.WriteLine($"Network;{(Network ? "IVAO" : "")};Instructor;{Field(InstructorCallsign)};{Field(InstructorVid)};Student;{Field(StudentCallsign)};{Field(StudentVid)}");
                    writer.WriteLine();
                    writer.WriteLine("Summary;ICAO type;Rules;Take-offs;Touch and go;Landings;Go-arounds;Low approaches;Low passes");
                    foreach ((string callsign, Seen f) in flights.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                        writer.WriteLine(string.Join(';', Field(callsign), Field(f.Type), Field(f.Rules), f.TakeOffs, f.TouchAndGos, f.Landings, f.GoArounds, f.LowApproaches, f.LowPasses));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                try { writer.Dispose(); } catch (Exception) { }
            }
            writer = null;
            fileName = "";
            seen.Clear();
            flights.Clear();
            network.Clear();
            Error = null;
        }
    }
}
