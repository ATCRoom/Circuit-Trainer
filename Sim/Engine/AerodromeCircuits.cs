using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATCRoom.Sim
{
    /// <summary>
    /// The circuits of one aerodrome (Circuit Trainer): the side of the circuit of each runway (the default of every aircraft
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
        /// <summary>The entry gates (Circuit Trainer): where the arriving aircraft start, at least 5 NM out.</summary>
        public List<EntryGate> Gates { get; set; } = [];
        /// <summary>The runway exits (the user, 4 Nov: no taxiways, so where it leaves the runway after landing): a name and a point on the runway edge.</summary>
        public List<EntryGate> Exits { get; set; } = [];
        /// <summary>The helipads registered at this aerodrome (the user, 5 Nov: H1, H2...; any other point clicked on the map too).</summary>
        public List<EntryGate> Helipads { get; set; } = [];
        /// <summary>The heights shown and written as heights above the runway (QFE); false: altitudes (QNH), the default (the user, 6 Nov).</summary>
        public bool Qfe { get; set; }
        /// <summary>
        /// A formation landing, both full stop (the user, 4 Nov; it depends on the country): false, "reduced rwy separation":
        /// the one behind touches down when the one ahead has rolled a third of the runway; true, "full rwy separation":
        /// when the one ahead has left the runway.
        /// </summary>
        public bool FullRunwaySeparation { get; set; }
        /// <summary>The long final and the initial point of this aerodrome (NM; the user, 4 Nov: they depend on the field); null: 10 and 3.</summary>
        public double? LongFinalNM { get; set; }
        public double? InitialPointNM { get; set; }
        /// <summary>The magnetic variation (degrees, east positive; the user 5 Nov: for the radials of the departures); null: 0.</summary>
        public double? MagneticVariation { get; set; }
        /// <summary>
        /// The base traffic of this aerodrome (the user, 7 Nov; Settings › Aircraft): the start of the callsigns given by
        /// themselves (null: SIM), the ICAO type of the aircraft without one, the class of the Quick traffic (null: none).
        /// </summary>
        public string? CallsignPrefix { get; set; }
        public string? IcaoType { get; set; }
        public string? DefaultClass { get; set; }
        /// <summary>The spacing of the range rings of the instructor's map (NM, the user 7 Nov; null: 5).</summary>
        public double? RingsNM { get; set; }
        /// <summary>The views of L1 and L2 kept by the instructor (the user, 4 Nov: programmable); null: the default ones.</summary>
        public SavedView? View1 { get; set; }
        public SavedView? View2 { get; set; }

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

        /// <summary>The lowest height above the runway a class altitude becomes (ft; the user, 7 Nov).</summary>
        public const double MinimumHeightFt = 300;

        /// <summary>An altitude of a class (ft QNH) as a height above a runway at this elevation: at least <see cref="MinimumHeightFt"/>.</summary>
        public static double Above(double altitudeFt, double elevationFt) =>
            Math.Max(MinimumHeightFt, altitudeFt - (double.IsFinite(elevationFt) ? elevationFt : 0));

        /// <summary>
        /// A copy of a class for this aerodrome, the runway at this elevation (ft): the altitudes of the class (QNH, the user 7 Nov)
        /// as heights above the runway (at least 300 ft), then the heights this aerodrome gives it (written above the threshold,
        /// kept as they are). The arrival from a gate at least 500 ft above its conventional circuit. A class already made for an
        /// aerodrome: a plain copy.
        /// </summary>
        public PerformanceClass Apply(PerformanceClass kind, double elevationFt = 0)
        {
            PerformanceClass copy = kind.Copy();
            if (kind.AtAerodrome) return copy;
            // (The arrival of the class: written, or "500 above its circuit", the rule kept above the circuit flown here.)
            bool ownArrival = kind.ArrivalHeightFt > 0 && double.IsFinite(kind.ArrivalHeightFt)
                && kind.ArrivalHeightFt != Math.Max(kind.ConventionalHeightFt, kind.CircuitHeightFt) + 500;
            // (Every value given first: a class written by hand without its heights gets those of its name, as altitudes.)
            copy.Normalize();
            kind = copy.Copy();
            copy.AtAerodrome = true;
            copy.CircuitHeightFt = Above(kind.CircuitHeightFt, elevationFt);
            copy.ConventionalHeightFt = Above(kind.ConventionalHeightFt, elevationFt);
            copy.WideHeightFt = Above(kind.WideHeightFt, elevationFt);
            copy.CloseHeightFt = Above(kind.CloseHeightFt, elevationFt);
            copy.OverheadHeightFt = Above(kind.OverheadHeightFt, elevationFt);
            copy.BreakHeightFt = Above(kind.BreakHeightFt, elevationFt);
            copy.HighKeyFt = Above(kind.HighKeyFt, elevationFt);
            copy.LowKeyFt = Above(kind.LowKeyFt, elevationFt);
            copy.ArrivalHeightFt = Above(kind.ArrivalHeightFt, elevationFt);
            ClassCircuit? here = Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));
            if (here != null)
            {
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
            }
            // ("500 above the circuit" stays a rule above the circuit flown here.)
            copy.ArrivalHeightFt = ownArrival ? Math.Max(copy.ArrivalHeightFt, copy.ConventionalHeightFt + 500) : copy.ConventionalHeightFt + 500;
            copy.Normalize();
            return copy;
        }

        /// <summary>
        /// The heights of the classes of another aerodrome copied here (the user, 7 Nov: Import heights): the same altitudes, so
        /// each height + the other runway's elevation - this one's (at least 300 ft above the runway). The heights of this
        /// aerodrome are replaced (those not written there: empty, the class's); nothing else is copied.
        /// </summary>
        public void CopyHeightsFrom(AerodromeCircuits other, double otherElevationFt, double elevationFt)
        {
            double shift = (double.IsFinite(otherElevationFt) ? otherElevationFt : 0) - (double.IsFinite(elevationFt) ? elevationFt : 0);
            // (Within the limits of each height: the initial and the High Key at least 500 ft.)
            double? Moved(double? height, double low = MinimumHeightFt, double high = 5000) =>
                height is double h && double.IsFinite(h) ? Math.Clamp(Math.Round(h + shift), low, high) : null;
            foreach (ClassCircuit mine in Classes)
            {
                mine.CircuitHeightFt = mine.ConventionalHeightFt = mine.WideHeightFt = mine.CloseHeightFt = null;
                mine.InitialHeightFt = mine.BreakHeightFt = mine.HighKeyFt = mine.LowKeyFt = null;
            }
            foreach (ClassCircuit theirs in other.Classes.Where(c => c != null && (c.Class ?? "").Trim().Length > 0))
            {
                ClassCircuit? mine = Classes.FirstOrDefault(c => string.Equals((c.Class ?? "").Trim(), theirs.Class.Trim(), StringComparison.OrdinalIgnoreCase));
                if (mine == null) Classes.Add(mine = new ClassCircuit { Class = theirs.Class.Trim() });
                mine.CircuitHeightFt = Moved(theirs.CircuitHeightFt);
                mine.ConventionalHeightFt = Moved(theirs.ConventionalHeightFt);
                mine.WideHeightFt = Moved(theirs.WideHeightFt);
                mine.CloseHeightFt = Moved(theirs.CloseHeightFt);
                mine.InitialHeightFt = Moved(theirs.InitialHeightFt, 500);
                mine.BreakHeightFt = Moved(theirs.BreakHeightFt);
                mine.HighKeyFt = Moved(theirs.HighKeyFt, 500, 15000);
                mine.LowKeyFt = Moved(theirs.LowKeyFt, MinimumHeightFt, 15000);
            }
            // (An entry left with nothing, and nothing of a newer version: gone.)
            Classes.RemoveAll(c => c is { CircuitHeightFt: null, ConventionalHeightFt: null, WideHeightFt: null, CloseHeightFt: null, InitialHeightFt: null,
                BreakHeightFt: null, HighKeyFt: null, LowKeyFt: null, Extra: null });
        }

        /// <summary>The elevation of the runways of this file (ft; the first one placed with one), or null: none placed.</summary>
        public double? Elevation(string? runway = null)
        {
            RunwayCircuit? found = Runways.FirstOrDefault(r => r != null && r.ElevationFt != null && string.Equals((r.Runway ?? "").Trim(), (runway ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                ?? Runways.FirstOrDefault(r => r != null && r.ElevationFt != null && r.HasPlace) ?? Runways.FirstOrDefault(r => r != null && r.ElevationFt != null);
            return found?.ElevationFt;
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
            file.Helipads = (file.Helipads ?? []).Where(g => g != null).ToList();
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
    /// One runway of an aerodrome: its circuit side and (Circuit Trainer, given by two clicks on the map) where it is: the
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

    /// <summary>A view of the map kept on L1 / L2: its centre (degrees) and how many NM across.</summary>
    public sealed class SavedView
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AcrossNM { get; set; }

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

    /// <summary>The heights (ft above the threshold) of one class at an aerodrome; a missing one: the class's (an altitude, QNH).</summary>
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
