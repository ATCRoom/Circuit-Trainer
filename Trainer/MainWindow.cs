using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ATCRoom.Maps;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The instructor's window (step 4a): the top view of the aerodrome and, on the right, the panel: the aerodrome (map,
    /// runway in use and where it is, its circuit side, the heights of the classes, the wind), the simulation (pause,
    /// speed, levels of the view, map layers), a new aircraft, and the commands of the selected aircraft.
    /// </summary>
    internal sealed class MainWindow : Window
    {
        private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x21, 0x28));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xE2, 0xEC));
        private static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA4));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x50));

        private readonly SimEngine engine = new();
        private readonly Airfield airfield = new();
        private readonly TowerCommands commands;
        private readonly TrainerSettings settings = TrainerSettings.Load();
        private readonly TowerView view = new();
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
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
        private readonly ChoiceRow<int> levelRow = new([(1, "L1"), (2, "L2")], (n, _) => n == 1 ? "Level 1 (F1): the aerodrome and its circuits." : "Level 2 (F2): the wide area, the IP and the long final.");
        /// <summary>The summaries of the closed sections of Settings.</summary>
        private readonly TextBlock gatesSummary = SectionSummary(), weatherSummary = SectionSummary(), circuitsSummary = SectionSummary(), mapSummary = SectionSummary();
        private readonly TextBox newRunwayName = new() { Width = 50 };
        private readonly TextBox newRunwayElevation = new() { Width = 60 };
        private readonly ComboBox heightClassBox = new();
        /// <summary>The heights of a class at this aerodrome: each circuit, the overhead's initial and break, the SFO's keys.</summary>
        private readonly TextBox conventionalHeight = new(), wideHeight = new(), closeHeight = new(), initialHeight = new(), breakHeight = new(),
            highKeyHeight = new(), lowKeyHeight = new();
        private readonly TextBox metarBox = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock windText = new();
        /// <summary>The METAR on the map: whole, or only the wind and the QNH.</summary>
        private readonly ChoiceRow<bool> metarShowRow = new([(false, "WHOLE"), (true, "WIND + QNH")], (only, _) => only
            ? "On the map only the station, the time, the wind and the QNH of the METAR." : "On the map the whole METAR.");
        private string appVersion = "";
        private readonly Button pauseButton = new();
        /// <summary>The same RUN / PAUSE in the Traffic List.</summary>
        private readonly Button trafficPauseButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 4, 0, 0) };
        /// <summary>The speed of the simulation (the user, 4 Nov: buttons, not a drop-down list).</summary>
        private readonly ChoiceRow<double> speedRow = new([(1.0, "x1"), (2.0, "x2"), (4.0, "x4")], (v, _) => v == 1 ? "Real time." : $"The time runs {v:0} times faster.");
        private readonly TextBox longFinalBox = new() { Width = 45 };
        private readonly TextBox initialPointBox = new() { Width = 45 };
        private readonly StackPanel layers = new();
        // New aircraft: rows of buttons (the choices kept for the next one).
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
        private readonly ChoiceRow<bool> departTurnRow = new([(false, "L"), (true, "R")], (right, _) => right ? "The turn to the radial to the right." : "The turn to the radial to the left.");
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
        private Grid toLine = null!, viaLine = null!, intervalLine = null!;
        /// <summary>The rows of New aircraft hidden when they do not apply (the user, 5 Nov: not grey, hidden).</summary>
        private Grid patternLine = null!, entryLine = null!, sideLine = null!, onFinalLine = null!, circuitsLine = null!;
        /// <summary>What every final does (the user, 5 Nov): F/S lands at the first one (arrivals and locals); T/G, L/A, L/P then the next circuit.</summary>
        private readonly ChoiceRow<EndAction> onFinalRow = new([(EndAction.FullStop, "F/S"), (EndAction.TouchAndGo, "T/G"), (EndAction.GoAround, "L/A"), (EndAction.LowPass, "L/P"), (PadEnd, "HPAD")],
            (_, t) => t == "HPAD" ? "Helicopter: it lands on a helipad (Helipad, below)." : "On each final: " + ChoiceTip(t));
        /// <summary>On Final HPAD (the user, 5 Nov; helicopters): a full stop on a helipad; a value of the row only, not of the engine.</summary>
        private const EndAction PadEnd = (EndAction)100;
        /// <summary>The helipad of New aircraft: a registered one, or POINT (clicked on the map).</summary>
        private ChoiceRow<string> padRow = new([]);
        private readonly StackPanel padHolder = new();
        private (double X, double Y)? padPoint;
        private Grid padLine = null!;
        /// <summary>The width of the names of the rows of New aircraft (the user, 5 Nov: longer, clearer names).</summary>
        private const double LabelWidth = 112;
        private readonly ChoiceRow<double> splitRow = new([(3.0, "3s"), (4.0, "4s"), (5.0, "5s"), (10.0, "10s")], (v, _) => $"At the split each one {v:0} s after the one before.");
        private readonly TextBox newGateName = new() { Width = 70 };
        private readonly TextBox newExitName = new() { Width = 70 };
        private readonly TextBox newPadName = new() { Width = 70 };
        /// <summary>A formation landing, both full stop (the user, 4 Nov; it depends on the country, kept in the aerodrome file).</summary>
        private readonly ChoiceRow<bool> separationRow = new([(false, "REDUCED RWY SEP"), (true, "FULL RWY SEP")], (full, _) => full
            ? "Full rwy separation: the next one of a formation lands once the one ahead has left the runway."
            : "Reduced rwy separation: the next one of a formation lands when the one ahead has rolled a third of the runway (both full stop).");
        /// <summary>The flight plans (advanced generation), their list and the status text of each row.</summary>
        private readonly List<TowerPlan> plans = [];
        private readonly StackPanel planList = new();
        private readonly StackPanel flyingList = new();
        /// <summary>The plan of the Traffic List being changed in New aircraft (the user, 4 Nov), or null; its buttons.</summary>
        private TowerPlan? editing;
        private Button toListButton = null!, cancelEditButton = null!, groupButton = null!;
        private string groupShown = "";
        private static readonly Brush CallsignBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0xC9, 0xF5));
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
        private Grid padEndLine = null!;
        private Button breakNowButton = null!, holdButton = null!, startSfoButton = null!, extendButton = null!, baseButton = null!, orbitButton = null!,
            orbitForeverButton = null!, goAroundButton = null!, sideLeftButton = null!, sideRightButton = null!, wideButton = null!, rejoinButton = null!, breakupButton = null!;
        private readonly ChoiceRow<CircuitType> circuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide?> sideRow = new([(null, "AD"), (CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => ChoiceTip(t));
        private ChoiceRow<string> classRow = new([]);
        private readonly StackPanel classHolder = new();
        private readonly TextBox callsignBox = new() { Width = 90 };
        private readonly TextBlock selectedText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBox headingBox = new() { Width = 55 }, heightBox = new() { Width = 55 }, speedKtBox = new() { Width = 55 };
        // The selected aircraft: each button an order at once.
        private readonly ChoiceRow<CircuitType> selCircuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => "From the next circuit (on the ground: CONV or WIDE only): " + ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide> selSideRow = new([(CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => "From the next circuit: " + ChoiceTip(t));
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
        private DockablePart settingsPart = null!, newPart = null!, trafficPart = null!, selectedPart = null!;
        private readonly StackPanel settingsContent = new(), newContent = new(), trafficContent = new(), selectedContent = new();
        /// <summary>Where a message is shown: in the part it is about (the user, 3 Nov), one line each.</summary>
        private enum Area { Aerodrome, Simulation, New, Traffic, Selected }
        private readonly Dictionary<Area, TextBlock> messages = Enum.GetValues<Area>().ToDictionary(area => area,
            _ => new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = WarnBrush, Margin = new Thickness(0, 6, 0, 0) });

        public MainWindow()
        {
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
            mapBar = new Border
            {
                Child = bar,
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x21, 0x28)),
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
            view.FieldClicked += LabelField;
            view.FieldWheel += LabelWheel;
            view.SpeedChoices = SpeedChoices;
            view.MenuAsked += callsign => OpenMenu(Menu(callsign));
            view.HeadingDragged += (callsign, heading) => { commands.Heading(callsign, heading); Say($"{callsign}: heading {heading:000} (out of the circuit; REJOIN brings it back).", Area.Selected); };
            Loaded += (_, _) => Start();
            Closing += (_, _) => settings.Save();
            PreviewKeyDown += OnKeys;
            timer.Tick += (_, _) => Tick();
        }

        // ----- Start -----------------------------------------------------------------------------------------------------

        private void Start()
        {
            Directory.CreateDirectory(Paths.Folder);
            (List<PerformanceClass> classes, _, string? note) = PerformanceFile.Load(Paths.Performance);
            engine.SetPerformanceClasses(classes);
            foreach (PerformanceClass kind in engine.Classes) heightClassBox.Items.Add(kind.Name);
            classRow = new ChoiceRow<string>(engine.Classes.Select(c => (c.Name, c.Name.ToUpperInvariant())), (name, _) => $"Class {name} (its values in performance.json; its heights at this aerodrome: Settings › Circuits).");
            // (The user, 4 Nov: the jet by default, the most used.)
            classRow.Show(engine.Classes.FirstOrDefault(c => string.Equals(c.Name, "Jet", StringComparison.OrdinalIgnoreCase))?.Name
                ?? (engine.Classes.Any(c => c.Name == PerformanceClass.DefaultName) ? PerformanceClass.DefaultName : engine.Classes[0].Name));
            classHolder.Children.Add(classRow);
            classRow.Picked += _ => ShowEntries();
            heightClassBox.SelectedIndex = heightClassBox.Items.Count > 0 ? 0 : -1;
            if (note != null) Say(note);
            engine.LongFinalNM = settings.LongFinalNM;
            engine.InitialPointNM = settings.InitialPointNM;
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
            metarBox.Text = settings.Metar;
            metarShowRow.Show(settings.MetarWindQnhOnly);
            ApplyMetar(quiet: true);
            if (settings.MapPath.Length > 0 && File.Exists(settings.MapPath)) OpenMap(settings.MapPath, settings.Runway);
            else ShowAerodrome();
            UpdatePause();
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Start();
        }

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double wall = now - lastTick;
            lastTick = now;
            engine.Advance(wall * speed);
            view.SetTraffic(engine.Snapshot(), engine.Time);
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
                bool allFloating = new[] { settingsPart, newPart, trafficPart, selectedPart }.All(part => part.IsFloating);
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
            if (e.Key == Key.F1) { GoLevel(1); e.Handled = true; }
            else if (e.Key == Key.F2) { GoLevel(2); e.Handled = true; }
            else if (e.Key == Key.Pause) { TogglePause(); e.Handled = true; }
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
            newPart = new DockablePart("New aircraft", newContent, panel, layer, Placement("New aircraft"), settings.Save);
            // (The user, 3 Nov: the flight plans as a part of their own, named as in the PAR trainer.)
            trafficPart = new DockablePart("Traffic List", trafficContent, panel, layer, Placement("Traffic List"), settings.Save);
            selectedPart = new DockablePart("Selected aircraft", selectedContent, panel, layer, Placement("Selected aircraft"), settings.Save);

            current = Section("Map", mapSummary, true);
            newRunwayName.ToolTip = "The name of the runway (for example 14).";
            newRunwayElevation.ToolTip = "The elevation of its threshold (ft): the heights of the labels are above it.";
            heightClassBox.ToolTip = "The class whose heights at this aerodrome are below.";
            metarBox.ToolTip = "A METAR line (or Get METAR): its wind is used and it is shown on the map (for example LIPI 031250Z 06012KT CAVOK 15/08 Q1018).";
            newGateName.ToolTip = "The name of the new gate (empty: the name of the VRP clicked).";
            longFinalBox.ToolTip = "Distance of the long final (NM): where LONG FIN starts.";
            initialPointBox.ToolTip = "Distance of the initial point before the threshold (NM).";
            callsignBox.ToolTip = "Empty: the next free callsign.";
            delayBox.ToolTip = "For the Traffic List: minutes after START ALL.";
            headingBox.ToolTip = "Heading (degrees true) for GO.";
            heightBox.ToolTip = "Height above the runway (ft) for GO.";
            speedKtBox.ToolTip = "Speed (kt) for GO.";
            current.Children.Add(mapText);
            Buttons(("Open map...", ChooseMap));
            Row("Runway in use", runwayHolder);
            Note("New runway (or its place again): its name and elevation, then click its threshold and its end on the map.");
            Row("Name / elevation ft", Inline(newRunwayName, newRunwayElevation));
            Buttons(("Place on the map", PlaceRunway), ("Remove runway", RemoveRunway));

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

            Note("Heights of a class at this aerodrome, ft above the runway (empty: the class's). Close: also the overhead's downwind; break empty: the close height.");
            Row("Class", heightClassBox);
            heightClassBox.SelectionChanged += (_, _) => ShowHeights();
            current.Children.Add(Labelled(("Conventional", conventionalHeight), ("Wide", wideHeight), ("Close", closeHeight)));
            current.Children.Add(Labelled(("Initial (OVH)", initialHeight), ("Break (OVH)", breakHeight)));
            current.Children.Add(Labelled(("High Key (SFO)", highKeyHeight), ("Low Key (SFO)", lowKeyHeight)));
            Buttons(("Save heights", SaveHeights));

            Row("Formation FS", separationRow);
            separationRow.Picked += full =>
            {
                airfield.Circuits.FullRunwaySeparation = full;
                string? error = airfield.Save();
                engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
                Say(error ?? (full ? "Full rwy separation: in a formation landing to a full stop, the next one lands once the one ahead has left the runway."
                    : "Reduced rwy separation: in a formation landing to a full stop, the next one lands when the one ahead has rolled a third of the runway."));
            };

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

            current = Section("Gates and exits", gatesSummary, false);
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

            current = newContent;
            Note("ARRIVAL: from a gate or the long final, it lands. LOCAL: from the runway, its circuits, it lands. DEPARTURE: from the runway, away (at once or after its circuits). TRANSIT: from gate to gate. Then ADD NOW, or TO LIST for the Traffic List. AD: the aerodrome's side (Settings › Circuits).");
            // (The user, 5 Nov: the class, the formation, then the type.)
            // (The user, 5 Nov: clearer names, a wider column for them.)
            Row("Type of AC", classHolder, null, LabelWidth);
            Row("Formation", formationRow, null, LabelWidth);
            intervalLine = Row("Break interval", splitRow, null, LabelWidth);
            Row("Type of flight", typeRow, null, LabelWidth);
            Row("Start from", fromHolder, null, LabelWidth);
            toLine = Row("Exit to", toHolder, null, LabelWidth);
            viaLine = Row("Via", viaRow, null, LabelWidth);
            typeRow.Show(Flight.Arrival);
            viaRow.Show(Via.Overhead);
            typeRow.Picked += type =>
            {
                // (The user, 5 Nov: a departure away at once by default.)
                if (type == Flight.Departure) lapsRow.Show(0);
                // (Back to one that lands: it lands at the first final unless asked.)
                if (type is Flight.Arrival or Flight.Local) onFinalRow.Show(EndAction.FullStop);
                ShowEntries();
            };
            viaRow.Picked += _ => ShowEntries();
            // (The user, 5 Nov: the pattern type first, then its entry, then its side.)
            patternLine = Row("Pattern type", circuitRow, null, LabelWidth);
            entryLine = Row("Pattern entry", entryRow, null, LabelWidth);
            sideLine = Row("Pattern side", sideRow, null, LabelWidth);
            // (The user, 5 Nov: what each final does, how many circuits; the end is the type's.)
            onFinalLine = Row("On Final", onFinalRow, null, LabelWidth);
            padLine = Row("Helipad", padHolder, null, LabelWidth);
            onFinalRow.Show(EndAction.FullStop);
            onFinalRow.Picked += _ => ShowEntries();
            circuitsLine = Row("Numb. of Circuits", lapsRow, null, LabelWidth);
            departLine = Row("Exit via", departRow, null, LabelWidth);
            RadialTip(radialBox);
            radialLine = Row("Radial °M", Wrap(radialBox, departTurnRow,
                ActionButton("FROM A POINT", () => view.AskPoint("Departure: click the point the radial starts from  -  Esc: cancel", (x, y) =>
                {
                    radialFrom = (x, y);
                    radialFromText.Text = "from the point clicked";
                })),
                ActionButton("MIDDLE", () => { radialFrom = null; radialFromText.Text = "from the middle of the runway"; }),
                radialFromText), null, LabelWidth);
            departRow.Show(false);
            departTurnRow.Show(true);
            departRow.Picked += _ => ShowEntries();
            // (The shorter turn as the radial is written; L / R clicked: the instructor's, kept.)
            radialBox.TextChanged += (_, _) => ShortTurnToRadial();
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
            };
            entryRow.Show(CircuitEntry.Downwind);
            circuitRow.Show(CircuitType.Conventional);
            sideRow.Show(null);
            RebuildFrom();
            // The rest when wanted (the user, 4 Nov): closed.
            StackPanel more = new();
            current.Children.Add(new Expander { Header = "More…", Content = more, Foreground = TextBrush, Margin = new Thickness(0, 4, 0, 2), IsExpanded = false });
            Row("Callsign", Inline(callsignBox, new TextBlock { Text = "(optional; a formation: ending with its number, VIPER01)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, LabelWidth);
            Row("Height ft", Inline(arrivalBox, new TextBlock { Text = "(at the gate / of the crossing / of a departure; empty: 500 above the circuit, departure 3000)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, LabelWidth);
            arrivalBox.ToolTip = "The height above the runway it arrives at, at the gate (empty: 500 ft above its circuit); it reaches the circuit height before the circuit. A departure: the height it climbs to when it leaves (empty: 3000 ft).";
            Row("Delay min", Inline(delayBox, new TextBlock { Text = "(for the Traffic List: after START ALL)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, LabelWidth);
            toListButton = ActionButton("TO LIST", AddPlan);
            cancelEditButton = ActionButton("CANCEL", CancelEdit);
            cancelEditButton.Visibility = Visibility.Collapsed;
            WrapPanel newButtons = Wrap(ActionButton("ADD NOW", AddAircraft), toListButton, cancelEditButton);
            newButtons.Margin = new Thickness(0, 2, 0, 0);
            current.Children.Add(newButtons);

            current = trafficContent;
            // (As in the PAR trainer, the user 4 Nov: the aircraft flying, one thin row each, and the plans waiting.)
            current.Children.Add(Caption("FLYING  (click: select · II freeze · ↺ its plan again · ✕ remove)"));
            current.Children.Add(flyingList);
            current.Children.Add(Caption("WAITING  (☐ GROUP · ▶ start · ⏸ hold · ⧉ duplicate · ✕ remove)"));
            current.Children.Add(planList);
            trafficPauseButton.Click += (_, _) => TogglePause();
            // (One row; more only when the part is narrow: the user, 4 Nov.)
            trafficPauseButton.Margin = new Thickness(0, 0, 4, 4);
            WrapPanel trafficButtons = Wrap(trafficPauseButton, ActionButton("START ALL", StartAllPlans), ActionButton("REMOVE ALL", RemoveAllAircraft),
                groupButton = ActionButton("GROUP", GroupOrUngroup), ActionButton("SAVE...", SaveScenario), ActionButton("LOAD...", LoadScenario), ActionButton("CLEAR ALL", ClearPlans));
            trafficButtons.Margin = new Thickness(0, 6, 0, 0);
            current.Children.Add(trafficButtons);

            current = selectedContent;
            current.Children.Add(selectedText);
            current.Children.Add(selectedPanel);

            StackPanel next = Group("2 · Next circuit", "Its plan: circuit, side and break from the next touch and go / go-around; the end of the final: this one.", selectedPanel);
            Row("Pattern", selCircuitRow, next, 62);
            // Another circuit: its side at this runway (then L / R to change it). Both from the next circuit.
            selCircuitRow.Picked += type => WithSelected(cs =>
            {
                CircuitSide side = engine.SideFor(type);
                selSideRow.Show(side);
                commands.NextCircuit(cs, type, side);
            });
            Row("Side", selSideRow, next, 62);
            selSideRow.Picked += side => WithSelected(cs => commands.NextCircuit(cs, selCircuitRow.HasValue ? selCircuitRow.Value : CircuitType.Conventional, side));
            Row("At the end", selEndRow, next, 62);
            selEndRow.Picked += end => WithSelected(cs =>
            {
                if (engine.Get(cs) is { CanHover: true } heli && (heli.HasPad || heli.WantsPad)) commands.Helipad(cs, double.NaN, double.NaN);
                commands.SetEnd(cs, end);
            });
            // (Only for the overhead and the close pattern, the user 5 Nov: the others have no break.)
            selBreakGrid = Row("Break", selBreakRow, next, 62);
            selBreakRow.Picked += where => WithSelected(cs => commands.SetBreak(cs, where));
            // (Helicopters, the user 5 Nov: the end of the final on a helipad.)
            padEndButton = ActionButton("HPAD", () => WithSelected(cs => PadMenu(cs, now: false)));
            padEndButton.ToolTip = "Helicopter: the final ends on a helipad (a registered one, or a point clicked on the map).";
            padEndLine = Row("Helipad", Inline(padEndButton), next, 62);
            selBreakPointGrid = Row("", Inline(ActionButton("BREAK AT A POINT", () => WithSelected(cs =>
                view.AskPoint($"{cs}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(cs, x, y))))), next, 62);

            StackPanel now = Group("3 · Now, in the circuit", "Standard orders at once, in the order of the circuit (grey: not where it is now).", selectedPanel);
            Button Order(string text, Action<string> action) => ActionButton(text, () => WithSelected(action));
            breakNowButton = Order("BREAK NOW", commands.BreakNow);
            holdButton = Order("HOLD HK", cs => commands.HoldAtHighKey(cs, true));
            startSfoButton = Order("START SFO", cs => commands.HoldAtHighKey(cs, false));
            extendButton = Order("EXTEND DWN", commands.ExtendDownwind);
            baseButton = Order("TURN BASE", commands.TurnBase);
            orbitButton = Order("ORBIT", commands.Orbit);
            // ORBIT ∞: orbits until CONTINUE (the same button while it orbits); Shift + ORBIT too.
            orbitForeverButton = Order("ORBIT ∞", cs =>
            {
                if (engine.Get(cs)?.OrbitForever == true) commands.Continue(cs); else commands.OrbitForever(cs);
            });
            orbitButton.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
                WithSelected(commands.OrbitForever);
                e.Handled = true;
            };
            goAroundButton = Order("GO AROUND", commands.GoAround);
            sideLeftButton = Order("SIDE NOW L", cs => SideNow(cs, CircuitSide.Left));
            sideRightButton = Order("SIDE NOW R", cs => SideNow(cs, CircuitSide.Right));
            wideButton = Order("WIDE NOW", cs => commands.FlyCircuit(cs, CircuitType.Wide, engine.Get(cs)?.CircuitSide ?? engine.SideFor(CircuitType.Wide)));
            Row("Arrival", Inline(breakNowButton, holdButton, startSfoButton), now, 62);
            Row("Downwind", Wrap(extendButton, baseButton, orbitButton, orbitForeverButton), now, 62);
            Row("Final", Inline(goAroundButton), now, 62);
            Row("Change", Wrap(sideLeftButton, sideRightButton, wideButton), now, 62);
            // (DCT TO, the user 5 Nov: at once, in flight.)
            dctButtons = [Order("DW", cs => commands.DirectTo(cs, DirectPoint.Downwind)), Order("BASE", cs => commands.DirectTo(cs, DirectPoint.Base)),
                Order("IP", cs => commands.DirectTo(cs, DirectPoint.InitialPoint)), Order("HK", cs => commands.DirectTo(cs, DirectPoint.HighKey))];
            dctButtons[0].ToolTip = "At once onto the downwind of the conventional circuit.";
            dctButtons[1].ToolTip = "At once straight to the start of the base of the conventional circuit, then the final.";
            dctButtons[2].ToolTip = "At once straight to the initial point, then the overhead.";
            dctButtons[3].ToolTip = "At once straight to the High Key, then the SFO.";
            padNowButton = Order("HPAD", cs => PadMenu(cs, now: true));
            padNowButton.ToolTip = "Helicopter: at once straight to a helipad (a registered one, or a point clicked on the map), a full stop there.";
            dctButtons = [.. dctButtons, padNowButton];
            Row("DCT TO", Wrap(dctButtons), now, 62);
            splitButton = Order("SPLIT", commands.Split);
            Row("Formation", Inline(splitButton), now, 62);
            expTaxiButton = Order("EXP TAXI", commands.ExpediteTaxi);
            Row("Runway", Inline(expTaxiButton), now, 62);

            StackPanel outside = Group("4 · Out of the circuit", "You fly it (MANUAL): heading, height above the runway, speed; or drag from its symbol on the map for a heading. REJOIN brings it back, BREAKUP takes it out.", selectedPanel);
            Row("Heading", Inline(headingBox, ActionButton("GO", () => WithSelected(cs => Number(headingBox, "a heading (0-360)", v => commands.Heading(cs, v))))), outside, 62);
            Row("Height ft", Inline(heightBox, ActionButton("GO", () => WithSelected(cs => Number(heightBox, "a height in feet above the runway", v => commands.Height(cs, v))))), outside, 62);
            Row("Speed kt", Inline(speedKtBox, ActionButton("GO", () => WithSelected(cs => Number(speedKtBox, "a speed in knots", v => commands.Speed(cs, v))))), outside, 62);
            rejoinButton = Order("REJOIN", commands.Rejoin);
            breakupButton = Order("BREAKUP", commands.Breakup);
            outside.Children.Add(Wrap(rejoinButton, breakupButton, Order("REMOVE", commands.Remove)));
            Note("Right click on an aircraft: all its commands. Its label can be dragged.", selectedPanel);
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

        private void Note(string text, Panel? into = null) => (into ?? current ?? panel).Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 2)
        });

        /// <summary>A group of the panel: a frame with its title and what it is for; its rows go in the panel returned.</summary>
        private static StackPanel Group(string title, string note, Panel into)
        {
            StackPanel inside = new();
            inside.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            inside.Children.Add(new TextBlock { Text = note, FontSize = 11, Foreground = DimBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            into.Children.Add(new Border
            {
                Child = inside,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x46, 0x50)),
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
            ["Save heights"] = "Save the heights of this class at this aerodrome (for the aircraft added from now on).",
            ["SPLIT"] = "The formation splits now: each one an aircraft of its own, the wingmen one interval after the other (overhead, close, SFO: only at the break, by itself).",
            ["GROUP"] = "The rows ticked (☐) into one formation, led by the first one's callsign (at most 4 aircraft).",
            ["REMOVE ALL"] = "Every aircraft out of the simulation (the rows stay, back to waiting).",
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
            ["ADD NOW"] = "Start this aircraft now.",
            ["CANCEL"] = "The plan being changed stays as it was.",
            ["TO LIST"] = "Put it in the Traffic List: started by ▶ or by START ALL after its delay.",
            ["START ALL"] = "Start every aircraft of the Traffic List waiting, each after its delay (held ones stay held).",
            ["SAVE..."] = "Save the flight plans as a scenario of this aerodrome.",
            ["LOAD..."] = "Load a scenario (its flight plans replace those of the list).",
            ["CLEAR ALL"] = "Every row out of the Traffic List (the aircraft flying go on).",
            ["▶"] = "Start this plan now.",
            ["⏸"] = "Hold this plan (it does not start); again: it goes on.",
            ["⏵"] = "Go on (it starts at its time).",
            ["⧉"] = "Duplicate: the next callsign, one minute later.",
            ["✕"] = "Remove this plan.",
            ["BREAK AT A POINT"] = "Overhead and close: click on the map where it breaks (along the runway).",
            ["BREAK NOW"] = "Overhead on the initial, close on the upwind: it breaks now (close: above 400 ft).",
            ["HOLD HK"] = "SFO: it waits circling over the High Key (until START SFO).",
            ["START SFO"] = "SFO: at the next pass over the High Key it starts the flame out pattern.",
            ["EXTEND DWN"] = "On the downwind: straight on beyond the base turn, until TURN BASE.",
            ["TURN BASE"] = "On the downwind: turn base now (also during an orbit). Still joining the circuit: straight to the start of the base.",
            ["ORBIT"] = "One orbit (360°) away from the runway, then on. Shift+click: orbits until CONTINUE.",
            ["ORBIT ∞"] = "Orbits until CONTINUE (this button while it orbits).",
            ["CONTINUE"] = "The orbits end: on along its circuit from here.",
            ["GO AROUND"] = "Only on the final: it stops descending, passes the threshold and keeps the runway heading until a new order.",
            ["SIDE NOW L"] = "Left-hand now: an S over the aerodrome onto the new downwind (on the final: a go-around).",
            ["SIDE NOW R"] = "Right-hand now: an S over the aerodrome onto the new downwind (on the final: a go-around).",
            ["WIDE NOW"] = "Out to the wide circuit now (to make room).",
            ["GO"] = "Give it: the aircraft leaves its circuit (MANUAL); REJOIN brings it back.",
            ["REJOIN"] = "Back into its circuit from where it is.",
            ["BREAKUP"] = "Out of the circuits: on the downwind a turn away from the runway, on the base on turning past the final. Then MANUAL.",
            ["REMOVE"] = "Remove this aircraft."
        };

        private static readonly Brush ActionBack = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0x86));
        private static readonly Brush ActionText = new SolidColorBrush(Color.FromRgb(0xB9, 0xCB, 0xD6));
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
            updating = false;
            ShowHeights();
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

        private void ShowHeights()
        {
            ClassCircuit? heights = HeightsOf(heightClassBox.SelectedItem as string);
            static string Text(double? value) => value is double v ? v.ToString("0", CultureInfo.InvariantCulture) : "";
            // (A file with one height for all the circuits: shown in each of them.)
            conventionalHeight.Text = Text(heights?.ConventionalHeightFt ?? heights?.CircuitHeightFt);
            wideHeight.Text = Text(heights?.WideHeightFt ?? heights?.CircuitHeightFt);
            closeHeight.Text = Text(heights?.CloseHeightFt ?? heights?.CircuitHeightFt);
            initialHeight.Text = Text(heights?.InitialHeightFt);
            breakHeight.Text = Text(heights?.BreakHeightFt);
            highKeyHeight.Text = Text(heights?.HighKeyFt);
            lowKeyHeight.Text = Text(heights?.LowKeyFt);
        }

        private void SaveHeights()
        {
            if (airfield.Map == null || heightClassBox.SelectedItem is not string name) { Say("Open a map first."); return; }
            double? Read(TextBox box)
            {
                string text = box.Text.Trim();
                return text.Length > 0 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0 ? value : null;
            }
            ClassCircuit heights = HeightsOf(name) ?? new ClassCircuit { Class = name };
            heights.CircuitHeightFt = null;
            heights.ConventionalHeightFt = Read(conventionalHeight);
            heights.WideHeightFt = Read(wideHeight);
            heights.CloseHeightFt = Read(closeHeight);
            heights.InitialHeightFt = Read(initialHeight);
            heights.BreakHeightFt = Read(breakHeight);
            heights.HighKeyFt = Read(highKeyHeight);
            heights.LowKeyFt = Read(lowKeyHeight);
            airfield.Circuits.Classes.RemoveAll(c => string.Equals(c.Class, name, StringComparison.OrdinalIgnoreCase));
            if (heights.ConventionalHeightFt != null || heights.WideHeightFt != null || heights.CloseHeightFt != null || heights.InitialHeightFt != null
                || heights.BreakHeightFt != null || heights.HighKeyFt != null || heights.LowKeyFt != null || heights.Extra != null) airfield.Circuits.Classes.Add(heights);
            string? error = airfield.Save();
            engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
            Say(error ?? $"Heights of {name} saved: for the aircraft added from now on.");
            ShowHeights();
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
            ShortTurnToRadial();
        }

        private static string VariationText(double variation) =>
            variation == 0 ? "0" : Math.Abs(variation).ToString("0.#", CultureInfo.InvariantCulture) + (variation > 0 ? "E" : "W");

        private static void RadialTip(TextBox box) =>
            box.ToolTip = "The radial (degrees magnetic, outbound) it intercepts after the take-off, from the middle of the runway or the point clicked.";

        /// <summary>The radial written (degrees magnetic), or NaN.</summary>
        private double RadialWritten() =>
            double.TryParse(radialBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double r) && r >= 0 && r <= 360 ? r % 360 : double.NaN;

        /// <summary>The turn to the radial the shorter way from the runway heading (the user, 5 Nov: the default; L / R change it).</summary>
        private void ShortTurnToRadial()
        {
            double radial = RadialWritten();
            if (turnChosen || double.IsNaN(radial) || double.IsNaN(airfield.Course)) return;
            double toward = radial + (airfield.Circuits.MagneticVariation ?? 0) - airfield.Course;
            departTurnRow.Show(SimEngine.Wrap(toward) >= 0);
        }

        /// <summary>The plan made of the rows of group 1 now.</summary>
        private TowerPlan PlanOfRows()
        {
            Flight Type() => typeRow.HasValue ? typeRow.Value : Flight.Arrival;
            int Laps() => lapsRow.HasValue ? lapsRow.Value : 1;
            EndAction OnFinal() => onFinalRow.HasValue ? onFinalRow.Value : EndAction.FullStop;
            double delay = double.TryParse(delayBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d >= 0 ? d : 0;
            return new TowerPlan
            {
                Callsign = callsignBox.Text.Trim().ToUpperInvariant(),
                Class = classRow.Value ?? "",
                From = fromRow.Value ?? From.LongFinal,
                Entry = entryRow.Value,
                Circuit = circuitRow.Value,
                Side = sideRow.Value,
                // (The plan of 5 Nov: On Final and Circuits; End and Laps only for the scenarios of before.)
                End = OnFinal() == PadEnd ? EndAction.FullStop : OnFinal(),
                OnFinal = OnFinal() == PadEnd ? EndAction.FullStop : OnFinal(),
                ToPad = OnFinal() == PadEnd,
                PadName = OnFinal() == PadEnd && padRow.HasValue && padRow.Value != PadPointName ? padRow.Value ?? "" : "",
                PadX = OnFinal() == PadEnd && padRow.Value == PadPointName ? padPoint?.X : null,
                PadY = OnFinal() == PadEnd && padRow.Value == PadPointName ? padPoint?.Y : null,
                Circuits = (OnFinal() is EndAction.FullStop or PadEnd) && (Type() is Flight.Arrival or Flight.Local) ? 0 : Laps(),
                Local = Type() == Flight.Local,
                TransitCircuits = Type() == Flight.Transit && viaRow.Value == Via.Circuits,
                Formation = formationRow.HasValue ? formationRow.Value : 1,
                Lands = null,
                SplitSeconds = splitRow.HasValue ? splitRow.Value : 3,
                Transit = Type() == Flight.Transit,
                Departure = Type() == Flight.Departure,
                Radial = Type() == Flight.Departure && departRow.HasValue && departRow.Value ? RadialWritten() : null,
                TurnRight = Type() == Flight.Departure && departTurnRow.HasValue ? departTurnRow.Value : null,
                RadialFromX = radialFrom?.X,
                RadialFromY = radialFrom?.Y,
                To = toRow.HasValue ? toRow.Value ?? "" : "",
                Overhead = Type() == Flight.Transit && viaRow.Value == Via.Overhead,
                HeightFt = double.TryParse(arrivalBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && h > 0 ? h : null,
                DelayMinutes = delay
            };
        }

        private void AddAircraft()
        {
            TowerPlan plan = PlanOfRows();
            string? error = Launch(plan, out string added);
            Say(error ?? (plan.ToPad && PadOf(plan) == null ? "No helipad yet: choose it in flight with HPAD, else it lands on the runway." : null), Area.New);
            if (error != null) return;
            // (The user, 3 Nov: the aircraft added at once are in the Traffic List too, flying.)
            Started(plan, added);
            // (In FLYING only, gone with it: not a plan of the list, as ADD NOW in the PAR trainer.)
            plan.Quick = true;
            plans.Add(plan);
            ShowPlans();
            callsignBox.Text = "";
            view.SetTraffic(engine.Snapshot(), engine.Time);
            view.Select(added);
        }

        /// <summary>An aircraft of a plan, now: from its gate, the runway or already in the circuit. Returns an error, or null.</summary>
        private string? Launch(TowerPlan plan, out string added)
        {
            plan.Upgrade();
            string? error = LaunchOne(plan, out added);
            if (error != null) return error;
            PlanOfCircuits(plan, added);
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
                var exit = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.To, StringComparison.OrdinalIgnoreCase));
                commands.SetLaps(added, Math.Max(1, circuits), each, each);
                commands.LeaveAfterCircuits(added, double.NaN, null, null, null, plan.HeightFt, exit.Name != null ? exit.X : double.NaN, exit.Name != null ? exit.Y : double.NaN);
                return;
            }
            if (plan.Departure)
            {
                if (circuits <= 0) return;
                commands.SetLaps(added, circuits, each, each);
                commands.LeaveAfterCircuits(added, RadialTrue(plan), plan.TurnRight, plan.RadialFromX, plan.RadialFromY, plan.HeightFt, double.NaN, double.NaN);
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

        /// <summary>The row of the helipads of New aircraft built again (the registered ones, then POINT).</summary>
        private void RebuildPads()
        {
            string? was = padRow.HasValue ? padRow.Value : null;
            padRow = new ChoiceRow<string>([.. airfield.Helipads.Select(h => (h.Name, h.Name)), (PadPointName, "POINT")],
                (value, _) => value == PadPointName ? "A point clicked on the map (click POINT again for another one)." : $"The helipad {value}.");
            padRow.Picked += value =>
            {
                if (value == PadPointName) view.AskPoint("Helipad: click where the helicopter lands  -  Esc: cancel", (x, y) => padPoint = (x, y));
            };
            padRow.Show(was != null && (was == PadPointName || airfield.Helipads.Any(h => h.Name == was)) ? was : airfield.Helipads.Count > 0 ? airfield.Helipads[0].Name : PadPointName);
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
                // A departure away at once (the user, 5 Nov: OUT): the runway heading, or the radial (magnetic: to true here).
                return commands.AddDeparture(RadialTrue(plan), plan.TurnRight, plan.RadialFromX, plan.RadialFromY, plan.HeightFt, kind, callsign, out added);
            }
            if (plan.From == From.Runway)
            {
                CircuitType type = plan.Circuit is CircuitType.Wide ? CircuitType.Wide : CircuitType.Conventional;
                return commands.Add(CircuitStart.Runway, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (plan.Transit)
            {
                var start = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.From, StringComparison.OrdinalIgnoreCase));
                var exit = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.To, StringComparison.OrdinalIgnoreCase));
                if (start.Name == null) return "A crossing starts at a gate: choose it in Start from.";
                if (exit.Name == null) return "A crossing ends at a gate: choose it in Exit to.";
                // (With circuits, the user 5 Nov: into the circuit from the gate From, out to the gate To later.)
                if (plan.TransitCircuits) return commands.AddAtGate(start.X, start.Y, plan.Entry, plan.Circuit, plan.Side, plan.End, kind, callsign, plan.HeightFt, out added);
                if (string.Equals(start.Name, exit.Name, StringComparison.OrdinalIgnoreCase)) return "A crossing goes to another gate (Exit to).";
                return commands.AddTransit(start.X, start.Y, exit.X, exit.Y, plan.Overhead, plan.HeightFt, kind, callsign, out added);
            }
            if (plan.From == From.LongFinal)
            {
                if (plan.Entry == CircuitEntry.Circling) return commands.Add(CircuitStart.Circling, CircuitType.Conventional, plan.Side, plan.End, kind, callsign, false, out added);
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
            var gate = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.From, StringComparison.OrdinalIgnoreCase));
            if (gate.Name == null) return $"There is no gate {plan.From} at this aerodrome.";
            return commands.AddAtGate(gate.X, gate.Y, plan.Entry, plan.Circuit, plan.Side, plan.End, kind, callsign, plan.HeightFt, out added);
        }

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
                plans[at] = plan;
                Say($"{PlanText(plan)}: changed.", Area.Traffic);
            }
            else plans.Add(plan);
            EndEdit();
            callsignBox.Text = "";
            turnChosen = false;
            ShowPlans();
        }

        /// <summary>A plan of WAITING into New aircraft, to change it (UPDATE) or to start a copy (ADD NOW).</summary>
        private void EditPlan(TowerPlan p)
        {
            editing = p;
            p.Upgrade();
            // (PTRN is gone, the user 5 Nov: such an old plan into the first gate.)
            if (p.From.Length > 0) fromRow.Show(From.IsInCircuit(p.From) ? FromItems()[0] : p.From);
            typeRow.Show(p.Transit ? Flight.Transit : p.Departure ? Flight.Departure : p.Local ? Flight.Local : Flight.Arrival);
            departRow.Show(p.Radial != null);
            radialBox.Text = p.Radial is double r && !double.IsNaN(r) ? r.ToString("0", CultureInfo.InvariantCulture) : "";
            if (p.TurnRight is bool right) departTurnRow.Show(right);
            turnChosen = p.TurnRight != null;
            radialFrom = p.RadialFromX is double fx && p.RadialFromY is double fy ? (fx, fy) : null;
            radialFromText.Text = radialFrom != null ? "from the point clicked" : "from the middle of the runway";
            if (p.To.Length > 0) toRow.Show(p.To);
            viaRow.Show(p.TransitCircuits ? Via.Circuits : p.Overhead ? Via.Overhead : Via.Direct);
            entryRow.Show(p.Entry);
            circuitRow.Show(p.Circuit);
            sideRow.Show(p.Side);
            if (p.Class.Length > 0) classRow.Show(p.Class);
            onFinalRow.Show(p.ToPad ? PadEnd : p.OnFinal is EndAction.FullStop or EndAction.TouchAndGo or EndAction.GoAround or EndAction.LowPass ? p.OnFinal : EndAction.FullStop);
            if (p.ToPad)
            {
                padPoint = p.PadX is double px && p.PadY is double py ? (px, py) : padPoint;
                padRow.Show(p.PadName.Length > 0 ? p.PadName : PadPointName);
            }
            int circuits = p.Circuits ?? 0;
            lapsRow.Show(circuits == 0 && !p.Departure ? 1 : circuits);
            formationRow.Show(Math.Clamp(p.Formation, 1, SimEngine.MaxFormation));
            splitRow.Show(p.SplitSeconds is 3.0 or 4.0 or 5.0 or 10.0 ? p.SplitSeconds : 3.0);
            intervalLine.Visibility = p.Formation > 1 ? Visibility.Visible : Visibility.Collapsed;
            callsignBox.Text = p.Callsign;
            arrivalBox.Text = p.HeightFt is double h ? h.ToString("0", CultureInfo.InvariantCulture) : "";
            delayBox.Text = p.DelayMinutes.ToString("0.#", CultureInfo.InvariantCulture);
            ShowEntries();

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
            toListButton.Content = "TO LIST";
            toListButton.ToolTip = Tips["TO LIST"];
            cancelEditButton.Visibility = Visibility.Collapsed;
        }

        private static string PlanText(TowerPlan p)
        {
            p.Upgrade();
            static string Short(EndAction end) => TowerCommands.Ends.FirstOrDefault(e => e.End == end).Short ?? "";
            string side = p.Side is CircuitSide s ? (s == CircuitSide.Left ? " L" : " R") : "";
            string circuit = TowerCommands.Circuits.FirstOrDefault(c => c.Type == p.Circuit).Short ?? "";
            int circuits = p.Circuits ?? 0;
            string entry = p.From is From.Runway ? "T/O" : p.From is From.LongFinal ? (p.Entry == CircuitEntry.Circling ? "CIRCLING" : "FINAL") : p.Entry.ToString().ToUpperInvariant();
            string height = p.HeightFt is double hf ? $" {hf:0} ft" : "";
            string team = p.Formation > 1 ? $" F{p.Formation}" : "";
            string who = $"{(p.Callsign.Length > 0 ? p.Callsign : "(auto)")}{team} {p.Class}";
            string delay = $"+{p.DelayMinutes:0.#}'";
            string way = p.Radial is double r ? $"R{r:000}°M {(p.TurnRight == false ? "L" : "R")}{(p.RadialFromX != null ? " (point)" : "")}" : "RWY HDG";
            if (p.Transit)
            {
                if (p.TransitCircuits) return $"{who}  TRANSIT {p.From}{height} → {entry} {circuit}{side} {circuits}× {Short(p.OnFinal)} → {p.To}  {delay}";
                return $"{who}  TRANSIT {p.From} → {(p.Overhead ? "OVHD → " : "")}{p.To}{height}  {delay}";
            }
            if (p.Departure)
            {
                if (circuits <= 0) return $"{who}  DEP{height} OUT {way}  {delay}";
                return $"{who}  DEP{height} → {circuit}{side} {circuits}× {Short(p.OnFinal)} → OUT {way}  {delay}";
            }
            string plan = p.ToPad ? $"HPAD {(p.PadName.Length > 0 ? p.PadName : "point")}" : p.OnFinal == EndAction.FullStop ? Short(EndAction.FullStop)
                : circuits == TowerPlan.Forever ? $"∞ {Short(p.OnFinal)}" : $"{Short(p.OnFinal)} {circuits}× → {Short(EndAction.FullStop)}";
            return $"{who}  {(p.Local ? "LOCAL" : p.From)}{height} → {entry} {circuit}{side} {plan}  {delay}";
        }

        /// <summary>The rows of the plans, built again (after a change).</summary>
        /// <summary>The aircraft of each plan shown (the list built again when they change: a formation split, one removed).</summary>
        private string plansKey = "";

        /// <summary>The plans waiting (not flying; those of ADD NOW are only in FLYING).</summary>
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
            Border holder = new() { Child = row, BorderBrush = Brushes.Gold, BorderThickness = new Thickness(0) };
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
        private static (string Callsign, string Detail) PlanParts(TowerPlan p)
        {
            string callsign = (p.Callsign.Length > 0 ? p.Callsign : "(auto)") + (p.Formation > 1 ? $" F{p.Formation}" : "");
            string text = PlanText(p);
            return (callsign, text.StartsWith(callsign, StringComparison.Ordinal) ? text[callsign.Length..].TrimStart() : text);
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
                    p.Now = p.Now == TowerPlan.State.Held ? (p.Due > 0 ? TowerPlan.State.Counting : TowerPlan.State.Waiting) : TowerPlan.State.Held;
                    UpdatePlans();
                });
                Button tick = SmallButton(p.Picked ? "☑" : "☐", () => { p.Picked = !p.Picked; ShowPlans(); });
                tick.ToolTip = "Tick the rows to GROUP into a formation.";
                (string name, string rest) = PlanParts(p);
                TextBlock callsign = CallsignText(name);
                TextBlock text = RowText(rest);
                // A click on the row: the plan into New aircraft, to change it (the user, 4 Nov).
                foreach (TextBlock clickable in new[] { callsign, text })
                {
                    clickable.Cursor = Cursors.Hand;
                    clickable.ToolTip = "Click: change it in New aircraft (UPDATE).";
                    clickable.MouseLeftButtonUp += (_, _) => EditPlan(p);
                }
                if (ReferenceEquals(p, editing)) callsign.Foreground = text.Foreground = Brushes.Gold;
                planList.Children.Add(ThinRow(planList, MoveWaiting, callsign, text, tick, SmallButton("▶", () => StartPlan(p)), hold, SmallButton("⧉", () => DuplicatePlan(p)),
                    SmallButton("✕", () => { plans.Remove(p); ShowPlans(); })));
                planRows.Add((p, text, hold));
                planCallsigns[p] = callsign;
            }
            if (planList.Children.Count == 0) planList.Children.Add(new TextBlock { Text = "None: New aircraft, then TO LIST.", Foreground = DimBrush, FontSize = 11 });
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
                remove.ToolTip = "Out of the simulation (its plan waits again; one of ADD NOW is gone).";
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
            if (plan == null) { Say($"{callsign} is not from a plan of the Traffic List (ADD NOW).", Area.Traffic); return; }
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
                    TowerPlan.State.Held => "HELD ",
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
                    Math.Max(0, Math.Round((a.AltitudeFt - airfield.ElevationFt) / 100)), Math.Round(a.GroundSpeedKt), a.Frozen ? "FROZEN" : TowerView.LegText(a));
                // (The selected one in yellow, as on the map.)
                bool chosen = a.Callsign == view.Selected;
                text.Foreground = chosen ? Brushes.Gold : TextBrush;
                if (flyingCallsigns.TryGetValue(cs, out TextBlock? name)) name.Foreground = chosen ? Brushes.Gold : CallsignBrush;
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

        /// <summary>REMOVE ALL: every aircraft out of the simulation; the rows of the Traffic List back to waiting (the user, 3 Nov).</summary>
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
            items.Add((From.LongFinal, "LONG FIN"));
            items.Add((From.Runway, "RWY"));
            fromRow = new ChoiceRow<string>(items, (value, _) => value switch
            {
                From.LongFinal => "On the final at the long final distance (Simulation), on the glide path.",
                From.Runway => "On the runway (a departure): it takes off, then the conventional or the wide circuit, or away (Circuits 0).",
                _ => $"Entry gate {value}: it starts here and joins its circuit by itself (Entry)."
            });
            fromRow.Picked += from =>
            {
                if (from == From.Runway && circuitRow.Value is not (CircuitType.Conventional or CircuitType.Wide)) circuitRow.Show(CircuitType.Conventional);
                ShowEntries();
            };
            fromRow.Show(was != null && items.Any(i => i.Item1 == was) ? was : items[0].Item1);
            fromHolder.Children.Clear();
            fromHolder.Children.Add(fromRow);
            // The exit gates of a crossing.
            string? wasTo = toRow.HasValue ? toRow.Value : null;
            List<(string, string)> gates = airfield.Gates.Select(g => (g.Name, g.Name)).ToList();
            toRow = new ChoiceRow<string>(gates, (value, _) => $"Exit gate {value}: the crossing aircraft is gone there.");
            if (gates.Count > 0) toRow.Show(wasTo != null && gates.Any(g => g.Item1 == wasTo) ? wasTo : gates[^1].Item1);
            toHolder.Children.Clear();
            toHolder.Children.Add(gates.Count > 0 ? toRow : new TextBlock { Text = "(no gates: add them in Aerodrome)", Foreground = DimBrush, FontSize = 11 });
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
        private List<string> FromItems() => [.. airfield.Gates.Select(g => g.Name), From.LongFinal, From.Runway];

        /// <summary>The entries that apply to where it starts: none from the long final or the runway; in the circuit: DW, IP, HK.</summary>
        private void ShowEntries()
        {
            // (The user, 5 Nov: four types, each with its end; the rows that apply lit, the others grey.)
            Flight type = typeRow.HasValue ? typeRow.Value : Flight.Arrival;
            bool transit = type == Flight.Transit, departure = type == Flight.Departure, local = type == Flight.Local, arrival = type == Flight.Arrival;
            bool runway = departure || local;
            Via via = viaRow.HasValue ? viaRow.Value : Via.Overhead;
            bool withCircuits = transit && via == Via.Circuits;
            // From: the runway for departures and locals; the gates (and the long final, for arrivals) for the others.
            foreach (string item in FromItems())
                fromRow.SetEnabled(item, runway ? item == From.Runway : item != From.Runway && !(transit && item == From.LongFinal));
            if (runway && fromRow.Value != From.Runway) fromRow.Show(From.Runway);
            if (!runway && (fromRow.Value == From.Runway || (transit && fromRow.Value == From.LongFinal))
                && FromItems().FirstOrDefault(i => i != From.Runway && !(transit && i == From.LongFinal)) is string other) fromRow.Show(other);
            string from = fromRow.Value ?? "";
            // Pattern: after a take-off the conventional or the wide (the others once it flies).
            foreach (CircuitType circuitType in Enum.GetValues<CircuitType>()) circuitRow.SetEnabled(circuitType, !runway || circuitType is CircuitType.Conventional or CircuitType.Wide);
            if (runway && circuitRow.Value is not (CircuitType.Conventional or CircuitType.Wide)) circuitRow.Show(CircuitType.Conventional);
            // On Final: F/S only for those that land.
            onFinalRow.SetEnabled(EndAction.FullStop, arrival || local);
            if (!(arrival || local) && onFinalRow.Value == EndAction.FullStop) onFinalRow.Show(EndAction.TouchAndGo);
            // HPAD: helicopters that land (the user, 5 Nov).
            bool helicopter = engine.Classes.FirstOrDefault(c => c.Name == classRow.Value)?.CanHover == true;
            bool padAllowed = helicopter && (arrival || local);
            onFinalRow.SetEnabled(PadEnd, padAllowed);
            if (!padAllowed && onFinalRow.Value == PadEnd) onFinalRow.Show(arrival || local ? EndAction.FullStop : EndAction.TouchAndGo);
            bool landsFirst = (arrival || local) && onFinalRow.Value is EndAction.FullStop or PadEnd;
            // Circuits: OUT for departures, ∞ for arrivals and locals.
            lapsRow.SetEnabled(0, departure);
            lapsRow.SetEnabled(TowerPlan.Forever, arrival || local);
            int laps = lapsRow.HasValue ? lapsRow.Value : 1;
            if ((!departure && laps == 0) || (!(arrival || local) && laps == TowerPlan.Forever)) lapsRow.Show(1);
            bool away = departure && lapsRow.Value == 0;
            bool inCircuit = !transit || withCircuits;
            if (departLine == null) return;
            static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;
            patternLine.Visibility = sideLine.Visibility = onFinalLine.Visibility = Shown(inCircuit && !away);
            circuitsLine.Visibility = Shown(departure || withCircuits || ((arrival || local) && !landsFirst));
            padLine.Visibility = Shown(padAllowed && onFinalRow.Value == PadEnd && inCircuit);
            entryLine.Visibility = Shown(arrival || withCircuits);
            departLine.Visibility = departure ? Visibility.Visible : Visibility.Collapsed;
            radialLine.Visibility = departure && departRow.HasValue && departRow.Value ? Visibility.Visible : Visibility.Collapsed;
            // (The crossing's rows only for a crossing.)
            toLine.Visibility = viaLine.Visibility = transit ? Visibility.Visible : Visibility.Collapsed;
            bool longFinal = from == From.LongFinal;
            // (From the long final: straight on (FINAL) or the circling; the circling only from there.)
            foreach (CircuitEntry entry in Enum.GetValues<CircuitEntry>())
                entryRow.SetEnabled(entry, longFinal ? entry is CircuitEntry.Final or CircuitEntry.Circling : entry != CircuitEntry.Circling);
            if (longFinal && entryRow.Value is not (CircuitEntry.Final or CircuitEntry.Circling)) entryRow.Show(CircuitEntry.Final);
            if (!longFinal && entryRow.Value == CircuitEntry.Circling) entryRow.Show(CircuitEntry.Downwind);
            if (longFinal && entryRow.Value == CircuitEntry.Circling) circuitRow.Show(CircuitType.Conventional);
            if (longFinal && circuitRow.Value is CircuitType.Overhead or CircuitType.Sfo) circuitRow.Show(CircuitType.Conventional);
        }

        private void AddGate()
        {
            if (airfield.Map == null) { Say("Open a map first."); return; }
            string typed = newGateName.Text.Trim().ToUpperInvariant();
            view.AskPoint("Entry gate: click it on the map (a VRP gives its name)  -  Esc: cancel", (x, y) =>
            {
                string name = typed.Length > 0 ? typed : view.MapPointNear(x, y)?.ToUpperInvariant() ?? $"G{airfield.Circuits.Gates.Count + 1}";
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
                    view.PendingHeightFt = Math.Clamp(Math.Round(from / 100) * 100 - step * 100, 100, 15000);
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

        // ----- The selected aircraft -------------------------------------------------------------------------------------

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
                breakNowButton.IsEnabled = (a.Circuit == CircuitType.Overhead && leg == "INITIAL") || (a.Circuit == CircuitType.Close && leg is "UPWIND" or "GO AROUND");
                holdButton.IsEnabled = sfo && !a.SfoHold && leg is "CLIMB" or "HIGH KEY";
                startSfoButton.IsEnabled = sfo && a.SfoHold;
                extendButton.IsEnabled = leg is "DOWNWIND" or "ORBIT" or "ORBITS";
                baseButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS"
                    || (leg == "JOIN" && a.Circuit is CircuitType.Conventional or CircuitType.Wide or CircuitType.Close);
                orbitButton.IsEnabled = (inCircuit && leg is "DOWNWIND" or "EXT DOWNWIND" or "UPWIND" or "CROSSWIND" or "JOIN" or "CIRCLING") || leg == "TRANSIT";
                orbitForeverButton.IsEnabled = a.OrbitForever || orbitButton.IsEnabled;
                // (Orbiting on and on: a thick border, the user 4 Nov; a click: out of the orbit.)
                orbitForeverButton.Content = "ORBIT ∞";
                orbitForeverButton.ToolTip = Tips[a.OrbitForever ? "CONTINUE" : "ORBIT ∞"];
                orbitForeverButton.BorderBrush = a.OrbitForever ? Brushes.Gold : Brushes.Transparent;
                orbitForeverButton.BorderThickness = new Thickness(a.OrbitForever ? 3 : 1);
                goAroundButton.IsEnabled = leg == "FINAL";
                sideLeftButton.IsEnabled = sideRightButton.IsEnabled = wideButton.IsEnabled = inCircuit;
                rejoinButton.IsEnabled = a.Contact == GroundContact.Airborne && (a.Circuit == null || leg is "MANUAL" or "GO AROUND");
                breakupButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS" or "BASE";
                splitButton.IsEnabled = a.SplitAllowed;
                expTaxiButton.IsEnabled = leg == "LANDED" && !a.ExpediteTaxi;
            }
            selectedText.Text = a == null ? "None: click an aircraft on the map."
                : $"{a.Callsign}{(a.Formation is { Count: > 1 } f ? $" F{f.Count} ({string.Join(" ", f)})" : "")}  {a.PerformanceClass}\n{Math.Round(a.AltitudeFt - airfield.ElevationFt):0} ft  {Math.Round(a.GroundSpeedKt):0} kt  {TowerView.LegText(a)}";
        }
    }
}
