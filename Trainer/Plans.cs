using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>Where a new aircraft starts: an entry gate (its name), the runway (a take-off), or already in the circuit (tests).</summary>
    internal static class From
    {
        public const string Runway = "RWY";
        public const string LongFinal = "LONG FIN";
        /// <summary>(Scenarios of 3 Nov: "IN CCT", then "CIRC": read as this.)</summary>
        public const string InCircuit = "PTRN";
        public static bool IsInCircuit(string from) => from is InCircuit or "CIRC" or "IN CCT";
    }

    /// <summary>
    /// A flight plan of the advanced traffic generation (as in the PAR trainer): an aircraft prepared before it flies, from
    /// a gate (or the runway) into a circuit, started by hand (▶) or with START ALL after its delay; it can be held.
    /// </summary>
    internal sealed class TowerPlan
    {
        /// <summary>Empty: the next free callsign of the prefix.</summary>
        public string Callsign { get; set; } = "";
        public string Class { get; set; } = "";
        /// <summary>The name of the gate, <see cref="From.Runway"/> or <see cref="From.InCircuit"/>.</summary>
        public string From { get; set; } = "";
        public CircuitEntry Entry { get; set; } = CircuitEntry.Downwind;
        public CircuitType Circuit { get; set; } = CircuitType.Conventional;
        /// <summary>Null: the aerodrome's side for the circuit.</summary>
        public CircuitSide? Side { get; set; }
        public EndAction End { get; set; } = EndAction.TouchAndGo;
        /// <summary>From a gate: the height it arrives at (ft above the runway); null: 500 ft above its circuit.</summary>
        public double? HeightFt { get; set; }
        /// <summary>Crossing the zone (not landing): to the gate <see cref="To"/>, over the field or straight.</summary>
        public bool Transit { get; set; }
        public string To { get; set; } = "";
        public bool Overhead { get; set; }
        /// <summary>A formation (the user, 4 Nov): how many (1: alone), which land together (null: all), the seconds between them at the split.</summary>
        public int Formation { get; set; } = 1;
        /// <summary>The plan of its circuits (the user, 4 Nov): how many (0: on and on), the last one's end. Null (the first scenarios): <see cref="End"/> every time.</summary>
        public int? Laps { get; set; }
        public EndAction Last { get; set; } = EndAction.FullStop;
        public List<bool>? Lands { get; set; }
        public double SplitSeconds { get; set; } = 3;
        /// <summary>Minutes after START ALL.</summary>
        public double DelayMinutes { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        // ----- While the program runs (not saved).
        public enum State { Waiting, Counting, Held, Flying }
        [JsonIgnore] public State Now { get; set; } = State.Waiting;
        /// <summary>When it starts (simulation time, s), counting.</summary>
        [JsonIgnore] public double Due { get; set; }
        /// <summary>The aircraft flying it.</summary>
        [JsonIgnore] public string Flying { get; set; } = "";
        /// <summary>All its aircraft (a formation: the leader and its wingmen, flying together or split).</summary>
        [JsonIgnore] public List<string> Members { get; set; } = [];
        /// <summary>Ticked in the list (for GROUP).</summary>
        [JsonIgnore] public bool Picked { get; set; }
        /// <summary>An aircraft of ADD NOW (not a plan of TO LIST): gone from the list with its aircraft, not saved.</summary>
        [JsonIgnore] public bool Quick { get; set; }

        public TowerPlan Copy() => new()
        {
            Callsign = Callsign, Class = Class, From = From, Entry = Entry, Circuit = Circuit, Side = Side, End = End, HeightFt = HeightFt, Transit = Transit, To = To, Overhead = Overhead,
            Formation = Formation, Lands = Lands?.ToList(), SplitSeconds = SplitSeconds, Laps = Laps, Last = Last,
            DelayMinutes = DelayMinutes, Extra = Extra == null ? null : new Dictionary<string, JsonElement>(Extra)
        };
    }

    /// <summary>A scenario: the flight plans of a lesson, for an aerodrome (file in the folder Scenarios).</summary>
    internal sealed class TowerScenario
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string Description { get; set; } = "Scenario of TWR Trainer: flight plans from entry gates into the circuits.";
        public string Airport { get; set; } = "";
        public string Runway { get; set; } = "";
        public List<TowerPlan> Plans { get; set; } = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public static string Folder => Path.Combine(Paths.Folder, "Scenarios");

        /// <summary>Reads a scenario. Throws (with a readable message) when it is not one.</summary>
        public static TowerScenario Load(string path)
        {
            try
            {
                TowerScenario? file = JsonSerializer.Deserialize<TowerScenario>(File.ReadAllText(path), Options);
                if (file == null) throw new InvalidDataException("Empty scenario.");
                file.Plans = (file.Plans ?? []).Where(p => p != null).ToList();
                return file;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Not a scenario of TWR Trainer ({ex.Message}).", ex);
            }
        }

        public void Save(string path)
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
    }
}
