using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// Where TWR Trainer keeps its files. Normally %AppData%\ATCRoom\TWRTrainer (settings, performance.json, the
    /// aerodromes) and the maps shared with the other programs in %AppData%\ATCRoom\Maps; portable when its settings
    /// file is beside the program: everything beside it (the Maps folder too).
    /// </summary>
    internal static class Paths
    {
        public const string SettingsFileName = "TWRTrainer.settings.json";

        public static bool Portable => File.Exists(Path.Combine(AppContext.BaseDirectory, SettingsFileName));

        private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string Folder => Portable ? AppContext.BaseDirectory : Path.Combine(AppData, "ATCRoom", "TWRTrainer");

        public static string MapsFolder => Portable ? Path.Combine(AppContext.BaseDirectory, "Maps") : Path.Combine(AppData, "ATCRoom", "Maps");

        /// <summary>The files of the circuits of the aerodromes (one per aerodrome, LIBN.json).</summary>
        public static string AerodromesFolder => Path.Combine(Folder, "Aerodromes");

        public static string Settings => Path.Combine(Folder, SettingsFileName);

        public static string Performance => Path.Combine(Folder, ATCRoom.Sim.PerformanceFile.FileName);

        /// <summary>The folders as said to the user (without the Windows user name).</summary>
        public static string FolderText => Portable ? "the folder of this program (portable)" : "%AppData%\\ATCRoom\\TWRTrainer";
        public static string MapsFolderText => Portable ? "the Maps folder beside this program (portable)" : "%AppData%\\ATCRoom\\Maps";
    }

    /// <summary>What TWR Trainer remembers between two starts. Fields it does not know are kept.</summary>
    internal sealed class TrainerSettings
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>The map opened last (full path).</summary>
        public string MapPath { get; set; } = "";
        /// <summary>The runway in use last.</summary>
        public string Runway { get; set; } = "";
        /// <summary>The last METAR line given (for the wind).</summary>
        public string Metar { get; set; } = "";
        /// <summary>The METAR on the map with only the wind and the QNH (the user, 3 Nov).</summary>
        public bool MetarWindQnhOnly { get; set; }
        /// <summary>The map categories turned off (null: the defaults: MVA, CTR, FIX and the others off by default).</summary>
        public List<string>? CategoriesOff { get; set; }
        public double LongFinalNM { get; set; } = 10;
        /// <summary>The sections of the panel open (they close to their bar).</summary>
        public bool AerodromeOpen { get; set; } = true;
        public bool SimulationOpen { get; set; } = true;
        /// <summary>The parts of the panel: in the column or floating over the map, their place and size.</summary>
        public Dictionary<string, PartPlacement>? Parts { get; set; }
        public double InitialPointNM { get; set; } = 3;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public static TrainerSettings Load()
        {
            try
            {
                if (File.Exists(Paths.Settings)) return JsonSerializer.Deserialize<TrainerSettings>(File.ReadAllText(Paths.Settings), Options) ?? new();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
            return new();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.Folder);
                File.WriteAllText(Paths.Settings, JsonSerializer.Serialize(this, Options));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
