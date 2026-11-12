using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// Where Circuit Trainer keeps its files. Normally %AppData%\ATCRoom\CircuitTrainer (settings, performance.json, the
    /// aerodromes) and the maps shared with the other programs in %AppData%\ATCRoom\Maps; portable when its settings
    /// file is beside the program: everything beside it (the Maps folder too).
    /// </summary>
    internal static class Paths
    {
        public const string SettingsFileName = "CircuitTrainer.settings.json";

        public static bool Portable => File.Exists(Path.Combine(AppContext.BaseDirectory, SettingsFileName));

        private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string Folder => Portable ? AppContext.BaseDirectory : Path.Combine(AppData, "ATCRoom", "CircuitTrainer");

        public static string MapsFolder => Portable ? Path.Combine(AppContext.BaseDirectory, "Maps") : Path.Combine(AppData, "ATCRoom", "Maps");

        /// <summary>The files of the circuits of the aerodromes (one per aerodrome, LIBN.json).</summary>
        public static string AerodromesFolder => Path.Combine(Folder, "Aerodromes");

        public static string Settings => Path.Combine(Folder, SettingsFileName);

        /// <summary>The photos for the debriefing (the user, 6 Nov): one folder for each session.</summary>
        public static string PhotosFolder => Path.Combine(Folder, "Photos");

        /// <summary>The logs of the flights (the user, 6-7 Nov): one CSV file for each session.</summary>
        public static string LogsFolder => Path.Combine(Folder, "Logs");

        public static string Performance => Path.Combine(Folder, ATCRoom.Sim.PerformanceFile.FileName);

        /// <summary>The folders as said to the user (without the Windows user name).</summary>
        public static string FolderText => Portable ? "the folder of this program (portable)" : "%AppData%\\ATCRoom\\CircuitTrainer";
        public static string MapsFolderText => Portable ? "the Maps folder beside this program (portable)" : "%AppData%\\ATCRoom\\Maps";
    }

    /// <summary>What Circuit Trainer remembers between two starts. Fields it does not know are kept.</summary>
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
        /// <summary>The runway file of AuroraPAR (runways.par) last used to place the runways (the user, 6 Nov).</summary>
        public string RunwaysParPath { get; set; } = "";
        /// <summary>The runway in use last.</summary>
        public string Runway { get; set; } = "";
        /// <summary>The pilot reaction (s, 0–5; the user, 10 Nov: as AuroraPAR v2 Trainer): the orders are carried out after it.</summary>
        public double PilotReaction { get; set; } = 1;
        /// <summary>The lesson through the direct link (VPN or home network) instead of the public relays, and the instructor's IP typed by the student.</summary>
        public bool LessonDirect { get; set; }
        public string LessonHost { get; set; } = "";
        /// <summary>The last METAR line given (for the wind).</summary>
        public string Metar { get; set; } = "";
        /// <summary>The METAR on the map with only the wind and the QNH (the user, 3 Nov).</summary>
        public bool MetarWindQnhOnly { get; set; }
        /// <summary>Where the bar of the map is (its top left corner on the map; null: top middle).</summary>
        public double? MapBarLeft { get; set; }
        public double? MapBarTop { get; set; }
        /// <summary>The map categories turned off (null: the defaults: MVA, CTR, FIX and the others off by default).</summary>
        public List<string>? CategoriesOff { get; set; }
        public double LongFinalNM { get; set; } = 10;
        /// <summary>The sections of the panel open (they close to their bar).</summary>
        public bool AerodromeOpen { get; set; } = true;
        /// <summary>The inside of the panels light (from the next start; the frames and the map stay dark).</summary>
        public bool LightPanels { get; set; }
        /// <summary>The plans waiting drawn on the map (WAITING TFC, the user 6 Nov).</summary>
        public bool ShowWaiting { get; set; } = true;
        /// <summary>Traffic generation on its Quick tab (else Advanced; the user, 7 Nov).</summary>
        public bool QuickTab { get; set; } = true;
        /// <summary>The range rings on the instructor's map (the user, 7 Nov; spacing per aerodrome, Settings › Map).</summary>
        public bool ShowRings { get; set; } = true;
        /// <summary>The traffic of the network (IVAO, through Aurora on this PC) on the map (the user, 7 Nov).</summary>
        public bool ShowIvao { get; set; }
        /// <summary>The map: 0 north up; 1 the runway in use across the screen, its threshold on the left; 2 on the right (the user, 7 Nov).</summary>
        public int MapTurn { get; set; }
        /// <summary>My VID on the network, typed (used when IVAO's list does not give it; the user, 7 Nov): the instructor's, or the student's in a lesson.</summary>
        public string InstructorVid { get; set; } = "";
        /// <summary>My callsign (the user, 9 Nov): the student's in the log of the lesson when he is not connected to IVAO.</summary>
        public string MyCallsign { get; set; } = "";
        /// <summary>The student's map turned (as <see cref="MapTurn"/>; the user, 9 Nov).</summary>
        public int StudentMapTurn { get; set; }
        /// <summary>The student's IVAO traffic from his own Aurora (the student console linked to another PC; the user, 9 Nov).</summary>
        public bool StudentIvao { get; set; }
        /// <summary>The last lesson code written to join one.</summary>
        public string LessonCode { get; set; } = "";
        /// <summary>The colours of the strips by kind of flight (names of StripColours; the user 6 Nov: white, yellow, pink).</summary>
        public string StripDeparture { get; set; } = "WHITE";
        public string StripArrival { get; set; } = "YELLOW";
        public string StripLocal { get; set; } = "PINK";
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
