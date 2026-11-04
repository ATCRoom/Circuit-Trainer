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
        private readonly ComboBox runwayBox = new();
        /// <summary>The side of each group of circuits at the runway in use: conventional and wide, jet (overhead, close), SFO.</summary>
        private readonly ComboBox conventionalSideBox = SideCombo();
        private readonly ComboBox jetSideBox = SideCombo();
        private readonly ComboBox sfoSideBox = SideCombo();
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
        private readonly ChoiceRow<bool> typeRow = new([(false, "ARRIVAL"), (true, "TRANSIT")], (transit, _) => transit
            ? "Crossing the zone: from the gate From to the gate To, at its height, not landing; gone at the gate To."
            : "Arriving: it joins its circuit (Entry) and lands or flies the circuit.");
        private ChoiceRow<string> toRow = new([]);
        private readonly StackPanel toHolder = new();
        private readonly ChoiceRow<bool> viaRow = new([(false, "DIRECT"), (true, "OVERHEAD")], (overhead, _) => overhead
            ? "Crossing over the field (the middle of the runway), then to the gate To." : "Crossing straight from gate to gate.");
        private readonly TextBox arrivalBox = new() { Width = 55 };
        /// <summary>A formation (the user, 4 Nov): how many, which of them land together, the seconds between them at the split.</summary>
        private readonly ChoiceRow<int> formationRow = new([(1, "1"), (2, "2"), (3, "3"), (4, "4")], (n, _) => n == 1
            ? "Alone." : $"A formation of {n}: one label until it splits (its wingmen: the numbers after the leader's: VIPER01, 02...).");
        /// <summary>The plan of the circuits: how many (0: on and on, touch and go), how the last one ends.</summary>
        private readonly ChoiceRow<int> lapsRow = new([(1, "1"), (2, "2"), (3, "3"), (4, "4"), (0, "∞")], (n, _) => n == 0
            ? "Circuits on and on, each with a touch and go (until you change it)." : n == 1 ? "One circuit, ended as in Last." : $"{n} circuits: {n - 1} with a touch and go, the last ended as in Last.");
        private readonly ChoiceRow<EndAction> lastRow = new([(EndAction.FullStop, "FS"), (EndAction.Departure, "DEP")], (end, _) => end == EndAction.FullStop
            ? "The last circuit ends with a full stop (landed, then gone)." : "The last circuit ends with a touch and go, then it leaves straight ahead.");
        private Grid toLine = null!, viaLine = null!, intervalLine = null!;
        private readonly ChoiceRow<double> splitRow = new([(3.0, "3"), (4.0, "4"), (5.0, "5"), (10.0, "10")], (v, _) => $"At the split each one {v:0} s after the one before.");
        private readonly TextBox newGateName = new() { Width = 70 };
        private readonly TextBox newExitName = new() { Width = 70 };
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
        private Button toListButton = null!, cancelEditButton = null!;
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
        private readonly ChoiceRow<CircuitType> selCircuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => "From the next circuit: " + ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide> selSideRow = new([(CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => "From the next circuit: " + ChoiceTip(t));
        private readonly ChoiceRow<EndAction> selEndRow = new(TowerCommands.Ends.Select(e => (e.End, e.Short)), (_, t) => "At the end of this final: " + ChoiceTip(t));
        private readonly ChoiceRow<BreakPoint> selBreakRow = new(TowerCommands.Breaks.Select(b => (b.Break, b.Short)), (_, t) => ChoiceTip("break " + t));
        private readonly StackPanel selectedPanel = new();
        /// <summary>The layer over the map where the parts taken out of the column float (empty: clicks go to the map).</summary>
        private readonly Canvas layer = new();
        /// <summary>The column of the parts on the right: hidden when they all float (the user, 4 Nov).</summary>
        private ScrollViewer column = null!;
        /// <summary>The four parts of the panel (in the column or floating).</summary>
        private DockablePart aerodromePart = null!, simulationPart = null!, newPart = null!, trafficPart = null!, selectedPart = null!;
        private readonly StackPanel aerodromeContent = new(), simulationContent = new(), newContent = new(), trafficContent = new(), selectedContent = new();
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
            map.Children.Add(layer);
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
            classRow = new ChoiceRow<string>(engine.Classes.Select(c => (c.Name, c.Name.ToUpperInvariant())), (name, _) => $"Class {name} (its values in performance.json; its heights at this aerodrome above).");
            // (The user, 4 Nov: the jet by default, the most used.)
            classRow.Show(engine.Classes.FirstOrDefault(c => string.Equals(c.Name, "Jet", StringComparison.OrdinalIgnoreCase))?.Name
                ?? (engine.Classes.Any(c => c.Name == PerformanceClass.DefaultName) ? PerformanceClass.DefaultName : engine.Classes[0].Name));
            classHolder.Children.Add(classRow);
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
                aerodromePart.SummaryText = airfield.Map == null ? "no map" : $"{airfield.Code} · {airfield.Runway?.Runway ?? "no runway"} · " + (sp < 0.5 ? "calm" : $"{f:000}/{sp:0}" + (g > 0 ? $"G{sp + g:0}" : ""));
                simulationPart.SummaryText = $"{(engine.Paused ? "paused" : "running")} · x{speed:0}";
                int waitingCount = plans.Count(p => p.Now != TowerPlan.State.Flying && !p.Quick);
                trafficPart.SummaryText = $"{engine.Count} flying" + (waitingCount > 0 ? $" · {waitingCount} waiting" : "");
                bool allFloating = new[] { aerodromePart, simulationPart, newPart, trafficPart, selectedPart }.All(part => part.IsFloating);
                column.Visibility = allFloating ? Visibility.Collapsed : Visibility.Visible;
                selectedPart.SummaryText = view.Selected ?? "";
            }
        }

        private void OnKeys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1) { view.ShowLevel(1); e.Handled = true; }
            else if (e.Key == Key.F2) { view.ShowLevel(2); e.Handled = true; }
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
            aerodromePart = new DockablePart("Aerodrome", aerodromeContent, panel, layer, Placement("Aerodrome"), settings.Save);
            simulationPart = new DockablePart("Simulation", simulationContent, panel, layer, Placement("Simulation"), settings.Save);
            newPart = new DockablePart("New aircraft", newContent, panel, layer, Placement("New aircraft"), settings.Save);
            // (The user, 3 Nov: the flight plans as a part of their own, named as in the PAR trainer.)
            trafficPart = new DockablePart("Traffic List", trafficContent, panel, layer, Placement("Traffic List"), settings.Save);
            selectedPart = new DockablePart("Selected aircraft", selectedContent, panel, layer, Placement("Selected aircraft"), settings.Save);

            current = aerodromeContent;
            runwayBox.ToolTip = "The runway in use (placed on the map below): the circuits are flown on it.";
            conventionalSideBox.ToolTip = "The side of the conventional and the wide circuits at this runway.";
            jetSideBox.ToolTip = "The side of the jet circuits (overhead, close pattern) at this runway.";
            sfoSideBox.ToolTip = "The side of the SFO at this runway.";
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
            Row("Runway in use", runwayBox);
            runwayBox.SelectionChanged += (_, _) =>
            {
                if (!updating && runwayBox.SelectedItem is string name) UseRunway(name);
            };
            Note("The side of each circuit at this runway:");
            Row("Conventional, wide", conventionalSideBox);
            Row("Jet (overhead, close)", jetSideBox);
            Row("SFO", sfoSideBox);
            foreach (ComboBox box in new[] { conventionalSideBox, jetSideBox, sfoSideBox }) box.SelectionChanged += (_, _) => SaveSides();
            Note("New runway (or its place again): its name and elevation, then click its threshold and its end on the map.");
            Row("Name / elevation ft", Inline(newRunwayName, newRunwayElevation));
            Buttons(("Place on the map", PlaceRunway), ("Remove runway", RemoveRunway));

            Note("Heights of a class at this aerodrome, ft above the runway (empty: the class's). Close: also the overhead's downwind; break empty: the close height.");
            Row("Class", heightClassBox);
            heightClassBox.SelectionChanged += (_, _) => ShowHeights();
            current.Children.Add(Labelled(("Conventional", conventionalHeight), ("Wide", wideHeight), ("Close", closeHeight)));
            current.Children.Add(Labelled(("Initial (OVH)", initialHeight), ("Break (OVH)", breakHeight)));
            current.Children.Add(Labelled(("High Key (SFO)", highKeyHeight), ("Low Key (SFO)", lowKeyHeight)));
            Buttons(("Save heights", SaveHeights));

            Row("METAR", metarBox);
            Buttons(("Get METAR", GetMetar), ("Apply METAR", () => ApplyMetar(quiet: false)));
            Row("On the map", metarShowRow);
            metarShowRow.Picked += only =>
            {
                settings.MetarWindQnhOnly = only;
                ShowMetar();
            };
            current.Children.Add(windText);

            Note("Entry gates (where the arriving aircraft start, at least 5 NM out): a name (empty: the VRP clicked), then click it on the map.");
            Row("Gate name", newGateName);
            current.Children.Add(ActionRow(("ADD GATE", AddGate), ("REMOVE GATE", RemoveGate)));
            Row("Formation FS", separationRow);
            separationRow.Picked += full =>
            {
                airfield.Circuits.FullRunwaySeparation = full;
                string? error = airfield.Save();
                engine.SetAerodrome(airfield.Circuits, airfield.Runway?.Runway);
                Say(error ?? (full ? "Full rwy separation: in a formation landing to a full stop, the next one lands once the one ahead has left the runway."
                    : "Reduced rwy separation: in a formation landing to a full stop, the next one lands when the one ahead has rolled a third of the runway."));
            };
            Note("Runway exits (where the landing aircraft leave the runway: each one the first it reaches slow enough): a name, then click on the runway edge.");
            Row("Exit name", newExitName);
            current.Children.Add(ActionRow(("ADD EXIT", AddExit), ("REMOVE EXIT", RemoveExit)));

            current = simulationContent;
            pauseButton.Click += (_, _) => TogglePause();
            Row("", pauseButton);
            Row("Speed", speedRow);
            speedRow.Show(1.0);
            speedRow.Picked += v => speed = v;
            Buttons(("Level 1 (F1)", () => view.ShowLevel(1)), ("Level 2 (F2)", () => view.ShowLevel(2)));
            Row("Long final / IP NM", Inline(longFinalBox, initialPointBox));
            longFinalBox.LostFocus += (_, _) => ApplyDistances();
            initialPointBox.LostFocus += (_, _) => ApplyDistances();
            Buttons(("Remove all aircraft", RemoveAllAircraft));
            Expander layersBox = new() { Header = "Map layers", Content = layers, Foreground = TextBrush, Margin = new Thickness(0, 6, 0, 0) };
            current.Children.Add(layersBox);

            current = newContent;
            Note("From an entry gate (it joins its circuit by itself), the long final, the runway (a take-off) or PTRN (already in the circuit, for tests). Then ADD NOW, or TO LIST for the Traffic List. AD: the aerodrome's side.");
            Row("From", fromHolder, null, 62);
            Row("Type", typeRow, null, 62);
            toLine = Row("To", toHolder, null, 62);
            viaLine = Row("Via", viaRow, null, 62);
            typeRow.Show(false);
            viaRow.Show(true);
            typeRow.Picked += _ => ShowEntries();
            Row("Entry", entryRow, null, 62);
            Row("Circuit", circuitRow, null, 62);
            Row("Side", sideRow, null, 62);
            Row("Class", classHolder, null, 62);
            // (The user, 4 Nov: a plan is the whole flight: how many circuits, how the last one ends; nothing else to think of.)
            Row("Circuits", lapsRow, null, 62);
            Row("Last", lastRow, null, 62);
            Row("Formation", formationRow, null, 62);
            intervalLine = Row("Interval s", splitRow, null, 62);
            formationRow.Show(1);
            splitRow.Show(3.0);
            lapsRow.Show(1);
            lastRow.Show(EndAction.FullStop);
            formationRow.Picked += n => intervalLine.Visibility = n > 1 ? Visibility.Visible : Visibility.Collapsed;
            intervalLine.Visibility = Visibility.Collapsed;
            lapsRow.Picked += laps => lastRow.IsEnabled = laps != 0;
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
            Row("Callsign", Inline(callsignBox, new TextBlock { Text = "(optional; a formation: ending with its number, VIPER01)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, 62);
            Row("Height ft", Inline(arrivalBox, new TextBlock { Text = "(at the gate / of the crossing; empty: 500 above the circuit)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, 62);
            arrivalBox.ToolTip = "The height above the runway it arrives at, at the gate (empty: 500 ft above its circuit); it reaches the circuit height before the circuit.";
            Row("Delay min", Inline(delayBox, new TextBlock { Text = "(for the Traffic List: after START ALL)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), more, 62);
            toListButton = ActionButton("TO LIST", AddPlan);
            cancelEditButton = ActionButton("CANCEL", CancelEdit);
            cancelEditButton.Visibility = Visibility.Collapsed;
            WrapPanel newButtons = Wrap(ActionButton("ADD NOW", AddAircraft), toListButton, cancelEditButton);
            newButtons.Margin = new Thickness(0, 2, 0, 0);
            current.Children.Add(newButtons);

            current = trafficContent;
            // (As in the PAR trainer, the user 4 Nov: the aircraft flying, one thin row each, and the plans waiting.)
            current.Children.Add(Caption("FLYING  (click: select · II freeze · ↺ its plan again · ✕ remove)"));
            current.Children.Add(new ScrollViewer { Content = flyingList, MaxHeight = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            current.Children.Add(Caption("WAITING  (☐ GROUP · ▶ start · ⏸ hold · ⧉ duplicate · ✕ remove)"));
            current.Children.Add(new ScrollViewer { Content = planList, MaxHeight = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            trafficPauseButton.Click += (_, _) => TogglePause();
            // (One row; more only when the part is narrow: the user, 4 Nov.)
            trafficPauseButton.Margin = new Thickness(0, 0, 4, 4);
            WrapPanel trafficButtons = Wrap(trafficPauseButton, ActionButton("START ALL", StartAllPlans), ActionButton("REMOVE ALL", RemoveAllAircraft),
                ActionButton("GROUP", GroupPlans), ActionButton("SAVE...", SaveScenario), ActionButton("LOAD...", LoadScenario), ActionButton("CLEAR ALL", ClearPlans));
            trafficButtons.Margin = new Thickness(0, 6, 0, 0);
            current.Children.Add(trafficButtons);

            current = selectedContent;
            current.Children.Add(selectedText);
            current.Children.Add(selectedPanel);

            StackPanel next = Group("2 · Next circuit", "Its plan: circuit, side and break from the next touch and go / go-around; the end of the final: this one.", selectedPanel);
            Row("Circuit", selCircuitRow, next, 62);
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
            selEndRow.Picked += end => WithSelected(cs => commands.SetEnd(cs, end));
            Row("Break", selBreakRow, next, 62);
            selBreakRow.Picked += where => WithSelected(cs => commands.SetBreak(cs, where));
            Row("", Inline(ActionButton("BREAK AT A POINT", () => WithSelected(cs =>
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
            outside.Children.Add(Wrap(rejoinButton, breakupButton,
                Order("HELIPAD", cs => view.AskPoint($"{cs}: click the helipad  -  Esc: cancel", (x, y) => commands.Helipad(cs, x, y))),
                Order("REMOVE", commands.Remove)));
            Note("Right click on an aircraft: all its commands. Its label can be dragged.", selectedPanel);
            current = panel;
            // Each part its message line, at its bottom; the folders in the Aerodrome.
            aerodromeContent.Children.Add(messages[Area.Aerodrome]);
            Note($"Maps: {Paths.MapsFolderText} (made with ATCRoom Map Creator). Aerodromes and settings: {Paths.FolderText}.", aerodromeContent);
            simulationContent.Children.Add(messages[Area.Simulation]);
            newContent.Children.Add(messages[Area.New]);
            trafficContent.Children.Add(messages[Area.Traffic]);
            selectedContent.Children.Add(messages[Area.Selected]);
            ShowSelected();
        }

        private static ComboBox SideCombo() => Combo<CircuitSide>([(CircuitSide.Left, "Left"), (CircuitSide.Right, "Right")]);

        /// <summary>The sides of the circuits of the runway in use, saved in the aerodrome file (the runway's own side: the conventional's).</summary>
        private void SaveSides()
        {
            if (updating || airfield.Runway == null) return;
            CircuitSide conventional = TagOf<CircuitSide>(conventionalSideBox);
            airfield.Runway.Side = conventional;
            airfield.Runway.ConventionalSide = conventional;
            airfield.Runway.JetSide = TagOf<CircuitSide>(jetSideBox);
            airfield.Runway.SfoSide = TagOf<CircuitSide>(sfoSideBox);
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
            "WIDE" => "Wide circuit (outside downwind).",
            "OVH" => "Overhead pattern (run and break): from the initial point.",
            "SFO" => "Simulated flame out: from the High Key.",
            "AD" => "The aerodrome's side for this circuit (Aerodrome, above).",
            "L" => "Left-hand circuit.",
            "R" => "Right-hand circuit.",
            "T/G" => "Touch and go, then the circuit again.",
            "FS" => "Full stop: it lands and is removed.",
            "G/A" => "Go-around at about 200 ft, then the circuit again.",
            "LOW" => "Low pass at about 100 ft, then the circuit again.",
            "DEP" => "Touch and go (or take-off), then it leaves straight out.",
            "break MID" => "Overhead: the break over the middle of the runway.",
            "break BEG" => "Overhead: the break over the beginning of the runway.",
            "break END" => "Overhead: the break over the end of the runway.",
            "break STBY" => "Overhead: standby for break: on along the initial until BREAK NOW.",
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
            ["Level 1 (F1)"] = "The aerodrome and its circuits (about 7 NM).",
            ["Level 2 (F2)"] = "The area around the aerodrome (about 40 NM): gates, initial point, long final.",
            ["Remove all aircraft"] = "Remove all the aircraft flying (the flight plans stay).",
            ["ADD GATE"] = "Click the entry gate on the map: a VRP gives its name (or the name written). At least 5 NM out.",
            ["REMOVE GATE"] = "Remove the gate written (or the one chosen in From).",
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
            ["BREAK AT A POINT"] = "Overhead: click on the map where it breaks (along the runway).",
            ["BREAK NOW"] = "Overhead, on the initial: it breaks now.",
            ["HOLD HK"] = "SFO: it waits circling over the High Key (until START SFO).",
            ["START SFO"] = "SFO: at the next pass over the High Key it starts the flame out pattern.",
            ["EXTEND DWN"] = "On the downwind: straight on beyond the base turn, until TURN BASE.",
            ["TURN BASE"] = "On the downwind: turn base now (also during an orbit).",
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
            ["HELIPAD"] = "Helicopter: click its landing pad on the map.",
            ["REMOVE"] = "Remove this aircraft."
        };

        private static Button ActionButton(string text, Action action)
        {
            Button button = new()
            {
                Content = text,
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(7, 2, 7, 2),
                MinWidth = 34,
                Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0x86)),
                Foreground = TextBrush,
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
                Area.Aerodrome => aerodromePart,
                Area.Simulation => simulationPart,
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
            view.ShowLevel(1);
            ShowAerodrome();
        }

        private void ShowAerodrome()
        {
            view.Hint = airfield.Map == null
                ? "Open a map (panel on the right: Open map...).\nNo map yet? Make one with ATCRoomMapCreator.exe, beside this program."
                : airfield.Runway == null ? "Place the runway in use: in the panel on the right write its name and elevation, then Place on the map." : "";
            view.InvalidateVisual();
            updating = true;
            mapText.Text = airfield.Map == null ? "No map: open one made with ATCRoom Map Creator."
                : $"{airfield.Map.Name} ({airfield.Code})" + (airfield.PlacedRunways.Count == 0 ? "\nNo runway yet: place one on the map (below)." : "");
            runwayBox.Items.Clear();
            foreach (RunwayCircuit runway in airfield.PlacedRunways) runwayBox.Items.Add(runway.Runway);
            runwayBox.SelectedItem = airfield.Runway?.Runway;
            foreach ((ComboBox box, CircuitType type) in new[] { (conventionalSideBox, CircuitType.Conventional), (jetSideBox, CircuitType.Close), (sfoSideBox, CircuitType.Sfo) })
            {
                if (airfield.Runway != null) SelectTag(box, airfield.Circuits.SideOf(airfield.Runway.Runway, type) ?? CircuitSide.Left); else box.SelectedIndex = -1;
            }
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
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>The plan made of the rows of group 1 now.</summary>
        private TowerPlan PlanOfRows()
        {
            double delay = double.TryParse(delayBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d >= 0 ? d : 0;
            return new TowerPlan
            {
                Callsign = callsignBox.Text.Trim().ToUpperInvariant(),
                Class = classRow.Value ?? "",
                From = fromRow.Value ?? From.InCircuit,
                Entry = entryRow.Value,
                Circuit = circuitRow.Value,
                Side = sideRow.Value,
                End = (lapsRow.HasValue ? lapsRow.Value : 1) == 1 ? lastRow.Value : EndAction.TouchAndGo,
                Laps = lapsRow.HasValue ? lapsRow.Value : 1,
                Last = lastRow.Value,
                Formation = formationRow.HasValue ? formationRow.Value : 1,
                Lands = null,
                SplitSeconds = splitRow.HasValue ? splitRow.Value : 3,
                Transit = typeRow.HasValue && typeRow.Value,
                To = toRow.HasValue ? toRow.Value ?? "" : "",
                Overhead = viaRow.HasValue && viaRow.Value,
                HeightFt = double.TryParse(arrivalBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && h > 0 ? h : null,
                DelayMinutes = delay
            };
        }

        private void AddAircraft()
        {
            TowerPlan plan = PlanOfRows();
            string? error = Launch(plan, out string added);
            Say(error, Area.New);
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
            string? error = LaunchOne(plan, out added);
            if (error != null) return error;
            // The plan of its circuits (a crossing has none).
            if (plan.Laps is int laps && !plan.Transit) commands.SetLaps(added, laps, plan.Last);
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

        private string? LaunchOne(TowerPlan plan, out string added)
        {
            added = "";
            string? callsign = plan.Callsign.Length > 0 ? plan.Callsign : null;
            string? kind = plan.Class.Length > 0 ? plan.Class : null;
            if (plan.From == From.Runway)
            {
                CircuitType type = plan.Circuit is CircuitType.Wide ? CircuitType.Wide : CircuitType.Conventional;
                return commands.Add(CircuitStart.Runway, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (plan.Transit)
            {
                var start = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.From, StringComparison.OrdinalIgnoreCase));
                var exit = airfield.Gates.FirstOrDefault(g => string.Equals(g.Name, plan.To, StringComparison.OrdinalIgnoreCase));
                if (start.Name == null) return "A crossing starts at a gate: choose it in From.";
                if (exit.Name == null) return "A crossing ends at a gate: choose it in To.";
                if (string.Equals(start.Name, exit.Name, StringComparison.OrdinalIgnoreCase)) return "A crossing goes to another gate (To).";
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
            if (editing != null && plans.IndexOf(editing) is int at && at >= 0)
            {
                // UPDATE: the plan changed in place, in the same place of the list.
                plans[at] = plan;
                Say($"{PlanText(plan)}: changed.", Area.Traffic);
            }
            else plans.Add(plan);
            EndEdit();
            callsignBox.Text = "";
            ShowPlans();
        }

        /// <summary>A plan of WAITING into New aircraft, to change it (UPDATE) or to start a copy (ADD NOW).</summary>
        private void EditPlan(TowerPlan p)
        {
            editing = p;
            if (p.From.Length > 0) fromRow.Show(From.IsInCircuit(p.From) ? From.InCircuit : p.From);
            typeRow.Show(p.Transit);
            if (p.To.Length > 0) toRow.Show(p.To);
            viaRow.Show(p.Overhead);
            entryRow.Show(p.Entry);
            circuitRow.Show(p.Circuit);
            sideRow.Show(p.Side);
            if (p.Class.Length > 0) classRow.Show(p.Class);
            int laps = p.Laps ?? (p.End == EndAction.TouchAndGo ? 0 : 1);
            lapsRow.Show(laps);
            lastRow.Show(p.Laps != null ? p.Last : p.End is EndAction.Departure ? EndAction.Departure : EndAction.FullStop);
            formationRow.Show(Math.Clamp(p.Formation, 1, SimEngine.MaxFormation));
            splitRow.Show(p.SplitSeconds is 3.0 or 4.0 or 5.0 or 10.0 ? p.SplitSeconds : 3.0);
            intervalLine.Visibility = p.Formation > 1 ? Visibility.Visible : Visibility.Collapsed;
            callsignBox.Text = p.Callsign;
            arrivalBox.Text = p.HeightFt is double h ? h.ToString("0", CultureInfo.InvariantCulture) : "";
            delayBox.Text = p.DelayMinutes.ToString("0.#", CultureInfo.InvariantCulture);
            ShowEntries();
            lastRow.IsEnabled = !p.Transit && laps != 0;
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
            string side = p.Side is CircuitSide s ? (s == CircuitSide.Left ? " L" : " R") : "";
            string circuit = TowerCommands.Circuits.FirstOrDefault(c => c.Type == p.Circuit).Short ?? "";
            string end = p.Laps is int laps
                ? (laps == 0 ? "∞ T/G" : laps == 1 ? TowerCommands.Ends.FirstOrDefault(e => e.End == p.Last).Short ?? "" : $"{laps}× → {TowerCommands.Ends.FirstOrDefault(e => e.End == p.Last).Short}")
                : TowerCommands.Ends.FirstOrDefault(e => e.End == p.End).Short ?? "";
            string entry = p.From is From.Runway ? "T/O" : p.From is From.LongFinal ? (p.Entry == CircuitEntry.Circling ? "CIRCLING" : "FINAL") : p.Entry.ToString().ToUpperInvariant();
            string height = p.HeightFt is double hf ? $" {hf:0} ft" : "";
            string team = p.Formation > 1 ? $" F{p.Formation}" : "";
            if (p.Transit) return $"{(p.Callsign.Length > 0 ? p.Callsign : "(auto)")}{team} {p.Class}  TRANSIT {p.From} → {(p.Overhead ? "OVHD → " : "")}{p.To}{height}  +{p.DelayMinutes:0.#}'";
            return $"{(p.Callsign.Length > 0 ? p.Callsign : "(auto)")}{team} {p.Class}  {p.From}{height} → {entry} {circuit}{side} {end}  +{p.DelayMinutes:0.#}'";
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

        /// <summary>A thin row: the callsign, its text, its buttons on the right.</summary>
        private static Grid ThinRow(TextBlock callsign, UIElement text, params Button[] buttons)
        {
            Grid row = new() { Margin = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(callsign);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            StackPanel right = new() { Orientation = Orientation.Horizontal };
            foreach (Button button in buttons) right.Children.Add(button);
            Grid.SetColumn(right, 2);
            row.Children.Add(right);
            return row;
        }

        /// <summary>The callsign of a plan (with its formation) and the rest of its text.</summary>
        private static (string Callsign, string Rest) PlanParts(TowerPlan p)
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
                planList.Children.Add(ThinRow(callsign, text, tick, SmallButton("▶", () => StartPlan(p)), hold, SmallButton("⧉", () => DuplicatePlan(p)),
                    SmallButton("✕", () => { plans.Remove(p); ShowPlans(); })));
                planRows.Add((p, text, hold));
                planCallsigns[p] = callsign;
            }
            if (planList.Children.Count == 0) planList.Children.Add(new TextBlock { Text = "None: New aircraft, then TO LIST.", Foreground = DimBrush, FontSize = 11 });
            UpdatePlans();
        }

        /// <summary>The list of the aircraft flying (every one in the simulation), built again when they change.</summary>
        private void ShowFlying(IReadOnlyList<AircraftState> traffic)
        {
            flyingList.Children.Clear();
            flyingRows.Clear();
            flyingCallsigns.Clear();
            flyingKey = string.Join(",", traffic.Select(t => t.Callsign));
            foreach (AircraftState aircraft in traffic)
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
                flyingList.Children.Add(ThinRow(callsign, text, freeze, again, remove));
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
                text.Text = state + PlanParts(plan).Rest;
                hold.Content = plan.Now == TowerPlan.State.Held ? "⏵" : "⏸";
            }
            IReadOnlyList<AircraftState> traffic = engine.Snapshot();
            if (string.Join(",", traffic.Select(t => t.Callsign)) != flyingKey) ShowFlying(traffic);
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
            items.Add((From.InCircuit, "PTRN"));
            fromRow = new ChoiceRow<string>(items, (value, _) => value switch
            {
                From.LongFinal => "On the final at the long final distance (Simulation), on the glide path.",
                From.Runway => "On the runway: it takes off, then the conventional or the wide circuit (or DEP: it leaves).",
                From.InCircuit => "Already in the circuit (for tests): on the downwind, at the initial point (OVH) or at the High Key (SFO).",
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
            separationRow.Show(airfield.Circuits.FullRunwaySeparation);
            engine.SetRunwayExits(airfield.Exits);
            view.InvalidateVisual();
        }

        /// <summary>The entries that apply to where it starts: none from the long final or the runway; in the circuit: DW, IP, HK.</summary>
        private void ShowEntries()
        {
            string from = fromRow.Value ?? "";
            bool transit = typeRow.HasValue && typeRow.Value;
            toRow.IsEnabled = viaRow.IsEnabled = transit;
            circuitRow.IsEnabled = sideRow.IsEnabled = lapsRow.IsEnabled = lastRow.IsEnabled = !transit;
            if (!transit && lapsRow.HasValue && lapsRow.Value == 0) lastRow.IsEnabled = false;
            // (The crossing's rows only for a crossing.)
            toLine.Visibility = viaLine.Visibility = transit ? Visibility.Visible : Visibility.Collapsed;
            entryRow.IsEnabled = !transit && from != From.Runway;
            bool circuit = from == From.InCircuit;
            bool longFinal = from == From.LongFinal;
            // (From the long final: straight on (FINAL) or the circling; the circling only from there.)
            foreach (CircuitEntry entry in Enum.GetValues<CircuitEntry>())
            {
                entryRow.SetEnabled(entry, longFinal ? entry is CircuitEntry.Final or CircuitEntry.Circling
                    : entry != CircuitEntry.Circling && !(circuit && entry is CircuitEntry.Base or CircuitEntry.Final));
            }
            if (longFinal && entryRow.Value is not (CircuitEntry.Final or CircuitEntry.Circling)) entryRow.Show(CircuitEntry.Final);
            if (!longFinal && entryRow.Value == CircuitEntry.Circling) entryRow.Show(CircuitEntry.Downwind);
            if (circuit && entryRow.Value is CircuitEntry.Base or CircuitEntry.Final) entryRow.Show(CircuitEntry.Downwind);
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

        private void RemoveGate()
        {
            string name = newGateName.Text.Trim().Length > 0 ? newGateName.Text.Trim() : fromRow.Value ?? "";
            if (!airfield.Circuits.Gates.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Say("Write the name of the gate to remove (or choose it in From).");
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
                else if (name == "NEXT" && Enum.TryParse(value, out EndAction end)) commands.SetEnd(callsign, end);
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
            if (type == CircuitType.Overhead)
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
            items.Add(Item("Helipad on the map...", () => view.AskPoint($"{callsign}: click the helipad  -  Esc: cancel", (x, y) => commands.Helipad(callsign, x, y))));
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
            if (a != null)
            {
                string leg = a.Leg;
                bool sfo = a.Circuit == CircuitType.Sfo, inCircuit = a.Circuit != null && a.Contact == GroundContact.Airborne && leg is not ("MANUAL" or "GO AROUND" or "DEPARTURE");
                breakNowButton.IsEnabled = a.Circuit == CircuitType.Overhead && leg == "INITIAL";
                holdButton.IsEnabled = sfo && !a.SfoHold && leg is "CLIMB" or "HIGH KEY";
                startSfoButton.IsEnabled = sfo && a.SfoHold;
                extendButton.IsEnabled = leg is "DOWNWIND" or "ORBIT" or "ORBITS";
                baseButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS";
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
