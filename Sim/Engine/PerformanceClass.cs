using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATCRoom.Sim
{
    /// <summary>
    /// What a kind of aircraft can do: speeds, climb and descent limits, accelerations, bank limit. Kept in the open
    /// data file performance.json (see docs/SIM-FORMAT.md), so the values can be changed without a new build.
    /// Speeds are true airspeeds (kt), vertical speeds ft/min, accelerations per second.
    /// </summary>
    public sealed class PerformanceClass
    {
        public string Name { get; set; } = "";
        public double MinSpeedKt { get; set; }
        public double MaxSpeedKt { get; set; }
        /// <summary>The initial speed: the default speed of a new aircraft of this class (and of the first point of a new flight plan).</summary>
        public double ApproachSpeedKt { get; set; }
        /// <summary>The speed on the final, near the threshold ("Slow down to final"); between the minimum and the initial speed.</summary>
        public double FinalSpeedKt { get; set; }
        public double MaxClimbFpm { get; set; }
        /// <summary>Largest rate of descent (ft/min, positive).</summary>
        public double MaxDescentFpm { get; set; }
        public double AccelerationKtPerSecond { get; set; }
        public double DecelerationKtPerSecond { get; set; }
        /// <summary>How fast the vertical speed changes (ft/min per second): a change from −700 to −1000 takes about a second at 300.</summary>
        public double VerticalAccelerationFpmPerSecond { get; set; }
        /// <summary>Bank angle limit for the turns (degrees): the turn rate is lower at high speed.</summary>
        public double MaxBankDegrees { get; set; }

        // ----- The circuit (TWR Trainer). Heights above the threshold; 0 or missing: the value of the class name, see CircuitDefaults.
        /// <summary>Height of the circuit (ft above the threshold).</summary>
        public double CircuitHeightFt { get; set; }
        /// <summary>Speed in the circuit (kt), until the final.</summary>
        public double CircuitSpeedKt { get; set; }
        /// <summary>Distance of the downwind from the runway (NM): conventional circuit.</summary>
        public double DownwindNM { get; set; }
        /// <summary>Distance of the downwind from the runway (NM): close pattern, the width of the jet circuit (at most 1 NM).</summary>
        public double CloseDownwindNM { get; set; }
        /// <summary>Distance of the downwind from the runway (NM): wide circuit (outside downwind).</summary>
        public double WideDownwindNM { get; set; }
        /// <summary>Bank angle of the turns in the circuit (degrees): steeper than the en-route turns, so the circuit keeps its shape.</summary>
        public double CircuitBankDegrees { get; set; }
        /// <summary>Close pattern: where the 180° turn rolls out on the final (NM before the threshold) and its height there (ft above the threshold).</summary>
        public double RollOutNM { get; set; }
        public double RollOutHeightFt { get; set; }
        /// <summary>Overhead pattern: height (ft above the threshold) and speed (kt) of the initial; bank of the break turn (degrees, a level turn with some G).</summary>
        public double OverheadHeightFt { get; set; }
        public double OverheadSpeedKt { get; set; }
        public double BreakBankDegrees { get; set; }

        /// <summary>Fields of a newer version of the file, kept as they are.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public PerformanceClass Copy()
        {
            PerformanceClass copy = (PerformanceClass)MemberwiseClone();
            copy.Extra = Extra == null ? null : new Dictionary<string, JsonElement>(Extra);
            return copy;
        }

        /// <summary>Repairs values written by hand: inside sensible limits, minimum ≤ final ≤ initial (approach) ≤ maximum.</summary>
        public void Normalize()
        {
            Name = (Name ?? "").Trim();
            MinSpeedKt = Clamp(MinSpeedKt, 30, 400, 100);
            MaxSpeedKt = Clamp(MaxSpeedKt, MinSpeedKt + 10, SimEngine.MaxSpeed, Math.Max(MinSpeedKt + 10, 250));
            ApproachSpeedKt = Clamp(ApproachSpeedKt, MinSpeedKt, MaxSpeedKt, (MinSpeedKt + MaxSpeedKt) / 2);
            FinalSpeedKt = Clamp(FinalSpeedKt, MinSpeedKt, ApproachSpeedKt, MinSpeedKt + (ApproachSpeedKt - MinSpeedKt) / 3);
            MaxClimbFpm = Clamp(MaxClimbFpm, 200, 8000, 2000);
            MaxDescentFpm = Clamp(MaxDescentFpm, 200, 8000, 3000);
            AccelerationKtPerSecond = Clamp(AccelerationKtPerSecond, 0.2, 10, 1.5);
            DecelerationKtPerSecond = Clamp(DecelerationKtPerSecond, 0.2, 10, 1);
            VerticalAccelerationFpmPerSecond = Clamp(VerticalAccelerationFpmPerSecond, 50, 2000, 300);
            MaxBankDegrees = Clamp(MaxBankDegrees, 10, 60, 30);
            (double height, double speed, double downwind, double close, double wide, double bank, double rollOut, double rollOutHeight) = CircuitDefaults(Name, ApproachSpeedKt, MaxBankDegrees);
            CircuitHeightFt = Clamp(CircuitHeightFt, 300, 5000, height);
            CircuitSpeedKt = Clamp(CircuitSpeedKt, FinalSpeedKt, MaxSpeedKt, speed);
            DownwindNM = Clamp(DownwindNM, 0.3, 5, downwind);
            CloseDownwindNM = Clamp(CloseDownwindNM, 0.2, 1.5, close);
            WideDownwindNM = Clamp(WideDownwindNM, DownwindNM, 8, Math.Max(wide, DownwindNM));
            CircuitBankDegrees = Clamp(CircuitBankDegrees, 10, 60, bank);
            RollOutNM = Clamp(RollOutNM, 0.3, 3, rollOut);
            RollOutHeightFt = Clamp(RollOutHeightFt, 150, 1500, rollOutHeight);
            (double overheadHeight, double overheadSpeed, double breakBank) = OverheadDefaults(Name, CircuitSpeedKt);
            OverheadHeightFt = Clamp(OverheadHeightFt, 500, 5000, overheadHeight);
            OverheadSpeedKt = Clamp(OverheadSpeedKt, CircuitSpeedKt, MaxSpeedKt, overheadSpeed);
            BreakBankDegrees = Clamp(BreakBankDegrees, 30, 80, breakBank);
        }

        /// <summary>
        /// The overhead values of the first classes (and of a class without them): initial height (ft above the threshold) and
        /// speed (kt), bank of the break (degrees). Provisional values of 2 Nov 2026 (kneeboard: initial at 1500 ft, 300-400 kt).
        /// </summary>
        public static (double Height, double Speed, double BreakBank) OverheadDefaults(string? name, double circuitSpeedKt) =>
            (name ?? "").Trim().ToUpperInvariant() switch
            {
                "LIGHT" => (1500, 110, 45),
                "TURBOPROP" => (1500, 220, 60),
                "JET" => (1500, 300, 75),
                _ => (1500, circuitSpeedKt + 30, 60)
            };

        /// <summary>
        /// The circuit values of the first classes (and of a class without them): height (ft above the threshold), speed
        /// (kt), downwind conventional, close (jet circuit, up to 1.5 NM) and wide (NM), bank in the circuit (degrees),
        /// roll out of the close pattern on the final (NM, ft). Provisional values of 2 Nov 2026 (the jet's from the user and
        /// an overhead-break kneeboard: close downwind 1.2 NM, roll out 1 NM at 450 ft), to be tuned in performance.json.
        /// </summary>
        public static (double Height, double Speed, double Downwind, double Close, double Wide, double Bank, double RollOut, double RollOutHeight) CircuitDefaults(
            string? name, double approachSpeedKt, double maxBank) =>
            (name ?? "").Trim().ToUpperInvariant() switch
            {
                "LIGHT" => (1000, 90, 1.0, 0.6, 2.5, 30, 0.5, 250),
                "TURBOPROP" => (1500, 150, 1.2, 0.8, 3.0, 45, 0.75, 350),
                "JET" => (1500, 200, 1.5, 1.2, 3.5, 60, 1.0, 450),
                _ => (1000, approachSpeedKt, 1.2, 0.8, 3.0, Math.Max(maxBank, 30), 0.75, 350)
            };

        private static double Clamp(double value, double min, double max, double fallback) =>
            double.IsFinite(value) && value > 0 ? Math.Clamp(value, min, max) : Math.Clamp(fallback, min, max);

        /// <summary>The classes of a first start: a light aircraft, a turboprop and a jet.</summary>
        public static List<PerformanceClass> Defaults() =>
        [
            new() { Name = "Light", MinSpeedKt = 60, MaxSpeedKt = 160, ApproachSpeedKt = 90, FinalSpeedKt = 70, MaxClimbFpm = 800, MaxDescentFpm = 1500,
                AccelerationKtPerSecond = 2, DecelerationKtPerSecond = 1.5, VerticalAccelerationFpmPerSecond = 400, MaxBankDegrees = 30,
                CircuitHeightFt = 1000, CircuitSpeedKt = 90, DownwindNM = 1.0, CloseDownwindNM = 0.6, WideDownwindNM = 2.5, CircuitBankDegrees = 30,
                RollOutNM = 0.5, RollOutHeightFt = 250, OverheadHeightFt = 1500, OverheadSpeedKt = 110, BreakBankDegrees = 45 },
            new() { Name = "Turboprop", MinSpeedKt = 90, MaxSpeedKt = 280, ApproachSpeedKt = 140, FinalSpeedKt = 110, MaxClimbFpm = 2000, MaxDescentFpm = 3000,
                AccelerationKtPerSecond = 1.5, DecelerationKtPerSecond = 1, VerticalAccelerationFpmPerSecond = 400, MaxBankDegrees = 30,
                CircuitHeightFt = 1500, CircuitSpeedKt = 150, DownwindNM = 1.2, CloseDownwindNM = 0.8, WideDownwindNM = 3.0, CircuitBankDegrees = 45,
                RollOutNM = 0.75, RollOutHeightFt = 350, OverheadHeightFt = 1500, OverheadSpeedKt = 220, BreakBankDegrees = 60 },
            new() { Name = "Jet", MinSpeedKt = 120, MaxSpeedKt = 350, ApproachSpeedKt = 180, FinalSpeedKt = 140, MaxClimbFpm = 3000, MaxDescentFpm = 4000,
                AccelerationKtPerSecond = 1.5, DecelerationKtPerSecond = 1, VerticalAccelerationFpmPerSecond = 300, MaxBankDegrees = 30,
                CircuitHeightFt = 1500, CircuitSpeedKt = 200, DownwindNM = 1.5, CloseDownwindNM = 1.2, WideDownwindNM = 3.5, CircuitBankDegrees = 60,
                RollOutNM = 1.0, RollOutHeightFt = 450, OverheadHeightFt = 1500, OverheadSpeedKt = 300, BreakBankDegrees = 75 }
        ];

        /// <summary>The class a new aircraft gets when none is chosen (by the engine).</summary>
        public const string DefaultName = "Turboprop";
        /// <summary>The class the Instructor+ proposes for new aircraft and flight plans.</summary>
        public const string ProposedName = "Jet";

        /// <summary>The final speeds of the first classes (files written before the final speed existed).</summary>
        private static double FirstFinalSpeed(string name) => name.Trim().ToUpperInvariant() switch
        {
            "LIGHT" => 70,
            "TURBOPROP" => 110,
            "JET" => 140,
            _ => 0
        };

        /// <summary>
        /// A class of a file written before the final speed existed: the final speed of its name (others: found by
        /// <see cref="Normalize"/>), and the initial speeds of 24 Oct 2026 where the old ones were never changed
        /// (Turboprop 120 → 140, Jet 140 → 180). True when something was changed.
        /// </summary>
        public bool UpgradeOld()
        {
            if (double.IsFinite(FinalSpeedKt) && FinalSpeedKt > 0) return false;
            string name = (Name ?? "").Trim().ToUpperInvariant();
            FinalSpeedKt = FirstFinalSpeed(name);
            // Another class: a third of the way from its minimum to its initial speed (repaired by Normalize if needed).
            if (FinalSpeedKt == 0) FinalSpeedKt = double.IsFinite(MinSpeedKt) && double.IsFinite(ApproachSpeedKt) && ApproachSpeedKt > MinSpeedKt && MinSpeedKt > 0
                ? MinSpeedKt + (ApproachSpeedKt - MinSpeedKt) / 3 : 100;
            if (name == "TURBOPROP" && ApproachSpeedKt == 120) ApproachSpeedKt = 140;
            if (name == "JET" && ApproachSpeedKt == 140) ApproachSpeedKt = 180;
            return true;
        }
    }

    /// <summary>
    /// The file performance.json: the performance classes. Read at start; written only when it is missing (then with
    /// the defaults). A file made by a newer version of the program is used but never overwritten; fields this
    /// version does not know are kept.
    /// </summary>
    public sealed class PerformanceFile
    {
        public const int CurrentFormatVersion = 1;
        public const string FileName = "performance.json";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // Forgiving with hand edits: "minSpeedKt" or "MinSpeedKt", 120 or "120".
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public List<PerformanceClass> Classes { get; set; } = PerformanceClass.Defaults();

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>The classes, repaired: names unique and not empty, at least one class.</summary>
        public List<PerformanceClass> NormalizedClasses()
        {
            List<PerformanceClass> result = [];
            foreach (PerformanceClass item in Classes ?? [])
            {
                if (item == null) continue;
                PerformanceClass copy = item.Copy();
                copy.Normalize();
                if (copy.Name.Length == 0 || result.Any(c => string.Equals(c.Name, copy.Name, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(copy);
            }
            return result.Count > 0 ? result : PerformanceClass.Defaults();
        }

        /// <summary>
        /// Reads the file. Missing or empty: the defaults, and the file is written. Damaged: kept as ".bad", the
        /// defaults written. Newer version: used as far as understood, <c>ReadOnly</c> (never written).
        /// <c>Message</c> says what happened when it was not a plain read (null otherwise).
        /// </summary>
        public static (List<PerformanceClass> Classes, bool ReadOnly, string? Message) Load(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    PerformanceFile created = new();
                    Write(path, created);
                    return (created.NormalizedClasses(), false, null);
                }
                PerformanceFile? file = JsonSerializer.Deserialize<PerformanceFile>(File.ReadAllText(path), JsonOptions);
                if (file == null) throw new JsonException("Empty document.");
                if (file.FormatVersion > CurrentFormatVersion)
                {
                    return (file.NormalizedClasses(), true, $"{FileName} was made by a newer version of AuroraPAR: it is used as far as this version understands it, and not changed.");
                }
                // Written before the final speed existed: the new values, and the file written again with them.
                bool upgraded = false;
                foreach (PerformanceClass item in file.Classes ?? []) if (item != null && item.UpgradeOld()) upgraded = true;
                if (upgraded)
                {
                    try { Write(path, file); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
                return (file.NormalizedClasses(), false, null);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                try { File.Copy(path, path + ".bad", overwrite: true); } catch (Exception) { }
                PerformanceFile created = new();
                try { Write(path, created); } catch (Exception) { }
                return (created.NormalizedClasses(), false, $"{FileName} could not be read: it was kept as {FileName}.bad and the default classes were written.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (PerformanceClass.Defaults(), true, $"{FileName} could not be read or written ({ex.Message}): the default classes are used.");
            }
        }

        public static void Write(string path, PerformanceFile file)
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        }
    }
}
