using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATCRoom.Sim
{
    /// <summary>
    /// The circuits of one aerodrome (TWR Trainer): the side of the circuit of each runway (the default of every aircraft
    /// and every circuit of that runway; the instructor can change it in flight) and, for the classes that fly there
    /// differently, the heights: circuit, initial and break of the overhead pattern, High and Low Key of the simulated
    /// flame out. What a file does not say comes from the class (performance.json). One JSON file per aerodrome, named
    /// with its ICAO code (LIBN.json); fields this version does not know are kept.
    /// </summary>
    public sealed class AerodromeCircuits
    {
        public const int CurrentFormatVersion = 1;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        /// <summary>ICAO code of the aerodrome.</summary>
        public string Airport { get; set; } = "";
        /// <summary>The side of the circuit of each runway ("14": Left).</summary>
        public List<RunwayCircuit> Runways { get; set; } = [];
        /// <summary>The heights of the classes that fly this aerodrome differently.</summary>
        public List<ClassCircuit> Classes { get; set; } = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>The side of the circuit of a runway ("14", "32R"; not case sensitive), or null when the file does not say.</summary>
        public CircuitSide? SideOf(string? runway)
        {
            string name = (runway ?? "").Trim();
            return Runways.FirstOrDefault(r => r != null && string.Equals((r.Runway ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))?.Side;
        }

        /// <summary>
        /// A copy of a class with the heights this aerodrome gives it (the class unchanged when the file says nothing of it).
        /// </summary>
        public PerformanceClass Apply(PerformanceClass kind)
        {
            PerformanceClass copy = kind.Copy();
            ClassCircuit? here = Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));
            if (here == null) return copy;
            if (here.CircuitHeightFt is double circuit) copy.CircuitHeightFt = circuit;
            if (here.InitialHeightFt is double initial) copy.OverheadHeightFt = initial;
            // The break: given, else the circuit height of this aerodrome (when given), else the class's.
            if (here.BreakHeightFt is double breakHeight) copy.BreakHeightFt = breakHeight;
            else if (here.CircuitHeightFt is double circuitHere) copy.BreakHeightFt = circuitHere;
            if (here.HighKeyFt is double highKey) copy.HighKeyFt = highKey;
            if (here.LowKeyFt is double lowKey) copy.LowKeyFt = lowKey;
            copy.Normalize();
            return copy;
        }

        /// <summary>Reads a file. Throws (with a readable message) when it is not one.</summary>
        public static AerodromeCircuits Parse(string json)
        {
            AerodromeCircuits? file;
            try
            {
                file = JsonSerializer.Deserialize<AerodromeCircuits>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Not a file of aerodrome circuits ({ex.Message}).", ex);
            }
            if (file == null) throw new InvalidDataException("Empty file of aerodrome circuits.");
            file.Airport = (file.Airport ?? "").Trim().ToUpperInvariant();
            file.Runways = (file.Runways ?? []).Where(r => r != null).ToList();
            file.Classes = (file.Classes ?? []).Where(c => c != null).ToList();
            return file;
        }

        public static AerodromeCircuits Load(string path) => Parse(File.ReadAllText(path));

        public void Save(string path)
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
    }

    /// <summary>The circuit side of one runway of an aerodrome.</summary>
    public sealed class RunwayCircuit
    {
        public string Runway { get; set; } = "";
        public CircuitSide Side { get; set; } = CircuitSide.Left;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>The heights (ft above the threshold) of one class at an aerodrome; a missing one: the class's.</summary>
    public sealed class ClassCircuit
    {
        public string Class { get; set; } = "";
        public double? CircuitHeightFt { get; set; }
        /// <summary>Overhead pattern: the initial (from the initial point to the break).</summary>
        public double? InitialHeightFt { get; set; }
        /// <summary>Overhead pattern: the height at the break.</summary>
        public double? BreakHeightFt { get; set; }
        public double? HighKeyFt { get; set; }
        public double? LowKeyFt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
