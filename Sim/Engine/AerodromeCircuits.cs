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
        /// <summary>The entry gates (TWR Trainer): where the arriving aircraft start, at least 5 NM out.</summary>
        public List<EntryGate> Gates { get; set; } = [];
        /// <summary>The runway exits (the user, 4 Nov: no taxiways, so where it leaves the runway after landing): a name and a point on the runway edge.</summary>
        public List<EntryGate> Exits { get; set; } = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// The side of the circuit of a runway ("14", "32R"; not case sensitive), or null when the file does not say. With a
        /// circuit: the side of its group at that runway (conventional and wide; the jet circuits, overhead and close; SFO),
        /// else the runway's.
        /// </summary>
        public CircuitSide? SideOf(string? runway, CircuitType? type = null)
        {
            string name = (runway ?? "").Trim();
            RunwayCircuit? found = Runways.FirstOrDefault(r => r != null && string.Equals((r.Runway ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (found == null) return null;
            CircuitSide? own = type switch
            {
                CircuitType.Conventional or CircuitType.Wide => found.ConventionalSide,
                CircuitType.Close or CircuitType.Overhead => found.JetSide,
                CircuitType.Sfo => found.SfoSide,
                _ => null
            };
            return own ?? found.Side;
        }

        /// <summary>
        /// A copy of a class with the heights this aerodrome gives it (the class unchanged when the file says nothing of it).
        /// </summary>
        public PerformanceClass Apply(PerformanceClass kind)
        {
            PerformanceClass copy = kind.Copy();
            ClassCircuit? here = Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));
            if (here == null) return copy;
            // One height for all its circuits (the first files), then the height of each circuit.
            if (here.CircuitHeightFt is double circuit)
            {
                copy.CircuitHeightFt = copy.ConventionalHeightFt = copy.WideHeightFt = copy.CloseHeightFt = circuit;
            }
            if (here.ConventionalHeightFt is double conventional) copy.ConventionalHeightFt = conventional;
            if (here.WideHeightFt is double wide) copy.WideHeightFt = wide;
            if (here.CloseHeightFt is double close) copy.CloseHeightFt = close;
            if (here.InitialHeightFt is double initial) copy.OverheadHeightFt = initial;
            // The break: given, else the height of the close pattern of this aerodrome (when given), else the class's.
            if (here.BreakHeightFt is double breakHeight) copy.BreakHeightFt = breakHeight;
            else if (here.CloseHeightFt is double closeHere) copy.BreakHeightFt = closeHere;
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
            file.Gates = (file.Gates ?? []).Where(g => g != null).ToList();
            file.Exits = (file.Exits ?? []).Where(g => g != null).ToList();
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

    /// <summary>
    /// One runway of an aerodrome: its circuit side and (TWR Trainer, given by two clicks on the map) where it is: the
    /// threshold and the end (degrees), the elevation of the threshold (ft). Missing: the runway has only its side.
    /// </summary>
    public sealed class RunwayCircuit
    {
        public string Runway { get; set; } = "";
        public CircuitSide Side { get; set; } = CircuitSide.Left;
        /// <summary>The side of each group of circuits at this runway (missing: <see cref="Side"/>): conventional and wide; the jet circuits (overhead, close); SFO.</summary>
        public CircuitSide? ConventionalSide { get; set; }
        public CircuitSide? JetSide { get; set; }
        public CircuitSide? SfoSide { get; set; }
        public double? ThresholdLatitude { get; set; }
        public double? ThresholdLongitude { get; set; }
        public double? EndLatitude { get; set; }
        public double? EndLongitude { get; set; }
        public double? ElevationFt { get; set; }

        /// <summary>Its threshold and its end are known.</summary>
        [JsonIgnore]
        public bool HasPlace => ThresholdLatitude is double a && ThresholdLongitude is double b && EndLatitude is double c && EndLongitude is double d
            && double.IsFinite(a + b + c + d) && (a != c || b != d);

        /// <summary>
        /// The name of the same runway in the other direction: 14 → 32, 09L → 27R, 36C → 18C (null when the name is not a
        /// runway number).
        /// </summary>
        public static string? Reciprocal(string? name)
        {
            string text = (name ?? "").Trim().ToUpperInvariant();
            int digits = 0;
            while (digits < text.Length && digits < 2 && char.IsAsciiDigit(text[digits])) digits++;
            if (digits == 0 || !int.TryParse(text[..digits], out int number) || number < 1 || number > 36) return null;
            string letter = text[digits..];
            if (letter.Length > 1 || (letter.Length == 1 && letter[0] is not ('L' or 'R' or 'C'))) return null;
            int other = (number + 18 - 1) % 36 + 1;
            string otherLetter = letter switch { "L" => "R", "R" => "L", _ => letter };
            return other.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + otherLetter;
        }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>An entry gate of an aerodrome: its name and place (degrees).</summary>
    public sealed class EntryGate
    {
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>The heights (ft above the threshold) of one class at an aerodrome; a missing one: the class's.</summary>
    public sealed class ClassCircuit
    {
        public string Class { get; set; } = "";
        /// <summary>One height for all its circuits (the first files; the heights of each circuit below come first).</summary>
        public double? CircuitHeightFt { get; set; }
        /// <summary>The height of each circuit: conventional, wide, close (also the overhead's downwind after the break).</summary>
        public double? ConventionalHeightFt { get; set; }
        public double? WideHeightFt { get; set; }
        public double? CloseHeightFt { get; set; }
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
