using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ATCRoom.Maps;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The instructor's window (step 4a): the top view of the aerodrome and, on the right, the panels: Settings (map,
    /// runway in use and where it is, its circuit side, the altitudes of the classes, the wind, ...), Traffic generation
    /// (Quick | Advanced), the Traffic List, Aircraft control (the commands of the selected aircraft), the Strip bay.
    /// </summary>
    internal sealed class MainWindow : Window
    {
        // (The inside of the panels: dark or light, the user 6 Nov; see Theme.)
        private static Brush PanelBrush => Theme.Panel;
        private static Brush TextBrush => Theme.Text;
        private static Brush DimBrush => Theme.Dim;
        private static Brush WarnBrush => Theme.Warn;

        private readonly SimEngine engine = new();
        private readonly Airfield airfield = new();
        private readonly TowerCommands commands;
        private readonly TrainerSettings settings = TrainerSettings.Load();
        private readonly TowerView view = new();
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
        /// <summary>The network (the user, 7 Nov): Aurora on this PC, asked every 2 s while IVAO is on; the log of the flights.</summary>
        private readonly Aurora aurora = new();
        private readonly FlightLog flightLog = new();
        private readonly DispatcherTimer networkTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private bool networkBusy, logErrorSaid;
        private DateTime networkCallsignAt = DateTime.MinValue, networkMetarAt = DateTime.MinValue;
        private string networkCallsign = "";
        private string networkProblem = "", networkMoving = "";
        private DateTime networkProblemSince;
        private DateTime? networkStillSince;
        private Button ivaoButton = null!;
        private Button northButton = null!, runwayButton = null!, sessionButton = null!;
        private int sessionShown = -1;
        /// <summary>The VID found in IVAO's list for the callsign of Aurora (the user, 7 Nov), and when it was asked.</summary>
        private string networkVid = "", vidFor = "";
        private DateTime vidAskedAt = DateTime.MinValue;
        private readonly TextBox vidBox = new() { Width = 90, MaxLength = 9 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double lastTick;
        private int ticks;
        private double speed = 1;
        /// <summary>The circuit chosen with the wheel on the label: for which aircraft, since when (s of the clock).</summary>
        private string? pendingCallsign;
        private double pendingSince;
        /// <summary>The height or the speed chosen with the wheel on the label: of this aircraft, since this time.</summary>
        private string? valueCallsign;
        private double valueSince;
        private string? headingWheel;
        private Window? keysWindow;
        private bool updating;

        // The panel.
        private readonly StackPanel panel = new() { Margin = new Thickness(10) };
        private readonly TextBlock mapText = new() { TextWrapping = TextWrapping.Wrap };
        /// <summary>The runway in use (buttons, rebuilt with the runways placed) and the side of each circuit there.</summary>
        private ChoiceRow<string> runwayRow = new([]);
        private readonly StackPanel runwayHolder = new();
        /// <summary>The side of each group of circuits at the runway in use: conventional and wide, jet (overhead, close), SFO.</summary>
        private readonly ChoiceRow<CircuitSide> conventionalSideRow = SideRow("conventional and wide circuits"), jetSideRow = SideRow("jet circuits (overhead, close)"), sfoSideRow = SideRow("SFO");
        /// <summary>The bar on the map (the user, 4 Nov): the controls of the lesson, always in sight.</summary>
        private readonly ChoiceRow<int> levelRow = new([(1, "L1"), (2, "L2")], (n, _) => n == 1 ? "Level 1 (Ctrl+F1): the aerodrome and its circuits." : "Level 2 (Ctrl+F2): the wide area, the IP and the long final.");
        /// <summary>The summaries of the closed sections of Settings.</summary>
        private readonly TextBlock gatesSummary = SectionSummary(), weatherSummary = SectionSummary(), circuitsSummary = SectionSummary(), mapSummary = SectionSummary();
        private readonly TextBox newRunwayName = new() { Width = 50 };
        private readonly TextBox newRunwayElevation = new() { Width = 60 };
        private readonly TextBox metarBox = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock windText = new();
        /// <summary>The METAR on the map: whole, or only the wind and the QNH.</summary>
        private readonly ChoiceRow<bool> metarShowRow = new([(false, "WHOLE"), (true, "WIND + QNH")], (only, _) => only
            ? "On the map only the station, the time, the wind and the QNH of the METAR." : "On the map the whole METAR.");
        private string appVersion = "";
        private readonly ChoiceRow<bool> panelsRow = new([(false, "DARK"), (true, "LIGHT")], (light, _) => light
            ? "The inside of the panels light (from the next start); their frames and the map stay dark." : "The inside of the panels dark, as the frames.");
        private readonly Button pauseButton = new();
        /// <summary>The same RUN / PAUSE in the Traffic List.</summary>
        private readonly Button trafficPauseButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 4, 0, 0) };
        /// <summary>The speed of the simulation (the user, 4 Nov: buttons, not a drop-down list).</summary>
        private readonly ChoiceRow<double> speedRow = new([(1.0, "x1"), (2.0, "x2"), (4.0, "x4")], (v, _) => v == 1 ? "Real time." : $"The time runs {v:0} times faster.");
        private readonly TextBox longFinalBox = new() { Width = 45 };
        private readonly TextBox initialPointBox = new() { Width = 45 };
        private readonly StackPanel layers = new();
        // Traffic generation: rows of buttons (the choices kept for the next one).
        /// <summary>Where it starts: an entry gate, the runway, or already in the circuit (tests); rebuilt with the gates.</summary>
        private ChoiceRow<string> fromRow = new([]);
        private readonly StackPanel fromHolder = new();
        private readonly ChoiceRow<CircuitEntry> entryRow = new([(CircuitEntry.Downwind, "DW"), (CircuitEntry.Base, "BASE"), (CircuitEntry.InitialPoint, "IP"),
            (CircuitEntry.HighKey, "HK"), (CircuitEntry.Final, "FINAL"), (CircuitEntry.Circling, "CIRCLING")], (_, t) => ChoiceTip("entry " + t));
        private readonly TextBox delayBox = new() { Width = 45, Text = "0" };
        /// <summary>An arrival or a crossing (to the gate To, straight or over the field).</summary>
        private enum Flight { Arrival, Local, Departure, Transit }
        /// <summary>The four types (the user, 5 Nov), each with its end: arrivals and locals land, departures leave, crossings leave at their gate.</summary>
        private readonly ChoiceRow<Flight> typeRow = new([(Flight.Arrival, "ARRIVAL"), (Flight.Local, "LOCAL"), (Flight.Departure, "DEPARTURE"), (Flight.Transit, "TRANSIT")], (type, _) => type switch
        {
            Flight.Transit => "Crossing the zone from Start from to Exit to: straight, over the field, or with circuits; gone at the exit gate.",
            Flight.Departure => "Departing from the runway: away at once (OUT) or after its circuits, along the runway heading or a radial; gone 10 NM out.",
            Flight.Local => "A local flight: it takes off, flies its circuits and lands.",
            _ => "Arriving from a gate or the long final: its circuits, then it lands."
        });
        private enum Via { Direct, Overhead, Circuits }
        /// <summary>A departure with no circuit (the user, 5 Nov): on along the runway heading, or onto a radial (magnetic) turning left or right, from a point (null: the middle of the runway).</summary>
        private readonly ChoiceRow<bool> departRow = new([(false, "RWY HDG"), (true, "RADIAL")], (radial, _) => radial
            ? "Above 400 ft it turns (L or R) to intercept the radial (magnetic, outbound) from the middle of the runway (or from a point), then along it."
            : "On along the runway heading.");
        /// <summary>
        /// The first turn to the way out (the user, 6 Nov): lit by itself by the side of the runway the exit is on (on the axis or
        /// the opposite radial: the aerodrome's circuit side); clicked: kept until the way out changes.
        /// </summary>
        private readonly ChoiceRow<bool> departTurnRow = new([(false, "L"), (true, "R")], (right, _) =>
            $"After the take-off the turn to its way out to the {(right ? "right" : "left")} (lit by itself by where the exit is; click to change it).");
        private Grid exitTurnLine = null!, exitAltLine = null!;
        /// <summary>The altitude it climbs to when it leaves (the user, 6 Nov; empty: 3000 ft QNH, the user 7 Nov).</summary>
        private readonly TextBox exitAltBox = new() { Width = 55 };
        private TextBlock exitAltName = null!;
        private readonly TextBox radialBox = new() { Width = 45 };
        private readonly TextBlock radialFromText = new() { Foreground = DimBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = "from the middle of the runway" };
        private (double X, double Y)? radialFrom;
        private bool turnChosen;
        private Grid departLine = null!, radialLine = null!;
        private readonly TextBox variationBox = new() { Width = 45 };
        private ChoiceRow<string> toRow = new([]);
        private readonly StackPanel toHolder = new();
        private readonly ChoiceRow<Via> viaRow = new([(Via.Direct, "DIRECT"), (Via.Overhead, "OVERHEAD"), (Via.Circuits, "CIRCUITS")], (via, _) => via switch
        {
            Via.Overhead => "Crossing over the field (the middle of the runway), then to the exit gate.",
            Via.Circuits => "Into the circuit from Start from (Pattern entry), its circuits (On Final), then out to the exit gate.",
            _ => "Crossing straight from gate to gate."
        });
        private readonly TextBox arrivalBox = new() { Width = 55 };
        /// <summary>The High Key of an SFO in Traffic generation (the user, 6 Nov; empty: the aerodrome's, Settings).</summary>
        private readonly TextBox hkBox = new() { Width = 55 };
        private Grid hkLine = null!;
        /// <summary>A formation (the user, 4 Nov): how many, which of them land together, the seconds between them at the split.</summary>
        private readonly ChoiceRow<int> formationRow = new([(1, "SINGLE"), (2, "2"), (3, "3"), (4, "4")], (n, _) => n == 1
            ? "Alone." : $"A formation of {n}: one label until it splits (its wingmen: the numbers after the leader's: VIPER01, 02...).");
        /// <summary>
        /// The circuits (the user, 5 Nov): arrivals and locals the circuits more than the first one (∞: on and on; lit when On
        /// Final is not F/S), departures and crossings the circuits before they leave (a departure OUT: away at once).
        /// </summary>
        private readonly ChoiceRow<int> lapsRow = new([(0, "OUT"), (1, "1"), (2, "2"), (3, "3"), (4, "4"), (TowerPlan.Forever, "∞")], (n, _) => n == 0
            ? "A departure away at once after the take-off (Exit via: the runway heading or a radial)." : n == TowerPlan.Forever
            ? "Circuits on and on, each final as in On Final (until you change it)." : $"{n} circuit(s) (arrivals and locals: more than the first one; departures and crossings: before they leave).");
        private Grid toLine = null!, viaLine = null!, intervalLine = null!, fromPointLine = null!, toPointLine = null!;
        private TextBlock toName = null!;
        /// <summary>The R/D points of Traffic generation (the user, 6 Nov): radial °M and distance NM from the middle of the runway.</summary>
        private readonly TextBox fromRadialBox = new() { Width = 45 }, fromDistanceBox = new() { Width = 40 }, toRadialBox = new() { Width = 45 }, toDistanceBox = new() { Width = 40 };
        /// <summary>The rows of Traffic generation hidden when they do not apply (the user, 5 Nov: not grey, hidden).</summary>
        private Grid patternLine = null!, entryLine = null!, sideLine = null!, onFinalLine = null!, circuitsLine = null!;
        /// <summary>What every final does (the user, 5 Nov): F/S lands at the first one (arrivals and locals); T/G, L/A, L/P then the next circuit.</summary>
        private readonly ChoiceRow<EndAction> onFinalRow = new([(EndAction.FullStop, "F/S"), (EndAction.TouchAndGo, "T/G"), (EndAction.GoAround, "L/A"), (EndAction.LowPass, "L/P"), (PadEnd, "HPAD")],
            (_, t) => t == "HPAD" ? "Helicopter: it lands on a helipad (Helipad, below)." : "On each final: " + ChoiceTip(t));
        /// <summary>On Final HPAD (the user, 5 Nov; helicopters): a full stop on a helipad; a value of the row only, not of the engine.</summary>
        private const EndAction PadEnd = (EndAction)100;
        /// <summary>The helipad of Traffic generation: a registered one, or POINT (clicked on the map).</summary>
        private ChoiceRow<string> padRow = new([]);
        private readonly StackPanel padHolder = new();
        private (double X, double Y)? padPoint;
        private Grid padLine = null!;
        private TextBlock padName = null!;
        /// <summary>The pad row of a helicopter departure: from the runway (the user, 6 Nov: as now).</summary>
        private const string PadRunwayName = "RWY";
        private bool wasPadDeparture;
        /// <summary>The width of the names of the rows of Traffic generation (the user, 5 Nov: longer, clearer names).</summary>
        private const double LabelWidth = 112;
        private readonly ChoiceRow<double> splitRow = new([(3.0, "3s"), (4.0, "4s"), (5.0, "5s"), (10.0, "10s")], (v, _) => $"At the split each one {v:0} s after the one before.");
        private readonly TextBox newGateName = new() { Width = 70 };
        private readonly TextBox newExitName = new() { Width = 70 };
        private readonly TextBox newPadName = new() { Width = 70 };
        private readonly ChoiceRow<bool> pressureRow = new([(false, "QNH"), (true, "QFE")], (qfe, _) => qfe
            ? "QFE: the heights shown and written are above the runway (HGT)." : "QNH: the heights shown and written are altitudes, above the sea (ALT).");
        private TextBlock arrivalName = null!, heightName = null!, hkName = null!;
        /// <summary>What is shown and written = the height above the runway + this (QNH: the elevation; QFE: 0).</summary>
        private double HeightOffset => airfield.Circuits.Qfe ? 0 : airfield.ElevationFt;
        private string HeightWord => airfield.Circuits.Qfe ? "Height" : "Altitude";
        /// <summary>A formation landing, both full stop (the user, 4 Nov; it depends on the country, kept in the aerodrome file).</summary>
        private readonly ChoiceRow<bool> separationRow = new([(true, "FULL"), (false, "REDUCED")], (full, _) => full
            ? "Full rwy separation: the next one of a formation lands once the one ahead has left the runway."
            : "Reduced rwy separation: the next one of a formation lands when the one ahead has rolled a third of the runway (both full stop).");
        /// <summary>The flight plans (advanced generation), their list and the status text of each row.</summary>
        private readonly List<TowerPlan> plans = [];
        private readonly StackPanel planList = new();
        private readonly StackPanel flyingList = new();
        /// <summary>The plan of the Traffic List being changed in Traffic generation (the user, 4 Nov), or null; its buttons.</summary>
        private TowerPlan? editing;
        private Button toListButton = null!, cancelEditButton = null!, groupButton = null!;
        private string groupShown = "";
        private static Brush CallsignBrush => Theme.Callsign;
        private readonly List<(string Callsign, TextBlock Text, Button Freeze)> flyingRows = [];
        private readonly Dictionary<string, TextBlock> flyingCallsigns = [];
        private string flyingKey = "";
        private static readonly FontFamily RowFont = new("Consolas");
        private readonly List<(TowerPlan Plan, TextBlock Status, Button Hold)> planRows = [];
        private readonly Dictionary<TowerPlan, TextBlock> planCallsigns = [];
        /// <summary>The panel the helpers add to (a section while it is built).</summary>
        private Panel current = null!;
        // The order buttons of the selected aircraft (grey when they do not apply).
        private Button splitButton = null!, expTaxiButton = null!;
        private Grid selBreakGrid = null!, selBreakPointGrid = null!;
        private Button[] dctButtons = [];
        private Button padEndButton = null!, padNowButton = null!;
        // (Aircraft control, the user 8 Nov: the box and its presets on one row; who lands of a formation.)
        private readonly WrapPanel altPresets = new(), speedPresets = new(), landPanel = new();
        private Grid landLine = null!;
        private string presetsFor = "";
        private Grid padEndLine = null!;
        private Button breakNowButton = null!, holdButton = null!, startSfoButton = null!, extendButton = null!, baseButton = null!, orbitButton = null!,
            goAroundButton = null!, sideLeftButton = null!, sideRightButton = null!, wideButton = null!, rejoinButton = null!, breakupButton = null!;
        private readonly ChoiceRow<CircuitType> circuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide?> sideRow = new([(null, "AD"), (CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => ChoiceTip(t));
        private ChoiceRow<string> classRow = new([]);
        private readonly StackPanel classHolder = new();
        private readonly TextBox callsignBox = new() { Width = 90 };
        // (The user, 6 Nov: the ICAO type, free text, and the flight rules: on the strips of the student and in the log.)
        private readonly TextBox icaoTypeBox = new() { Width = 70, CharacterCasing = CharacterCasing.Upper, MaxLength = 8, ToolTip = "The ICAO type (F16, TOR, EUFI, C172...): free text, on the strips (empty: the aerodrome's, Settings › Aircraft)." };
        private readonly ChoiceRow<bool> rulesRow = new([(false, "VFR"), (true, "IFR")], (ifr, _) => ifr ? "Flight rules IFR (on the strips: I)." : "Flight rules VFR (on the strips: V).");
        private readonly TextBlock selectedText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBox headingBox = new() { Width = 55 }, heightBox = new() { Width = 55 }, speedKtBox = new() { Width = 55 };
        // The selected aircraft: each button an order at once.
        private readonly ChoiceRow<CircuitType> selCircuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => "From the next circuit (on the ground: CONV or WIDE only): " + ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide> selSideRow = new([(CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => "The side: of the circuit ordered, else now (an S over the aerodrome): " + ChoiceTip(t));
        private readonly ChoiceRow<EndAction> selEndRow = new(TowerCommands.Ends.Select(e => (e.End, e.Short)), (_, t) => "At the end of this final: " + ChoiceTip(t));
        private readonly ChoiceRow<BreakPoint> selBreakRow = new(TowerCommands.Breaks.Select(b => (b.Break, b.Short)), (_, t) => ChoiceTip("break " + t));
        private readonly StackPanel selectedPanel = new();
        /// <summary>The layer over the map where the parts taken out of the column float (empty: clicks go to the map).</summary>
        private readonly Canvas layer = new();
        /// <summary>The column of the parts on the right: hidden when they all float (the user, 4 Nov).</summary>
        private ScrollViewer column = null!;
        /// <summary>The bar of the lesson on the map (Run / Pause, speed, L1 / L2), movable.</summary>
        private Border mapBar = null!;
        /// <summary>The order of FLYING chosen by the instructor (drag and drop): callsigns, the new ones at the bottom.</summary>
        private readonly List<string> flyingOrder = [];
        /// <summary>A row of the Traffic List being dragged (by its grip).</summary>
        private Border? dragRow;
        /// <summary>The four parts of the panel (in the column or floating).</summary>
        private DockablePart settingsPart = null!, newPart = null!, trafficPart = null!, selectedPart = null!, stripPart = null!;
        // (The user, 6 Nov: the strip bay, here to see how it behaves; later the student's.)
        private readonly StripBay stripBay = new() { Margin = new Thickness(4) };
        private Button waitingButton = null!;
        /// <summary>The colours a strip can have (the user, 6 Nov: changeable; default white, yellow, pink).</summary>
        private static readonly (string Name, Color Colour)[] StripColours =
        [
            ("WHITE", Color.FromRgb(0xF4, 0xF4, 0xF0)), ("YELLOW", Color.FromRgb(0xF2, 0xE2, 0x8C)), ("PINK", Color.FromRgb(0xF2, 0xC4, 0xD6)),
            ("GREEN", Color.FromRgb(0xCD, 0xE8, 0xC0)), ("BLUE", Color.FromRgb(0xC9, 0xDD, 0xF2)), ("ORANGE", Color.FromRgb(0xF5, 0xCF, 0xA0)),
            ("GREY", Color.FromRgb(0xD6, 0xD9, 0xDC))
        ];
        private readonly StackPanel settingsContent = new(), newContent = new(), trafficContent = new(), selectedContent = new();
        /// <summary>The heights of the classes at this aerodrome (Settings › Circuits, the user 7 Nov).</summary>
        private readonly HeightsTable heightsTable = new() { Margin = new Thickness(0, 2, 0, 2) };
        /// <summary>The base traffic of this aerodrome (Settings › Aircraft, the user 7 Nov): callsign prefix, ICAO type, class.</summary>
        private readonly TextBox prefixBox = new() { Width = 70, CharacterCasing = CharacterCasing.Upper, MaxLength = 6 };
        private readonly TextBox defaultTypeBox = new() { Width = 70, CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
        private ChoiceRow<string> defaultClassRow = new([]);
        private readonly StackPanel defaultClassHolder = new();
        /// <summary>Traffic generation: the Quick and the Advanced tabs (the user, 7 Nov, as in AuroraPAR Trainer).</summary>
        private readonly ChoiceRow<bool> tabRow = new([(true, "QUICK"), (false, "ADVANCED")], (quick, _) => quick
            ? "One click, one aircraft at once: the base traffic of this aerodrome (Settings › Aircraft)." : "Every choice of a flight: ADD NOW, or TO LIST for the Traffic List; the scenarios.");
        private readonly StackPanel quickContent = new(), advancedContent = new();
        /// <summary>The range rings (the user, 7 Nov): their spacing per aerodrome, the button on the bar of the map.</summary>
        private readonly ChoiceRow<double> ringsRow = new([(1, "1"), (2, "2"), (5, "5"), (10, "10")], (nm, _) => $"Range rings every {nm:0} NM around the middle of the runway (up to 30 NM).");
        private Button ringsButton = null!;
        private readonly TextBlock quickBase = new() { Foreground = DimBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
        /// <summary>Where a message is shown: in the part it is about (the user, 3 Nov), one line each.</summary>
        private enum Area { Aerodrome, Simulation, New, Traffic, Selected }
        private readonly Dictionary<Area, TextBlock> messages = Enum.GetValues<Area>().ToDictionary(area => area,
            _ => new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = WarnBrush, Margin = new Thickness(0, 6, 0, 0) });

        /// <summary>The window of this run (for the crash handlers of Program, on any thread).</summary>
        internal static MainWindow? Current { get; private set; }

        public MainWindow()
        {
            Current = this;
            commands = new TowerCommands(engine, airfield);
            string version = Assembly.GetExecutingAssembly().GetName().Version is Version v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";
            appVersion = version;
            Title = $"TWR Trainer {version} - instructor";
            try
            {
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/TWRTrainer.ico"));
            }
            catch (Exception ex) when (ex is IOException or UriFormatException or NotSupportedException)
            {
                // (No icon: the window's default one.)
            }
            Width = 1400;
            Height = 900;
            WindowState = WindowState.Maximized;
            Background = PanelBrush;
            Foreground = TextBrush;

            BuildPanel();
            DockPanel root = new();
            column = new() { Content = panel, Width = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = PanelBrush };
            ScrollViewer scroll = column;
            DockPanel.SetDock(scroll, Dock.Right);
            root.Children.Add(scroll);
            Grid map = new();
            map.Children.Add(view);
            StackPanel bar = new() { Orientation = Orientation.Horizontal };
            // (Movable, the user 4 Nov: by its grip; a double click on it: back to the top middle.)
            TextBlock barGrip = new()
            {
                Text = "⋮⋮", Foreground = DimBrush, FontSize = 14, Cursor = Cursors.SizeAll, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(2, 0, 2, 0), Background = Brushes.Transparent,
                ToolTip = "Drag to move the bar; double click: back to the top middle."
            };
            bar.Children.Add(barGrip);
            bar.Children.Add(pauseButton);
            bar.Children.Add(new TextBlock { Text = "Speed", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
            speedRow.VerticalAlignment = VerticalAlignment.Center;
            levelRow.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(speedRow);
            bar.Children.Add(new TextBlock { Text = "View", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) });
            bar.Children.Add(levelRow);
            // (The user, 7 Nov: range rings, the instructor's map only.)
            ringsButton = ActionButton("", ToggleRings);
            ringsButton.Margin = new Thickness(12, 0, 0, 4);
            bar.Children.Add(ringsButton);
            // (The user, 7 Nov: NORTH up, or the runway in use across the screen; RWY again: turned round.)
            northButton = ActionButton("NORTH", () => SetMapTurn(0));
            northButton.Margin = new Thickness(12, 0, 0, 4);
            bar.Children.Add(northButton);
            runwayButton = ActionButton("RWY", () => SetMapTurn(settings.MapTurn == 1 ? 2 : 1));
            runwayButton.Margin = new Thickness(4, 0, 0, 4);
            bar.Children.Add(runwayButton);
            // (The user, 7 Nov: the traffic of IVAO from Aurora, only shown; its METAR used.)
            ivaoButton = ActionButton("", ToggleIvao);
            ivaoButton.Margin = new Thickness(4, 0, 0, 4);
            bar.Children.Add(ivaoButton);
            // (The user, 8 Nov: the session of the training or the exam, for the log; nothing starts flying.)
            sessionButton = ActionButton("START SESSION", ToggleSession);
            sessionButton.Margin = new Thickness(12, 0, 0, 4);
            bar.Children.Add(sessionButton);
            // (The user, 6 Nov: a photo of the map with its labels for the debriefing; also Ctrl+S and the middle mouse button.)
            Button photoButton = ActionButton("PHOTO", TakePhoto);
            photoButton.Margin = new Thickness(12, 0, 4, 4);
            photoButton.ToolTip = "A picture of the map with all the labels, for the debriefing (also Ctrl+S, or the middle mouse button on the map). Saved in the Photos folder, one folder for each session.";
            Button folderButton = ActionButton("FOLDER", OpenPhotos);
            folderButton.ToolTip = "Opens the folder of the photos of this session (Photos).";
            bar.Children.Add(photoButton);
            bar.Children.Add(folderButton);
            photoText.VerticalAlignment = VerticalAlignment.Center;
            photoText.Margin = new Thickness(4, 0, 2, 4);
            bar.Children.Add(photoText);
            // (The user, 7 Nov: the mark of a point of the plan in Advanced dragged on the map: the point moved.)
            view.MarkMoved += (mark, x, y) =>
            {
                if (mark == "START") SetPoint(fromRadialBox, fromDistanceBox, x, y);
                else if (mark == "EXIT") SetPoint(toRadialBox, toDistanceBox, x, y);
            };
            view.MouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                TakePhoto();
                e.Handled = true;
            };
            mapBar = new Border
            {
                Child = bar,
                Background = Theme.MapBar,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5D, 0x7A, 0x99)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 0)
            };
            map.Children.Add(layer);
            layer.Children.Add(mapBar);
            layer.SizeChanged += (_, _) => PlaceBar();
            mapBar.SizeChanged += (_, _) => PlaceBar();
            Point? barFrom = null;
            Point barStart = default;
            barGrip.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount >= 2)
                {
                    settings.MapBarLeft = settings.MapBarTop = null;
                    PlaceBar();
                    settings.Save();
                    e.Handled = true;
                    return;
                }
                barFrom = e.GetPosition(layer);
                barStart = new Point(Canvas.GetLeft(mapBar), Canvas.GetTop(mapBar));
                barGrip.CaptureMouse();
                e.Handled = true;
            };
            barGrip.MouseMove += (_, e) =>
            {
                if (barFrom is not Point from) return;
                Vector moved = e.GetPosition(layer) - from;
                settings.MapBarLeft = barStart.X + moved.X;
                settings.MapBarTop = barStart.Y + moved.Y;
                PlaceBar();
            };
            barGrip.MouseLeftButtonUp += (_, e) =>
            {
                if (barFrom == null) return;
                barFrom = null;
                barGrip.ReleaseMouseCapture();
                settings.Save();
                e.Handled = true;
            };
            root.Children.Add(map);
            Content = root;

            view.SelectionChanged += _ => ShowSelected();
            // (A plan waiting clicked on the map: into Traffic generation, as a click on its row.)
            view.WaitingClicked += tag => { if (tag is TowerPlan plan && plans.Contains(plan) && plan.Now != TowerPlan.State.Flying) EditPlan(plan); };
            view.FieldClicked += LabelField;
            view.FieldWheel += LabelWheel;
            view.SpeedChoices = SpeedChoices;
            view.CanBreakNow = engine.CanBreakNow;
            view.MenuAsked += callsign => OpenMenu(Menu(callsign));
            view.HeadingDragged += (callsign, heading) => { commands.Heading(callsign, heading); Say($"{callsign}: heading {heading:000} (out of the circuit; REJOIN brings it back).", Area.Selected); };
            Loaded += (_, _) => Start();
            Closing += (_, _) =>
            {
                settings.Save();
                timer.Stop();
                networkTimer.Stop();
                flightLog.EndSession(engine.Time, "the program closed");
                flightLog.Close(engine.Time, final: true);
                aurora.Close();
            };
            // (The user, 7 Nov: the orders of the instructor in the log of the flights.)
            commands.Logged = (callsign, what, detail) => flightLog.Event(engine.Time, what, callsign, detail, "INSTRUCTOR");
            networkTimer.Tick += async (_, _) => await PollNetwork();
            PreviewKeyDown += OnKeys;
            timer.Tick += (_, _) => Tick();
        }

        // ----- Start -----------------------------------------------------------------------------------------------------

        private void Start()
        {
            Directory.CreateDirectory(Paths.Folder);
            (List<PerformanceClass> classes, _, string? note) = PerformanceFile.Load(Paths.Performance);
            engine.SetPerformanceClasses(classes);
            MakeClassRow(null);
            if (note != null) Say(note);
            engine.LongFinalNM = settings.LongFinalNM;
            engine.InitialPointNM = settings.InitialPointNM;
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
            metarBox.Text = settings.Metar;
            vidBox.Text = settings.InstructorVid;
            metarShowRow.Show(settings.MetarWindQnhOnly);
            ApplyMetar(quiet: true);
            if (settings.MapPath.Length > 0 && File.Exists(settings.MapPath)) OpenMap(settings.MapPath, settings.Runway);
            else ShowAerodrome();
            UpdatePause();
            // (A log left open by a program that could not close it: marked, the user 8 Nov.)
            FlightLog.MarkUnfinished();
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Start();
            ShowIvaoButton();
            ShowSession();
            networkTimer.Start();
        }

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double wall = now - lastTick;
            lastTick = now;
            engine.Advance(wall * speed);
            IReadOnlyList<AircraftState> traffic = engine.Snapshot();
            view.SetTraffic(traffic, engine.Time);
            // (The log of the flights, the user 6-7 Nov: what changed since the last time.)
            flightLog.Aerodrome = airfield.Code;
            flightLog.Runway = airfield.Runway?.Runway ?? "";
            flightLog.Observe(traffic, engine.Time, FlightInfo);
            if (flightLog.InSession && (int)(engine.Time - flightLog.SessionStart) != sessionShown) ShowSession();
            if (flightLog.Error is string logError)
            {
                if (!logErrorSaid) Say(logError);
                logErrorSaid = true;
            }
            else logErrorSaid = false;
            view.Status = view.Picking ? view.Status : engine.Paused ? "PAUSED" : "";
            if (view.PendingCircuit is CircuitType chosen && now - pendingSince > 1.2)
            {
                // The wheel stopped: the circuit chosen is ordered.
                view.PendingCircuit = null;
                if (pendingCallsign != null && pendingCallsign == view.Selected)
                {
                    commands.NextCircuit(pendingCallsign, chosen, engine.SideFor(chosen));
                    ShowSelected();
                }
                pendingCallsign = null;
            }
            if (headingWheel != null && now - valueSince > 0.6)
            {
                // The wheel on the HDG box stopped: that heading (out of its circuit).
                if (headingWheel == view.Selected && double.TryParse(headingBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double hdg)) commands.Heading(headingWheel, hdg % 360);
                headingWheel = null;
            }
            if ((view.PendingHeightFt != null || view.PendingSpeedKt != null) && now - valueSince > 0.6)
            {
                // The wheel stopped: the height or the speed chosen is ordered.
                if (valueCallsign != null && valueCallsign == view.Selected)
                {
                    if (view.PendingHeightFt is double feet) commands.Height(valueCallsign, feet);
                    if (view.PendingSpeedKt is double knots) commands.Speed(valueCallsign, knots);
                }
                view.PendingHeightFt = view.PendingSpeedKt = null;
                valueCallsign = null;
            }
            if (++ticks % 10 == 0)
            {
                ShowSelectedValues();
                UpdatePlans();
                (double f, double sp, double g) = engine.Wind;
                settingsPart.SummaryText = airfield.Map == null ? "no map" : $"{airfield.Code} · {airfield.Runway?.Runway ?? "no runway"} · " + (sp < 0.5 ? "calm" : $"{f:000}/{sp:0}" + (g > 0 ? $"G{sp + g:0}" : ""));
                mapSummary.Text = airfield.Map == null ? "no map" : $"{airfield.Code} · {airfield.Runway?.Runway ?? "no runway"}";
                circuitsSummary.Text = $"LF {engine.LongFinalNM:0.#} · IP {engine.InitialPointNM:0.#} NM";
                gatesSummary.Text = $"{airfield.Gates.Count} gates · {airfield.Exits.Count} exits · {airfield.Helipads.Count} helipads";
                weatherSummary.Text = settings.Metar.Length > 0 ? MetarWind.WindAndQnh(settings.Metar) : "no METAR";
                int waitingCount = plans.Count(p => p.Now != TowerPlan.State.Flying && !p.Quick);
                trafficPart.SummaryText = $"{engine.Count} flying" + (waitingCount > 0 ? $" · {waitingCount} waiting" : "");
                bool allFloating = new[] { settingsPart, newPart, trafficPart, selectedPart, stripPart }.All(part => part.IsFloating);
                RefreshStrips();
                column.Visibility = allFloating ? Visibility.Collapsed : Visibility.Visible;
                selectedPart.SummaryText = view.Selected ?? "";
            }
        }

        /// <summary>The bar on the map at its place (top middle unless moved), always inside the map.</summary>
        private void PlaceBar()
        {
            if (mapBar == null || layer.ActualWidth <= 0) return;
            double width = mapBar.ActualWidth, height = mapBar.ActualHeight;
            double left = settings.MapBarLeft ?? (layer.ActualWidth - width) / 2;
            double top = settings.MapBarTop ?? 8;
            Canvas.SetLeft(mapBar, Math.Clamp(left, 0, Math.Max(0, layer.ActualWidth - width)));
            Canvas.SetTop(mapBar, Math.Clamp(top, 0, Math.Max(0, layer.ActualHeight - height)));
        }

        /// <summary>L1 / L2: the view kept on it for this aerodrome, else the default one (10 / 20 NM).</summary>
        private void GoLevel(int level)
        {
            SavedView? kept = level == 1 ? airfield.Circuits.View1 : airfield.Circuits.View2;
            if (kept != null && airfield.Map != null)
            {
                (double x, double y) = airfield.Plane.FromGeo(kept.Latitude, kept.Longitude);
                view.ShowView(x, y, kept.AcrossNM);
            }
            else view.ShowLevel(level);
            levelRow.Show(level);
        }

        /// <summary>L1 / L2 programmed (the user, 4 Nov): the view of now kept on it, or the default one again.</summary>
        private void KeepLevel(int level, bool keep)
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            SavedView? kept = null;
            if (keep)
            {
                (double x, double y, double across) = view.CurrentView;
                (double lat, double lon) = airfield.Plane.ToGeo(x, y);
                kept = new SavedView { Latitude = lat, Longitude = lon, AcrossNM = across };
            }
            if (level == 1) airfield.Circuits.View1 = kept; else airfield.Circuits.View2 = kept;
            string? error = airfield.Save();
            Say(error ?? (keep ? $"L{level}: this view ({view.CurrentView.AcrossNM:0.#} NM across) kept for {airfield.Code}." : $"L{level}: the default view again."));
            GoLevel(level);
        }

        private void OnKeys(object sender, KeyEventArgs e)
        {
            bool typing = Keyboard.FocusedElement is TextBox or ComboBox or ComboBoxItem or ListBoxItem or Slider or PasswordBox or System.Windows.Controls.Primitives.ToggleButton;
            // (The user, 8 Nov: F1-F6 are DCT TO of the selected aircraft; the levels: Ctrl+F1 / F2, or F1 / F2 with none selected.)
            if (e.Key is Key.F1 or Key.F2 && (Keyboard.Modifiers == ModifierKeys.Control || view.Selected == null))
            {
                GoLevel(e.Key == Key.F1 ? 1 : 2);
                e.Handled = true;
            }
            else if (e.Key == Key.F12) { ShowKeys(); e.Handled = true; }
            else if (!typing && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0 && OrderKey(e.Key == Key.System ? e.SystemKey : e.Key)) e.Handled = true;
            else if (e.Key == Key.Pause) { TogglePause(); e.Handled = true; }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { TakePhoto(); e.Handled = true; }
            // (The user, 7 Nov: Tab the next aircraft, Shift+Tab the one before; in a text box Tab stays the next box.)
            // (The user, 8 Nov: ← / → the side of the selected aircraft, as SIDE L / R of its label; with Shift: now, as SIDE NOW.)
            // (Only in its circuit, as the SIDE buttons; not when a list or a box of choices has the keys, the review of 8 Nov.)
            else if (e.Key is Key.Left or Key.Right && Keyboard.FocusedElement is not (TextBox or ComboBox or ComboBoxItem or ListBoxItem or Slider or PasswordBox)
                && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0
                && view.Selected is string chosen && engine.Get(chosen) is AircraftState plane && plane.Contact == GroundContact.Airborne
                && plane.Circuit != null && !plane.Transit)
            {
                CircuitSide side = e.Key == Key.Left ? CircuitSide.Left : CircuitSide.Right;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) { if (side != plane.CircuitSide) SideNow(chosen, side); }
                else LabelField(chosen, $"SIDE={side}");
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && Keyboard.FocusedElement is not TextBox && (Keyboard.Modifiers & ~ModifierKeys.Shift) == 0)
            {
                SelectNext((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                e.Handled = true;
            }
        }

        /// <summary>
        /// The keys of the orders (the user, 8 Nov): fixed, on the selected aircraft, as its buttons (only when they can be given).
        /// False: not a key of an order.
        /// </summary>
        private bool OrderKey(Key key)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (key == Key.Space && !shift) { TogglePause(); return true; }
            Button? button = key switch
            {
                Key.F1 => dctButtons[0], Key.F2 => dctButtons[1], Key.F3 => dctButtons[2], Key.F4 => dctButtons[3], Key.F5 => dctButtons[4], Key.F6 => dctButtons[5],
                Key.B => breakNowButton, Key.E => extendButton, Key.N => baseButton, Key.O => orbitButton, Key.G => goAroundButton,
                Key.W => wideButton, Key.H => holdButton, Key.S => startSfoButton, Key.U => shift ? breakupButton : splitButton,
                Key.X => expTaxiButton, Key.R => rejoinButton,
                _ => null
            };
            if (button != null)
            {
                if (view.Selected == null) return false;
                ShowSelectedValues();
                if (button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                else Say($"{KeyName(key, shift)}: not now for {view.Selected}.", Area.Selected);
                return true;
            }
            if (view.Selected is not string cs || engine.Get(cs) is not AircraftState a) return false;
            int pattern = key switch { Key.D1 or Key.NumPad1 => 1, Key.D2 or Key.NumPad2 => 2, Key.D3 or Key.NumPad3 => 3, Key.D4 or Key.NumPad4 => 4, Key.D5 or Key.NumPad5 => 5, _ => 0 };
            if (pattern > 0 && !shift)
            {
                CircuitType type = new[] { CircuitType.Conventional, CircuitType.Close, CircuitType.Wide, CircuitType.Overhead, CircuitType.Sfo }[pattern - 1];
                // (On the ground and taking off: the conventional or the wide, as its buttons.)
                if (a.Contact == GroundContact.Airborne || type is CircuitType.Conventional or CircuitType.Wide) LabelField(cs, $"CIRC={type}");
                else Say($"{TowerCommands.Circuits.First(c => c.Type == type).Short}: once it flies.", Area.Selected);
                return true;
            }
            EndAction? end = key switch { Key.F => EndAction.FullStop, Key.T => EndAction.TouchAndGo, Key.A => EndAction.GoAround, Key.P => EndAction.LowPass, Key.D => EndAction.Departure, _ => null };
            if (end is EndAction chosenEnd && !shift) { LabelField(cs, $"NEXT={chosenEnd}"); return true; }
            if (key == Key.Delete && !shift) { RemoveAsked(cs); return true; }
            TextBox? box = key switch { Key.L => heightBox, Key.K => speedKtBox, Key.V => headingBox, _ => null };
            if (box != null && !shift)
            {
                if (!box.IsVisible) return false;
                box.Focus();
                Keyboard.Focus(box);
                box.SelectAll();
                return true;
            }
            return false;
        }

        private static string KeyName(Key key, bool shift) => (shift ? "Shift+" : "") + key switch { Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5 or Key.F6 => key.ToString(), _ => key.ToString().ToUpperInvariant() };

        /// <summary>F12: the list of the keys (the user, 8 Nov).</summary>
        private void ShowKeys()
        {
            const string text =
                "On the selected aircraft (not while writing in a box):\n\n" +
                "NEXT CIRCUIT\n" +
                "  1 2 3 4 5   pattern CONV, CLS, WIDE, OVH, SFO\n" +
                "  ← →         side (of the circuit ordered, or now)\n" +
                "  F T A P D   end: F/S, T/G, L/A, L/P, DEP\n\n" +
                "DCT TO\n" +
                "  F1 F2       DW CONV, DW CLS\n" +
                "  F3 F4       BASE CONV, BASE CLS\n" +
                "  F5 F6       IP, HK\n\n" +
                "NOW\n" +
                "  Shift+← →   side now L, R\n" +
                "  B           break now\n" +
                "  E           extend downwind\n" +
                "  N           base now\n" +
                "  O           orbit (Shift+O: on and on; orbiting on and on: go on)\n" +
                "  G           go around\n" +
                "  W           wide now\n" +
                "  H / S       hold at the High Key / start SFO\n" +
                "  X           expedite taxi\n" +
                "  U / Shift+U split / breakup\n" +
                "  R           rejoin its circuit\n" +
                "  Del         remove (asked first)\n\n" +
                "VALUES\n" +
                "  L / K / V   to the altitude / speed / heading box (type + Enter, Esc: back)\n\n" +
                "OTHERS\n" +
                "  Tab / Shift+Tab   next / previous aircraft\n" +
                "  Space       run / pause\n" +
                "  Ctrl+F1 / F2   level 1 / 2 of the map (F1 / F2 with no aircraft selected)\n" +
                "  Ctrl+S      photo of the map\n" +
                "  F12         this list";
            if (keysWindow is { IsVisible: true })
            {
                keysWindow.Activate();
                return;
            }
            Window keys = keysWindow = new()
            {
                Title = "TWR Trainer - keys",
                Owner = this,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Theme.Panel,
                Content = new TextBlock { Text = text, FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = TextBrush, Margin = new Thickness(16) }
            };
            keys.KeyDown += (_, k) => { if (k.Key is Key.Escape or Key.F12) keys.Close(); };
            keys.Show();
        }

        /// <summary>The next (or the one before) aircraft selected, in the order of FLYING; none selected: the first one.</summary>
        private void SelectNext(int step)
        {
            List<string> order = flyingOrder.Where(cs => engine.Get(cs) != null).ToList();
            if (order.Count == 0) return;
            int at = view.Selected is string now ? order.IndexOf(now) : -1;
            int next = at < 0 ? (step > 0 ? 0 : order.Count - 1) : ((at + step) % order.Count + order.Count) % order.Count;
            view.Select(order[next]);
        }

        // ----- The panel -------------------------------------------------------------------------------------------------

        private void BuildPanel()
        {
            settings.Parts ??= [];
            PartPlacement Placement(string name)
            {
                if (!settings.Parts.TryGetValue(name, out PartPlacement? found)) settings.Parts[name] = found = new PartPlacement();
                return found;
            }
            // (The user, 4 Nov: Aerodrome and Simulation one part, Settings, where Aerodrome was.)
            if (!settings.Parts.ContainsKey("Settings") && settings.Parts.TryGetValue("Aerodrome", out PartPlacement? old)) settings.Parts["Settings"] = old;
            settingsPart = new DockablePart("Settings", settingsContent, panel, layer, Placement("Settings"), settings.Save);
            // (The user, 7 Nov: the names of AuroraPAR Trainer; the places saved under the old names kept.)
            newPart = new DockablePart("Traffic generation", newContent, panel, layer, Placement("New aircraft"), settings.Save);
            // (The user, 3 Nov: the flight plans as a part of their own, named as in the PAR trainer.)
            trafficPart = new DockablePart("Traffic List", trafficContent, panel, layer, Placement("Traffic List"), settings.Save);
            selectedPart = new DockablePart("Aircraft control", selectedContent, panel, layer, Placement("Selected aircraft"), settings.Save);
            // (Floating by default, narrow, the user 6 Nov.)
            if (!settings.Parts.ContainsKey("Strip bay")) settings.Parts["Strip bay"] = new PartPlacement { Floating = true, Width = 260, Height = 420, Left = 380, Top = 60 };
            stripBay.ColourOf = StripBrush;
            stripPart = new DockablePart("Strip bay", stripBay, panel, layer, Placement("Strip bay"), settings.Save);

            // (The user, 6 Nov: the look of the panels and of the strips, at the top of Settings.)
            current = Section("Appearance", null, false);
            Row("Panels", panelsRow);
            panelsRow.Show(Theme.Light);
            panelsRow.Picked += light =>
            {
                settings.LightPanels = light;
                settings.Save();
                Say(light == Theme.Light ? "Panels as they are now." : $"Panels {(light ? "light" : "dark")} from the next start of TWR Trainer.");
            };
            Note("Strips (Strip bay): the colour of each kind of flight.");
            StripColourRow("Departures", () => settings.StripDeparture, v => settings.StripDeparture = v);
            StripColourRow("Arrivals", () => settings.StripArrival, v => settings.StripArrival = v);
            StripColourRow("Local / transit", () => settings.StripLocal, v => settings.StripLocal = v);

            current = Section("Map", mapSummary, true);
            newRunwayName.ToolTip = "The name of the runway (for example 14).";
            newRunwayElevation.ToolTip = "The elevation of its threshold (ft): the altitudes (QNH) of the labels and of the classes are reckoned with it.";
            metarBox.ToolTip = "A METAR line (or Get METAR): its wind is used and it is shown on the map (for example LIPI 031250Z 06012KT CAVOK 15/08 Q1018).";
            newGateName.ToolTip = "The name of the new gate (empty: the name of the VRP clicked).";
            longFinalBox.ToolTip = "Distance of the long final (NM): where LONG FIN starts.";
            initialPointBox.ToolTip = "Distance of the initial point before the threshold (NM).";
            callsignBox.ToolTip = "Empty: the next free callsign of the aerodrome's prefix (Settings › Aircraft).";
            delayBox.ToolTip = "For the Traffic List: minutes after START ALL.";
            current.Children.Add(mapText);
            Buttons(("Open map...", ChooseMap));
            Row("Runway in use", runwayHolder);
            Note("New runway (or its place again): its name and elevation, then click its threshold and its end on the map.");
            Row("Name / elevation ft", Inline(newRunwayName, newRunwayElevation));
            Buttons(("Place on the map", PlaceRunway), ("Remove runway", RemoveRunway));
            // (The user, 6 Nov: the thresholds of runways.par of AuroraPAR, no clicks.)
            Buttons(("From runways.par...", RunwaysFromPar));
            Row("Range rings NM", ringsRow);
            ringsRow.Picked += nm =>
            {
                if (airfield.Map == null) { Say("Open a map first: the range rings are kept with its aerodrome."); ShowRings(); return; }
                airfield.Circuits.RingsNM = nm == 5 ? null : nm;
                Say(airfield.Save());
                if (!settings.ShowRings) { settings.ShowRings = true; settings.Save(); }
                ShowRings();
            };
            // (The user, 6 Nov: altitudes (QNH) or heights above the runway (QFE), for this aerodrome.)
            Row("Pressure", pressureRow);
            pressureRow.Show(false);
            pressureRow.Picked += qfe =>
            {
                // (The altitudes written in Settings › Circuits and not saved yet: saved first, in the old reference.)
                heightsTable.CommitAll();
                // (What is typed already, in the new reference: the same height.)
                double before = HeightOffset;
                airfield.Circuits.Qfe = qfe;
                foreach (TextBox box in new[] { arrivalBox, hkBox, exitAltBox })
                    if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed))
                        box.Text = Math.Round(typed - before + HeightOffset).ToString(CultureInfo.InvariantCulture);
                if (airfield.Map != null) Say(airfield.Save());
                ShowPressure();
            };

            current = Section("Circuits", circuitsSummary, false);
            Note("The side of each circuit at this runway:");
            Row("Conventional, wide", conventionalSideRow);
            Row("Jet (overhead, close)", jetSideRow);
            Row("SFO", sfoSideRow);
            foreach (ChoiceRow<CircuitSide> row in new[] { conventionalSideRow, jetSideRow, sfoSideRow }) row.Picked += _ => SaveSides();
            Row("Long final / IP NM", Inline(longFinalBox, initialPointBox));
            longFinalBox.LostFocus += (_, _) => ApplyDistances();
            initialPointBox.LostFocus += (_, _) => ApplyDistances();
            variationBox.ToolTip = "The magnetic variation of this aerodrome (for example 3E or 2W; empty: 0): the radials of the departures are magnetic.";
            Row("Variation", Inline(variationBox, new TextBlock { Text = "(E / W, for the radials)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }));
            variationBox.LostFocus += (_, _) => ApplyVariation();
            // (The user, 7 Nov: the heights of the classes here, out of the Aircraft classes window; altitudes QNH first.)
            Note("The altitudes of each class here (QNH; QFE: heights above the runway). White: of this aerodrome; grey: the class's (an empty box). Saved at once.");
            current.Children.Add(heightsTable);
            heightsTable.Say = text => Say(text);
            heightsTable.Save = () =>
            {
                string? error = airfield.Save();
                engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
                return error;
            };
            Buttons(("Import heights...", ImportHeights));

            // (The user, 6 Nov night: the aircraft in a section of their own, out of Circuits.)
            current = Section("Aircraft", null, false);
            // (The user, 7 Nov: the base traffic of this aerodrome, kept in its file.)
            Note("The base traffic of this aerodrome: Quick, and the aircraft added without a callsign or an ICAO type.");
            prefixBox.ToolTip = "The start of the callsigns given by themselves (1 to 6 letters or digits; empty: SIM): IBLA → IBLA1, IBLA2... (a number not flying).";
            defaultTypeBox.ToolTip = "The ICAO type of the aircraft added without one (C172, F16...; free text, on the strips).";
            Row("Callsign prefix", Inline(prefixBox, new TextBlock { Text = "(empty: SIM)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }));
            Row("Class", defaultClassHolder);
            Row("ICAO type", defaultTypeBox);
            prefixBox.LostFocus += (_, _) => ApplyBaseTraffic();
            defaultTypeBox.LostFocus += (_, _) => ApplyBaseTraffic();
            prefixBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyBaseTraffic(); };
            defaultTypeBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyBaseTraffic(); };
            // (The user, 6 Nov: all the classes in one window, their performance; their altitudes here: Circuits.)
            Note("The classes of aircraft: their performance (all aerodromes). Their altitudes here: Settings › Circuits.");
            Buttons(("Aircraft classes...", OpenClasses));
            Note("Formation landing to a full stop: separation of the next aircraft.");
            Row("Formation separation", separationRow);
            separationRow.Picked += full =>
            {
                airfield.Circuits.FullRunwaySeparation = full;
                string? error = airfield.Save();
                engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
                Say(error ?? (full ? "Full rwy separation: in a formation landing to a full stop, the next one lands once the one ahead has left the runway."
                    : "Reduced rwy separation: in a formation landing to a full stop, the next one lands when the one ahead has rolled a third of the runway."));
            };

            // (The user, 7 Nov: the instructor's VID for the log; found in IVAO's list when connected, else this one.)
            current = Section("Network", null, false);
            Note("IVAO through Aurora on this PC (IVAO on the bar of the map). Your VID goes in the log of the flights: found by itself from your callsign when connected, else the one written here.");
            vidBox.ToolTip = "Your IVAO VID (a number): used when IVAO's list of who is connected does not give it.";
            Row("My VID", vidBox);
            vidBox.LostFocus += (_, _) => ApplyVid();
            vidBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyVid(); };

            current = Section("Weather", weatherSummary, false);
            Row("METAR", metarBox);
            Buttons(("Get METAR", GetMetar), ("Apply METAR", () => ApplyMetar(quiet: false)));
            Row("On the map", metarShowRow);
            metarShowRow.Picked += only =>
            {
                settings.MetarWindQnhOnly = only;
                ShowMetar();
            };
            current.Children.Add(windText);

            current = Section("Gates and RWY exits", gatesSummary, false);
            Note("Entry gates (where the arriving aircraft start, at least 5 NM out): a name (empty: the VRP clicked), then click it on the map.");
            Row("Gate name", newGateName);
            current.Children.Add(ActionRow(("ADD GATE", AddGate), ("REMOVE GATE", RemoveGate)));
            Note("Runway exits (where the landing aircraft leave the runway: each one the first it reaches slow enough): a name, then click on the runway edge.");
            Row("Exit name", newExitName);
            current.Children.Add(ActionRow(("ADD EXIT", AddExit), ("REMOVE EXIT", RemoveExit)));
            // (The user, 5 Nov: helipads registered at the aerodrome; any other point clicked too.)
            newPadName.ToolTip = "The name of the new helipad (empty: H1, H2...).";
            Row("Helipad name", newPadName);
            current.Children.Add(ActionRow(("ADD HELIPAD", AddHelipad), ("REMOVE HELIPAD", RemoveHelipad)));

            current = Section("Map layers", null, false);
            current.Children.Add(layers);
            // The bar on the map: Run / Pause, the speed, the level.
            pauseButton.Click += (_, _) => TogglePause();
            pauseButton.Padding = new Thickness(10, 2, 10, 2);
            speedRow.Show(1.0);
            speedRow.Picked += v => speed = v;
            levelRow.Show(1);
            levelRow.Picked += GoLevel;
            foreach (int level in new[] { 1, 2 })
            {
                if (levelRow.ButtonOf(level) is not Button button) continue;
                int n = level;
                ContextMenu menu = new();
                menu.Items.Add(Item("Keep this view", () => KeepLevel(n, true)));
                menu.Items.Add(Item($"Default ({(n == 1 ? 10 : 20)} NM)", () => KeepLevel(n, false)));
                button.ContextMenu = menu;
                button.ToolTip = (n == 1 ? "Level 1 (F1)" : "Level 2 (F2)") + ". Right click: keep the view of now on it (for this aerodrome), or the default one.";
            }

            // Traffic generation (the user, 7 Nov: named as in AuroraPAR Trainer, its tabs Quick | Advanced).
            newContent.Children.Add(tabRow);
            newContent.Children.Add(quickContent);
            newContent.Children.Add(advancedContent);
            tabRow.Picked += quick =>
            {
                settings.QuickTab = quick;
                settings.Save();
                ShowTab();
            };
            ShowTab();
            BuildQuick();
            current = advancedContent;
            Note("ARRIVAL: from a gate or the long final, it lands. LOCAL: from the runway, its circuits, it lands. DEPARTURE: from the runway, away (at once or after its circuits). TRANSIT: from gate to gate. Then ADD NOW, or TO LIST for the Traffic List. AD: the aerodrome's side (Settings › Circuits).");
            // (The user, 5 Nov: the class, the formation, then the type.)
            // (The user, 5 Nov: clearer names, a wider column for them.)
            Row("Type of AC", classHolder, null, LabelWidth);
            Row("ICAO type", Inline(icaoTypeBox, new TextBlock { Text = "(free text: F16, TOR, C172...)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth);
            Row("Callsign", Inline(callsignBox, new TextBlock { Text = "(optional; a formation: ending with its number, VIPER01)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth);
            Row("Rules", rulesRow, null, LabelWidth);
            rulesRow.Show(false);
            Row("Formation", formationRow, null, LabelWidth);
            intervalLine = Row("Break interval", splitRow, null, LabelWidth);
            Row("Type of flight", typeRow, null, LabelWidth);
            Row("Start from", fromHolder, null, LabelWidth);
            // (The user, 6 Nov: a point as radial / distance instead of a gate.)
            // (The user, 7 Nov: POINT, clicked on the map, moved by dragging its mark; kept as radial / distance as before.)
            fromPointLine = Row("Start point", PointPicker(fromRadialBox, fromDistanceBox, "START"), null, LabelWidth);
            // (The user, 6 Nov: one row for the ways out of a departure or a transit: RWY HDG, RADIAL, a gate, an R/D point.)
            toLine = Row("Exit to", toHolder, null, LabelWidth);
            toName = (TextBlock)toLine.Children[0];
            toPointLine = Row("Exit point", PointPicker(toRadialBox, toDistanceBox, "EXIT"), null, LabelWidth);
            departLine = Row("Exit via", departRow, null, LabelWidth);
            departLine.Visibility = Visibility.Collapsed;
            RadialTip(radialBox);
            radialLine = Row("Radial °M", Wrap(radialBox,
                ActionButton("FROM A POINT", () => view.AskPoint("Radial: click the point it starts from  -  Esc: cancel", (x, y) =>
                {
                    radialFrom = (x, y);
                    radialFromText.Text = "from the point clicked";
                })),
                ActionButton("MIDDLE", () => { radialFrom = null; radialFromText.Text = "from the middle of the runway"; }),
                radialFromText), null, LabelWidth);
            // (The user, 6 Nov: the turn to the way out, L / R by itself; the exit altitude apart from the entry height.)
            exitTurnLine = Row("Exit turn", departTurnRow, null, LabelWidth);
            exitAltLine = Row("Exit altitude ft", Inline(exitAltBox, new TextBlock { Text = "(empty: 3000 ft QNH)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth);
            exitAltName = (TextBlock)exitAltLine.Children[0];
            exitAltBox.ToolTip = "The altitude it climbs to when it leaves (empty: 3000 ft QNH, at least 1000 ft above the runway).";
            viaLine = Row("Via", viaRow, null, LabelWidth);
            typeRow.Show(Flight.Arrival);
            viaRow.Show(Via.Overhead);
            typeRow.Picked += type =>
            {
                // (The user, 5 Nov: a departure away at once by default; a helicopter from the runway unless a pad is chosen.)
                if (type == Flight.Departure)
                {
                    lapsRow.Show(0);
                    padRow.Show(PadRunwayName);
                }
                // (Back to one that lands: it lands at the first final unless asked.)
                if (type is Flight.Arrival or Flight.Local) onFinalRow.Show(EndAction.FullStop);
                ShowEntries();
            };
            viaRow.Picked += _ => ShowEntries();
            // (The user, 5 Nov: the pattern type first, then its entry, then its side.)
            patternLine = Row("Pattern type", circuitRow, null, LabelWidth);
            entryLine = Row("Pattern entry", entryRow, null, LabelWidth);
            sideLine = Row("Pattern side", sideRow, null, LabelWidth);
            hkLine = Row("HK altitude ft", Inline(hkBox, new TextBlock { Text = "(empty: Settings › Circuits)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth);
            hkName = (TextBlock)hkLine.Children[0];
            hkBox.ToolTip = "The High Key of this aircraft's SFO (empty: the one of Settings › Circuits).";
            // (The user, 5 Nov: what each final does, how many circuits; the end is the type's.)
            onFinalLine = Row("On Final", onFinalRow, null, LabelWidth);
            padLine = Row("Helipad", padHolder, null, LabelWidth);
            padName = (TextBlock)padLine.Children[0];
            onFinalRow.Show(EndAction.FullStop);
            onFinalRow.Picked += _ => ShowEntries();
            circuitsLine = Row("Numb. of Circuits", lapsRow, null, LabelWidth);
            departRow.Show(false);
            departTurnRow.Show(true);
            departRow.Picked += _ => ShowEntries();
            // (The turn by itself as the way out is written; L / R clicked: the instructor's, kept until the way out changes.)
            foreach (TextBox box in new[] { radialBox, toRadialBox, toDistanceBox })
                box.TextChanged += (_, _) => WayOutChanged();
            departTurnRow.Picked += _ => turnChosen = true;
            formationRow.Show(1);
            splitRow.Show(3.0);
            lapsRow.Show(1);
            formationRow.Picked += n => intervalLine.Visibility = n > 1 ? Visibility.Visible : Visibility.Collapsed;
            intervalLine.Visibility = Visibility.Collapsed;
            lapsRow.Picked += _ => ShowEntries();
            // (The user, 3 Nov: IP is the overhead's, HK the SFO's; after a take-off only the conventional or the wide.)
            entryRow.Picked += entry =>
            {
                if (entry == CircuitEntry.InitialPoint) circuitRow.Show(CircuitType.Overhead);
                else if (entry == CircuitEntry.HighKey) circuitRow.Show(CircuitType.Sfo);
                else if (entry == CircuitEntry.Circling) circuitRow.Show(CircuitType.Conventional);
                else if (circuitRow.Value is CircuitType.Overhead or CircuitType.Sfo) circuitRow.Show(CircuitType.Conventional);
                // (From the long final: the pattern rows follow FINAL / CIRCLING, the user 7 Nov.)
                ShowEntries();
            };
            circuitRow.Picked += type =>
            {
                if (fromRow.Value == From.Runway && type is not (CircuitType.Conventional or CircuitType.Wide))
                {
                    Say("After a take-off: the conventional or the wide circuit (or DEP at the end).", Area.New);
                    circuitRow.Show(CircuitType.Conventional);
                    return;
                }
                if (fromRow.Value == From.LongFinal && type is CircuitType.Overhead or CircuitType.Sfo)
                {
                    Say("From the long final: the conventional, close or wide circuit (or CIRCLING).", Area.New);
                    circuitRow.Show(CircuitType.Conventional);
                    return;
                }
                if (entryRow.Value == CircuitEntry.Circling && type != CircuitType.Conventional) entryRow.Show(CircuitEntry.Final);
                if (type == CircuitType.Overhead) entryRow.Show(CircuitEntry.InitialPoint);
                else if (type == CircuitType.Sfo) entryRow.Show(CircuitEntry.HighKey);
                else if (entryRow.Value is CircuitEntry.InitialPoint or CircuitEntry.HighKey) entryRow.Show(CircuitEntry.Downwind);
                // (The High Key row with the SFO only.)
                ShowEntries();
            };
            entryRow.Show(CircuitEntry.Downwind);
            circuitRow.Show(CircuitType.Conventional);
            sideRow.Show(null);
            RebuildFrom();
            // (The user, 7 Nov: the rows of "More…" always in sight, at the bottom; the callsign under the ICAO type.)
            arrivalName = (TextBlock)Row("Height ft", Inline(arrivalBox, new TextBlock { Text = "(at the gate / of the crossing; empty: 500 above the circuit)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth).Children[0];
            arrivalBox.ToolTip = "The height it arrives at, at the gate (empty: 500 ft above its circuit); it reaches the circuit height before the circuit. A crossing: its height. (A departure: Exit altitude.)";
            Row("Delay min", Inline(delayBox, new TextBlock { Text = "(for the Traffic List: after START ALL)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, LabelWidth);
            toListButton = ActionButton("TO LIST", AddPlan);
            cancelEditButton = ActionButton("CANCEL", CancelEdit);
            cancelEditButton.Visibility = Visibility.Collapsed;
            WrapPanel newButtons = Wrap(ActionButton("ADD NOW", AddAircraft), toListButton, cancelEditButton);
            newButtons.Margin = new Thickness(0, 2, 0, 0);
            current.Children.Add(newButtons);
            // (The user, 7 Nov: the scenarios here, as in AuroraPAR Trainer; files, not the simulation: plain buttons.)
            WrapPanel scenario = new() { Margin = new Thickness(0, 8, 0, 0) };
            scenario.Children.Add(new TextBlock { Text = "Scenario", Foreground = DimBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) });
            current.Children.Add(scenario);
            Buttons(scenario, ("LOAD...", LoadScenario), ("SAVE...", SaveScenario));

            current = trafficContent;
            // (As in the PAR trainer, the user 4 Nov: the aircraft flying, one thin row each, and the plans waiting.)
            // (The user, 6 Nov: the buttons with the list they act on.)
            trafficPauseButton.Click += (_, _) => TogglePause();
            trafficPauseButton.Margin = new Thickness(0, 0, 4, 0);
            // (The user, 8 Nov: the names of AuroraPAR Trainer: every aircraft back to the WAITING list; REMOVE ALL empties the list.)
            Button removeAll = ActionButton("▼ WAITING LIST", RemoveAllAircraft);
            removeAll.Margin = new Thickness(0);
            current.Children.Add(Header(Caption("FLYING"), trafficPauseButton, removeAll));
            current.Children.Add(Legend("click: select · II freeze · ↺ its plan again · ✕ remove"));
            current.Children.Add(flyingList);
            waitingButton = ActionButton("", ToggleWaiting);
            waitingButton.Margin = new Thickness(0);
            ShowWaitingButton();
            current.Children.Add(Header(Caption("WAITING"), waitingButton));
            current.Children.Add(Legend("☐ GROUP · ▶ start · ⏸ hold · ⧉ duplicate · ✕ remove"));
            current.Children.Add(planList);
            Button clearAll = ActionButton("REMOVE ALL", ClearPlans);
            clearAll.Margin = new Thickness(0, 0, 0, 4);
            DockPanel waitingButtons = new() { Margin = new Thickness(0, 6, 0, 0), LastChildFill = false };
            DockPanel.SetDock(clearAll, Dock.Right);
            waitingButtons.Children.Add(clearAll);
            waitingButtons.Children.Add(ActionButton("START ALL", StartAllPlans));
            waitingButtons.Children.Add(groupButton = ActionButton("GROUP", GroupOrUngroup));
            current.Children.Add(waitingButtons);

            current = selectedContent;
            current.Children.Add(selectedText);
            current.Children.Add(selectedPanel);

            // (The user, 8 Nov: as the label, in its order and with its names; by phases; the keys beside the buttons.)
            Button Order(string text, Action<string> action) => ActionButton(text, () => WithSelected(action));
            Button Keyed(string text, string key, Action<string> action, bool now = false)
            {
                Button button = Order(text, action);
                string tip = button.ToolTip as string ?? text;
                if (key.Length > 0)
                {
                    StackPanel content = new() { Orientation = Orientation.Horizontal };
                    content.Children.Add(new TextBlock { Text = text });
                    content.Children.Add(new TextBlock { Text = key, FontSize = 9, Opacity = 0.65, Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                    button.Content = content;
                    tip += $" Key: {key}.";
                }
                button.ToolTip = tip;
                if (now) NowLook(button);
                return button;
            }

            StackPanel values = Group("1 · Altitude, speed and heading",
                "A box: the value now; the mouse wheel, or type + Enter (Esc: back). ALT, SPD: it keeps its circuit (AUTO: its circuit's). HDG: out of its circuit, you fly it; REJOIN: back in.", selectedPanel);
            foreach (TextBox box in new[] { heightBox, speedKtBox, headingBox })
            {
                box.Width = 40;
                box.TextAlignment = TextAlignment.Center;
                box.Margin = new Thickness(0, 0, 6, 4);
            }
            heightBox.ToolTip = "The altitude (ALT, hundreds of ft: 25 = 2500; or the feet) or the height (HGT): Enter. Mouse wheel: ±100 ft. Key: L.";
            speedKtBox.ToolTip = "The speed (kt): Enter. Mouse wheel: ±10 kt. Key: K.";
            headingBox.ToolTip = "A heading (°, true): Enter; it leaves its circuit (MANUAL). Mouse wheel: ±5°. Or drag from its symbol on the map. Key: V.";
            Grid altLine = Row("ALT", altPresets, values, 40);
            heightName = (TextBlock)altLine.Children[0];
            Row("SPD", speedPresets, values, 40);
            altPresets.Children.Add(heightBox);
            speedPresets.Children.Add(speedKtBox);
            rejoinButton = Keyed("REJOIN", "R", commands.Rejoin);
            rejoinButton.ToolTip = "Back into its circuit (from MANUAL, a heading). Key: R.";
            Row("HDG", Wrap(headingBox, rejoinButton, new TextBlock { Text = "or drag from its symbol", Foreground = DimBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }), values, 40);
            ValueBox(heightBox, "ALT");
            ValueBox(speedKtBox, "SPD");
            ValueBox(headingBox, "HDG");

            StackPanel next = Group("2 · Next circuit", "PATTERN, BREAK: from the next touch and go or go-around. SIDE: now (or of the circuit ordered). END: the end of this final.", selectedPanel);
            Row("PATTERN", selCircuitRow, next, 62);
            // Another circuit: its side at this runway (then L / R to change it). Both from the next circuit.
            selCircuitRow.Picked += type => WithSelected(cs =>
            {
                CircuitSide side = engine.SideFor(type);
                selSideRow.Show(side);
                commands.NextCircuit(cs, type, side);
            });
            Row("SIDE", selSideRow, next, 62);
            // (As SIDE of the label, the user 8 Nov: now, or the side of the circuit ordered.)
            selSideRow.Picked += side => WithSelected(cs => LabelField(cs, $"SIDE={side}"));
            // (Only for the overhead and the close pattern, the user 5 Nov: the others have no break.)
            selBreakGrid = Row("BREAK", selBreakRow, next, 62);
            selBreakRow.Picked += where => WithSelected(cs => commands.SetBreak(cs, where));
            selBreakPointGrid = Row("", Inline(ActionButton("BREAK AT A POINT", () => WithSelected(cs =>
                view.AskPoint($"{cs}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(cs, x, y))))), next, 62);
            Row("END", selEndRow, next, 62);
            selEndRow.Picked += end => WithSelected(cs =>
            {
                if (engine.Get(cs) is { CanHover: true } heli && (heli.HasPad || heli.WantsPad)) commands.Helipad(cs, double.NaN, double.NaN);
                commands.SetEnd(cs, end);
            });
            // (Helicopters, the user 5 Nov: the end of the final on a helipad.)
            padEndButton = ActionButton("HPAD", () => WithSelected(cs => PadMenu(cs, now: false)));
            padEndButton.ToolTip = "Helicopter: the final ends on a helipad (a registered one, or a point clicked on the map).";
            padEndLine = Row("", Inline(padEndButton), next, 62);
            // (A formation: who lands, as LAND of the label.)
            landLine = Row("LAND", landPanel, next, 62);
            landLine.Visibility = Visibility.Collapsed;

            StackPanel now = Group("3 · Now, in the circuit", "At once. Blue: where it goes; orange: what it does now (grey: not where it is).", selectedPanel);
            // (DCT TO, the user 5 Nov: at once, in flight; DW CLS and BASE CLS, the user 8 Nov.)
            dctButtons = [Keyed("DW CONV", "F1", cs => commands.DirectTo(cs, DirectPoint.Downwind)), Keyed("DW CLS", "F2", cs => commands.DirectTo(cs, DirectPoint.CloseDownwind)),
                Keyed("BASE CONV", "F3", cs => commands.DirectTo(cs, DirectPoint.Base)), Keyed("BASE CLS", "F4", cs => commands.DirectTo(cs, DirectPoint.CloseBase)),
                Keyed("IP", "F5", cs => commands.DirectTo(cs, DirectPoint.InitialPoint)), Keyed("HK", "F6", cs => commands.DirectTo(cs, DirectPoint.HighKey))];
            dctButtons[0].ToolTip = "At once onto the downwind of the conventional circuit. Key: F1.";
            dctButtons[1].ToolTip = "At once onto the downwind of the close pattern (narrower). Key: F2.";
            dctButtons[2].ToolTip = "At once straight to the start of the base of the conventional circuit, then the final. Key: F3.";
            dctButtons[3].ToolTip = "At once straight to the start of the base of the close pattern (narrower), then the final. Key: F4.";
            dctButtons[4].ToolTip = "At once straight to the initial point, then the overhead. Key: F5.";
            dctButtons[5].ToolTip = "At once straight to the High Key, then the SFO. Key: F6.";
            padNowButton = Order("HPAD", cs => PadMenu(cs, now: true));
            padNowButton.ToolTip = "Helicopter: at once straight to a helipad (a registered one, or a point clicked on the map), a full stop there.";
            dctButtons = [.. dctButtons, padNowButton];
            Row("DCT TO", Wrap(dctButtons), now, 62);
            extendButton = Keyed("EXT DWN", "E", commands.ExtendDownwind, now: true);
            baseButton = Keyed("BASE", "N", commands.TurnBase, now: true);
            // ORB (as the label): one orbit; Shift: on and on; orbiting on and on, a click goes on along its circuit.
            orbitButton = Keyed("ORB", "O", cs =>
            {
                if (engine.Get(cs)?.OrbitForever == true) commands.Continue(cs);
                else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) commands.OrbitForever(cs);
                else commands.Orbit(cs);
            }, now: true);
            goAroundButton = Keyed("G/A", "G", commands.GoAround, now: true);
            breakNowButton = Keyed("BREAK NOW", "B", commands.BreakNow, now: true);
            Row("NOW", Wrap(extendButton, baseButton, orbitButton, goAroundButton, breakNowButton), now, 62);
            sideLeftButton = Keyed("SIDE NOW L", "⇧←", cs => SideNow(cs, CircuitSide.Left), now: true);
            sideRightButton = Keyed("SIDE NOW R", "⇧→", cs => SideNow(cs, CircuitSide.Right), now: true);
            // (The user, 8 Nov: the wide circuit on its own side at this runway, not the side it flies now.)
            wideButton = Keyed("WIDE NOW", "W", cs => commands.FlyCircuit(cs, CircuitType.Wide, engine.SideFor(CircuitType.Wide)), now: true);
            Row("", Wrap(sideLeftButton, sideRightButton, wideButton), now, 62);
            holdButton = Keyed("HOLD HK", "H", cs => commands.HoldAtHighKey(cs, true), now: true);
            startSfoButton = Keyed("START SFO", "S", cs => commands.HoldAtHighKey(cs, false), now: true);
            expTaxiButton = Keyed("EXP TAXI", "X", commands.ExpediteTaxi, now: true);
            Row("", Wrap(holdButton, startSfoButton, expTaxiButton), now, 62);
            splitButton = Keyed("SPLIT", "U", commands.Split, now: true);
            breakupButton = Keyed("BREAKUP", "⇧U", commands.Breakup, now: true);
            Row("FORMATION", Inline(splitButton, breakupButton), now, 62);

            Button removeButton = Keyed("REMOVE", "Del", cs => RemoveAsked(cs));
            removeButton.Background = removeButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x26, 0x26));
            DockPanel bottom = new() { Margin = new Thickness(0, 4, 0, 0), LastChildFill = false };
            TextBlock keysNote = new() { Text = "F12: all the keys · Right click on an aircraft: its menu", Foreground = DimBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(removeButton, Dock.Right);
            bottom.Children.Add(removeButton);
            bottom.Children.Add(keysNote);
            selectedPanel.Children.Add(bottom);
            current = panel;
            // Each part its message line, at its bottom; the folders in the Aerodrome.
            settingsContent.Children.Add(messages[Area.Aerodrome]);
            settingsContent.Children.Add(messages[Area.Simulation]);
            Note($"Maps: {Paths.MapsFolderText} (made with ATCRoom Map Creator). Aerodromes and settings: {Paths.FolderText}.", settingsContent);
            newContent.Children.Add(messages[Area.New]);
            trafficContent.Children.Add(messages[Area.Traffic]);
            selectedContent.Children.Add(messages[Area.Selected]);
            ShowSelected();
        }

        private static ChoiceRow<CircuitSide> SideRow(string what) => new([(CircuitSide.Left, "LEFT"), (CircuitSide.Right, "RIGHT")], (side, _) => $"The {what} on the {side.ToString().ToLowerInvariant()} at this runway.");

        private static TextBlock SectionSummary() => new() { Foreground = DimBrush, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        /// <summary>A section of Settings: its title (and the summary shown beside it), open or closed; its content returned.</summary>
        private StackPanel Section(string title, TextBlock? summary, bool open)
        {
            StackPanel inside = new() { Margin = new Thickness(4, 2, 0, 6) };
            StackPanel header = new() { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
            if (summary != null) header.Children.Add(summary);
            settingsContent.Children.Add(new Expander { Header = header, Content = inside, IsExpanded = open, Foreground = TextBrush, Margin = new Thickness(0, 4, 0, 0) });
            return inside;
        }

        /// <summary>The sides of the circuits of the runway in use, saved in the aerodrome file (the runway's own side: the conventional's).</summary>
        private void SaveSides()
        {
            if (updating || airfield.Runway == null) return;
            CircuitSide conventional = conventionalSideRow.Value;
            airfield.Runway.Side = conventional;
            airfield.Runway.ConventionalSide = conventional;
            airfield.Runway.JetSide = jetSideRow.Value;
            airfield.Runway.SfoSide = sfoSideRow.Value;
            Say(airfield.Save());
            engine.SetAerodrome(airfield.Circuits, airfield.Runway.Runway);
        }

        private static ComboBox Combo<T>(IEnumerable<(T Value, string Text)> items)
        {
            ComboBox box = new();
            foreach ((T value, string text) in items) box.Items.Add(new ComboBoxItem { Content = text, Tag = value });
            return box;
        }

        private static T TagOf<T>(ComboBox box) => box.SelectedItem is ComboBoxItem { Tag: T value } ? value : default!;

        private static void SelectTag<T>(ComboBox box, T value)
        {
            foreach (ComboBoxItem item in box.Items)
            {
                if (Equals(item.Tag, value)) { box.SelectedItem = item; return; }
            }
        }

        private void Header(string text) => (current ?? panel).Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 4),
            Foreground = TextBrush
        });

        private TextBlock Note(string text, Panel? into = null)
        {
            TextBlock note = new()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = DimBrush,
                FontSize = 11,
                Margin = new Thickness(0, 6, 0, 2)
            };
            (into ?? current ?? panel).Children.Add(note);
            return note;
        }

        /// <summary>A group of the panel: a frame with its title and what it is for; its rows go in the panel returned.</summary>
        private static StackPanel Group(string title, string note, Panel into)
        {
            StackPanel inside = new();
            inside.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            inside.Children.Add(new TextBlock { Text = note, FontSize = 11, Foreground = DimBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            into.Children.Add(new Border
            {
                Child = inside,
                BorderBrush = Theme.Section,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 6, 0, 2)
            });
            return inside;
        }

        private static WrapPanel Wrap(params UIElement[] items)
        {
            WrapPanel row = new();
            foreach (UIElement item in items) row.Children.Add(item);
            return row;
        }

        private static WrapPanel ActionRow(params (string Text, Action Action)[] buttons)
        {
            WrapPanel row = new() { Margin = new Thickness(0, 2, 0, 0) };
            foreach ((string text, Action action) in buttons) row.Children.Add(ActionButton(text, action));
            return row;
        }

        /// <summary>The number in a box (invariant or local decimal point), else a message.</summary>
        private void Number(TextBox box, string what, Action<double> use)
        {
            string text = box.Text.Trim().Replace(',', '.');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) { Say(null, Area.Selected); use(value); }
            else Say($"Write {what}.", Area.Selected);
        }

        /// <summary>The side changed now (an S over the aerodrome, the same circuit).</summary>
        private void SideNow(string callsign, CircuitSide side)
        {
            AircraftState? a = engine.Get(callsign);
            commands.FlyCircuit(callsign, a?.Circuit ?? CircuitType.Conventional, side);
        }

        /// <summary>What each choice means (its tooltip), by its short name.</summary>
        private static string ChoiceTip(string text) => text switch
        {
            "entry DW" => "A 45° entry onto the downwind.",
            "entry BASE" => "Straight to the start of the base, then the final.",
            "entry IP" => "Straight to the initial point (overhead pattern).",
            "entry HK" => "To the High Key, climbing on its circle (SFO); it waits there until START SFO.",
            "entry FINAL" => "Straight in: onto the final 4 NM out (from LONG FIN: from the long final).",
            "entry CIRCLING" => "Only from LONG FIN: the approach to the opposite runway, then aside onto the downwind at circuit height and the conventional circuit of the runway in use.",
            "CONV" => "Conventional circuit: upwind, crosswind, downwind, base, final.",
            "CLS" => "Close pattern: the jet circuit without the initial (180° turns to the downwind and to the final).",
            "WIDE" => "Wide circuit (outside downwind/normal pattern).",
            "OVH" => "Overhead pattern (run and break): from the initial point.",
            "SFO" => "Simulated flame out: from the High Key.",
            "AD" => "The aerodrome's side for this circuit (Settings › Circuits), taken when it starts.",
            "L" => "Left-hand circuit.",
            "R" => "Right-hand circuit.",
            "T/G" => "Touch and go, then the circuit again.",
            "F/S" => "Full stop: it lands and is removed.",
            "L/A" => "Low approach: down to the short final (about 200 ft), then up to the circuit height and the circuit again.",
            "L/P" => "Low pass: down to 400 ft, on at 400 ft all along the runway, then the circuit again.",
            "DEP" => "Touch and go (or take-off), then it leaves straight out; gone 10 NM out.",
            "break MID" => "Overhead: the break over the middle of the runway. Close: the turn to the downwind there (above 400 ft).",
            "break BEG" => "Overhead: the break over the beginning of the runway. Close: the turn to the downwind there (above 400 ft).",
            "break END" => "Overhead: the break over the end of the runway. Close: the turn to the downwind there (above 400 ft).",
            "break STBY" => "Standby for break: overhead on along the initial, close on along the runway track, until BREAK NOW.",
            _ => ""
        };

        /// <summary>What each button does (its tooltip), by its text.</summary>
        private static readonly Dictionary<string, string> Tips = new()
        {
            ["Open map..."] = "Open a map made with ATCRoom Map Creator (the maps folder shared by the programs).",
            ["Place on the map"] = "Write the name and the elevation of the runway, then click its threshold and its end on the map.",
            ["Remove runway"] = "Remove the runway in use (and the same runway the other way) from the aerodrome file.",
            ["Import heights..."] = "The altitudes of the classes of another aerodrome of TWR Trainer copied here (the same altitudes, QNH); asked before replacing.",
            ["Aircraft classes..."] = "The classes of aircraft and their performance (all aerodromes); their altitudes here: Settings › Circuits.",
            ["SPLIT"] = "The formation splits now: each one an aircraft of its own, the wingmen one interval after the other (overhead, close, SFO: only at the break, by itself).",
            ["GROUP"] = "The rows ticked (☐) into one formation, led by the first one's callsign (at most 4 aircraft).",
            ["▼ WAITING LIST"] = "Every aircraft out of the simulation: their rows back in WAITING.",
            ["EXP TAXI"] = "Landed, rolling out: expedite taxi, at least 45 kt to the nearest exit it can still take.",
            ["ADD EXIT"] = "A runway exit: its name (empty: the next letter), then click on the runway edge where it leaves.",
            ["REMOVE EXIT"] = "Remove the exit whose name is written.",
            ["Apply METAR"] = "Take the wind from the METAR line and show it on the map.",
            ["Get METAR"] = "The newest METAR of this aerodrome from the internet (aviationweather.gov, by its ICAO code): into the box, its wind applied.",
            ["Level 1 (F1)"] = "The aerodrome and its circuits (10 NM, or the view saved on it).",
            ["Level 2 (F2)"] = "The area around the aerodrome (20 NM, or the view saved on it): gates, initial point, long final.",
            ["Remove all aircraft"] = "Remove all the aircraft flying (the flight plans stay).",
            ["ADD GATE"] = "Click the entry gate on the map: a VRP gives its name (or the name written). At least 5 NM out.",
            ["REMOVE GATE"] = "Remove the gate written (or the one chosen in Start from).",
            ["ADD NOW"] = "Start this aircraft now (its plan in the Traffic List: ✕ puts it back in WAITING).",
            ["CANCEL"] = "The plan being changed stays as it was.",
            ["TO LIST"] = "Put it in the Traffic List: started by ▶ or by START ALL after its delay.",
            ["START ALL"] = "Start every aircraft of the Traffic List waiting, each after its delay (held ones stay held).",
            ["SAVE..."] = "Save the flight plans as a scenario of this aerodrome.",
            ["LOAD..."] = "Load a scenario (its flight plans replace those of the list).",
            ["REMOVE ALL"] = "Every row out of the Traffic List (the aircraft flying go on).",
            ["▶"] = "Start this plan now.",
            ["⏸"] = "Hold this plan (it does not start); again: it goes on.",
            ["⏵"] = "Go on (it starts at its time).",
            ["⧉"] = "Duplicate: the next callsign, one minute later.",
            ["✕"] = "Remove this plan.",
            ["BREAK AT A POINT"] = "Overhead and close: click on the map where it breaks (along the runway).",
            ["BREAK NOW"] = "Overhead on the initial, close on the upwind: it breaks now (close: above 400 ft). Also during a touch and go before a close pattern: it turns above 400 ft (another break point: cancelled).",
            ["HOLD HK"] = "SFO: it waits circling over the High Key (until START SFO).",
            ["START SFO"] = "SFO: at the next pass over the High Key it starts the flame out pattern.",
            ["EXTEND DWN"] = "On the downwind: straight on beyond the base turn, until TURN BASE.",
            ["TURN BASE"] = "On the downwind: turn base now (also during an orbit). Still joining the circuit: straight to the start of the base.",
            ["ORBIT"] = "One orbit (360°) away from the runway, then on. Shift+click: orbits until CONTINUE.",
            ["ORBIT ∞"] = "Orbits until CONTINUE (this button while it orbits).",
            ["EXT DWN"] = "On the downwind: straight on beyond the base turn, until BASE.",
            ["BASE"] = "On the downwind: turn base now (also during an orbit). Still joining the circuit: straight to the start of the base.",
            ["ORB"] = "One orbit (360°) away from the runway, then on. Shift: orbits on and on (thick border); then a click: on along its circuit.",
            ["G/A"] = "Only on the final: it stops descending, passes the threshold and keeps the runway heading until a new order.",
            ["EXP TAXI"] = "Landed: off the runway quickly.",
            ["SPLIT"] = "The formation splits (each aircraft on its own).",
            ["CONTINUE"] = "The orbits end: on along its circuit from here.",
            ["GO AROUND"] = "Only on the final: it stops descending, passes the threshold and keeps the runway heading until a new order.",
            ["SIDE NOW L"] = "Left-hand now: an S over the aerodrome onto the new downwind (on the final: a go-around).",
            ["SIDE NOW R"] = "Right-hand now: an S over the aerodrome onto the new downwind (on the final: a go-around).",
            ["WIDE NOW"] = "Out to the wide circuit now (to make room).",
            ["GO"] = "Give it: the aircraft leaves its circuit (MANUAL); REJOIN brings it back.",
            ["REJOIN"] = "Back into its circuit from where it is.",
            ["BREAKUP"] = "Out of the circuits: on the downwind a turn away from the runway, on the base on turning past the final. Then MANUAL.",
            ["REMOVE"] = "Remove this aircraft (asked first)."
        };

        private static readonly Brush ActionBack = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0x86));
        private static Brush ActionText => Theme.ActionText;
        private static readonly Brush LitBack = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A));
        private static readonly Brush LitText = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A));

        private static Button ActionButton(string text, Action action)
        {
            Button button = new()
            {
                Content = text,
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(7, 2, 7, 2),
                MinWidth = 34,
                Background = ActionBack,
                BorderBrush = ActionBack,
                // (A little less light than the text: under the mouse it turns white, ButtonStyle.)
                Foreground = ActionText,
                Focusable = false
            };
            button.Click += (_, _) => action();
            if (Tips.TryGetValue(text, out string? tip)) button.ToolTip = tip;
            ToolTipService.SetShowOnDisabled(button, true);
            return button;
        }

        // (The user, 8 Nov: the NOW orders orange, not to mix them up with DCT TO.)
        private static readonly Brush NowBack = new SolidColorBrush(Color.FromRgb(0x8A, 0x4A, 0x1C)), NowEdge = new SolidColorBrush(Color.FromRgb(0xD0, 0x80, 0x3A)),
            NowText = new SolidColorBrush(Color.FromRgb(0xFB, 0xEB, 0xDD));

        private static void NowLook(Button button)
        {
            button.Background = NowBack;
            button.BorderBrush = NowEdge;
            button.Foreground = NowText;
        }

        /// <summary>REMOVE of Aircraft control and the Del key: asked first.</summary>
        private void RemoveAsked(string callsign)
        {
            if (MessageBox.Show(this, $"Remove {callsign}?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            commands.Remove(callsign);
        }

        /// <summary>
        /// A box of ALT, SPD or HDG (the user, 8 Nov): it shows the value now; the wheel as on the label; typed + Enter: the order;
        /// Esc: the value now again; leaving it without Enter: nothing.
        /// </summary>
        private void ValueBox(TextBox box, string field)
        {
            box.PreviewMouseWheel += (_, e) =>
            {
                e.Handled = true;
                if (view.Selected is not string cs) return;
                int step = e.Delta > 0 ? -1 : 1;
                if (field == "HDG")
                {
                    // ±5° a notch, ordered when the wheel stops a moment (as ALT and SPD).
                    double from = double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double h) ? h : engine.Get(cs)?.HeadingTrue ?? 0;
                    double next = ((Math.Round(from / 5) * 5 - step * 5) % 360 + 360) % 360;
                    box.Text = $"{(next == 0 ? 360 : next):000}";
                    headingWheel = cs;
                    valueSince = clock.Elapsed.TotalSeconds;
                }
                else
                {
                    LabelWheel(cs, field, step);
                    ShowSelectedValues();
                }
            };
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(this, view);
                    ShowSelectedValues();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (view.Selected is not string cs) return;
                    if (!double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v < 1
                        || (field == "HDG" && v > 360))
                    {
                        Say(field == "HDG" ? "Write a heading (1-360)." : "Write a number.", Area.Selected);
                        return;
                    }
                    if (field == "ALT") commands.Height(cs, Math.Clamp((v < 200 ? v * 100 : v) - HeightOffset, 100, 15000));
                    else if (field == "SPD") commands.Speed(cs, v);
                    else commands.Heading(cs, v % 360);
                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(this, view);
                    ShowSelected();
                }
            };
        }

        /// <summary>The AUTO and the presets of ALT and SPD (made again for another aircraft or other presets), the one now yellow.</summary>
        private void ShowPresets(AircraftState a)
        {
            IReadOnlyList<double> speeds = SpeedChoices(a);
            string key = $"{a.Callsign}|{HeightOffset}|{string.Join(",", speeds)}|{a.Formation?.Count}";
            if (key != presetsFor)
            {
                presetsFor = key;
                altPresets.Children.Clear();
                altPresets.Children.Add(heightBox);
                altPresets.Children.Add(Preset("AUTO", "ALT=AUTO", "Its circuit's altitude again."));
                foreach (double h in TowerView.HeightChoices)
                    altPresets.Children.Add(Preset($"{Math.Round((h + HeightOffset) / 100):000}", $"ALT={h:0}", $"{Math.Round((h + HeightOffset) / 100) * 100:0} ft; it keeps its circuit."));
                speedPresets.Children.Clear();
                speedPresets.Children.Add(speedKtBox);
                speedPresets.Children.Add(Preset("AUTO", "SPD=AUTO", "Its circuit's speed again."));
                foreach (double v in speeds) speedPresets.Children.Add(Preset($"{v:0}", $"SPD={v:0}", $"{v:0} kt; it keeps its circuit."));
                landPanel.Children.Clear();
                if (a.Formation is { Count: > 1 } crew)
                    for (int i = 0; i < crew.Count; i++)
                    {
                        int member = i;
                        Button land = ActionButton($"{member + 1}", () => WithSelected(cs => LabelField(cs, $"LAND={member}")));
                        land.ToolTip = $"{crew[member]}: yellow lands, grey goes around (from a circuit at most 2 land).";
                        landPanel.Children.Add(land);
                    }
            }
            // The one now: yellow.
            double? alt = !a.RouteAltitude && a.VerticalMode == VerticalMode.Altitude ? a.TargetAltitudeFt - airfield.ElevationFt : null;
            double? spd = !a.RouteSpeed ? a.TargetSpeedKt : null;
            foreach (Button b in altPresets.Children.OfType<Button>())
                Lit(b, b.Tag as string == "ALT=AUTO" ? a.RouteAltitude : alt is double x && b.Tag is string t && double.TryParse(t[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && Math.Abs(h - x) < 30);
            foreach (Button b in speedPresets.Children.OfType<Button>())
                Lit(b, b.Tag as string == "SPD=AUTO" ? a.RouteSpeed : spd is double y && b.Tag is string t && double.TryParse(t[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && Math.Abs(v - y) < 3);
            if (a.Formation is { } team)
                for (int i = 0; i < landPanel.Children.Count; i++)
                    Lit((Button)landPanel.Children[i], a.Lands == null || i >= a.Lands.Count || a.Lands[i]);
            landLine.Visibility = a.Formation is { Count: > 1 } ? Visibility.Visible : Visibility.Collapsed;

            Button Preset(string text, string field, string tip)
            {
                Button b = ActionButton(text, () => WithSelected(cs => LabelField(cs, field)));
                b.Tag = field;
                b.ToolTip = tip;
                return b;
            }
            static void Lit(Button b, bool on)
            {
                b.Background = b.BorderBrush = on ? LitBack : ActionBack;
                b.Foreground = on ? LitText : ActionText;
            }
        }

        private Grid Row(string label, UIElement control, Panel? into = null, double labelWidth = 130)
        {
            Grid grid = new() { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock text = new() { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = TextBrush };
            grid.Children.Add(text);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            (into ?? current ?? panel).Children.Add(grid);
            return grid;
        }

        /// <summary>Boxes side by side, each with its name above it.</summary>
        private static Grid Labelled(params (string Name, TextBox Box)[] boxes)
        {
            Grid grid = new() { Margin = new Thickness(0, 2, 0, 2) };
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            for (int i = 0; i < boxes.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                TextBlock name = new() { Text = boxes[i].Name, FontSize = 11, Foreground = DimBrush };
                Grid.SetColumn(name, i);
                grid.Children.Add(name);
                boxes[i].Box.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(boxes[i].Box, i);
                Grid.SetRow(boxes[i].Box, 1);
                grid.Children.Add(boxes[i].Box);
            }
            return grid;
        }

        private static StackPanel Inline(params UIElement[] items)
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal };
            foreach (UIElement item in items)
            {
                if (item is FrameworkElement element) element.Margin = new Thickness(0, 0, 4, 0);
                row.Children.Add(item);
            }
            return row;
        }

        private void Buttons(params (string Text, Action Action)[] buttons) => Buttons(current ?? panel, buttons);

        private static void Buttons(Panel into, params (string Text, Action Action)[] buttons)
        {
            WrapPanel row = new() { Margin = new Thickness(0, 3, 0, 3) };
            foreach ((string text, Action action) in buttons)
            {
                Button button = new() { Content = text, Margin = new Thickness(0, 0, 5, 4), Padding = new Thickness(8, 2, 8, 2) };
                button.Click += (_, _) => action();
                if (Tips.TryGetValue(text, out string? tip)) button.ToolTip = tip;
                row.Children.Add(button);
            }
            into.Children.Add(row);
        }

        /// <summary>A message in its part (null: none); the part closed to its bar: on its bar for a few seconds.</summary>
        private void Say(string? text, Area area = Area.Aerodrome)
        {
            messages[area].Text = text ?? "";
            DockablePart? part = area switch
            {
                Area.Aerodrome or Area.Simulation => settingsPart,
                Area.New => newPart,
                Area.Traffic => trafficPart,
                _ => selectedPart
            };
            if (part != null && !part.IsOpen && !string.IsNullOrEmpty(text)) part.Flash(text);
        }

        // ----- Photos for the debriefing (the user, 6 Nov) ----------------------------------------------------------------

        /// <summary>The folder of the photos of this session (made at the first photo: the date and time of the start).</summary>
        // (A new folder at each START SESSION, the user 8 Nov; before one: the folder of this run of the program.)
        private string photoSession = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        private string PhotoFolder => Path.Combine(Paths.PhotosFolder, photoSession);
        private int photoCount;
        private readonly TextBlock photoText = new() { Foreground = DimBrush, FontSize = 11 };
        private DispatcherTimer? photoTextTimer;

        /// <summary>
        /// A picture of the map with all the labels of the tracks, at the moment of the click (the simulation goes on), saved
        /// as Photo-03_T+12m40s.png (the number of the session, the simulated time) in the folder of the session.
        /// </summary>
        private void TakePhoto()
        {
            if (view.ActualWidth < 20 || view.ActualHeight < 20) return;
            RenderTargetBitmap bitmap;
            try
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(view);
                Rect area = new(0, 0, view.ActualWidth, view.ActualHeight);
                DrawingVisual picture = new();
                using (DrawingContext dc = picture.RenderOpen()) dc.DrawRectangle(new VisualBrush(view) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, area);
                bitmap = new RenderTargetBitmap((int)Math.Ceiling(area.Width * dpi.DpiScaleX), (int)Math.Ceiling(area.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(picture);
                bitmap.Freeze();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OutOfMemoryException)
            {
                PhotoSaid("Photo not taken (the map could not be drawn).");
                return;
            }
            string folder = PhotoFolder;
            int number = ++photoCount;
            double time = Math.Max(0, engine.Time);
            // (In a session: the time of the session, as in its log.)
            double shown = flightLog.InSession ? Math.Max(0, time - flightLog.SessionStart) : time;
            string name = string.Create(CultureInfo.InvariantCulture, $"Photo-{number:00}_T+{(int)(shown / 60)}m{(int)(shown % 60):00}s.png");
            // (A mark in the log of the flights, the user 6 Nov.)
            flightLog.Event(time, $"PHOTO {number}", detail: name, source: "INSTRUCTOR");
            // (Written on another thread: the simulation does not wait.)
            System.Threading.Tasks.Task.Run(() =>
            {
                string said;
                try
                {
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, name);
                    // (The number of the session: never over a photo already there.)
                    if (File.Exists(path)) path = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".png");
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using FileStream file = File.Create(path);
                    encoder.Save(file);
                    said = $"Photo {number}: {Path.GetFileName(path)}";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
                {
                    // (No path in the message: the Windows user name.)
                    said = $"Photo {number} not saved: the Photos folder cannot be written (read-only, or the disk is full).";
                }
                Dispatcher.InvokeAsync(() => PhotoSaid(said));
            });
        }

        /// <summary>What happened to a photo, beside PHOTO on the bar of the map for a few seconds (not on the map: not in the next photo).</summary>
        private void PhotoSaid(string text)
        {
            photoText.Text = text;
            photoTextTimer?.Stop();
            photoTextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            photoTextTimer.Tick += (_, _) =>
            {
                photoText.Text = "";
                photoTextTimer?.Stop();
            };
            photoTextTimer.Start();
        }

        /// <summary>The folder of the photos of this session (the Photos folder before the first photo) in the Explorer.</summary>
        private void OpenPhotos()
        {
            string folder = photoCount > 0 && Directory.Exists(PhotoFolder) ? PhotoFolder : Paths.PhotosFolder;
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                PhotoSaid("The Photos folder cannot be opened.");
            }
        }

        // ----- Aerodrome -------------------------------------------------------------------------------------------------

        private void ChooseMap()
        {
            try { Directory.CreateDirectory(Paths.MapsFolder); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "ATCRoom map (*.json)|*.json",
                InitialDirectory = Directory.Exists(Paths.MapsFolder) ? Paths.MapsFolder : "",
                Title = "Open a map (made with ATCRoom Map Creator)"
            };
            if (dialog.ShowDialog(this) == true) OpenMap(dialog.FileName, null);
        }

        private void OpenMap(string path, string? runway)
        {
            if (engine.Count > 0 && MessageBox.Show(this, "The aircraft flying now are removed. Go on?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            try
            {
                airfield.OpenMap(path);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Say($"The map could not be opened: {ex.Message}");
                return;
            }
            settings.MapPath = path;
            // (Another map: the session of the log ends.)
            flightLog.EndSession(engine.Time, "another map opened");
            ShowSession();
            // The layers of this map: those it has, on as saved (or the defaults).
            HashSet<string> present = new(airfield.Map!.Lines.Select(l => l.Category).Concat(airfield.Map.Labels.Select(l => l.Category))
                .Concat(airfield.Map.Points.Select(p => p.Category)), StringComparer.OrdinalIgnoreCase);
            view.CategoriesOn.Clear();
            HashSet<string> off = new(settings.CategoriesOff ?? MapCategory.All.Select(c => c.Id).Except(DefaultCategories()), StringComparer.OrdinalIgnoreCase);
            foreach (MapCategoryInfo category in MapCategory.All.Where(c => !off.Contains(c.Id))) view.CategoriesOn.Add(category.Id);
            layers.Children.Clear();
            foreach (MapCategoryInfo category in MapCategory.All.Where(c => present.Contains(c.Id)))
            {
                CheckBox box = new() { Content = category.Name, IsChecked = view.CategoriesOn.Contains(category.Id), Foreground = TextBrush, Margin = new Thickness(0, 1, 0, 1) };
                box.Checked += (_, _) => Layer(category.Id, true);
                box.Unchecked += (_, _) => Layer(category.Id, false);
                layers.Children.Add(box);
            }
            string? use = airfield.PlacedRunways.Any(r => string.Equals(r.Runway, runway, StringComparison.OrdinalIgnoreCase))
                ? runway : airfield.PlacedRunways.FirstOrDefault()?.Runway;
            UseRunway(use, ask: false);
        }

        /// <summary>The layers on at first: all but MVA, CTR and FIX (the user, 1 Nov: off by default, still available).</summary>
        private static IEnumerable<string> DefaultCategories() =>
            MapCategory.All.Where(c => (c.ShapesOn || c.LabelsOn || c.Id is MapCategory.Vrp or MapCategory.Atz)
                && c.Id is not (MapCategory.Mva or MapCategory.Ctr or MapCategory.Fix)).Select(c => c.Id);

        private void Layer(string id, bool on)
        {
            if (on) view.CategoriesOn.Add(id); else view.CategoriesOn.Remove(id);
            settings.CategoriesOff = MapCategory.All.Select(c => c.Id).Where(id => !view.CategoriesOn.Contains(id)).ToList();
            view.InvalidateVisual();
        }

        private void UseRunway(string? name, bool ask = true)
        {
            bool changed = !string.Equals(airfield.Runway?.Runway, name, StringComparison.OrdinalIgnoreCase);
            if (changed && ask && engine.Count > 0
                && MessageBox.Show(this, "Another runway: the aircraft flying now are removed. Go on?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            {
                ShowAerodrome();
                return;
            }
            engine.Clear();
            view.Select(null);
            airfield.SetRunway(name);
            if (airfield.Runway != null)
            {
                engine.SetGlidePath(airfield.Course, 3, airfield.ElevationFt, airfield.LengthNM);
                engine.SetAerodrome(airfield.Circuits, airfield.Runway.Runway);
                settings.Runway = airfield.Runway.Runway;
            }
            else
            {
                engine.SetGlidePath(double.NaN, 0);
                engine.SetAerodrome(airfield.Circuits, null);
            }
            view.Course = airfield.Course;
            view.LengthNM = airfield.LengthNM;
            view.ElevationFt = airfield.ElevationFt;
            ShowPressure();
            view.SetMap(airfield.Map, airfield.Plane);
            RebuildFrom();
            ShowAerodrome();
            GoLevel(1);
        }

        private void ShowAerodrome()
        {
            view.Hint = airfield.Map == null
                ? "Open a map (Settings › Map: Open map...).\nNo map yet? Make one with ATCRoomMapCreator.exe, beside this program."
                : airfield.Runway == null ? "Place the runway in use: in Settings › Map write its name and elevation, then Place on the map." : "";
            view.InvalidateVisual();
            updating = true;
            mapText.Text = airfield.Map == null ? "No map: open one made with ATCRoom Map Creator."
                : $"{airfield.Map.Name} ({airfield.Code})" + (airfield.PlacedRunways.Count == 0 ? "\nNo runway yet: place one on the map (below)." : "");
            runwayRow = new ChoiceRow<string>(airfield.PlacedRunways.Select(r => (r.Runway, r.Runway)), (name, _) => $"Runway {name} in use: the circuits are flown on it.");
            if (airfield.Runway != null) runwayRow.Show(airfield.Runway.Runway);
            runwayRow.Picked += name =>
            {
                if (!updating) UseRunway(name);
            };
            runwayHolder.Children.Clear();
            runwayHolder.Children.Add(airfield.PlacedRunways.Count > 0 ? runwayRow : new TextBlock { Text = "(none placed yet)", Foreground = DimBrush, FontSize = 11 });
            foreach ((ChoiceRow<CircuitSide> row, CircuitType type) in new[] { (conventionalSideRow, CircuitType.Conventional), (jetSideRow, CircuitType.Close), (sfoSideRow, CircuitType.Sfo) })
            {
                row.IsEnabled = airfield.Runway != null;
                if (airfield.Runway != null) row.Show(airfield.Circuits.SideOf(airfield.Runway.Runway, type) ?? CircuitSide.Left);
            }
            // The long final and the IP of this aerodrome (the user, 4 Nov).
            engine.LongFinalNM = airfield.Circuits.LongFinalNM ?? 10;
            engine.InitialPointNM = airfield.Circuits.InitialPointNM ?? 3;
            view.LongFinalNM = engine.LongFinalNM;
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
            variationBox.Text = VariationText(airfield.Circuits.MagneticVariation ?? 0);
            if (airfield.Runway != null)
            {
                newRunwayName.Text = airfield.Runway.Runway;
                newRunwayElevation.Text = airfield.ElevationFt.ToString("0", CultureInfo.InvariantCulture);
            }
            ShowBaseTraffic();
            ShowHeights();
            ShowRings();
            ShowMapTurn();
            updating = false;
        }

        private void PlaceRunway()
        {
            string name = newRunwayName.Text.Trim().ToUpperInvariant();
            if (airfield.Map == null) { Say("Open a map first."); return; }
            if (name.Length == 0) { Say("Write the name of the runway (for example 14)."); return; }
            if (!double.TryParse(newRunwayElevation.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double elevation))
            {
                Say("Write the elevation of the threshold in feet (for example 340): the heights of the labels are above it.");
                return;
            }
            if (engine.Count > 0 && MessageBox.Show(this, "The aircraft flying now are removed. Go on?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            Say(null);
            view.AskPoint($"Runway {name}: click its THRESHOLD (zoom in with the wheel first)  -  Esc: cancel", (tx, ty) =>
                view.AskPoint($"Runway {name}: now click its END  -  Esc: cancel", (ex, ey) =>
                {
                    if (Math.Sqrt((ex - tx) * (ex - tx) + (ey - ty) * (ey - ty)) < 0.05) { Say("The two points are too close: place it again."); return; }
                    airfield.PlaceRunway(name, (tx, ty), (ex, ey), elevation);
                    Say(airfield.Save());
                    UseRunway(name, ask: false);
                }));
        }

        /// <summary>
        /// The runways of this aerodrome from the runway file of AuroraPAR (chosen each time, the last one proposed): each
        /// runway once (its glide paths together); with both ways in the file, their thresholds are its two ends; with one, the
        /// other end along its true heading by its length. Each threshold with its own elevation.
        /// </summary>
        private void RunwaysFromPar()
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            if (airfield.Code.Length == 0) { Say("The map has no aerodrome code (ICAO): place the runway on the map."); return; }
            string last = settings.RunwaysParPath;
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "AuroraPAR runway file (runways.par)|runways.par|All files (*.*)|*.*",
                InitialDirectory = last.Length > 0 && Directory.Exists(Path.GetDirectoryName(last)) ? Path.GetDirectoryName(last)! : "",
                FileName = last.Length > 0 ? Path.GetFileName(last) : "runways.par",
                Title = "The runway file of AuroraPAR (runways.par, in the folder of AuroraPAR)"
            };
            if (dialog.ShowDialog(this) != true) return;
            settings.RunwaysParPath = dialog.FileName;
            settings.Save();
            List<RunwaysPar.Threshold> thresholds;
            try
            {
                thresholds = RunwaysPar.Read(dialog.FileName, airfield.Code);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"runways.par could not be read: {ex.Message}");
                return;
            }
            if (thresholds.Count == 0) { Say($"No runway of {airfield.Code} in runways.par: place it on the map."); return; }
            // Each runway with the other way (when the file has it).
            List<(RunwaysPar.Threshold One, RunwaysPar.Threshold? Other)> runways = [];
            foreach (RunwaysPar.Threshold t in thresholds)
            {
                if (runways.Any(r => r.One == t || r.Other == t)) continue;
                string? back = RunwayCircuit.Reciprocal(t.Runway);
                runways.Add((t, thresholds.FirstOrDefault(o => back != null && o.Runway == back)));
            }
            string names = string.Join(", ", runways.Select(r => r.Other != null ? $"{r.One.Runway}/{r.Other.Runway}" : r.One.Runway));
            string flying = engine.Count > 0 ? "\n\nThe aircraft flying now are removed." : "";
            if (MessageBox.Show(this, $"Place runway {names} of {airfield.Code} from runways.par? A runway already placed with that name is moved there.{flying}",
                "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            foreach ((RunwaysPar.Threshold one, RunwaysPar.Threshold? other) in runways)
            {
                (double tx, double ty) = airfield.Plane.FromGeo(one.Latitude, one.Longitude);
                (double ex, double ey) = other != null ? airfield.Plane.FromGeo(other.Latitude, other.Longitude)
                    : (tx + one.LengthM / 1852 * Math.Sin(one.HeadingTrue * Math.PI / 180), ty + one.LengthM / 1852 * Math.Cos(one.HeadingTrue * Math.PI / 180));
                if (Math.Sqrt((ex - tx) * (ex - tx) + (ey - ty) * (ey - ty)) < 0.05) { Say($"Runway {one.Runway} in runways.par is too short: not placed."); continue; }
                airfield.PlaceRunway(one.Runway, (tx, ty), (ex, ey), one.ElevationFt, other?.ElevationFt);
            }
            string? error = airfield.Save();
            UseRunway(airfield.Runway?.Runway ?? runways[0].One.Runway, ask: false);
            Say(error ?? $"Runway {names} placed from runways.par.");
        }

        private void RemoveRunway()
        {
            if (airfield.Runway == null) return;
            string other = RunwayCircuit.Reciprocal(airfield.Runway.Runway) is string o ? $" and {o}" : "";
            if (MessageBox.Show(this, $"Remove runway {airfield.Runway.Runway}{other} from the aerodrome file?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            airfield.RemoveRunway(airfield.Runway.Runway);
            Say(airfield.Save());
            UseRunway(airfield.PlacedRunways.FirstOrDefault()?.Runway, ask: false);
        }

        private ClassCircuit? HeightsOf(string? name) =>
            airfield.Circuits.Classes.FirstOrDefault(c => string.Equals(c.Class, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>QNH or QFE of this aerodrome (the user, 6 Nov): the names and the values of the heights shown and written.</summary>
        private void ShowPressure()
        {
            bool qfe = airfield.Circuits.Qfe;
            pressureRow.Show(qfe);
            view.Qfe = qfe;
            ShowHeights();
            if (arrivalName == null) return;
            arrivalName.Text = $"{HeightWord} ft";
            exitAltName.Text = $"Exit {HeightWord.ToLowerInvariant()} ft";
            heightName.Text = qfe ? "HGT" : "ALT";
            hkName.Text = $"HK {HeightWord.ToLowerInvariant()} ft";
            heightBox.ToolTip = qfe ? "The height above the runway (hundreds of ft: 25 = 2500; or the feet): Enter. Mouse wheel: ±100 ft. Key: L."
                : "The altitude (QNH, hundreds of ft: 25 = 2500; or the feet): Enter. Mouse wheel: ±100 ft. Key: L.";
        }

        /// <summary>The row of the classes in Traffic generation, made again (at the start, after the Aircraft classes window).</summary>
        private void MakeClassRow(string? keep)
        {
            classHolder.Children.Clear();
            classRow = new ChoiceRow<string>(engine.Classes.Select(c => (c.Name, c.Name.ToUpperInvariant())),
                (name, _) => $"Class {name} (its performance: Settings › Aircraft › Aircraft classes; its altitudes here: Settings › Circuits).");
            // (The user, 4 Nov: the jet by default, the most used.)
            classRow.Show(engine.Classes.FirstOrDefault(c => string.Equals(c.Name, keep, StringComparison.OrdinalIgnoreCase))?.Name
                ?? engine.Classes.FirstOrDefault(c => string.Equals(c.Name, "Jet", StringComparison.OrdinalIgnoreCase))?.Name
                ?? (engine.Classes.Any(c => c.Name == PerformanceClass.DefaultName) ? PerformanceClass.DefaultName : engine.Classes[0].Name));
            classHolder.Children.Add(classRow);
            classRow.Picked += _ => { ShowEntries(); ShowBaseTraffic(); };
            // (The class of the base traffic of this aerodrome, Settings › Aircraft, the user 7 Nov.)
            defaultClassHolder.Children.Clear();
            defaultClassRow = new ChoiceRow<string>(engine.Classes.Select(c => (c.Name, c.Name.ToUpperInvariant())),
                (name, _) => $"The class of the Quick traffic of this aerodrome: {name} (the ICAO type is only its name on the strips).");
            defaultClassRow.Picked += name =>
            {
                if (airfield.Map == null) { Say("Open a map first: the base traffic is kept with its aerodrome."); ShowBaseTraffic(); return; }
                airfield.Circuits.DefaultClass = name;
                Say(airfield.Save());
                ShowBaseTraffic();
            };
            defaultClassHolder.Children.Add(defaultClassRow);
            ShowBaseTraffic();
            ShowHeights();
        }

        /// <summary>The heights table of Settings › Circuits again (the classes, this aerodrome with its runway in use, QNH / QFE).</summary>
        private void ShowHeights() =>
            heightsTable.Show(engine.Classes, airfield.Map != null && airfield.Runway != null ? airfield.Circuits : null, airfield.ElevationFt, airfield.Circuits.Qfe);

        /// <summary>
        /// The base traffic of this aerodrome shown (Settings › Aircraft and the Quick tab) and given to the engine: the callsign
        /// prefix (missing: SIM), the ICAO type, the class.
        /// </summary>
        private void ShowBaseTraffic()
        {
            AerodromeCircuits here = airfield.Circuits;
            string prefix = (here.CallsignPrefix ?? "").Trim().ToUpperInvariant();
            try { engine.CallsignPrefix = prefix.Length > 0 ? prefix : "SIM"; }
            catch (ArgumentException) { engine.CallsignPrefix = "SIM"; prefix = ""; }
            prefixBox.Text = prefix;
            defaultTypeBox.Text = (here.IcaoType ?? "").Trim().ToUpperInvariant();
            string? kind = QuickClass(out bool own);
            if (own && kind != null) defaultClassRow.Show(kind);
            else defaultClassRow.Show("");
            quickBase.Text = $"Base traffic: {engine.CallsignPrefix}1, {engine.CallsignPrefix}2...  ·  {(defaultTypeBox.Text.Length > 0 ? defaultTypeBox.Text : "no ICAO type")}  ·  "
                + $"{(kind ?? "?").ToUpperInvariant()}{(own ? "" : " (as in Advanced)")}  ·  VFR. Settings › Aircraft.";
        }

        /// <summary>The class of the Quick traffic: the aerodrome's (own true), else the one chosen in Advanced; null: none.</summary>
        private string? QuickClass(out bool own)
        {
            string? kind = engine.Classes.FirstOrDefault(c => string.Equals(c.Name, airfield.Circuits.DefaultClass, StringComparison.OrdinalIgnoreCase))?.Name;
            own = kind != null;
            return kind ?? (classRow.HasValue ? classRow.Value : null);
        }

        /// <summary>The callsign prefix and the ICAO type of the base traffic written (Settings › Aircraft): kept with the aerodrome.</summary>
        private void ApplyBaseTraffic()
        {
            string prefix = prefixBox.Text.Trim().ToUpperInvariant();
            string type = defaultTypeBox.Text.Trim().ToUpperInvariant();
            string? newPrefix = prefix.Length > 0 ? prefix : null, newType = type.Length > 0 ? type : null;
            if (newPrefix == airfield.Circuits.CallsignPrefix && newType == airfield.Circuits.IcaoType) return;
            if (airfield.Map == null) { Say("Open a map first: the base traffic is kept with its aerodrome."); ShowBaseTraffic(); return; }
            if (prefix.Length > 0 && (prefix.Length > 6 || !prefix.All(char.IsAsciiLetterOrDigit)))
            {
                Say("The callsign prefix: 1 to 6 letters or digits (empty: SIM).");
                ShowBaseTraffic();
                return;
            }
            airfield.Circuits.CallsignPrefix = newPrefix;
            airfield.Circuits.IcaoType = newType;
            string? error = airfield.Save();
            ShowBaseTraffic();
            Say(error ?? $"Base traffic of {airfield.Code}: {engine.CallsignPrefix}1, {engine.CallsignPrefix}2...{(newType != null ? $", {newType}" : "")}.");
        }

        /// <summary>
        /// Import heights (the user, 7 Nov): the altitudes of the classes of another aerodrome of this program (a menu of them),
        /// the same altitudes here (the heights above the runway follow the elevations); asked before replacing.
        /// </summary>
        private void ImportHeights()
        {
            if (airfield.Map == null || airfield.Runway == null) { Say("Open a map and place its runway first: the altitudes are reckoned with its elevation."); return; }
            List<(string Code, string Path)> files = [];
            try
            {
                if (Directory.Exists(Paths.AerodromesFolder))
                    foreach (string path in Directory.EnumerateFiles(Paths.AerodromesFolder, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                        if (!string.Equals(code, airfield.Code, StringComparison.OrdinalIgnoreCase)) files.Add((code, path));
                    }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // (Not readable: none.)
            }
            if (files.Count == 0) { Say("No other aerodrome yet: TWR Trainer keeps one for each map opened (with its runways placed)."); return; }
            OpenMenu(files.OrderBy(f => f.Code, StringComparer.Ordinal).Select(f => Item(f.Code, () => ImportHeightsFrom(f.Code, f.Path))).ToList());
        }

        private void ImportHeightsFrom(string code, string path)
        {
            AerodromeCircuits other;
            try
            {
                other = AerodromeCircuits.Load(path);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Say($"The file of {code} could not be read: nothing imported.");
                return;
            }
            if (other.Elevation() is not double otherElevation) { Say($"{code} has no elevation: open its map and place its runways first (Settings › Map)."); return; }
            if (!other.Classes.Any(c => c != null && (c.CircuitHeightFt ?? c.ConventionalHeightFt ?? c.WideHeightFt ?? c.CloseHeightFt ?? c.InitialHeightFt ?? c.BreakHeightFt ?? c.HighKeyFt ?? c.LowKeyFt) != null))
            {
                Say($"{code} has no altitudes of its own (all are the classes' ones): nothing to import.");
                return;
            }
            if (MessageBox.Show(this, $"Replace the altitudes of the classes at {airfield.Code} with those of {code}?\n\nThe same altitudes (QNH); those not written at {code}: the classes' ones.",
                "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            airfield.Circuits.CopyHeightsFrom(other, otherElevation, airfield.ElevationFt);
            string? error = airfield.Save();
            engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
            ShowHeights();
            Say(error ?? $"The altitudes of {code} imported: for the aircraft added from now on.");
        }

        /// <summary>
        /// The Aircraft classes window: the performance saved in performance.json (the other values of each class kept as they
        /// are in the file); the altitudes of the aerodrome follow the classes renamed or removed; then the classes of the engine
        /// and the row of Traffic generation again.
        /// </summary>
        private void OpenClasses()
        {
            bool map = airfield.Map != null;
            // The performance file read first: one of a newer version is not changed (said before any change is typed).
            PerformanceFile file;
            try
            {
                file = File.Exists(Paths.Performance)
                    ? System.Text.Json.JsonSerializer.Deserialize<PerformanceFile>(File.ReadAllText(Paths.Performance), PerformanceFile.JsonOptions) ?? new PerformanceFile()
                    : new PerformanceFile();
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Say($"{PerformanceFile.FileName} could not be read: nothing saved ({ex.Message}).", Area.Aerodrome);
                return;
            }
            if (file.FormatVersion > PerformanceFile.CurrentFormatVersion)
            {
                Say($"{PerformanceFile.FileName} was made by a newer version of TWR Trainer: the classes can't be changed with this one.", Area.Aerodrome);
                return;
            }
            ClassesWindow window = new(engine.Classes) { Owner = this };
            if (window.ShowDialog() != true || window.Result is not { } saved) return;
            List<PerformanceClass> before = file.Classes ?? [];
            List<PerformanceClass> after = [];
            foreach (ClassesWindow.Saved item in saved)
            {
                PerformanceClass? old = item.OriginalName == null ? null
                    : before.FirstOrDefault(c => c != null && string.Equals((c.Name ?? "").Trim(), item.OriginalName, StringComparison.OrdinalIgnoreCase));
                // (The class as it flew, every value given, with the changes: a new name does not bring other defaults.)
                PerformanceClass kind = item.Class.Copy();
                if (old?.Extra != null) kind.Extra = new Dictionary<string, System.Text.Json.JsonElement>(old.Extra);
                after.Add(kind);
            }
            file.Classes = after;
            try
            {
                PerformanceFile.Write(Paths.Performance, file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"{PerformanceFile.FileName} could not be written: nothing saved ({ex.Message}).", Area.Aerodrome);
                return;
            }
            // (The class chosen in Traffic generation followed, also renamed.)
            string? keep = saved.FirstOrDefault(item => string.Equals(item.OriginalName, classRow.Value, StringComparison.OrdinalIgnoreCase))?.Class.Name ?? classRow.Value;
            (List<PerformanceClass> classes, _, string? note) = PerformanceFile.Load(Paths.Performance);
            engine.SetPerformanceClasses(classes);
            // The altitudes of this aerodrome (Settings › Circuits) follow a class renamed; those of a class removed go (what this
            // version does not know kept). The base traffic's class follows too.
            string? error = null;
            if (map)
            {
                List<ClassCircuit> heights = airfield.Circuits.Classes.Where(c => c != null).ToList();
                ClassCircuit? Find(string? name) => name == null ? null : heights.FirstOrDefault(c => string.Equals((c.Class ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase));
                foreach (string removed in window.OpenedWith.Where(name => !saved.Any(item => string.Equals(item.OriginalName, name, StringComparison.OrdinalIgnoreCase))))
                {
                    if (Find(removed) is ClassCircuit gone && gone.Extra == null) heights.Remove(gone);
                    if (string.Equals(airfield.Circuits.DefaultClass, removed, StringComparison.OrdinalIgnoreCase)) airfield.Circuits.DefaultClass = null;
                }
                foreach (ClassesWindow.Saved item in saved.Where(item => item.OriginalName != null && !string.Equals(item.OriginalName, item.Class.Name, StringComparison.Ordinal)))
                {
                    if (Find(item.OriginalName) is ClassCircuit renamed) renamed.Class = item.Class.Name;
                    if (string.Equals(airfield.Circuits.DefaultClass, item.OriginalName, StringComparison.OrdinalIgnoreCase)) airfield.Circuits.DefaultClass = item.Class.Name;
                }
                airfield.Circuits.Classes.Clear();
                airfield.Circuits.Classes.AddRange(heights);
                error = airfield.Save();
                engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
            }
            MakeClassRow(keep);
            ShowEntries();
            ShowBaseTraffic();
            Say(error ?? note ?? "Aircraft classes saved: for the aircraft added from now on.", Area.Aerodrome);
        }

        private void ApplyMetar(bool quiet)
        {
            string metar = metarBox.Text.Trim();
            settings.Metar = metar;
            if (metar.Length == 0)
            {
                engine.SetWind(0, 0, 0);
            }
            else if (MetarWind.TryParse(metar, out double from, out double speedKt, out double gust))
            {
                engine.SetWind(from, speedKt, gust);
            }
            else if (!quiet)
            {
                Say("No wind found in the METAR (for example 14012G22KT).");
                ShowMetar();
                return;
            }
            (double f, double s, double g) = engine.Wind;
            windText.Text = s < 0.5 ? "Wind: calm" : (engine.WindVariable ? $"Wind: variable {s:0} kt (changing slowly)" : $"Wind: {f:000}° {s:0} kt") + (g > 0 ? $", gusts +{g:0}" : "");
            ShowMetar();
            if (!quiet) Say(null);
        }

        /// <summary>The METAR at the top of the map: whole, or only the wind and the QNH.</summary>
        private void ShowMetar()
        {
            string metar = settings.Metar.Trim();
            view.Metar = settings.MetarWindQnhOnly ? MetarWind.WindAndQnh(metar) : metar;
            view.InvalidateVisual();
        }

        /// <summary>The METAR of the aerodrome from the internet (aviationweather.gov), only when asked: into the box, applied.</summary>
        private async void GetMetar()
        {
            if (airfield.Map == null) { Say("Open a map first: its ICAO code is asked."); return; }
            string code = airfield.Code.Trim().ToUpperInvariant();
            if (code.Length != 4 || !code.All(char.IsAsciiLetterOrDigit)) { Say($"The map's code \"{code}\" is not an ICAO code (4 letters): write the METAR in the box."); return; }
            Say($"Asking the METAR of {code} on the internet...");
            string? metar = await OnlineMetar.FetchAsync(code, appVersion);
            if (metar == null)
            {
                Say($"No METAR of {code} from the internet (no connection, or no METAR for it). You can write one in the box.");
                return;
            }
            metarBox.Text = metar;
            ApplyMetar(quiet: false);
            Say($"METAR {code} {OnlineMetar.TimeOf(metar)} from the internet (aviationweather.gov).");
        }

        // ----- Simulation ------------------------------------------------------------------------------------------------

        private void TogglePause()
        {
            engine.Paused = !engine.Paused;
            UpdatePause();
        }

        private void UpdatePause()
        {
            pauseButton.Content = engine.Paused ? "Run" : "Pause";
            trafficPauseButton.Content = engine.Paused ? "RUN" : "PAUSE";
        }

        private void ApplyDistances()
        {
            if (double.TryParse(longFinalBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double final)) engine.LongFinalNM = final;
            if (double.TryParse(initialPointBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double ip)) engine.InitialPointNM = ip;
            settings.LongFinalNM = engine.LongFinalNM;
            settings.InitialPointNM = engine.InitialPointNM;
            view.LongFinalNM = engine.LongFinalNM;
            // (Kept with the aerodrome: they depend on the field.)
            if (airfield.Map != null)
            {
                airfield.Circuits.LongFinalNM = engine.LongFinalNM;
                airfield.Circuits.InitialPointNM = engine.InitialPointNM;
                Say(airfield.Save());
            }
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>The magnetic variation of the aerodrome (the user, 5 Nov): 3E, 2W, -2 (west), empty 0; kept in its file.</summary>
        private void ApplyVariation()
        {
            string text = variationBox.Text.Trim().ToUpperInvariant().Replace(',', '.');
            double sign = 1;
            if (text.EndsWith('W')) { sign = -1; text = text[..^1].Trim(); }
            else if (text.EndsWith('E')) text = text[..^1].Trim();
            double variation = text.Length == 0 ? 0
                : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && Math.Abs(v) <= 60 ? sign * v : double.NaN;
            if (double.IsNaN(variation))
            {
                Say("The variation: degrees with E or W (for example 3E).");
                variationBox.Text = VariationText(airfield.Circuits.MagneticVariation ?? 0);
                return;
            }
            if (airfield.Map != null && (airfield.Circuits.MagneticVariation ?? 0) != variation)
            {
                airfield.Circuits.MagneticVariation = variation == 0 ? null : variation;
                Say(airfield.Save());
            }
            variationBox.Text = VariationText(variation);
            AutoExitTurn();
        }

        private static string VariationText(double variation) =>
            variation == 0 ? "0" : Math.Abs(variation).ToString("0.#", CultureInfo.InvariantCulture) + (variation > 0 ? "E" : "W");

        private static void RadialTip(TextBox box) =>
            box.ToolTip = "The radial (degrees magnetic, outbound) it intercepts after the take-off, from the middle of the runway or the point clicked.";

        /// <summary>The radial written (degrees magnetic; 360 = 0, kept as written, the user 6 Nov), or NaN.</summary>
        private double RadialWritten() =>
            double.TryParse(radialBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double r) && r >= 0 && r <= 360 ? r : double.NaN;

        /// <summary>The way out written changed (the way, its radial, its R/D point): the turn by itself again.</summary>
        private void WayOutChanged()
        {
            turnChosen = false;
            AutoExitTurn();
        }

        /// <summary>The turn to the way out by itself (the user, 6 Nov), unless clicked by hand: see <see cref="ExitSideRight"/>.</summary>
        private void AutoExitTurn()
        {
            if (turnChosen || ExitSideRight() is not bool right) return;
            departTurnRow.Show(right);
        }

        /// <summary>
        /// The side of the runway axis (seen from the take-off) the exit of Traffic generation is on (the user, 6 Nov): right true;
        /// on the axis (0.3 NM) or the opposite radial: the aerodrome's circuit side; null: none (RWY HDG, nothing written).
        /// </summary>
        private bool? ExitSideRight()
        {
            if (double.IsNaN(airfield.Course) || !toRow.HasValue) return null;
            string to = toRow.Value ?? "";
            bool circuitRight = (airfield.Runway != null ? airfield.Circuits.SideOf(airfield.Runway.Runway, CircuitType.Conventional) : null) == CircuitSide.Right;
            if (to == ExitWay.RunwayHeading) return null;
            if (to == ExitWay.Radial)
            {
                double radial = RadialWritten();
                if (double.IsNaN(radial)) return null;
                double toward = SimEngine.Wrap(radial + (airfield.Circuits.MagneticVariation ?? 0) - airfield.Course);
                return Math.Abs(toward) < 3 || Math.Abs(toward) > 177 ? circuitRight : toward > 0;
            }
            if ((to == From.Point ? RdPoint(Typed(toRadialBox, 0, 360), Typed(toDistanceBox, 0.1, 200)) : PlaceOf(to, null, null)) is not (double x, double y)) return null;
            // (The plane from the threshold: right of the axis positive.)
            double course = airfield.Course * Math.PI / 180;
            double lateral = x * Math.Cos(course) - y * Math.Sin(course);
            return Math.Abs(lateral) < 0.3 ? circuitRight : lateral > 0;
        }

        /// <summary>The plan made of the rows of group 1 now.</summary>
        private TowerPlan PlanOfRows()
        {
            Flight Type() => typeRow.HasValue ? typeRow.Value : Flight.Arrival;
            int Laps() => lapsRow.HasValue ? lapsRow.Value : 1;
            EndAction OnFinal() => onFinalRow.HasValue ? onFinalRow.Value : EndAction.FullStop;
            double delay = double.TryParse(delayBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d >= 0 ? d : 0;
            // (Departures and transits using the runway: the four ways out, the turn, the exit altitude.)
            bool RunwayWay() => Type() == Flight.Departure || (Type() == Flight.Transit && viaRow.HasValue && viaRow.Value == Via.Circuits);
            bool UsesPad() => OnFinal() == PadEnd || PadDeparture();
            return new TowerPlan
            {
                Callsign = callsignBox.Text.Trim().ToUpperInvariant(),
                Class = classRow.Value ?? "",
                // (None written: the aerodrome's base traffic, Settings › Aircraft, the user 7 Nov.)
                IcaoType = icaoTypeBox.Text.Trim().Length > 0 ? icaoTypeBox.Text.Trim().ToUpperInvariant() : (airfield.Circuits.IcaoType ?? "").Trim().ToUpperInvariant(),
                Ifr = rulesRow.HasValue && rulesRow.Value,
                From = fromRow.Value ?? From.LongFinal,
                Entry = entryRow.Value,
                Circuit = circuitRow.Value,
                Side = sideRow.Value,
                // (The plan of 5 Nov: On Final and Circuits; End and Laps only for the scenarios of before.)
                End = OnFinal() == PadEnd ? EndAction.FullStop : OnFinal(),
                OnFinal = OnFinal() == PadEnd ? EndAction.FullStop : OnFinal(),
                ToPad = OnFinal() == PadEnd,
                PadName = UsesPad() && padRow.HasValue && padRow.Value is not (PadPointName or PadRunwayName) ? padRow.Value ?? "" : "",
                PadX = UsesPad() && padRow.Value == PadPointName ? padPoint?.X : null,
                PadY = UsesPad() && padRow.Value == PadPointName ? padPoint?.Y : null,
                FromPad = PadDeparture() && padRow.HasValue && padRow.Value != PadRunwayName,
                WayOut = true,
                Circuits = (OnFinal() is EndAction.FullStop or PadEnd) && (Type() is Flight.Arrival or Flight.Local) ? 0 : Laps(),
                Local = Type() == Flight.Local,
                TransitCircuits = Type() == Flight.Transit && viaRow.Value == Via.Circuits,
                Formation = formationRow.HasValue ? formationRow.Value : 1,
                Lands = null,
                SplitSeconds = splitRow.HasValue ? splitRow.Value : 3,
                Transit = Type() == Flight.Transit,
                Departure = Type() == Flight.Departure,
                // (The way out of a departure or a transit, the user 6 Nov: RWY HDG, RADIAL, a gate, an R/D point.)
                Radial = (Type() is Flight.Departure or Flight.Transit) && toRow.HasValue && toRow.Value == ExitWay.Radial ? RadialWritten() : null,
                // (The turn to every way out but the runway heading, the user 6 Nov.)
                TurnRight = RunwayWay() && toRow.HasValue && toRow.Value != ExitWay.RunwayHeading && departTurnRow.HasValue ? departTurnRow.Value : null,
                ExitAltFt = RunwayWay() && double.TryParse(exitAltBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double e) && e - HeightOffset > 0 ? e - HeightOffset : null,
                RadialFromX = radialFrom?.X,
                RadialFromY = radialFrom?.Y,
                To = (Type() is Flight.Departure or Flight.Transit) && toRow.HasValue ? toRow.Value ?? "" : "",
                FromRadial = fromRow.Value == From.Point ? Typed(fromRadialBox, 0, 360) : null,
                FromDistance = fromRow.Value == From.Point ? Typed(fromDistanceBox, 0.1, 200) : null,
                ToRadial = toRow.HasValue && toRow.Value == From.Point ? Typed(toRadialBox, 0, 360) : null,
                ToDistance = toRow.HasValue && toRow.Value == From.Point ? Typed(toDistanceBox, 0.1, 200) : null,
                Overhead = Type() == Flight.Transit && viaRow.Value == Via.Overhead,
                // (Written as shown: altitudes with QNH, the user 6 Nov.)
                HeightFt = double.TryParse(arrivalBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && h - HeightOffset > 0 ? h - HeightOffset : null,
                HighKeyFt = circuitRow.Value == CircuitType.Sfo && double.TryParse(hkBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double k) && k - HeightOffset > 0 ? k - HeightOffset : null,
                DelayMinutes = delay
            };
        }

        private void AddAircraft()
        {
            TowerPlan plan = PlanOfRows();
            if (StartNow(plan) is not string added) return;
            Say(plan.ToPad && PadOf(plan) == null ? "No helipad yet: choose it in flight with HPAD, else it lands on the runway."
                : plan.FromPad && plan.Departure && PadOf(plan) == null ? "No helipad clicked: it takes off from the runway." : null, Area.New);
            callsignBox.Text = "";
        }

        /// <summary>A plan flown at once (ADD NOW, Quick): in the Traffic List, its aircraft added, selected; null when it could not start (said).</summary>
        private string? StartNow(TowerPlan plan)
        {
            string? error = Launch(plan, out string added);
            if (error != null) { Say(error, Area.New); return null; }
            // (The user, 3 Nov: the aircraft added at once are in the Traffic List too, flying.)
            Started(plan, added);
            // (The user, 7 Nov: a plan of the list like the others, so ✕ or its end puts it back in WAITING; with its callsign.)
            if (plan.Callsign.Trim().Length == 0 && plan.Formation <= 1) plan.Callsign = added;
            plans.Add(plan);
            ShowPlans();
            view.SetTraffic(engine.Snapshot(), engine.Time);
            view.Select(added);
            return added;
        }

        // ----- Quick traffic (the user, 7 Nov) ----------------------------------------------------------------------------

        private enum QuickKind { LongFinal, InboundIp, Departure, LocalConventional, LocalWide }

        /// <summary>The Quick tab or the Advanced one of Traffic generation (saved).</summary>
        private void ShowTab()
        {
            tabRow.Show(settings.QuickTab);
            quickContent.Visibility = settings.QuickTab ? Visibility.Visible : Visibility.Collapsed;
            advancedContent.Visibility = settings.QuickTab ? Visibility.Collapsed : Visibility.Visible;
            ShowPointMarks();
        }

        /// <summary>The Quick tab: one click, one aircraft at once, VFR, the base traffic of the aerodrome (Settings › Aircraft).</summary>
        private void BuildQuick()
        {
            quickContent.Children.Add(quickBase);
            QuickRow("LONG FIN", QuickKind.LongFinal, "From the long final, straight in, full stop.");
            QuickRow("INBOUND IP", QuickKind.InboundIp, "From the long final at the altitude of the initial (IP), kept to the IP, then the overhead, full stop.");
            QuickRow("DEPARTURE", QuickKind.Departure, "From the runway, away along the runway heading, up to 3000 ft QNH.");
            QuickRow("LOCAL CONV", QuickKind.LocalConventional, "From the runway: take-off, a conventional circuit, full stop.");
            QuickRow("LOCAL WIDE", QuickKind.LocalWide, "From the runway: take-off, a wide circuit, full stop.");
            Note("The circuit side of the aerodrome. In the Traffic List (FLYING) until it is gone. Every other flight: Advanced.", quickContent);
        }

        private void QuickRow(string text, QuickKind kind, string what)
        {
            Button button = ActionButton(text, () => AddQuick(kind, text));
            button.MinWidth = 92;
            button.ToolTip = what;
            button.VerticalAlignment = VerticalAlignment.Top;
            Grid row = new() { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(button);
            TextBlock note = new() { Text = what, Foreground = DimBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 2, 0, 0) };
            Grid.SetColumn(note, 1);
            row.Children.Add(note);
            quickContent.Children.Add(row);
        }

        /// <summary>A Quick aircraft now (the user, 7 Nov): the class, the ICAO type and the callsign of the aerodrome's base traffic; F/S.</summary>
        private void AddQuick(QuickKind kind, string name)
        {
            bool runway = kind is QuickKind.Departure or QuickKind.LocalConventional or QuickKind.LocalWide;
            TowerPlan plan = new()
            {
                Class = QuickClass(out _) ?? "",
                IcaoType = (airfield.Circuits.IcaoType ?? "").Trim().ToUpperInvariant(),
                Ifr = false,
                WayOut = true,
                From = runway ? From.Runway : From.LongFinal,
                Entry = kind switch { QuickKind.LongFinal => CircuitEntry.Final, QuickKind.InboundIp => CircuitEntry.InitialPoint, _ => CircuitEntry.Downwind },
                Circuit = kind switch { QuickKind.InboundIp => CircuitType.Overhead, QuickKind.LocalWide => CircuitType.Wide, _ => CircuitType.Conventional },
                End = EndAction.FullStop,
                OnFinal = EndAction.FullStop,
                Circuits = 0,
                Local = kind is QuickKind.LocalConventional or QuickKind.LocalWide,
                Departure = kind == QuickKind.Departure,
                To = kind == QuickKind.Departure ? ExitWay.RunwayHeading : "",
                Formation = 1
            };
            if (StartNow(plan) is string added) Say($"{added} started: {name}.", Area.New);
        }

        /// <summary>An aircraft of a plan, now: from its gate, the runway or already in the circuit. Returns an error, or null.</summary>
        private string? Launch(TowerPlan plan, out string added)
        {
            plan.Upgrade();
            string? error = LaunchOne(plan, out added);
            if (error != null) return error;
            PlanOfCircuits(plan, added);
            // (The SFO: the height written is its High Key, the user 6 Nov.)
            if (!plan.Transit && plan.HighKeyFt is double highKey && engine.Get(added)?.Circuit == CircuitType.Sfo) commands.Height(added, highKey);
            // (A helicopter departing from a helipad, the user 6 Nov: still there, its hover, then as from the runway.)
            if (plan.FromPad && plan.Departure && plan.Formation <= 1 && PadOf(plan) is { } padFrom) commands.TakeOffFromPad(added, padFrom.X, padFrom.Y);
            if (plan.ToPad && PadOf(plan) is { } pad) commands.Helipad(added, pad.X, pad.Y);
            // (No pad yet, the user 5 Nov: it starts anyway, HPAD? on its label; none given by the final: the runway.)
            else if (plan.ToPad) commands.WantPad(added);
            if (plan.Formation <= 1) return null;
            // A formation: the aircraft added leads it (its wingmen after its number).
            error = commands.MakeFormation(added, plan.Formation, plan.Lands, plan.SplitSeconds);
            if (error != null)
            {
                commands.Remove(added);
                added = "";
            }
            return error;
        }

        /// <summary>
        /// The circuits of the plan of 5 Nov in the engine (finals counted there): arrivals and locals land at the last final
        /// (F/S: at the first one; Circuits more); departures and crossings with circuits leave after their last final.
        /// </summary>
        private void PlanOfCircuits(TowerPlan plan, string added)
        {
            int circuits = plan.Circuits ?? 0;
            EndAction each = plan.OnFinal is EndAction.TouchAndGo or EndAction.GoAround or EndAction.LowPass ? plan.OnFinal : EndAction.TouchAndGo;
            if (plan.Transit)
            {
                if (!plan.TransitCircuits) return;
                (double radial, (double X, double Y)? exit, _) = ExitOf(plan);
                // (From the long final its approach first, then its circuits: OUT = the approach only, the user 6 Nov.)
                commands.SetLaps(added, plan.From == From.LongFinal ? Math.Max(0, circuits) + 1 : Math.Max(1, circuits), each, each);
                commands.LeaveAfterCircuits(added, radial, plan.TurnRight, plan.RadialFromX, plan.RadialFromY, plan.ExitAltFt, exit?.X ?? double.NaN, exit?.Y ?? double.NaN);
                return;
            }
            if (plan.Departure)
            {
                if (circuits <= 0) return;
                (double radial, (double X, double Y)? exit, _) = ExitOf(plan);
                commands.SetLaps(added, circuits, each, each);
                commands.LeaveAfterCircuits(added, radial, plan.TurnRight, plan.RadialFromX, plan.RadialFromY, plan.ExitAltFt, exit?.X ?? double.NaN, exit?.Y ?? double.NaN);
                return;
            }
            if (plan.OnFinal == EndAction.FullStop) commands.SetLaps(added, 1, EndAction.FullStop, EndAction.TouchAndGo);
            else if (circuits == TowerPlan.Forever) commands.SetLaps(added, 0, EndAction.FullStop, each);
            else commands.SetLaps(added, circuits + 1, EndAction.FullStop, each);
        }

        private const string PadPointName = "POINT";

        /// <summary>The helipad of a plan: the registered one of its name, or its point (null: none found).</summary>
        private (double X, double Y)? PadOf(TowerPlan plan)
        {
            foreach ((string name, double px, double py) in airfield.Helipads)
                if (plan.PadName.Length > 0 && string.Equals(name, plan.PadName, StringComparison.OrdinalIgnoreCase)) return (px, py);
            return plan.PadX is double x && plan.PadY is double y ? (x, y) : null;
        }

        /// <summary>The row of the helipads of Traffic generation built again (the registered ones, then POINT).</summary>
        private void RebuildPads()
        {
            string? was = padRow.HasValue ? padRow.Value : null;
            padRow = new ChoiceRow<string>([(PadRunwayName, "RWY"), .. airfield.Helipads.Select(h => (h.Name, h.Name)), (PadPointName, "POINT")],
                (value, _) => value == PadPointName ? "A point clicked on the map (click POINT again for another one)."
                    : value == PadRunwayName ? "A helicopter departure from the runway, as an aeroplane."
                    : $"The helipad {value} (a departure: still there, a hover of {SimEngine.PadHoverFt:0} ft, then away along the runway).");
            padRow.Picked += value =>
            {
                if (value == PadPointName) view.AskPoint(PadDeparture() ? "Helipad: click where the helicopter takes off  -  Esc: cancel" : "Helipad: click where the helicopter lands  -  Esc: cancel", (x, y) => padPoint = (x, y));
            };
            padRow.Show(was != null && (was is PadPointName or PadRunwayName || airfield.Helipads.Any(h => h.Name == was)) ? was : airfield.Helipads.Count > 0 ? airfield.Helipads[0].Name : PadPointName);
            if (departLine != null) ShowEntries();
            padHolder.Children.Clear();
            padHolder.Children.Add(padRow);
        }

        /// <summary>The radial of a departure in degrees true (magnetic + the variation), or NaN: the runway heading.</summary>
        private double RadialTrue(TowerPlan plan) =>
            plan.Radial is double written && !double.IsNaN(written) ? written + (airfield.Circuits.MagneticVariation ?? 0) : double.NaN;

        private string? LaunchOne(TowerPlan plan, out string added)
        {
            added = "";
            string? callsign = plan.Callsign.Length > 0 ? plan.Callsign : null;
            string? kind = plan.Class.Length > 0 ? plan.Class : null;
            if (plan.Radial is double r && double.IsNaN(r)) return "Write the radial (0 to 360, magnetic) in Radial °M.";
            if (plan.Departure && (plan.Circuits ?? 0) <= 0)
            {
                // A departure away at once (the user, 5 Nov: OUT): the runway heading, the radial (magnetic: to true here), or
                // straight to a gate or an R/D point (the user, 6 Nov).
                (double radial, (double X, double Y)? point, string? wrong) = ExitOf(plan);
                if (wrong != null) return wrong;
                return commands.AddDeparture(radial, plan.TurnRight, plan.RadialFromX, plan.RadialFromY, plan.ExitAltFt, kind, callsign, out added,
                    point?.X ?? double.NaN, point?.Y ?? double.NaN);
            }
            // (A departure with circuits to an R/D point not written or a gate no more there: said, not started; the review of 6 Nov.)
            if ((plan.Departure || (plan.Transit && plan.TransitCircuits)) && ExitOf(plan).Error is string exitWrong) return exitWrong;
            if (plan.From == From.Runway)
            {
                CircuitType type = plan.Circuit is CircuitType.Wide ? CircuitType.Wide : CircuitType.Conventional;
                return commands.Add(CircuitStart.Runway, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (plan.Transit)
            {
                (double radial, (double X, double Y)? exit, string? wrong) = ExitOf(plan);
                if (wrong != null) return wrong;
                if (plan.TransitCircuits)
                {
                    // (The user, 6 Nov: from the long final, to do something on the runway, then away.)
                    if (plan.From == From.LongFinal)
                    {
                        if (plan.Entry == CircuitEntry.Circling) return commands.Add(CircuitStart.Circling, CircuitType.Conventional, plan.Side, plan.End, kind, callsign, false, out added);
                        CircuitType type = plan.Circuit is CircuitType.Overhead or CircuitType.Sfo ? CircuitType.Conventional : plan.Circuit;
                        return commands.Add(CircuitStart.LongFinal, type, plan.Side, plan.End, kind, callsign, false, out added);
                    }
                    // (With circuits, the user 5 Nov: into the circuit from its start, out later.)
                    if (PlaceOf(plan.From, plan.FromRadial, plan.FromDistance) is not (double sx, double sy)) return StartMissing(plan);
                    return commands.AddAtGate(sx, sy, plan.Entry, plan.Circuit, plan.Side, plan.End, kind, callsign, plan.HeightFt, out added);
                }
                if (PlaceOf(plan.From, plan.FromRadial, plan.FromDistance) is not (double fx, double fy)) return StartMissing(plan);
                if (exit is not (double tx, double ty)) return "A crossing goes to a gate or a point (Exit to).";
                if (Math.Abs(fx - tx) < 0.05 && Math.Abs(fy - ty) < 0.05) return "A crossing goes to another place (Exit to).";
                return commands.AddTransit(fx, fy, tx, ty, plan.Overhead, plan.HeightFt, kind, callsign, out added);
            }
            if (plan.From == From.LongFinal)
            {
                if (plan.Entry == CircuitEntry.Circling) return commands.Add(CircuitStart.Circling, CircuitType.Conventional, plan.Side, plan.End, kind, callsign, false, out added);
                // (Quick INBOUND IP, the user 7 Nov: from the long final at the initial's altitude, the overhead.)
                if (plan.Entry == CircuitEntry.InitialPoint && plan.Circuit == CircuitType.Overhead) return commands.AddInboundIp(plan.Side, plan.End, kind, callsign, out added);
                CircuitType type = plan.Circuit is CircuitType.Overhead or CircuitType.Sfo ? CircuitType.Conventional : plan.Circuit;
                return commands.Add(CircuitStart.LongFinal, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (From.IsInCircuit(plan.From))
            {
                CircuitStart start = plan.Entry switch
                {
                    CircuitEntry.InitialPoint => CircuitStart.InitialPoint,
                    CircuitEntry.HighKey => CircuitStart.HighKey,
                    CircuitEntry.Final => CircuitStart.LongFinal,
                    _ => CircuitStart.Downwind
                };
                return commands.Add(start, plan.Circuit, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (PlaceOf(plan.From, plan.FromRadial, plan.FromDistance) is not (double gx, double gy)) return StartMissing(plan);
            return commands.AddAtGate(gx, gy, plan.Entry, plan.Circuit, plan.Side, plan.End, kind, callsign, plan.HeightFt, out added);
        }

        private static string StartMissing(TowerPlan plan) =>
            plan.From == From.Point ? "Click the start point on the map (Start point: CLICK ON MAP)." : $"There is no gate {plan.From} at this aerodrome.";

        // ----- Flight plans (advanced generation) ----------------------------------------------------------------------

        private void AddPlan()
        {
            TowerPlan plan = PlanOfRows();
            if (plan.Radial is double radial && double.IsNaN(radial))
            {
                Say("Write the radial (0 to 360, magnetic) in Radial °M.", Area.New);
                return;
            }
            if (editing != null && plans.IndexOf(editing) is int at && at >= 0)
            {
                // UPDATE: the plan changed in place, in the same place of the list.
                // (The student's notes on its strip follow the plan changed, the review of 6 Nov.)
                stripBay.Rekey(plans[at], plan);
                plans[at] = plan;
                Say($"{PlanText(plan)}: changed.", Area.Traffic);
            }
            else plans.Add(plan);
            EndEdit();
            callsignBox.Text = "";
            turnChosen = false;
            ShowPlans();
        }

        /// <summary>A plan of WAITING into Traffic generation, to change it (UPDATE) or to start a copy (ADD NOW).</summary>
        private void EditPlan(TowerPlan p)
        {
            editing = p;
            // (Its rows are in Advanced, the user 7 Nov.)
            if (settings.QuickTab)
            {
                settings.QuickTab = false;
                settings.Save();
                ShowTab();
            }
            p.Upgrade();
            // (PTRN is gone, the user 5 Nov: such an old plan into the first gate.)
            if (p.From.Length > 0) fromRow.Show(From.IsInCircuit(p.From) ? DefaultFrom() : p.From);
            typeRow.Show(p.Transit ? Flight.Transit : p.Departure ? Flight.Departure : p.Local ? Flight.Local : Flight.Arrival);
            departRow.Show(p.Radial != null);
            radialBox.Text = p.Radial is double r && !double.IsNaN(r) ? r.ToString("0", CultureInfo.InvariantCulture) : "";
            radialFrom = p.RadialFromX is double fx && p.RadialFromY is double fy ? (fx, fy) : null;
            radialFromText.Text = radialFrom != null ? "from the point clicked" : "from the middle of the runway";
            // (The way out; a departure of before: To empty, the radial written or not.)
            toRow.Show(p.To.Length > 0 ? p.To : p.Radial != null ? ExitWay.Radial : ExitWay.RunwayHeading);
            fromRadialBox.Text = Number(p.FromRadial);
            fromDistanceBox.Text = Number(p.FromDistance);
            toRadialBox.Text = Number(p.ToRadial);
            toDistanceBox.Text = Number(p.ToDistance);
            viaRow.Show(p.TransitCircuits ? Via.Circuits : p.Overhead ? Via.Overhead : Via.Direct);
            entryRow.Show(p.Entry);
            circuitRow.Show(p.Circuit);
            sideRow.Show(p.Side);
            if (p.Class.Length > 0)
            {
                // (A class no more there, removed or renamed in Aircraft classes: said, the default one chosen.)
                string? known = engine.Classes.FirstOrDefault(c => string.Equals(c.Name, p.Class, StringComparison.OrdinalIgnoreCase))?.Name;
                if (known != null) classRow.Show(known);
                else
                {
                    MakeClassRow(null);
                    Say($"Class {p.Class} no longer exists: {classRow.Value} chosen.", Area.New);
                }
            }
            onFinalRow.Show(p.ToPad ? PadEnd : p.OnFinal is EndAction.FullStop or EndAction.TouchAndGo or EndAction.GoAround or EndAction.LowPass ? p.OnFinal : EndAction.FullStop);
            if (p.ToPad || p.FromPad)
            {
                padPoint = p.PadX is double px && p.PadY is double py ? (px, py) : padPoint;
                padRow.Show(p.PadName.Length > 0 && airfield.Helipads.Any(h => string.Equals(h.Name, p.PadName, StringComparison.OrdinalIgnoreCase)) ? airfield.Helipads.First(h => string.Equals(h.Name, p.PadName, StringComparison.OrdinalIgnoreCase)).Name : PadPointName);
            }
            else if (p.Departure) padRow.Show(PadRunwayName);
            int circuits = p.Circuits ?? 0;
            lapsRow.Show(circuits == 0 && !p.Departure && !(p.Transit && p.From == From.LongFinal) ? 1 : circuits);
            formationRow.Show(Math.Clamp(p.Formation, 1, SimEngine.MaxFormation));
            splitRow.Show(p.SplitSeconds is 3.0 or 4.0 or 5.0 or 10.0 ? p.SplitSeconds : 3.0);
            intervalLine.Visibility = p.Formation > 1 ? Visibility.Visible : Visibility.Collapsed;
            callsignBox.Text = p.Callsign;
            icaoTypeBox.Text = p.IcaoType;
            rulesRow.Show(p.Ifr);
            arrivalBox.Text = p.HeightFt is double h ? (h + HeightOffset).ToString("0", CultureInfo.InvariantCulture) : "";
            hkBox.Text = p.HighKeyFt is double k ? (k + HeightOffset).ToString("0", CultureInfo.InvariantCulture) : "";
            exitAltBox.Text = p.ExitAltFt is double ea ? (ea + HeightOffset).ToString("0", CultureInfo.InvariantCulture) : "";
            delayBox.Text = p.DelayMinutes.ToString("0.#", CultureInfo.InvariantCulture);
            // (Its own pad kept, not RWY by default.)
            wasPadDeparture = PadDeparture();
            ShowEntries();
            // (Its turn as saved, kept; none saved: by itself.)
            if (p.TurnRight is bool right)
            {
                departTurnRow.Show(right);
                turnChosen = true;
            }
            else WayOutChanged();

            toListButton.Content = "UPDATE";
            toListButton.ToolTip = "The plan of the Traffic List changed as in these rows (in the same place).";
            cancelEditButton.Visibility = Visibility.Visible;
            Say($"Changing {PlanText(p)}: UPDATE to keep the changes, CANCEL to leave it as it was.", Area.New);
            ShowPlans();
        }

        private void CancelEdit()
        {
            EndEdit();
            Say(null, Area.New);
            ShowPlans();
        }

        private void EndEdit()
        {
            editing = null;
            // (The turn of the plan changed was its own: the next ones by themselves again, the review of 7 Nov.)
            turnChosen = false;
            AutoExitTurn();
            toListButton.Content = "TO LIST";
            toListButton.ToolTip = Tips["TO LIST"];
            cancelEditButton.Visibility = Visibility.Collapsed;
        }

        private string PlanText(TowerPlan p)
        {
            p.Upgrade();
            static string Short(EndAction end) => TowerCommands.Ends.FirstOrDefault(e => e.End == end).Short ?? "";
            string side = p.Side is CircuitSide s ? (s == CircuitSide.Left ? " L" : " R") : "";
            string circuit = TowerCommands.Circuits.FirstOrDefault(c => c.Type == p.Circuit).Short ?? "";
            int circuits = p.Circuits ?? 0;
            string entry = p.From is From.Runway ? "T/O" : p.From is From.LongFinal ? (p.Entry == CircuitEntry.Circling ? "CIRCLING" : p.Circuit == CircuitType.Overhead ? "IP" : "FINAL") : p.Entry.ToString().ToUpperInvariant();
            string height = (p.HeightFt is double hf ? $" {hf + HeightOffset:0} ft" : "") + (p.HighKeyFt is double hk ? $" HK {hk + HeightOffset:0} ft" : "");
            string team = p.Formation > 1 ? $" F{p.Formation}" : "";
            string type = p.IcaoType.Length > 0 ? p.IcaoType : p.Class;
            string who = $"{(p.Callsign.Length > 0 ? p.Callsign : "(auto)")}{team}{(type.Length > 0 ? " " + type : "")} {(p.Ifr ? "I" : "V")}";
            string delay = $"+{p.DelayMinutes:0.#}'";
            // (The way out, the user 6 Nov: a gate, an R/D point, the runway heading, a radial.)
            string way = p.To == From.Point ? RdText(p.ToRadial, p.ToDistance)
                : p.To.Length > 0 && p.To is not (ExitWay.RunwayHeading or ExitWay.Radial) ? p.To
                : p.Radial is double r ? $"R{r:000}°M {(p.TurnRight == false ? "L" : "R")}{(p.RadialFromX != null ? " (point)" : "")}" : "RWY HDG";
            string start = p.From == From.Point ? RdText(p.FromRadial, p.FromDistance) : p.From == From.LongFinal ? "LF" : p.From;
            // (The exit altitude, the user 6 Nov; a helicopter from a helipad.)
            string exitAlt = p.ExitAltFt is double ea ? $" {ea + HeightOffset:0} ft" : "";
            string turn = p.TurnRight is bool tr && p.To != ExitWay.Radial && p.To != ExitWay.RunwayHeading && p.To.Length > 0 ? (tr ? " R" : " L") : "";
            string pad = p.FromPad ? $" HPAD {(p.PadName.Length > 0 ? p.PadName : "point")}" : "";
            if (p.Transit)
            {
                if (p.TransitCircuits) return $"{who}  TRANSIT {start}{height} → {entry} {circuit}{side} {(circuits <= 0 ? "OUT" : $"{circuits}×")} {Short(p.OnFinal)} → {way}{turn}{exitAlt}  {delay}";
                return $"{who}  TRANSIT {start} → {(p.Overhead ? "OVHD → " : "")}{way}{height}  {delay}";
            }
            if (p.Departure)
            {
                if (circuits <= 0) return $"{who}  DEP{pad} OUT {way}{turn}{exitAlt}  {delay}";
                return $"{who}  DEP{pad} → {circuit}{side} {circuits}× {Short(p.OnFinal)} → OUT {way}{turn}{exitAlt}  {delay}";
            }
            string plan = p.ToPad ? $"HPAD {(p.PadName.Length > 0 ? p.PadName : "point")}" : p.OnFinal == EndAction.FullStop ? Short(EndAction.FullStop)
                : circuits == TowerPlan.Forever ? $"∞ {Short(p.OnFinal)}" : $"{Short(p.OnFinal)} {circuits}× → {Short(EndAction.FullStop)}";
            return $"{who}  {(p.Local ? "LOCAL" : start)}{height} → {entry} {circuit}{side} {plan}  {delay}";
        }

        /// <summary>The rows of the plans, built again (after a change).</summary>
        /// <summary>The aircraft of each plan shown (the list built again when they change: a formation split, one removed).</summary>
        private string plansKey = "";

        /// <summary>The plans waiting (not flying).</summary>
        private List<TowerPlan> Waiting() => plans.Where(p => p.Now != TowerPlan.State.Flying && !p.Quick).ToList();

        private string PlansKey() => string.Join(";", Waiting().Select(p => $"{p.GetHashCode()}{(p.Picked ? "+" : "")}"));

        private static TextBlock Caption(string text) => new() { Text = text, FontSize = 10, Foreground = DimBrush, Margin = new Thickness(0, 6, 0, 1), TextTrimming = TextTrimming.CharacterEllipsis };

        private static TextBlock RowText(string text) => new()
        {
            Text = text, FontFamily = RowFont, FontSize = 11, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = text
        };

        /// <summary>The callsign of a row: bold, a colour of its own, in a column of its own (the user, 4 Nov).</summary>
        private static TextBlock CallsignText(string text) => new()
        {
            Text = text, FontFamily = RowFont, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = CallsignBrush,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
        };

        /// <summary>A thin row: its grip (drag and drop: the order), the callsign, its text, its buttons on the right.</summary>
        private Border ThinRow(StackPanel list, Action<int, int> move, TextBlock callsign, UIElement text, params Button[] buttons)
        {
            Grid row = new() { Margin = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock grip = new()
            {
                Text = "⋮⋮", Foreground = DimBrush, FontSize = 12, Cursor = Cursors.SizeNS, VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent, ToolTip = "Drag up or down: its place in the list (a list of priorities)."
            };
            row.Children.Add(grip);
            Grid.SetColumn(callsign, 1);
            row.Children.Add(callsign);
            Grid.SetColumn(text, 2);
            row.Children.Add(text);
            StackPanel right = new() { Orientation = Orientation.Horizontal };
            foreach (Button button in buttons) right.Children.Add(button);
            Grid.SetColumn(right, 3);
            row.Children.Add(right);
            Border holder = new() { Child = row, BorderBrush = Theme.Chosen, BorderThickness = new Thickness(0) };
            // Drag and drop by the grip (the user, 4 Nov): a gold line where it will go.
            grip.MouseLeftButtonDown += (_, e) =>
            {
                dragRow = holder;
                grip.CaptureMouse();
                e.Handled = true;
            };
            grip.MouseMove += (_, e) =>
            {
                if (dragRow != holder) return;
                MarkDrop(list, DropIndex(list, e.GetPosition(list).Y));
            };
            grip.MouseLeftButtonUp += (_, e) =>
            {
                if (dragRow != holder) return;
                dragRow = null;
                grip.ReleaseMouseCapture();
                int from = list.Children.IndexOf(holder), to = DropIndex(list, e.GetPosition(list).Y);
                MarkDrop(list, -1);
                if (from >= 0 && to != from && to != from + 1) move(from, to);
                e.Handled = true;
            };
            return holder;
        }

        /// <summary>Where a row dropped at this height of the list goes: before the row under it (the count: at the end).</summary>
        private static int DropIndex(StackPanel list, double y)
        {
            for (int i = 0; i < list.Children.Count; i++)
            {
                if (list.Children[i] is not FrameworkElement child) continue;
                double top = child.TranslatePoint(new Point(0, 0), list).Y;
                if (y < top + child.ActualHeight / 2) return i;
            }
            return list.Children.Count;
        }

        /// <summary>The gold line where a dragged row will go (−1: none).</summary>
        private static void MarkDrop(StackPanel list, int at)
        {
            for (int i = 0; i < list.Children.Count; i++)
            {
                if (list.Children[i] is not Border row) continue;
                bool last = i == list.Children.Count - 1;
                row.BorderThickness = new Thickness(0, i == at ? 2 : 0, 0, last && at == list.Children.Count ? 2 : 0);
            }
        }

        /// <summary>A plan of WAITING moved to another place of the list (before the one at <paramref name="to"/>).</summary>
        private void MoveWaiting(int from, int to)
        {
            List<TowerPlan> waiting = Waiting();
            if (from < 0 || from >= waiting.Count) return;
            TowerPlan moved = waiting[from];
            TowerPlan? before = to < waiting.Count ? waiting[to] : null;
            plans.Remove(moved);
            if (before != null) plans.Insert(plans.IndexOf(before), moved);
            else plans.Insert(plans.IndexOf(waiting[^1] == moved ? waiting[^2] : waiting[^1]) + 1, moved);
            ShowPlans();
        }

        /// <summary>An aircraft of FLYING moved to another place of the list.</summary>
        private void MoveFlying(int from, int to)
        {
            if (from < 0 || from >= flyingOrder.Count) return;
            string moved = flyingOrder[from];
            string? before = to < flyingOrder.Count ? flyingOrder[to] : null;
            flyingOrder.RemoveAt(from);
            flyingOrder.Insert(before != null ? flyingOrder.IndexOf(before) : flyingOrder.Count, moved);
            ShowFlying(engine.Snapshot());
        }

        /// <summary>The callsign of a plan (with its formation) and the rest of its text.</summary>
        private (string Callsign, string Detail) PlanParts(TowerPlan p)
        {
            string callsign = (p.Callsign.Length > 0 ? p.Callsign : "(auto)") + (p.Formation > 1 ? $" F{p.Formation}" : "");
            string text = PlanText(p);
            return (callsign, text.StartsWith(callsign, StringComparison.Ordinal) ? text[callsign.Length..].TrimStart() : text);
        }

        /// <summary>The meaning of the small buttons of a list, on its own line under its caption.</summary>
        private static TextBlock Legend(string text) => new() { Text = text, FontSize = 10, Foreground = DimBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };

        /// <summary>A caption with buttons on its right (the Traffic List, the user 6 Nov).</summary>
        private static DockPanel Header(TextBlock caption, params Button[] buttons)
        {
            DockPanel header = new() { Margin = new Thickness(0, 6, 0, 2) };
            StackPanel right = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            foreach (Button button in buttons) right.Children.Add(button);
            DockPanel.SetDock(right, Dock.Right);
            header.Children.Add(right);
            caption.VerticalAlignment = VerticalAlignment.Bottom;
            caption.Margin = new Thickness(0, 0, 6, 1);
            header.Children.Add(caption);
            return header;
        }

        /// <summary>The ICAO type and the rules (V / I) of an aircraft, from its plan; empty: not known.</summary>
        private (string Type, string Rules) FlightInfo(string callsign)
        {
            TowerPlan? plan = plans.FirstOrDefault(p => string.Equals(p.Flying, callsign, StringComparison.OrdinalIgnoreCase)
                || p.Members.Contains(callsign, StringComparer.OrdinalIgnoreCase));
            return plan == null ? ("", "") : (plan.IcaoType, plan.Ifr ? "I" : "V");
        }

        /// <summary>My VID written (Settings › Network): digits only, kept; in the log when IVAO's list does not give it.</summary>
        private void ApplyVid()
        {
            string vid = vidBox.Text.Trim();
            if (vid.Length > 0 && !vid.All(char.IsAsciiDigit))
            {
                Say("Your VID: digits only (empty: none).");
                vidBox.Text = settings.InstructorVid;
                return;
            }
            if (vid == settings.InstructorVid) return;
            settings.InstructorVid = vid;
            settings.Save();
            // (Only while connected: off the network the log leaves it empty, the user 7 Nov.)
            if (networkCallsign.Length > 0) flightLog.InstructorVid = networkVid.Length > 0 ? networkVid : vid;
            Say(vid.Length > 0 ? $"Your VID {vid} kept (used when IVAO's list does not give it)." : null);
        }

        /// <summary>
        /// The VID of the callsign of Aurora from IVAO's list (the user, 7 Nov): asked when the callsign is new, then every 3
        /// minutes while not found; else the one written in Settings › Network.
        /// </summary>
        private async Task FindVid()
        {
            if (networkCallsign.Length == 0)
            {
                networkVid = vidFor = "";
                flightLog.InstructorVid = "";
                return;
            }
            if (networkCallsign == vidFor && (networkVid.Length > 0 || DateTime.UtcNow - vidAskedAt < TimeSpan.FromMinutes(3))) return;
            if (networkCallsign != vidFor) networkVid = "";
            vidFor = networkCallsign;
            vidAskedAt = DateTime.UtcNow;
            string asked = networkCallsign;
            string? found = await IvaoDirectory.FindVidAsync(asked, appVersion);
            if (asked != networkCallsign) return;
            networkVid = found ?? "";
            string vid = networkVid.Length > 0 ? networkVid : settings.InstructorVid;
            if (vid != flightLog.InstructorVid)
            {
                flightLog.InstructorVid = vid;
                if (vid.Length > 0) flightLog.Event(engine.Time, "VID", detail: $"{asked} {vid}{(networkVid.Length > 0 ? " (IVAO's list)" : " (written)")}", source: "INSTRUCTOR");
            }
        }

        /// <summary>
        /// START SESSION / END SESSION (the user, 8 Nov): the training or the exam in the log (a new file, the time from 0, a
        /// new folder of photos); nothing starts flying. Ended: the summary written, the simulation paused (asked first).
        /// </summary>
        private void ToggleSession()
        {
            if (!flightLog.InSession)
            {
                photoSession = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
                photoCount = 0;
                flightLog.Aerodrome = airfield.Code;
                flightLog.Runway = airfield.Runway?.Runway ?? "";
                flightLog.StartSession(engine.Time, engine.Snapshot(), FlightInfo);
                // (A network trouble going on at the start: written, so its RESTORED has a beginning.)
                if (networkProblem.Length > 0) flightLog.Event(engine.Time, networkProblem, detail: "going on at the start of the session", source: "IVAO");
                Say(flightLog.Error ?? $"Session started: the log in {flightLog.FileName}.");
            }
            else
            {
                if (MessageBox.Show(this, "End the session? Its log is closed with the summary, and the simulation paused.", "TWR Trainer",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                string file = flightLog.FileName;
                flightLog.EndSession(engine.Time);
                // (The photos after it: a folder of their own again.)
                photoSession = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
                photoCount = 0;
                engine.Paused = true;
                UpdatePause();
                Say($"Session ended: {file}");
            }
            ShowSession();
        }

        private void ShowSession()
        {
            if (sessionButton == null) return;
            bool on = flightLog.InSession;
            int seconds = on ? (int)Math.Max(0, engine.Time - flightLog.SessionStart) : -1;
            sessionShown = seconds;
            sessionButton.Content = on ? string.Create(CultureInfo.InvariantCulture, $"END SESSION {seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}") : "START SESSION";
            sessionButton.Background = on ? new SolidColorBrush(Color.FromRgb(0x63, 0x99, 0x22)) : Theme.OffBack;
            sessionButton.Foreground = on ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)) : Theme.OffText;
            sessionButton.BorderBrush = sessionButton.Background;
            sessionButton.Tag = on ? "on" : null;
            sessionButton.ToolTip = on ? "The session of the training or the exam: written in the log (Logs). Click: end it (asked first)."
                : "Start the session of the training or the exam: a new log of the flights, the time from 0:00:00, a new folder of photos. Nothing starts flying.";
        }

        /// <summary>Something went wrong (Program, the user 8 Nov): written in the log of the session; fatal: the session closed.</summary>
        public void ProgramProblem(Exception error, bool fatal) => flightLog.Problem(engine.Time, $"{error.GetType().Name}: {error.Message}", fatal);

        /// <summary>NORTH / RWY: the map north up, or the runway in use across the screen (threshold left, or right); saved.</summary>
        private void SetMapTurn(int turn)
        {
            settings.MapTurn = turn;
            settings.Save();
            ShowMapTurn();
        }

        private void ShowMapTurn()
        {
            int turn = double.IsNaN(airfield.Course) ? 0 : settings.MapTurn;
            view.Rotation = turn == 0 ? 0 : airfield.Course - 90 + (turn == 2 ? 180 : 0);
            view.InvalidateVisual();
            if (northButton == null || runwayButton == null) return;
            foreach ((Button button, bool on) in new[] { (northButton, turn == 0), (runwayButton, turn != 0) })
            {
                button.Background = on ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A)) : Theme.OffBack;
                button.Foreground = on ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)) : Theme.OffText;
                button.BorderBrush = button.Background;
                button.Tag = on ? "on" : null;
            }
            northButton.ToolTip = "The map with the north up.";
            runwayButton.ToolTip = turn == 0 ? "The map turned: the runway in use across the screen, its threshold on the left."
                : $"The runway in use across the screen, its threshold on the {(turn == 1 ? "left" : "right")}. Click: turned round.";
        }

        /// <summary>IVAO on the bar of the map: the traffic of the network from Aurora shown or not (saved).</summary>
        private void ToggleIvao()
        {
            settings.ShowIvao = !settings.ShowIvao;
            settings.Save();
            networkCallsignAt = networkMetarAt = DateTime.MinValue;
            if (!settings.ShowIvao)
            {
                networkCallsign = networkVid = vidFor = "";
                networkProblem = networkMoving = "";
                networkStillSince = null;
                view.Network = [];
                flightLog.ObserveNetwork([], engine.Time);
                flightLog.SetNetwork(false, "", engine.Time);
                view.InvalidateVisual();
            }
            ShowIvaoButton();
            _ = PollNetwork();
        }

        private void ShowIvaoButton()
        {
            if (ivaoButton == null) return;
            bool on = settings.ShowIvao, linked = on && aurora.Connected, network = linked && networkCallsign.Length > 0;
            ivaoButton.Content = !on ? "IVAO" : linked ? "● IVAO" : "IVAO ?";
            ivaoButton.Background = !on ? Theme.OffBack : network ? new SolidColorBrush(Color.FromRgb(0x63, 0x99, 0x22)) : new SolidColorBrush(Color.FromRgb(0xEF, 0x9F, 0x27));
            ivaoButton.Foreground = !on ? Theme.OffText : new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A));
            ivaoButton.BorderBrush = ivaoButton.Background;
            ivaoButton.Tag = on ? "on" : null;
            ivaoButton.ToolTip = !on ? "The traffic of IVAO from Aurora on this PC: off. Click: on (only shown, nothing is sent to the network)."
                : !linked ? "Aurora not found on this PC: open Aurora (with its link for other programs on). Click: off."
                : network ? $"Aurora on IVAO as {networkCallsign}: its traffic on the map (light blue), its METAR used, both in the log. Click: off."
                : "Aurora found, but not connected to IVAO: no traffic yet. Click: off.";
        }

        /// <summary>
        /// The network (every 2 s while IVAO is on): Aurora connected again when needed, its callsign (every 30 s, in the
        /// log), the METAR of the aerodrome (every minute: into the METAR box, applied), the traffic within 60 NM on the map.
        /// </summary>
        private async Task PollNetwork()
        {
            if (networkBusy || !settings.ShowIvao) return;
            networkBusy = true;
            try
            {
                if (!aurora.Connected && !await aurora.TryConnect())
                {
                    networkCallsign = networkVid = vidFor = "";
                    view.Network = [];
                    flightLog.ObserveNetwork([], engine.Time);
                    flightLog.SetNetwork(false, "", engine.Time, row: false);
                    networkCallsignAt = DateTime.MinValue;
                    NetworkCondition("AURORA LOST", "Aurora does not answer (closed, or its link for other programs off)");
                    return;
                }
                if (DateTime.UtcNow - networkCallsignAt > TimeSpan.FromSeconds(30))
                {
                    networkCallsignAt = DateTime.UtcNow;
                    string callsign = await aurora.GetConnectedCallsign() ?? "";
                    // (Turned off meanwhile: nothing more, the review of 7 Nov.)
                    if (!settings.ShowIvao) return;
                    networkCallsign = callsign;
                    flightLog.SetNetwork(networkCallsign.Length > 0, networkCallsign, engine.Time, row: networkCallsign.Length > 0);
                    if (networkCallsign.Length == 0) NetworkCondition("IVAO LOST", "Aurora answers but is not connected to IVAO");
                    await FindVid();
                    if (!settings.ShowIvao) return;
                }
                string code = airfield.Code.Trim().ToUpperInvariant();
                if (airfield.Map != null && code.Length == 4 && DateTime.UtcNow - networkMetarAt > TimeSpan.FromMinutes(1))
                {
                    networkMetarAt = DateTime.UtcNow;
                    if (await aurora.GetMetar(code) is string metar && settings.ShowIvao && metar != settings.Metar.Trim())
                    {
                        metarBox.Text = metar;
                        ApplyMetar(quiet: true);
                    }
                }
                List<(string, double, double, double, double, double)> shown = [];
                List<IvaoAircraft> near = [];
                if (airfield.Map != null)
                {
                    foreach (string callsign in (await aurora.GetTrafficList()).Take(80))
                    {
                        if (!settings.ShowIvao) break;
                        if (await aurora.GetTrafficPosition(callsign) is not IvaoAircraft a) continue;
                        (double x, double y) = airfield.Plane.FromGeo(a.Latitude, a.Longitude);
                        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Sqrt(x * x + y * y) > 60) continue;
                        shown.Add((a.Callsign, x, y, a.AltitudeFt, a.SpeedKt, a.Track));
                        near.Add(a);
                    }
                }
                if (!settings.ShowIvao) return;
                view.Network = shown;
                flightLog.ObserveNetwork(near, engine.Time);
                view.InvalidateVisual();
                // The data stopped (the user, 8 Nov): aircraft that fly (above 30 kt) not moving at all for 30 s; else fine.
                string moving = string.Join("|", near.Where(a => a.SpeedKt > 30).OrderBy(a => a.Callsign, StringComparer.Ordinal)
                    .Select(a => string.Create(CultureInfo.InvariantCulture, $"{a.Callsign}:{a.Latitude:F5}:{a.Longitude:F5}")));
                if (moving.Length > 0 && moving == networkMoving) networkStillSince ??= DateTime.UtcNow;
                else networkStillSince = null;
                networkMoving = moving;
                if (networkCallsign.Length > 0)
                    NetworkCondition(networkStillSince is DateTime still && DateTime.UtcNow - still > TimeSpan.FromSeconds(30) ? "IVAO DATA STOPPED" : "",
                        "no new position of the aircraft flying for 30 s");
            }
            catch (Exception ex)
            {
                // (Anything unexpected from the network: tried again at the next turn, the program goes on; in the log.)
                flightLog.Problem(engine.Time, $"IVAO: {ex.GetType().Name}: {ex.Message}", fatal: false);
            }
            finally
            {
                networkBusy = false;
                ShowIvaoButton();
            }
        }

        /// <summary>
        /// The state of the network data in the log (the user, 8 Nov): AURORA LOST, IVAO LOST, IVAO DATA STOPPED when it begins,
        /// RESTORED with how long it lasted when it is fine again ("": fine).
        /// </summary>
        private void NetworkCondition(string problem, string detail)
        {
            if (problem == networkProblem) return;
            if (problem.Length > 0)
            {
                if (networkProblem.Length == 0) networkProblemSince = DateTime.UtcNow;
                flightLog.Event(engine.Time, problem, detail: detail, source: "IVAO");
            }
            else
            {
                int gap = (int)(DateTime.UtcNow - networkProblemSince).TotalSeconds;
                flightLog.Event(engine.Time, "RESTORED", detail: string.Create(CultureInfo.InvariantCulture, $"after {gap / 60}:{gap % 60:00} ({networkProblem})"), source: "IVAO");
            }
            networkProblem = problem;
        }

        /// <summary>RINGS on the bar of the map: the range rings shown or not (saved).</summary>
        private void ToggleRings()
        {
            settings.ShowRings = !settings.ShowRings;
            settings.Save();
            ShowRings();
        }

        /// <summary>The range rings on the map (their spacing: the aerodrome's, 5 NM when not chosen), the row and the button.</summary>
        private void ShowRings()
        {
            double spacing = airfield.Circuits.RingsNM is double nm && nm is 1 or 2 or 5 or 10 ? nm : 5;
            ringsRow.Show(spacing);
            view.RingsNM = settings.ShowRings ? spacing : 0;
            view.InvalidateVisual();
            if (ringsButton == null) return;
            ringsButton.Content = "RINGS";
            ringsButton.Background = settings.ShowRings ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A)) : Theme.OffBack;
            ringsButton.Foreground = settings.ShowRings ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)) : Theme.OffText;
            ringsButton.BorderBrush = ringsButton.Background;
            ringsButton.Tag = settings.ShowRings ? "on" : null;
            ringsButton.ToolTip = (settings.ShowRings ? "Range rings shown" : "Range rings hidden") + $" (every {spacing:0} NM around the middle of the runway; Settings › Map). Click: "
                + (settings.ShowRings ? "hide them." : "show them.");
        }

        /// <summary>WAITING TFC: the plans waiting on the map or not (saved).</summary>
        private void ToggleWaiting()
        {
            settings.ShowWaiting = !settings.ShowWaiting;
            settings.Save();
            ShowWaitingButton();
            RefreshStrips();
        }

        private void ShowWaitingButton()
        {
            waitingButton.Content = settings.ShowWaiting ? "👁 WAITING TFC" : "⊘ WAITING TFC";
            waitingButton.Background = settings.ShowWaiting ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A)) : Theme.OffBack;
            waitingButton.Foreground = settings.ShowWaiting ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)) : Theme.OffText;
            waitingButton.BorderBrush = waitingButton.Background;
            // (Lit: under the mouse only its border changes, its text stays readable on the yellow.)
            waitingButton.Tag = settings.ShowWaiting ? "on" : null;
            waitingButton.ToolTip = settings.ShowWaiting
                ? "The aircraft waiting are on the map, still, in grey (click one: it goes into Traffic generation). Click: hide them."
                : "The aircraft waiting are not on the map. Click: show them, still, where they will start.";
        }

        /// <summary>A colour row of the strips (Settings › Appearance).</summary>
        private void StripColourRow(string label, Func<string> get, Action<string> set)
        {
            ChoiceRow<string> row = new(StripColours.Select(c => (c.Name, c.Name)), (name, _) => $"{label}: {name.ToLowerInvariant()} strips.");
            row.Show(StripColours.Any(c => c.Name == get()) ? get() : StripColours[0].Name);
            row.Picked += name =>
            {
                set(name);
                settings.Save();
                stripBay.Redraw();
            };
            Row(label, row);
        }

        private Brush StripBrush(StripKind kind)
        {
            string name = kind switch { StripKind.Departure => settings.StripDeparture, StripKind.Arrival => settings.StripArrival, _ => settings.StripLocal };
            Color colour = StripColours.FirstOrDefault(c => c.Name == name).Colour;
            if (colour == default) colour = kind switch { StripKind.Departure => StripColours[0].Colour, StripKind.Arrival => StripColours[1].Colour, _ => StripColours[2].Colour };
            SolidColorBrush brush = new(colour);
            brush.Freeze();
            return brush;
        }

        /// <summary>What a plan's strip shows: kind of flight, entry (the exit of a departure), altitude in hundreds.</summary>
        private (StripKind Kind, string Entry, string Altitude) StripOf(TowerPlan p)
        {
            StripKind kind = p.Departure ? StripKind.Departure : p.Local || p.Transit ? StripKind.Local : StripKind.Arrival;
            string entry = p.Departure
                ? (p.To == From.Point ? RdText(p.ToRadial, p.ToDistance)
                    : p.To.Length > 0 && p.To is not (ExitWay.RunwayHeading or ExitWay.Radial) ? p.To
                    : p.Radial is double r && !double.IsNaN(r) ? $"R{r:000}" : "RWY")
                : p.From switch
                {
                    From.LongFinal => p.Entry == CircuitEntry.Circling ? "CIRC" : "LF",
                    From.Runway => "RWY",
                    From.Point => RdText(p.FromRadial, p.FromDistance),
                    _ when From.IsInCircuit(p.From) => "CCT",
                    _ => p.From
                };
            double? shown = p.Departure ? p.ExitAltFt : p.HeightFt;
            string altitude = shown is double h ? $"{Math.Max(0, Math.Round((h + HeightOffset) / 100)):000}" : "";
            return (kind, entry, altitude);
        }

        /// <summary>
        /// Each second: the strips (Coordination: the plans waiting; In contact: the aircraft of the plans flying, also those
        /// split off a formation) and the plans waiting on the map (WAITING TFC).
        /// </summary>
        private void RefreshStrips()
        {
            List<StripData> coordination = [], contact = [];
            List<WaitingMark> marks = [];
            foreach (TowerPlan p in plans)
            {
                (StripKind kind, string entry, string altitude) = StripOf(p);
                string type = p.IcaoType.Length > 0 ? p.IcaoType : p.Class.ToUpperInvariant();
                if (p.Now == TowerPlan.State.Flying)
                {
                    if (p.Flying.Length > 0) stripBay.Rekey(p, p.Flying);
                    foreach (string member in MembersFlying(p))
                    {
                        // (A formation not split yet: one strip, F2, F3..., the review of 6 Nov.)
                        int count = engine.Get(member)?.Formation?.Count ?? 1;
                        contact.Add(new StripData(member, count > 1 ? $"{member} F{count}" : member, type, p.Ifr, kind, entry, altitude));
                    }
                    continue;
                }
                if (p.Quick) continue;
                string name = (p.Callsign.Length > 0 ? p.Callsign : "(AUTO)") + (p.Formation > 1 ? $" F{p.Formation}" : "");
                coordination.Add(new StripData(p, name, type, p.Ifr, kind, entry, altitude));
                if (settings.ShowWaiting && WaitingPlace(p) is (double x, double y, bool ground))
                {
                    string state = p.Now switch
                    {
                        TowerPlan.State.Counting => $"{(int)(Math.Max(0, p.Due - engine.Time) / 60)}:{(int)(Math.Max(0, p.Due - engine.Time) % 60):00}",
                        TowerPlan.State.Held => "HELD",
                        _ => ""
                    };
                    string where = ground ? "GND" : altitude.Length > 0 ? altitude : entry;
                    marks.Add(new WaitingMark(x, y, $"{name} {type}", $"{where} {state}".Trim(), p.Now == TowerPlan.State.Held,
                        engine.Classes.FirstOrDefault(c => string.Equals(c.Name, p.Class, StringComparison.OrdinalIgnoreCase))?.CanHover == true, p.Formation > 1, p));
                }
            }
            stripBay.Show(coordination, contact);
            view.Waiting = marks;
            view.InvalidateVisual();
        }

        /// <summary>Where a plan waiting will start (the plane, NM), and whether on the ground; null when not known (no runway, no gate).</summary>
        private (double X, double Y, bool Ground)? WaitingPlace(TowerPlan p)
        {
            (double X, double Y)? At(CircuitStart start, CircuitType type) => engine.StartPoint(start, type, p.Side, p.Class.Length > 0 ? p.Class : null);
            (double X, double Y)? point;
            bool ground = false;
            if (p.Departure && p.FromPad && p.Formation <= 1 && PadOf(p) is (double padX, double padY))
            {
                point = (padX, padY);
                ground = true;
            }
            else if (p.Departure || p.From == From.Runway)
            {
                point = At(CircuitStart.Runway, CircuitType.Conventional);
                ground = true;
            }
            else if (p.From == From.LongFinal) point = At(p.Entry == CircuitEntry.Circling ? CircuitStart.Circling : CircuitStart.LongFinal, CircuitType.Conventional);
            else if (p.From == From.Point) point = RdPoint(p.FromRadial, p.FromDistance);
            else if (From.IsInCircuit(p.From))
            {
                CircuitStart start = p.Entry switch
                {
                    CircuitEntry.InitialPoint => CircuitStart.InitialPoint,
                    CircuitEntry.HighKey => CircuitStart.HighKey,
                    CircuitEntry.Final => CircuitStart.LongFinal,
                    _ => CircuitStart.Downwind
                };
                point = At(start, p.Circuit);
            }
            else
            {
                var gate = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, p.From, StringComparison.OrdinalIgnoreCase));
                point = gate.Name != null ? (gate.X, gate.Y) : null;
            }
            return point is (double x, double y) ? (x, y, ground) : null;
        }

        /// <summary>The list of the plans waiting, built again.</summary>
        private void ShowPlans()
        {
            planList.Children.Clear();
            planRows.Clear();
            planCallsigns.Clear();
            plansKey = PlansKey();
            foreach (TowerPlan plan in Waiting())
            {
                TowerPlan p = plan;
                Button hold = SmallButton("⏸", () =>
                {
                    // (The user, 7 Nov: held, the countdown stops too; let go, it goes on from where it was.)
                    if (p.Now == TowerPlan.State.Held)
                    {
                        if (p.Due > 0) p.Due = engine.Time + p.HeldLeft;
                        p.Now = p.Due > 0 ? TowerPlan.State.Counting : TowerPlan.State.Waiting;
                    }
                    else
                    {
                        p.HeldLeft = p.Now == TowerPlan.State.Counting ? Math.Max(0, p.Due - engine.Time) : 0;
                        if (p.Now != TowerPlan.State.Counting) p.Due = 0;
                        p.Now = TowerPlan.State.Held;
                    }
                    UpdatePlans();
                });
                Button tick = SmallButton(p.Picked ? "☑" : "☐", () => { p.Picked = !p.Picked; ShowPlans(); });
                tick.ToolTip = "Tick the rows to GROUP into a formation.";
                (string name, string rest) = PlanParts(p);
                TextBlock callsign = CallsignText(name);
                TextBlock text = RowText(rest);
                // A click on the row: the plan into Traffic generation, to change it (the user, 4 Nov).
                foreach (TextBlock clickable in new[] { callsign, text })
                {
                    clickable.Cursor = Cursors.Hand;
                    clickable.ToolTip = "Click: change it in Traffic generation › Advanced (UPDATE).";
                    clickable.MouseLeftButtonUp += (_, _) => EditPlan(p);
                }
                if (ReferenceEquals(p, editing)) callsign.Foreground = text.Foreground = Theme.Chosen;
                planList.Children.Add(ThinRow(planList, MoveWaiting, callsign, text, tick, SmallButton("▶", () => StartPlan(p)), hold, SmallButton("⧉", () => DuplicatePlan(p)),
                    SmallButton("✕", () => { plans.Remove(p); ShowPlans(); })));
                planRows.Add((p, text, hold));
                planCallsigns[p] = callsign;
            }
            if (planList.Children.Count == 0) planList.Children.Add(new TextBlock { Text = "None: Traffic generation › Advanced, then TO LIST.", Foreground = DimBrush, FontSize = 11 });
            ShowGroupButton();
            UpdatePlans();
        }

        /// <summary>The list of the aircraft flying (every one in the simulation), built again when they change.</summary>
        private void ShowFlying(IReadOnlyList<AircraftState> traffic)
        {
            flyingList.Children.Clear();
            flyingRows.Clear();
            flyingCallsigns.Clear();
            flyingKey = string.Join(",", traffic.Select(t => t.Callsign).Order());
            // In the instructor's order: the gone ones out, the new ones at the bottom.
            flyingOrder.RemoveAll(cs => !traffic.Any(t => t.Callsign == cs));
            foreach (AircraftState t in traffic) if (!flyingOrder.Contains(t.Callsign)) flyingOrder.Add(t.Callsign);
            foreach (AircraftState aircraft in traffic.OrderBy(t => flyingOrder.IndexOf(t.Callsign)))
            {
                string cs = aircraft.Callsign;
                Button freeze = SmallButton("II", () => engine.SetFrozen(cs, engine.Get(cs)?.Frozen != true));
                freeze.ToolTip = "Freeze it where it is / let it go on.";
                Button again = SmallButton("↺", () => RestartPlanOf(cs));
                again.ToolTip = "Its plan again from the start (an aircraft of the Traffic List).";
                Button remove = SmallButton("✕", () => commands.Remove(cs));
                remove.ToolTip = "Out of the simulation: its plan waits again in WAITING.";
                TextBlock callsign = CallsignText(cs);
                TextBlock text = RowText("");
                foreach (TextBlock clickable in new[] { callsign, text })
                {
                    clickable.Cursor = Cursors.Hand;
                    clickable.MouseLeftButtonUp += (_, _) => view.Select(cs);
                }
                flyingList.Children.Add(ThinRow(flyingList, MoveFlying, callsign, text, freeze, again, remove));
                flyingRows.Add((cs, text, freeze));
                flyingCallsigns[cs] = callsign;
            }
            if (traffic.Count == 0) flyingList.Children.Add(new TextBlock { Text = "None.", Foreground = DimBrush, FontSize = 11 });
        }

        /// <summary>The plan of an aircraft (its leader's) flown again from its start: its aircraft removed, started again.</summary>
        private void RestartPlanOf(string callsign)
        {
            TowerPlan? plan = plans.FirstOrDefault(p => p.Now == TowerPlan.State.Flying && !p.Quick && (p.Members.Contains(callsign) || p.Flying == callsign));
            if (plan == null) { Say($"{callsign} is not from a plan of the Traffic List.", Area.Traffic); return; }
            foreach (string m in MembersFlying(plan)) commands.Remove(m);
            plan.Now = TowerPlan.State.Waiting;
            StartPlan(plan);
        }

        private static Button SmallButton(string text, Action action)
        {
            Button button = ActionButton(text, action);
            button.MinWidth = 20;
            button.FontSize = 11;
            button.Padding = new Thickness(3, 0, 3, 0);
            button.Margin = new Thickness(2, 0, 0, 0);
            return button;
        }

        /// <summary>
        /// The two lists: starts the plans whose time has come; a plan whose aircraft are all gone waits again (one of ADD
        /// NOW is gone); the rows of the aircraft flying with their values.
        /// </summary>
        private void UpdatePlans()
        {
            double time = engine.Time;
            foreach (TowerPlan plan in plans.ToList())
            {
                if (plan.Now == TowerPlan.State.Flying && MembersFlying(plan).Count == 0)
                {
                    if (plan.Quick) { plans.Remove(plan); continue; }
                    plan.Now = TowerPlan.State.Waiting;
                    plan.Due = 0;
                }
                if (plan.Now == TowerPlan.State.Counting && time >= plan.Due) StartPlan(plan);
            }
            if (PlansKey() != plansKey)
            {
                ShowPlans();
                return;
            }
            foreach ((TowerPlan plan, TextBlock text, Button hold) in planRows)
            {
                string state = plan.Now switch
                {
                    TowerPlan.State.Counting => $"{(int)(Math.Max(0, plan.Due - time) / 60)}:{(int)(Math.Max(0, plan.Due - time) % 60):00} ",
                    TowerPlan.State.Held => plan.Due > 0 ? $"HELD {(int)(plan.HeldLeft / 60)}:{(int)(plan.HeldLeft % 60):00} " : "HELD ",
                    _ => ""
                };
                text.Text = state + PlanParts(plan).Detail;
                hold.Content = plan.Now == TowerPlan.State.Held ? "⏵" : "⏸";
            }
            IReadOnlyList<AircraftState> traffic = engine.Snapshot();
            if (string.Join(",", traffic.Select(t => t.Callsign).Order()) != flyingKey && dragRow == null) ShowFlying(traffic);
            foreach ((string cs, TextBlock text, Button freeze) in flyingRows)
            {
                AircraftState? a = traffic.FirstOrDefault(t => t.Callsign == cs);
                if (a == null) continue;
                string team = a.Formation is { Count: > 1 } f ? $"F{f.Count}" : "";
                text.Text = string.Format(CultureInfo.InvariantCulture, "{0,-3}{1:000} {2,3} {3}", team,
                    Math.Max(0, Math.Round((a.AltitudeFt - airfield.ElevationFt + HeightOffset) / 100)), Math.Round(a.GroundSpeedKt), a.Frozen ? "FROZEN" : TowerView.LegText(a));
                // (The selected one in yellow, as on the map.)
                bool chosen = a.Callsign == view.Selected;
                text.Foreground = chosen ? Theme.Chosen : TextBrush;
                if (flyingCallsigns.TryGetValue(cs, out TextBlock? name)) name.Foreground = chosen ? Theme.Chosen : CallsignBrush;
                freeze.Content = a.Frozen ? "▶" : "II";
            }
        }

        private void StartPlan(TowerPlan plan)
        {
            if (plan.Now == TowerPlan.State.Flying) return;
            string? error = Launch(plan, out string added);
            if (error != null)
            {
                Say($"{PlanText(plan)}: {error}", Area.Traffic);
                plan.Now = TowerPlan.State.Held;
                // (Let go: tried again at once, the review of 8 Nov.)
                plan.HeldLeft = 0;
            }
            else
            {
                Started(plan, added);
            }
            UpdatePlans();
        }

        /// <summary>A plan flying: its aircraft (a formation: the leader and its wingmen, each one in the list once split).</summary>
        private static void Started(TowerPlan plan, string added)
        {
            plan.Now = TowerPlan.State.Flying;
            plan.Flying = added;
            plan.Members = plan.Formation > 1 ? [added, .. SimEngine.FormationNames(added, plan.Formation) ?? []] : [added];
        }

        /// <summary>The aircraft of a plan in the simulation now (each one split off a formation as well).</summary>
        private List<string> MembersFlying(TowerPlan plan) =>
            plan.Now != TowerPlan.State.Flying ? [] : (plan.Members.Count > 0 ? plan.Members : [plan.Flying]).Where(m => engine.Get(m) != null).ToList();

        /// <summary>START ALL: every plan waiting starts after its delay (held ones stay held).</summary>
        private void StartAllPlans()
        {
            foreach (TowerPlan plan in plans.Where(p => p.Now == TowerPlan.State.Waiting))
            {
                plan.Now = TowerPlan.State.Counting;
                plan.Due = engine.Time + plan.DelayMinutes * 60;
            }
            UpdatePlans();
        }

        /// <summary>A copy with the next callsign (AZA101 → AZA102), one minute later.</summary>
        private void DuplicatePlan(TowerPlan plan)
        {
            TowerPlan copy = plan.Copy();
            if (copy.Callsign.Length > 0)
            {
                string stem = copy.Callsign.TrimEnd("0123456789".ToCharArray());
                string digits = copy.Callsign[stem.Length..];
                copy.Callsign = digits.Length > 0 && int.TryParse(digits, out int n) ? stem + (n + 1).ToString(new string('0', digits.Length)) : copy.Callsign + "2";
            }
            copy.DelayMinutes = plan.DelayMinutes + 1;
            plans.Insert(plans.IndexOf(plan) + 1, copy);
            ShowPlans();
        }

        private void ClearPlans()
        {
            if (plans.Count > 0 && MessageBox.Show(this, $"Remove the {plans.Count} rows of the Traffic List (the aircraft flying go on)?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            plans.Clear();
            ShowPlans();
        }

        /// <summary>▼ WAITING LIST: every aircraft out of the simulation; the rows of the Traffic List back to waiting (the user, 3 Nov).</summary>
        private void RemoveAllAircraft()
        {
            engine.Clear();
            view.Select(null);
            UpdatePlans();
        }

        /// <summary>
        /// The GROUP button (the user, 4 Nov): lit with two or more rows ticked (GROUP), or with one formation ticked
        /// (UNGROUP); grey otherwise. Its message says what it will do.
        /// </summary>
        private void ShowGroupButton()
        {
            if (groupButton == null) return;
            List<TowerPlan> picked = plans.Where(p => p.Picked && p.Now != TowerPlan.State.Flying && !p.Quick).ToList();
            bool group = picked.Count >= 2, ungroup = picked.Count == 1 && picked[0].Formation > 1;
            groupButton.Content = ungroup ? "UNGROUP" : "GROUP";
            groupButton.IsEnabled = group || ungroup;
            groupButton.Background = group || ungroup ? LitBack : ActionBack;
            groupButton.BorderBrush = group || ungroup ? LitBack : ActionBack;
            groupButton.Foreground = group || ungroup ? LitText : ActionText;
            groupButton.Tag = group || ungroup ? "on" : null;
            string what = group
                ? $"GROUP: {picked.Count} ticked → {PlanParts(picked[0]).Callsign.Split(' ')[0]} F{Math.Min(SimEngine.MaxFormation, picked.Sum(p => Math.Max(1, p.Formation)))} (the plan of {PlanParts(picked[0]).Callsign})"
                : ungroup ? $"UNGROUP: {PlanParts(picked[0]).Callsign} back to {(picked[0].GroupedFrom is { Count: > 0 } g ? $"its {g.Count} rows" : $"{picked[0].Formation} single rows")}" : "";
            if (what != groupShown)
            {
                groupShown = what;
                Say(what.Length > 0 ? what : null, Area.Traffic);
            }
        }

        private void GroupOrUngroup()
        {
            List<TowerPlan> picked = plans.Where(p => p.Picked && p.Now != TowerPlan.State.Flying && !p.Quick).ToList();
            if (picked.Count == 1 && picked[0].Formation > 1) UngroupPlan(picked[0]);
            else GroupPlans();
        }

        /// <summary>UNGROUP: a formation back to single rows in its place: the rows it was made of, or one per member.</summary>
        private void UngroupPlan(TowerPlan group)
        {
            List<TowerPlan> rows;
            if (group.GroupedFrom is { Count: > 0 } from) rows = from.Select(p => p.Copy()).ToList();
            else
            {
                List<string> names = group.Callsign.Length > 0 ? [group.Callsign, .. SimEngine.FormationNames(group.Callsign, group.Formation) ?? []] : [];
                rows = Enumerable.Range(0, group.Formation).Select(i =>
                {
                    TowerPlan single = group.Copy();
                    single.Formation = 1;
                    single.Lands = null;
                    single.GroupedFrom = null;
                    single.Callsign = i < names.Count ? names[i] : "";
                    return single;
                }).ToList();
            }
            int at = plans.IndexOf(group);
            plans.RemoveAt(at);
            plans.InsertRange(at, rows);
            Say($"{PlanParts(group).Callsign}: {rows.Count} single rows again.", Area.Traffic);
            ShowPlans();
        }

        /// <summary>GROUP: the rows ticked (not flying) into one formation, the first one's callsign leading (at most 4 aircraft).</summary>
        private void GroupPlans()
        {
            List<TowerPlan> picked = plans.Where(p => p.Picked && p.Now != TowerPlan.State.Flying).ToList();
            int count = picked.Sum(p => Math.Max(1, p.Formation));
            if (picked.Count < 2) { Say("Tick two or more rows (☐) not flying, then GROUP.", Area.Traffic); return; }
            if (count > SimEngine.MaxFormation) { Say($"At most {SimEngine.MaxFormation} aircraft in a formation ({count} ticked).", Area.Traffic); return; }
            TowerPlan group = picked[0].Copy();
            group.Formation = count;
            group.Lands = null;
            // (Kept for UNGROUP: the rows it is made of, each with its own plan.)
            group.GroupedFrom = picked.Select(p => { TowerPlan copy = p.Copy(); copy.Picked = false; return copy; }).ToList();
            plans.Insert(plans.IndexOf(picked[0]), group);
            foreach (TowerPlan p in picked) plans.Remove(p);
            Say($"{PlanText(group)}: a formation of {count}.", Area.Traffic);
            ShowPlans();
        }

        private void SaveScenario()
        {
            if (!plans.Any(p => !p.Quick)) { Say("No plans in the Traffic List (TO LIST): nothing to save.", Area.Traffic); return; }
            try { Directory.CreateDirectory(TowerScenario.Folder); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Filter = "TWR Trainer scenario (*.json)|*.json",
                InitialDirectory = TowerScenario.Folder,
                FileName = (airfield.Code.Length > 0 ? airfield.Code : "scenario") + " scenario.json"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                new TowerScenario { Airport = airfield.Code, Runway = airfield.Runway?.Runway ?? "", Plans = plans.Where(p => !p.Quick).Select(p => p.Copy()).ToList() }.Save(dialog.FileName);
                Say("Scenario saved.", Area.Traffic);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"The scenario could not be saved: {ex.Message}", Area.Traffic);
            }
        }

        private void LoadScenario()
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "TWR Trainer scenario (*.json)|*.json",
                InitialDirectory = Directory.Exists(TowerScenario.Folder) ? TowerScenario.Folder : ""
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                TowerScenario scenario = TowerScenario.Load(dialog.FileName);
                if (plans.Count > 0 && MessageBox.Show(this, "The flight plans of the list are replaced. Go on?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                plans.Clear();
                plans.AddRange(scenario.Plans);
                ShowPlans();
                Say(scenario.Airport.Length > 0 && !string.Equals(scenario.Airport, airfield.Code, StringComparison.OrdinalIgnoreCase)
                    ? $"Scenario of {scenario.Airport}, not of {airfield.Code}: its gates may be missing here." : null, Area.Traffic);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Say($"The scenario could not be read: {ex.Message}", Area.Traffic);
            }
        }

        // ----- Entry gates -----------------------------------------------------------------------------------------------

        /// <summary>The row "From": the gates of the aerodrome, the runway, in the circuit (the choice kept when it is still there).</summary>
        private void RebuildFrom()
        {
            string? was = fromRow.HasValue ? fromRow.Value : null;
            List<(string, string)> items = airfield.Gates.Select(g => (g.Name, g.Name)).ToList();
            items.Add((From.Point, "POINT"));
            items.Add((From.LongFinal, "LONG FIN"));
            items.Add((From.Runway, "RWY"));
            fromRow = new ChoiceRow<string>(items, (value, _) => value switch
            {
                From.LongFinal => "On the final at the long final distance (Simulation), on the glide path.",
                From.Runway => "On the runway (a departure): it takes off, then the conventional or the wide circuit, or away (Circuits 0).",
                From.Point => "A point clicked on the map (Start point), instead of a gate; drag its mark to move it.",
                _ => $"Entry gate {value}: it starts here and joins its circuit by itself (Entry)."
            });
            fromRow.Picked += from =>
            {
                if (from == From.Runway && circuitRow.Value is not (CircuitType.Conventional or CircuitType.Wide)) circuitRow.Show(CircuitType.Conventional);
                ShowEntries();
            };
            fromRow.Show(was != null && items.Any(i => i.Item1 == was) ? was : DefaultFrom());
            fromHolder.Children.Clear();
            fromHolder.Children.Add(fromRow);
            // The ways out (the user, 6 Nov): along the runway, onto a radial, to a gate, to an R/D point.
            string? wasTo = toRow.HasValue ? toRow.Value : null;
            List<(string, string)> exits = [(ExitWay.RunwayHeading, "RWY HDG"), (ExitWay.Radial, "RADIAL"), .. airfield.Gates.Select(g => (g.Name, g.Name)), (From.Point, "POINT")];
            toRow = new ChoiceRow<string>(exits, (value, _) => value switch
            {
                ExitWay.RunwayHeading => "Away along the runway heading, gone 10 NM out.",
                ExitWay.Radial => "Away onto a radial (Radial °M, its turn), as a departure; gone 10 NM out.",
                From.Point => "Straight to a point clicked on the map (Exit point), gone there; drag its mark to move it.",
                _ => $"Straight to the gate {value}, gone there."
            });
            toRow.Picked += _ =>
            {
                ShowEntries();
                WayOutChanged();
            };
            toRow.Show(wasTo != null && exits.Any(g => g.Item1 == wasTo) ? wasTo : ExitWay.RunwayHeading);
            toHolder.Children.Clear();
            toHolder.Children.Add(toRow);
            ShowEntries();
            view.Gates = airfield.Gates;
            view.Exits = airfield.Exits;
            view.Helipads = airfield.Helipads;
            RebuildPads();
            separationRow.Show(airfield.Circuits.FullRunwaySeparation);
            engine.SetRunwayExits(airfield.Exits);
            view.InvalidateVisual();
        }

        /// <summary>The values of the row From (the gates, LONG FIN, RWY).</summary>
        private List<string> FromItems() => [.. airfield.Gates.Select(g => g.Name), From.Point, From.LongFinal, From.Runway];

        /// <summary>The start chosen when there is no other (the review of 6 Nov): the first gate, else the long final (not R/D, empty).</summary>
        private string DefaultFrom() => airfield.Gates.Count > 0 ? airfield.Gates[0].Name : From.LongFinal;

        /// <summary>
        /// A point of a plan (the user, 7 Nov): CLICK ON MAP, then its mark on the map dragged to move it. Kept in the two boxes
        /// (not shown) as radial °M / distance NM from the middle of the runway, as the R/D points before; shown beside the button.
        /// </summary>
        private StackPanel PointPicker(TextBox radial, TextBox distance, string mark)
        {
            TextBlock shown = new() { Foreground = DimBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 4) };
            void Show()
            {
                shown.Text = Typed(radial, 0, 360) is double r && Typed(distance, 0.1, 200) is double d
                    ? string.Format(CultureInfo.InvariantCulture, "{0:000}° / {1:0.0} NM (drag its mark {2})", r, d, mark) : "not chosen";
                ShowPointMarks();
            }
            radial.TextChanged += (_, _) => Show();
            distance.TextChanged += (_, _) => Show();
            Button click = ActionButton("CLICK ON MAP", () =>
            {
                if (double.IsNaN(airfield.Course)) { Say("Place the runway in use first: a point is kept from its middle.", Area.New); return; }
                view.AskPoint($"{mark}: click the point on the map  -  Esc: cancel", (x, y) => SetPoint(radial, distance, x, y));
            });
            click.ToolTip = $"Click the point on the map; then drag its mark ({mark}) to move it.";
            Show();
            return Inline(click, shown);
        }

        /// <summary>A point of the plane written into the boxes of a point: radial °M and distance NM from the middle of the runway.</summary>
        private void SetPoint(TextBox radial, TextBox distance, double x, double y)
        {
            if (double.IsNaN(airfield.Course)) return;
            double course = airfield.Course * Math.PI / 180, half = Math.Max(airfield.LengthNM, 0.3) / 2;
            double dx = x - half * Math.Sin(course), dy = y - half * Math.Cos(course);
            double nm = Math.Clamp(Math.Sqrt(dx * dx + dy * dy), 0.1, 200);
            double bearing = Math.Atan2(dx, dy) * 180 / Math.PI - (airfield.Circuits.MagneticVariation ?? 0);
            bearing = Math.Round(((bearing % 360) + 360) % 360, 1) % 360;
            radial.Text = bearing.ToString("0.#", CultureInfo.InvariantCulture);
            distance.Text = Math.Round(nm, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>The marks of the points of the plan in Advanced (START, EXIT) on the map, to drag (the user, 7 Nov).</summary>
        private void ShowPointMarks()
        {
            List<(string Name, double X, double Y)> marks = [];
            if (fromPointLine != null && toPointLine != null && !settings.QuickTab)
            {
                if (fromPointLine.Visibility == Visibility.Visible && RdPoint(Typed(fromRadialBox, 0, 360), Typed(fromDistanceBox, 0.1, 200)) is (double fx, double fy))
                    marks.Add(("START", fx, fy));
                if (toPointLine.Visibility == Visibility.Visible && RdPoint(Typed(toRadialBox, 0, 360), Typed(toDistanceBox, 0.1, 200)) is (double tx, double ty))
                    marks.Add(("EXIT", tx, ty));
            }
            view.Marks = marks;
            view.InvalidateVisual();
        }

        private static double? Typed(TextBox box, double low, double high) =>
            double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= low && v <= high ? v : null;

        private static string Number(double? value) => value is double v ? v.ToString("0.#", CultureInfo.InvariantCulture) : "";

        /// <summary>An R/D point on the plane: radial °M (+ the variation: true) and distance NM from the middle of the runway in use.</summary>
        private (double X, double Y)? RdPoint(double? radialM, double? distance)
        {
            if (radialM is not double radial || distance is not double nm || double.IsNaN(airfield.Course)) return null;
            double course = airfield.Course * Math.PI / 180, half = Math.Max(airfield.LengthNM, 0.3) / 2;
            double bearing = (radial + (airfield.Circuits.MagneticVariation ?? 0)) * Math.PI / 180;
            return (half * Math.Sin(course) + nm * Math.Sin(bearing), half * Math.Cos(course) + nm * Math.Cos(bearing));
        }

        /// <summary>A gate (its name) or the R/D point of a plan: where on the plane, or null.</summary>
        private (double X, double Y)? PlaceOf(string name, double? radial, double? distance)
        {
            if (name == From.Point) return RdPoint(radial, distance);
            var gate = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            return gate.Name != null ? (gate.X, gate.Y) : null;
        }

        /// <summary>The R/D text of a point (R270/12).</summary>
        private static string RdText(double? radial, double? distance) => string.Create(CultureInfo.InvariantCulture, $"R{radial ?? 0:000}/{distance ?? 0:0.#}");

        /// <summary>
        /// The way out of a plan (departure, transit): a point (gate or R/D), else along the runway heading (radial NaN) or onto
        /// the radial (true); Error when the gate is not there or the R/D point not written. (Plans of before: To empty = the
        /// runway heading, or the radial when written.)
        /// </summary>
        private (double Radial, (double X, double Y)? Point, string? Error) ExitOf(TowerPlan p)
        {
            string to = p.To;
            if (to.Length == 0 || to == ExitWay.RunwayHeading || to == ExitWay.Radial)
                return (to == ExitWay.RunwayHeading ? double.NaN : RadialTrue(p), null, to == ExitWay.Radial && p.Radial == null ? "Write the radial (0 to 360, magnetic) in Radial °M." : null);
            if (PlaceOf(to, p.ToRadial, p.ToDistance) is (double x, double y)) return (double.NaN, (x, y), null);
            return (double.NaN, null, to == From.Point ? "Click the exit point on the map (Exit point: CLICK ON MAP)." : $"There is no gate {to} at this aerodrome.");
        }

        /// <summary>The entries that apply to where it starts: none from the long final or the runway; in the circuit: DW, IP, HK.</summary>
        private void ShowEntries()
        {
            // (The user, 5 Nov: four types, each with its end; the rows that apply lit, the others grey.)
            Flight type = typeRow.HasValue ? typeRow.Value : Flight.Arrival;
            bool transit = type == Flight.Transit, departure = type == Flight.Departure, local = type == Flight.Local, arrival = type == Flight.Arrival;
            bool runway = departure || local;
            // From: the runway for departures and locals; a gate, an R/D point or the long final for the others (the user, 6 Nov:
            // a transit from the long final too).
            foreach (string item in FromItems()) fromRow.SetEnabled(item, runway ? item == From.Runway : item != From.Runway);
            if (runway && fromRow.Value != From.Runway) fromRow.Show(From.Runway);
            if (!runway && fromRow.Value == From.Runway) fromRow.Show(DefaultFrom());
            string from = fromRow.Value ?? "";
            // (A transit from the long final comes to do something on the runway: its circuits, OUT = only the approach.)
            bool fromFinal = transit && from == From.LongFinal;
            viaRow.SetEnabled(Via.Direct, !fromFinal);
            viaRow.SetEnabled(Via.Overhead, !fromFinal);
            if (fromFinal && viaRow.Value != Via.Circuits) viaRow.Show(Via.Circuits);
            Via via = viaRow.HasValue ? viaRow.Value : Via.Overhead;
            bool withCircuits = transit && via == Via.Circuits;
            // The ways out: all four for a departure and a transit using the runway; a gate or an R/D point for a crossing.
            bool usesRunway = departure || withCircuits;
            toRow.SetEnabled(ExitWay.RunwayHeading, usesRunway);
            toRow.SetEnabled(ExitWay.Radial, usesRunway);
            if (!usesRunway && toRow.Value is ExitWay.RunwayHeading or ExitWay.Radial)
                toRow.Show(airfield.Gates.Count > 0 ? airfield.Gates[^1].Name : From.Point);
            // Pattern: after a take-off the conventional or the wide (the others once it flies).
            // (From the long final: no overhead, no SFO, the user 7 Nov: grey, not refused after the click.)
            bool longFinal = from == From.LongFinal;
            foreach (CircuitType circuitType in Enum.GetValues<CircuitType>())
            {
                bool allowed = runway ? circuitType is CircuitType.Conventional or CircuitType.Wide
                    : !longFinal || circuitType is not (CircuitType.Overhead or CircuitType.Sfo);
                circuitRow.SetEnabled(circuitType, allowed);
            }
            if (longFinal && circuitRow.Value is CircuitType.Overhead or CircuitType.Sfo) circuitRow.Show(CircuitType.Conventional);
            if (runway && circuitRow.Value is not (CircuitType.Conventional or CircuitType.Wide)) circuitRow.Show(CircuitType.Conventional);
            // On Final: F/S only for those that land.
            onFinalRow.SetEnabled(EndAction.FullStop, arrival || local);
            if (!(arrival || local) && onFinalRow.Value == EndAction.FullStop) onFinalRow.Show(EndAction.TouchAndGo);
            // HPAD: helicopters that land (the user, 5 Nov).
            bool helicopter = engine.Classes.FirstOrDefault(c => c.Name == classRow.Value)?.CanHover == true;
            bool padAllowed = helicopter && (arrival || local);
            onFinalRow.SetEnabled(PadEnd, padAllowed);
            if (!padAllowed && onFinalRow.Value == PadEnd) onFinalRow.Show(arrival || local ? EndAction.FullStop : EndAction.TouchAndGo);
            // (A helicopter departing: from the runway or a helipad, the user 6 Nov; landing on a pad: a pad, not RWY.)
            bool padDeparture = PadDeparture();
            padRow.SetEnabled(PadRunwayName, padDeparture);
            // (Just become a helicopter departure, by its type or its class: from the runway unless a pad is chosen.)
            if (padDeparture && (!padRow.HasValue || !wasPadDeparture)) padRow.Show(PadRunwayName);
            wasPadDeparture = padDeparture;
            if (!padDeparture && padRow.Value == PadRunwayName) padRow.Show(airfield.Helipads.Count > 0 ? airfield.Helipads[0].Name : PadPointName);
            bool landsFirst = (arrival || local) && onFinalRow.Value is EndAction.FullStop or PadEnd;
            // Circuits: OUT for departures, ∞ for arrivals and locals.
            bool outAllowed = departure || fromFinal;
            lapsRow.SetEnabled(0, outAllowed);
            lapsRow.SetEnabled(TowerPlan.Forever, arrival || local);
            int laps = lapsRow.HasValue ? lapsRow.Value : 1;
            if ((!outAllowed && laps == 0) || (!(arrival || local) && laps == TowerPlan.Forever)) lapsRow.Show(1);
            bool away = departure && lapsRow.Value == 0;
            bool inCircuit = !transit || withCircuits;
            if (departLine == null) return;
            static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;
            patternLine.Visibility = sideLine.Visibility = onFinalLine.Visibility = Shown(inCircuit && !away);
            // (From the long final, the user 7 Nov: the pattern is only for the circuits after the first approach; none after
            // it (F/S, or a transit OUT): no pattern, no side; CIRCLING: always the conventional, its side kept.)
            bool circling = longFinal && entryRow.Value == CircuitEntry.Circling;
            bool straightOnly = longFinal && !circling && (((arrival || local) && landsFirst) || (transit && lapsRow.Value == 0));
            if (straightOnly || circling) patternLine.Visibility = Visibility.Collapsed;
            if (straightOnly) sideLine.Visibility = Visibility.Collapsed;
            hkLine.Visibility = Shown(inCircuit && !away && circuitRow.Value == CircuitType.Sfo);
            circuitsLine.Visibility = Shown(departure || withCircuits || ((arrival || local) && !landsFirst));
            padLine.Visibility = Shown((padAllowed && onFinalRow.Value == PadEnd && inCircuit) || padDeparture);
            padName.Text = padDeparture ? "Take off from" : "Helipad";
            entryLine.Visibility = Shown(arrival || withCircuits);
            departLine.Visibility = Visibility.Collapsed;
            // (The way out: "Exit via" for a departure, "Exit to" for a transit, the user 5 / 6 Nov.)
            toLine.Visibility = Shown(departure || transit);
            toName.Text = departure ? "Exit via" : "Exit to";
            radialLine.Visibility = Shown(usesRunway && toRow.Value == ExitWay.Radial);
            exitTurnLine.Visibility = Shown(usesRunway && toRow.Value != ExitWay.RunwayHeading);
            exitAltLine.Visibility = Shown(usesRunway);
            toPointLine.Visibility = Shown((departure || transit) && toRow.Value == From.Point);
            fromPointLine.Visibility = Shown(!runway && from == From.Point);
            ShowPointMarks();
            viaLine.Visibility = Shown(transit);
            // (From the long final: straight on (FINAL) or the circling; the circling only from there.)
            foreach (CircuitEntry entry in Enum.GetValues<CircuitEntry>())
                entryRow.SetEnabled(entry, longFinal ? entry is CircuitEntry.Final or CircuitEntry.Circling : entry != CircuitEntry.Circling);
            if (longFinal && entryRow.Value is not (CircuitEntry.Final or CircuitEntry.Circling)) entryRow.Show(CircuitEntry.Final);
            if (!longFinal && entryRow.Value == CircuitEntry.Circling) entryRow.Show(CircuitEntry.Downwind);
            if (longFinal && entryRow.Value == CircuitEntry.Circling) circuitRow.Show(CircuitType.Conventional);
            if (longFinal && circuitRow.Value is CircuitType.Overhead or CircuitType.Sfo) circuitRow.Show(CircuitType.Conventional);
            AutoExitTurn();
        }

        /// <summary>A helicopter departure in Traffic generation (it may take off from a helipad, the user 6 Nov).</summary>
        private bool PadDeparture() =>
            typeRow.HasValue && typeRow.Value == Flight.Departure && engine.Classes.FirstOrDefault(c => c.Name == classRow.Value)?.CanHover == true;

        /// <summary>A name of the rows Start from / Exit via (R/D, RWY HDG, RADIAL, LONG FIN, RWY, PTRN...), not for a gate.</summary>
        private static bool ReservedGateName(string name) =>
            name is From.Point or From.LongFinal or From.Runway or ExitWay.RunwayHeading or ExitWay.Radial || From.IsInCircuit(name);

        private void AddGate()
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            string typed = newGateName.Text.Trim().ToUpperInvariant();
            view.AskPoint("Entry gate: click it on the map (a VRP gives its name)  -  Esc: cancel", (x, y) =>
            {
                string name = typed.Length > 0 ? typed : view.MapPointNear(x, y)?.ToUpperInvariant() ?? $"G{airfield.Circuits.Gates.Count + 1}";
                // (Names of the rows Start from and Exit via: not a gate's, the review of 6 Nov.)
                if (ReservedGateName(name))
                {
                    Say($"{name} is a name of the program (Start from, Exit via): choose another name for the gate.");
                    return;
                }
                airfield.AddGate(name, x, y);
                string? error = airfield.Save();
                double from = airfield.FromField(x, y);
                Say(error ?? (from < 5 ? $"Gate {name} added, but only {from:0.0} NM from the aerodrome (at least 5 NM out)." : $"Gate {name} added ({from:0.0} NM out)."));
                newGateName.Text = "";
                RebuildFrom();
            });
        }

        private void AddExit()
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            string typed = newExitName.Text.Trim().ToUpperInvariant();
            view.AskPoint("Runway exit: click it on the runway edge  -  Esc: cancel", (x, y) =>
            {
                string name = typed.Length > 0 ? typed : Enumerable.Range(0, 26).Select(i => ((char)('A' + i)).ToString())
                    .FirstOrDefault(l => !airfield.Exits.Any(e => string.Equals(e.Name, l, StringComparison.OrdinalIgnoreCase))) ?? "Z";
                airfield.AddExit(name, x, y);
                string? error = airfield.Save();
                Say(error ?? $"Exit {name} added.");
                newExitName.Text = "";
                RebuildFrom();
            });
        }

        private void RemoveExit()
        {
            string name = newExitName.Text.Trim().ToUpperInvariant();
            if (name.Length == 0) { Say("Write the name of the exit to remove."); return; }
            if (!airfield.Exits.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))) { Say($"There is no exit {name}."); return; }
            airfield.RemoveExit(name);
            Say(airfield.Save());
            newExitName.Text = "";
            RebuildFrom();
        }

        /// <summary>A helipad for a helicopter (the user, 5 Nov): the registered ones, or a point clicked on the map; now: straight there (DCT TO), else at the end of its final.</summary>
        private void PadMenu(string callsign, bool now)
        {
            void Go(double x, double y)
            {
                if (now) commands.DirectToPad(callsign, x, y); else commands.Helipad(callsign, x, y);
                ShowSelected();
            }
            List<MenuItem> items = airfield.Helipads.Select(h => Item(h.Name, () => Go(h.X, h.Y))).ToList();
            items.Add(Item("Point on the map...", () => view.AskPoint($"{callsign}: click where it lands  -  Esc: cancel", Go)));
            OpenMenu(items);
        }

        private void AddHelipad()
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            string typed = newPadName.Text.Trim().ToUpperInvariant();
            view.AskPoint("Helipad: click it on the map  -  Esc: cancel", (x, y) =>
            {
                string name = typed.Length > 0 && typed != PadPointName ? typed : Enumerable.Range(1, 99).Select(i => $"H{i}")
                    .First(n => !airfield.Helipads.Any(h => string.Equals(h.Name, n, StringComparison.OrdinalIgnoreCase)));
                airfield.AddHelipad(name, x, y);
                Say(airfield.Save() ?? $"Helipad {name} added.");
                newPadName.Text = "";
                RebuildFrom();
            });
        }

        private void RemoveHelipad()
        {
            string name = newPadName.Text.Trim().ToUpperInvariant();
            if (name.Length == 0) { Say("Write the name of the helipad to remove."); return; }
            if (!airfield.Helipads.Any(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))) { Say($"There is no helipad {name}."); return; }
            airfield.RemoveHelipad(name);
            Say(airfield.Save());
            newPadName.Text = "";
            RebuildFrom();
        }

        private void RemoveGate()
        {
            string name = newGateName.Text.Trim().Length > 0 ? newGateName.Text.Trim() : fromRow.Value ?? "";
            if (!airfield.Circuits.Gates.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Say("Write the name of the gate to remove (or choose it in Start from).");
                return;
            }
            if (MessageBox.Show(this, $"Remove the gate {name}?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            airfield.RemoveGate(name);
            Say(airfield.Save());
            newGateName.Text = "";
            RebuildFrom();
        }

        // ----- The commands on the label (step 4b): the same commands as the panel -------------------------------------

        private void LabelField(string callsign, string field)
        {
            AircraftState? a = engine.Get(callsign);
            if (a == null) return;
            CircuitType type = a.Circuit ?? CircuitType.Conventional;
            // A value chosen in the buttons under a field.
            if (field.Split('=') is [string name, string value])
            {
                if (name == "SIDE" && Enum.TryParse(value, out CircuitSide chosenSide))
                {
                    // The side of the next circuit if one is ordered, else of this one (an S over the field).
                    if (a.NextCircuit is CircuitType nextType) commands.NextCircuit(callsign, nextType, chosenSide);
                    else if (chosenSide != a.CircuitSide) commands.FlyCircuit(callsign, type, chosenSide);
                }
                else if (name == "LAND" && int.TryParse(value, out int member) && a.Formation is { } crew)
                {
                    List<bool> lands = Enumerable.Range(0, crew.Count).Select(i => a.Lands == null || i >= a.Lands.Count || a.Lands[i]).ToList();
                    if (member < lands.Count) lands[member] = !lands[member];
                    commands.FormationLanding(callsign, lands);
                }
                else if (name == "ALT")
                {
                    if (value == "AUTO") commands.HeightAuto(callsign);
                    else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double feet)) commands.Height(callsign, feet);
                }
                else if (name == "SPD")
                {
                    if (value == "AUTO") commands.SpeedAuto(callsign);
                    else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double knots)) commands.Speed(callsign, knots);
                }
                else if (name == "CIRC" && Enum.TryParse(value, out CircuitType circuit)) commands.NextCircuit(callsign, circuit, engine.SideFor(circuit));
                else if (name == "DCT" && Enum.TryParse(value, out DirectPoint point)) commands.DirectTo(callsign, point);
                else if (name == "PAD")
                {
                    PadMenu(callsign, now: value == "NOW");
                    return;
                }
                else if (name == "NEXT" && Enum.TryParse(value, out EndAction end))
                {
                    // (Another end for a helicopter going to its pad: the pad dropped, the user 5 Nov.)
                    if (a.CanHover && (a.HasPad || a.WantsPad)) commands.Helipad(callsign, double.NaN, double.NaN);
                    commands.SetEnd(callsign, end);
                }
                else if (name == "BRK" && Enum.TryParse(value, out BreakPoint where))
                {
                    if (where == BreakPoint.Point) view.AskPoint($"{callsign}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(callsign, x, y));
                    else commands.SetBreak(callsign, where);
                }
                ShowSelected();
                return;
            }
            switch (field)
            {
                case "SIDE":
                    commands.FlyCircuit(callsign, type, a.CircuitSide == CircuitSide.Left ? CircuitSide.Right : CircuitSide.Left);
                    break;
                case "EXTEND":
                    commands.ExtendDownwind(callsign);
                    break;
                case "BASE":
                    commands.TurnBase(callsign);
                    break;
                case "BREAKNOW":
                    commands.BreakNow(callsign);
                    break;
                case "HOLD":
                    commands.HoldAtHighKey(callsign, true);
                    break;
                case "START":
                    commands.HoldAtHighKey(callsign, false);
                    break;
                case "ORBIT":
                    // (Shift + click: orbits until clicked again, the user 4 Nov.)
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) commands.OrbitForever(callsign);
                    else commands.Orbit(callsign);
                    break;
                case "REJOIN":
                    commands.Rejoin(callsign);
                    break;
                case "EXPTAXI":
                    commands.ExpediteTaxi(callsign);
                    break;
                case "CONTINUE":
                    commands.Continue(callsign);
                    break;
                case "GA":
                    commands.GoAround(callsign);
                    break;
                case "SPLIT":
                    commands.Split(callsign);
                    break;
                case "WIDENOW":
                    // (Its own side at this runway, the user 8 Nov.)
                    commands.FlyCircuit(callsign, CircuitType.Wide, engine.SideFor(CircuitType.Wide));
                    break;
                case "BREAKUP":
                    commands.Breakup(callsign);
                    break;
            }
            ShowSelected();
        }

        /// <summary>
        /// The wheel on NEXT or BRK: the next (or the previous) value. On the circuit: the circuit shown with a "?", ordered
        /// when the wheel stops for a moment (each notch is not an order).
        /// </summary>
        private void LabelWheel(string callsign, string field, int step)
        {
            AircraftState? a = engine.Get(callsign);
            if (a == null) return;
            if (field.StartsWith("LAND=", StringComparison.Ordinal) && a.Formation is { } team)
            {
                // The wheel on LAND: how many land, from the leader on.
                int landing = Enumerable.Range(0, team.Count).Count(i => a.Lands == null || i >= a.Lands.Count || a.Lands[i]);
                landing = Math.Clamp(landing - step, 0, team.Count);
                commands.FormationLanding(callsign, Enumerable.Range(0, team.Count).Select(i => i < landing).ToList());
                ShowSelected();
                return;
            }
            if (field is "ALT" or "SPD")
            {
                // (The user, 3 Nov: the wheel on the height ±100 ft, on the speed ±10 kt; ordered when it stops a moment.)
                if (field == "ALT")
                {
                    double from = view.PendingHeightFt ?? (!a.RouteAltitude && a.VerticalMode == VerticalMode.Altitude ? a.TargetAltitudeFt : a.AltitudeFt) - airfield.ElevationFt;
                    // (In hundreds of what is shown: altitudes with QNH, heights with QFE.)
                    double shown = Math.Round((from + view.HeightOffset) / 100) * 100 - step * 100;
                    view.PendingHeightFt = Math.Clamp(shown - view.HeightOffset, 100, 15000);
                }
                else
                {
                    double from = view.PendingSpeedKt ?? (!a.RouteSpeed ? a.TargetSpeedKt : a.SpeedKt);
                    PerformanceClass? kind = engine.Classes.FirstOrDefault(c => string.Equals(c.Name, a.PerformanceClass, StringComparison.OrdinalIgnoreCase));
                    view.PendingSpeedKt = Math.Clamp(Math.Round(from / 10) * 10 - step * 10, kind?.MinSpeedKt ?? 0, kind?.MaxSpeedKt ?? 600);
                }
                valueCallsign = callsign;
                valueSince = clock.Elapsed.TotalSeconds;
                view.InvalidateVisual();
                return;
            }
            if (field == "CIRC")
            {
                CircuitType now = view.PendingCircuit ?? a.NextCircuit ?? a.Circuit ?? CircuitType.Conventional;
                int i = Array.FindIndex(TowerCommands.Circuits, c => c.Type == now);
                view.PendingCircuit = TowerCommands.Circuits[((i + step) % TowerCommands.Circuits.Length + TowerCommands.Circuits.Length) % TowerCommands.Circuits.Length].Type;
                pendingCallsign = callsign;
                pendingSince = clock.Elapsed.TotalSeconds;
                view.InvalidateVisual();
                return;
            }
            if (field == "NEXT")
            {
                int i = Array.FindIndex(TowerCommands.Ends, e => e.End == a.EndAction);
                commands.SetEnd(callsign, TowerCommands.Ends[((i + step) % TowerCommands.Ends.Length + TowerCommands.Ends.Length) % TowerCommands.Ends.Length].End);
            }
            else if (field == "BRK")
            {
                int i = Array.FindIndex(TowerCommands.Breaks, b => b.Break == a.Break);
                commands.SetBreak(callsign, TowerCommands.Breaks[((i + step) % TowerCommands.Breaks.Length + TowerCommands.Breaks.Length) % TowerCommands.Breaks.Length].Break);
            }
            ShowSelected();
        }

        /// <summary>The speeds offered under the label: from the class's minimum to its maximum, about seven round values.</summary>
        private IReadOnlyList<double> SpeedChoices(AircraftState a)
        {
            PerformanceClass? kind = engine.Classes.FirstOrDefault(c => string.Equals(c.Name, a.PerformanceClass, StringComparison.OrdinalIgnoreCase));
            if (kind == null) return [];
            double low = Math.Max(10, Math.Ceiling(Math.Max(kind.MinSpeedKt, 10) / 10) * 10);
            double stepKt = Math.Max(10, Math.Round((kind.MaxSpeedKt - low) / 6 / 10) * 10);
            List<double> list = [];
            for (double v = low; v <= kind.MaxSpeedKt + 0.1 && list.Count < 9; v += stepKt) list.Add(v);
            return list;
        }

        private static MenuItem Item(string text, Action action, bool check = false)
        {
            MenuItem item = new() { Header = text, IsChecked = check };
            item.Click += (_, _) => action();
            return item;
        }

        private List<MenuItem> CircuitItems(string callsign)
        {
            AircraftState? a = engine.Get(callsign);
            return TowerCommands.Circuits.Select(c => Item(c.Text, () => { commands.NextCircuit(callsign, c.Type, engine.SideFor(c.Type)); ShowSelected(); }, (a?.NextCircuit ?? a?.Circuit) == c.Type)).ToList();
        }

        private List<MenuItem> EndItems(string callsign, EndAction now) =>
            TowerCommands.Ends.Select(e => Item(e.Text, () => { commands.SetEnd(callsign, e.End); ShowSelected(); }, e.End == now)).ToList();

        private List<MenuItem> BreakItems(string callsign, BreakPoint now)
        {
            List<MenuItem> items = TowerCommands.Breaks.Select(b => Item(b.Text, () => { commands.SetBreak(callsign, b.Break); ShowSelected(); }, b.Break == now)).ToList();
            items.Add(Item("Point on the map...", () => view.AskPoint($"{callsign}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(callsign, x, y)), now == BreakPoint.Point));
            return items;
        }

        /// <summary>The menu of all the commands of an aircraft (right click on it).</summary>
        private List<MenuItem> Menu(string callsign)
        {
            AircraftState? a = engine.Get(callsign);
            if (a == null) return [];
            CircuitType type = a.Circuit ?? CircuitType.Conventional;
            MenuItem circuit = new() { Header = "Next circuit" };
            foreach (MenuItem item in CircuitItems(callsign)) circuit.Items.Add(item);
            MenuItem side = new() { Header = "Side now" };
            side.Items.Add(Item("Left", () => commands.FlyCircuit(callsign, type, CircuitSide.Left), a.CircuitSide == CircuitSide.Left));
            side.Items.Add(Item("Right", () => commands.FlyCircuit(callsign, type, CircuitSide.Right), a.CircuitSide == CircuitSide.Right));
            MenuItem end = new() { Header = "At the end" };
            foreach (MenuItem item in EndItems(callsign, a.EndAction)) end.Items.Add(item);
            List<MenuItem> items = [circuit, side, end];
            if (type is CircuitType.Overhead or CircuitType.Close)
            {
                MenuItem where = new() { Header = "Break" };
                foreach (MenuItem item in BreakItems(callsign, a.Break)) where.Items.Add(item);
                items.Add(where);
                items.Add(Item("BREAK NOW", () => commands.BreakNow(callsign)));
            }
            if (type == CircuitType.Sfo) items.Add(a.SfoHold ? Item("Start SFO", () => commands.HoldAtHighKey(callsign, false)) : Item("Hold at High Key", () => commands.HoldAtHighKey(callsign, true)));
            items.Add(Item("Extend downwind", () => commands.ExtendDownwind(callsign)));
            items.Add(Item("Turn base", () => commands.TurnBase(callsign)));
            items.Add(Item("Orbit", () => commands.Orbit(callsign)));
            items.Add(Item("Go around", () => commands.GoAround(callsign)));
            items.Add(Item("Rejoin the circuit", () => commands.Rejoin(callsign)));
            if (a.Leg == "LANDED") items.Add(Item("Expedite taxi", () => commands.ExpediteTaxi(callsign)));
            if (a?.CanHover == true) items.Add(Item("Helipad...", () => PadMenu(callsign, now: false)));
            items.Add(Item("Remove", () => commands.Remove(callsign)));
            return items;
        }

        private void OpenMenu(List<MenuItem> items)
        {
            if (items.Count == 0) return;
            ContextMenu menu = new() { PlacementTarget = view, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
            foreach (MenuItem item in items) menu.Items.Add(item);
            menu.IsOpen = true;
        }

        // ----- Aircraft control: the selected aircraft --------------------------------------------------------------------

        private void WithSelected(Action<string> action)
        {
            if (view.Selected is string callsign) action(callsign);
            else Say("Select an aircraft first (click it on the map).", Area.Selected);
        }

        private void ShowSelected()
        {
            selectedPanel.IsEnabled = view.Selected != null;
            AircraftState? a = view.Selected is string cs ? engine.Get(cs) : null;
            if (a != null)
            {
                selCircuitRow.Show(a.NextCircuit ?? a.Circuit ?? CircuitType.Conventional);
                selSideRow.Show(a.NextCircuit != null ? a.NextSide : a.CircuitSide);
                selEndRow.Show(a.EndAction);
                selBreakRow.Show(a.Break);
            }
            ShowSelectedValues();
        }

        private void ShowSelectedValues()
        {
            AircraftState? a = view.Selected is string cs ? engine.Get(cs) : null;
            if (a != null)
            {
                // The state of the aircraft (not right after a click: the order may not be carried out yet).
                selCircuitRow.Refresh(a.NextCircuit ?? a.Circuit ?? CircuitType.Conventional);
                selSideRow.Refresh(a.NextCircuit != null ? a.NextSide : a.CircuitSide);
                selEndRow.Refresh(a.EndAction);
                selBreakRow.Refresh(a.Break);
            }
            CircuitType? planned = selCircuitRow.HasValue ? selCircuitRow.Value : a?.NextCircuit ?? a?.Circuit;
            if (selBreakGrid != null) selBreakGrid.Visibility = selBreakPointGrid.Visibility = planned is CircuitType.Overhead or CircuitType.Close ? Visibility.Visible : Visibility.Collapsed;
            if (a != null)
            {
                string leg = a.Leg;
                bool sfo = a.Circuit == CircuitType.Sfo, inCircuit = a.Circuit != null && a.Contact == GroundContact.Airborne && leg is not ("MANUAL" or "GO AROUND" or "DEPARTURE");
                foreach (Button dct in dctButtons) dct.IsEnabled = a.Contact == GroundContact.Airborne && a.Circuit != null && !a.Transit;
                // (The helipad: helicopters only; DCT TO HPAD also out of a circuit.)
                padNowButton.Visibility = padEndLine.Visibility = a.CanHover ? Visibility.Visible : Visibility.Collapsed;
                padNowButton.IsEnabled = a.CanHover && a.Contact == GroundContact.Airborne && !a.Transit;
                padEndButton.IsEnabled = a.CanHover && a.Circuit != null && !a.Transit;
                // (On the ground and taking off: the conventional or the wide, the user 5 Nov.)
                foreach (CircuitType pattern in Enum.GetValues<CircuitType>())
                    selCircuitRow.SetEnabled(pattern, a.Contact == GroundContact.Airborne || pattern is CircuitType.Conventional or CircuitType.Wide);
                // (The user, 8 Nov: also during the touch and go, before a close pattern: it turns above 400 ft.)
                breakNowButton.IsEnabled = engine.CanBreakNow(a.Callsign);
                holdButton.IsEnabled = sfo && !a.SfoHold && leg is "CLIMB" or "HIGH KEY";
                startSfoButton.IsEnabled = sfo && a.SfoHold;
                extendButton.IsEnabled = leg is "DOWNWIND" or "ORBIT" or "ORBITS";
                baseButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS"
                    || (leg == "JOIN" && a.Circuit is CircuitType.Conventional or CircuitType.Wide or CircuitType.Close);
                // ORB: one orbit (Shift: on and on); orbiting on and on, a thick border and a click goes on along its circuit.
                orbitButton.IsEnabled = a.OrbitForever || (inCircuit && leg is "DOWNWIND" or "EXT DOWNWIND" or "UPWIND" or "CROSSWIND" or "JOIN" or "CIRCLING") || leg == "TRANSIT";
                orbitButton.BorderBrush = a.OrbitForever ? Theme.Chosen : NowEdge;
                orbitButton.BorderThickness = new Thickness(a.OrbitForever ? 3 : 1);
                goAroundButton.IsEnabled = leg == "FINAL";
                sideLeftButton.IsEnabled = sideRightButton.IsEnabled = inCircuit;
                // (As on the label.)
                wideButton.IsEnabled = a.Contact == GroundContact.Airborne && a.Circuit != null && !a.Transit && leg is not ("MANUAL" or "GO AROUND");
                rejoinButton.IsEnabled = a.Contact == GroundContact.Airborne && (a.Circuit == null || leg is "MANUAL" or "GO AROUND");
                breakupButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS" or "BASE";
                splitButton.IsEnabled = a.SplitAllowed;
                expTaxiButton.IsEnabled = leg == "LANDED" && !a.ExpediteTaxi;
                ShowPresets(a);
                // The boxes: the values now (not while one is written in, nor while its wheel turns).
                if (!heightBox.IsKeyboardFocusWithin)
                    heightBox.Text = $"{Math.Round(((view.PendingHeightFt ?? a.AltitudeFt - airfield.ElevationFt) + HeightOffset) / 100):000}";
                if (!speedKtBox.IsKeyboardFocusWithin) speedKtBox.Text = $"{Math.Round(view.PendingSpeedKt ?? a.SpeedKt):0}";
                if (!headingBox.IsKeyboardFocusWithin && headingWheel == null)
                {
                    double hdg = Math.Round(a.HeadingTrue) % 360;
                    headingBox.Text = $"{(hdg == 0 ? 360 : hdg):000}";
                }
            }
            selectedText.Text = a == null ? "None: click an aircraft on the map."
                : $"{a.Callsign}{(a.Formation is { Count: > 1 } f ? $" F{f.Count} ({string.Join(" ", f)})" : "")}  {a.PerformanceClass}  ·  {TowerCommands.Ends.First(e => e.End == a.EndAction).Short}  ·  {TowerView.LegText(a)}";
        }
    }
}
