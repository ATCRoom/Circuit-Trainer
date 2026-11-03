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
        private readonly Button pauseButton = new();
        private readonly ComboBox speedBox = Combo<double>([(1.0, "x1"), (2.0, "x2"), (4.0, "x4")]);
        private readonly TextBox longFinalBox = new() { Width = 45 };
        private readonly TextBox initialPointBox = new() { Width = 45 };
        private readonly StackPanel layers = new();
        // New aircraft: rows of buttons (the choices kept for the next one).
        /// <summary>Where it starts: an entry gate, the runway, or already in the circuit (tests); rebuilt with the gates.</summary>
        private ChoiceRow<string> fromRow = new([]);
        private readonly StackPanel fromHolder = new();
        private readonly ChoiceRow<CircuitEntry> entryRow = new([(CircuitEntry.Downwind, "DW"), (CircuitEntry.Base, "BASE"), (CircuitEntry.InitialPoint, "IP"),
            (CircuitEntry.HighKey, "HK"), (CircuitEntry.Final, "FINAL")], (_, t) => ChoiceTip("entry " + t));
        private readonly TextBox delayBox = new() { Width = 45, Text = "0" };
        private readonly TextBox arrivalBox = new() { Width = 55 };
        private readonly TextBox newGateName = new() { Width = 70 };
        /// <summary>The flight plans (advanced generation), their list and the status text of each row.</summary>
        private readonly List<TowerPlan> plans = [];
        private readonly StackPanel planList = new();
        private readonly List<(TowerPlan Plan, TextBlock Status, Button Hold)> planRows = [];
        /// <summary>The panel the helpers add to (a section while it is built).</summary>
        private Panel current = null!;
        // The order buttons of the selected aircraft (grey when they do not apply).
        private Button breakNowButton = null!, holdButton = null!, startSfoButton = null!, extendButton = null!, baseButton = null!, orbitButton = null!,
            orbitForeverButton = null!, goAroundButton = null!, sideLeftButton = null!, sideRightButton = null!, wideButton = null!, rejoinButton = null!, breakupButton = null!;
        private readonly ChoiceRow<CircuitType> circuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)), (_, t) => ChoiceTip(t));
        private readonly ChoiceRow<CircuitSide?> sideRow = new([(null, "AD"), (CircuitSide.Left, "L"), (CircuitSide.Right, "R")], (_, t) => ChoiceTip(t));
        private ChoiceRow<string> classRow = new([]);
        private readonly StackPanel classHolder = new();
        private readonly ChoiceRow<EndAction> endRow = new(TowerCommands.Ends.Select(e => (e.End, e.Short)), (_, t) => ChoiceTip(t));
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
        /// <summary>The four parts of the panel (in the column or floating).</summary>
        private DockablePart aerodromePart = null!, simulationPart = null!, newPart = null!, selectedPart = null!;
        private readonly StackPanel aerodromeContent = new(), simulationContent = new(), newContent = new(), selectedContent = new();
        private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap, Foreground = WarnBrush, Margin = new Thickness(0, 8, 0, 0) };

        public MainWindow()
        {
            commands = new TowerCommands(engine, airfield);
            string version = Assembly.GetExecutingAssembly().GetName().Version is Version v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";
            Title = $"TWR Trainer {version} - instructor";
            Width = 1400;
            Height = 900;
            WindowState = WindowState.Maximized;
            Background = PanelBrush;
            Foreground = TextBrush;

            BuildPanel();
            DockPanel root = new();
            ScrollViewer scroll = new() { Content = panel, Width = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = PanelBrush };
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
            view.MenuAsked += callsign => OpenMenu(Menu(callsign));
            view.HeadingDragged += (callsign, heading) => { commands.Heading(callsign, heading); Say($"{callsign}: heading {heading:000} (out of the circuit; REJOIN brings it back)."); };
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
            classRow.Show(engine.Classes.Any(c => c.Name == PerformanceClass.DefaultName) ? PerformanceClass.DefaultName : engine.Classes[0].Name);
            classHolder.Children.Add(classRow);
            heightClassBox.SelectedIndex = heightClassBox.Items.Count > 0 ? 0 : -1;
            if (note != null) Say(note);
            engine.LongFinalNM = settings.LongFinalNM;
            engine.InitialPointNM = settings.InitialPointNM;
            longFinalBox.Text = engine.LongFinalNM.ToString("0.#", CultureInfo.InvariantCulture);
            initialPointBox.Text = engine.InitialPointNM.ToString("0.#", CultureInfo.InvariantCulture);
            metarBox.Text = settings.Metar;
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
            if (++ticks % 10 == 0)
            {
                ShowSelectedValues();
                UpdatePlans();
                (double f, double sp, double g) = engine.Wind;
                aerodromePart.SummaryText = airfield.Map == null ? "no map" : $"{airfield.Code} · {airfield.Runway?.Runway ?? "no runway"} · " + (sp < 0.5 ? "calm" : $"{f:000}/{sp:0}" + (g > 0 ? $"G{sp + g:0}" : ""));
                simulationPart.SummaryText = $"{(engine.Paused ? "paused" : "running")} · x{speed:0}";
                newPart.SummaryText = plans.Count > 0 ? $"{plans.Count} plans" : "";
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
            selectedPart = new DockablePart("Selected aircraft", selectedContent, panel, layer, Placement("Selected aircraft"), settings.Save);

            current = aerodromeContent;
            runwayBox.ToolTip = "The runway in use (placed on the map below): the circuits are flown on it.";
            conventionalSideBox.ToolTip = "The side of the conventional and the wide circuits at this runway.";
            jetSideBox.ToolTip = "The side of the jet circuits (overhead, close pattern) at this runway.";
            sfoSideBox.ToolTip = "The side of the SFO at this runway.";
            newRunwayName.ToolTip = "The name of the runway (for example 14).";
            newRunwayElevation.ToolTip = "The elevation of its threshold (ft): the heights of the labels are above it.";
            heightClassBox.ToolTip = "The class whose heights at this aerodrome are below.";
            metarBox.ToolTip = "A METAR line: its wind is used (for example LIPI 031250Z 06012KT CAVOK 15/08 Q1018).";
            newGateName.ToolTip = "The name of the new gate (empty: the name of the VRP clicked).";
            speedBox.ToolTip = "Speed of the simulation (x2, x4: the time runs faster).";
            longFinalBox.ToolTip = "Distance of the long final (NM): where LONG FIN starts.";
            initialPointBox.ToolTip = "Distance of the initial point before the threshold (NM).";
            callsignBox.ToolTip = "Empty: the next free callsign.";
            delayBox.ToolTip = "For the flight plans: minutes after START ALL.";
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

            Row("METAR (wind)", metarBox);
            Buttons(("Apply METAR", () => ApplyMetar(quiet: false)));
            current.Children.Add(windText);

            Note("Entry gates (where the arriving aircraft start, at least 5 NM out): a name (empty: the VRP clicked), then click it on the map.");
            Row("Gate name", newGateName);
            current.Children.Add(ActionRow(("ADD GATE", AddGate), ("REMOVE GATE", RemoveGate)));

            current = simulationContent;
            pauseButton.Click += (_, _) => TogglePause();
            Row("", pauseButton);
            Row("Speed", speedBox);
            speedBox.SelectedIndex = 0;
            speedBox.SelectionChanged += (_, _) => speed = speedBox.SelectedItem is ComboBoxItem { Tag: double s } ? s : 1;
            Buttons(("Level 1 (F1)", () => view.ShowLevel(1)), ("Level 2 (F2)", () => view.ShowLevel(2)));
            Row("Long final / IP NM", Inline(longFinalBox, initialPointBox));
            longFinalBox.LostFocus += (_, _) => ApplyDistances();
            initialPointBox.LostFocus += (_, _) => ApplyDistances();
            Buttons(("Remove all aircraft", () => { engine.Clear(); view.Select(null); }));
            Expander layersBox = new() { Header = "Map layers", Content = layers, Foreground = TextBrush, Margin = new Thickness(0, 6, 0, 0) };
            current.Children.Add(layersBox);

            current = newContent;
            Note("From an entry gate (it joins its circuit by itself), the long final, the runway (a take-off) or CIRC (already in the circuit, for tests). Then ADD NOW, or TO PLANS for the list below. AD: the aerodrome's side.");
            Row("From", fromHolder, null, 62);
            Row("Entry", entryRow, null, 62);
            Row("Circuit", circuitRow, null, 62);
            Row("Side", sideRow, null, 62);
            Row("Class", classHolder, null, 62);
            Row("At the end", endRow, null, 62);
            // (The user, 3 Nov: IP is the overhead's, HK the SFO's; after a take-off only the conventional or the wide.)
            entryRow.Picked += entry =>
            {
                if (entry == CircuitEntry.InitialPoint) circuitRow.Show(CircuitType.Overhead);
                else if (entry == CircuitEntry.HighKey) circuitRow.Show(CircuitType.Sfo);
                else if (circuitRow.Value is CircuitType.Overhead or CircuitType.Sfo) circuitRow.Show(CircuitType.Conventional);
            };
            circuitRow.Picked += type =>
            {
                if (fromRow.Value == From.Runway && type is not (CircuitType.Conventional or CircuitType.Wide))
                {
                    Say("After a take-off: the conventional or the wide circuit (or DEP at the end).");
                    circuitRow.Show(CircuitType.Conventional);
                    return;
                }
                if (type == CircuitType.Overhead) entryRow.Show(CircuitEntry.InitialPoint);
                else if (type == CircuitType.Sfo) entryRow.Show(CircuitEntry.HighKey);
                else if (entryRow.Value is CircuitEntry.InitialPoint or CircuitEntry.HighKey) entryRow.Show(CircuitEntry.Downwind);
            };
            entryRow.Show(CircuitEntry.Downwind);
            circuitRow.Show(CircuitType.Conventional);
            sideRow.Show(null);
            endRow.Show(EndAction.TouchAndGo);
            RebuildFrom();
            Row("Callsign", Inline(callsignBox, new TextBlock { Text = "(optional)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center }), null, 62);
            Row("Height ft", Inline(arrivalBox, new TextBlock { Text = "(at the gate; empty: 500 above its circuit)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 }), null, 62);
            arrivalBox.ToolTip = "The height above the runway it arrives at, at the gate (empty: 500 ft above its circuit); it reaches the circuit height before the circuit.";
            Row("Delay min", Inline(delayBox, new TextBlock { Text = "(for the plans: after START ALL)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center }), null, 62);
            current.Children.Add(ActionRow(("ADD NOW", AddAircraft), ("TO PLANS", AddPlan)));
            StackPanel plansGroup = Group("Flight plans", "▶ start now · ⏸ hold / go on · ⧉ duplicate · ✕ remove. START ALL: each after its delay.", current);
            plansGroup.Children.Add(planList);
            plansGroup.Children.Add(ActionRow(("START ALL", StartAllPlans), ("SAVE...", SaveScenario), ("LOAD...", LoadScenario), ("CLEAR", ClearPlans)));

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
            panel.Children.Add(message);
            Note($"Maps: {Paths.MapsFolderText} (made with ATCRoom Map Creator). Aerodromes and settings: {Paths.FolderText}.");
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
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) { Say(null); use(value); }
            else Say($"Write {what}.");
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
            "entry FINAL" => "Straight in: onto the final 4 NM out.",
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
            ["Apply METAR"] = "Take the wind from the METAR line.",
            ["Level 1 (F1)"] = "The aerodrome and its circuits (about 7 NM).",
            ["Level 2 (F2)"] = "The area around the aerodrome (about 40 NM): gates, initial point, long final.",
            ["Remove all aircraft"] = "Remove all the aircraft flying (the flight plans stay).",
            ["ADD GATE"] = "Click the entry gate on the map: a VRP gives its name (or the name written). At least 5 NM out.",
            ["REMOVE GATE"] = "Remove the gate written (or the one chosen in From).",
            ["ADD NOW"] = "Start this aircraft now.",
            ["TO PLANS"] = "Put it in the flight plans: started by ▶ or by START ALL after its delay.",
            ["START ALL"] = "Start every flight plan waiting, each after its delay (held ones stay held).",
            ["SAVE..."] = "Save the flight plans as a scenario of this aerodrome.",
            ["LOAD..."] = "Load a scenario (its flight plans replace those of the list).",
            ["CLEAR"] = "Remove all the flight plans (the aircraft flying go on).",
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

        private void Row(string label, UIElement control, Panel? into = null, double labelWidth = 130)
        {
            Grid grid = new() { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock text = new() { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = TextBrush };
            grid.Children.Add(text);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            (into ?? current ?? panel).Children.Add(grid);
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

        private void Say(string? text)
        {
            message.Text = text ?? "";
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
                return;
            }
            (double f, double s, double g) = engine.Wind;
            windText.Text = s < 0.5 ? "Wind: calm" : $"Wind: {f:000}° {s:0} kt" + (g > 0 ? $", gusts +{g:0}" : "");
            if (!quiet) Say(null);
        }

        // ----- Simulation ------------------------------------------------------------------------------------------------

        private void TogglePause()
        {
            engine.Paused = !engine.Paused;
            UpdatePause();
        }

        private void UpdatePause() => pauseButton.Content = engine.Paused ? "Run" : "Pause";

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
                End = endRow.Value,
                HeightFt = double.TryParse(arrivalBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && h > 0 ? h : null,
                DelayMinutes = delay
            };
        }

        private void AddAircraft()
        {
            string? error = Launch(PlanOfRows(), out string added);
            Say(error);
            if (error != null) return;
            callsignBox.Text = "";
            view.SetTraffic(engine.Snapshot(), engine.Time);
            view.Select(added);
        }

        /// <summary>An aircraft of a plan, now: from its gate, the runway or already in the circuit. Returns an error, or null.</summary>
        private string? Launch(TowerPlan plan, out string added)
        {
            added = "";
            string? callsign = plan.Callsign.Length > 0 ? plan.Callsign : null;
            string? kind = plan.Class.Length > 0 ? plan.Class : null;
            if (plan.From == From.Runway)
            {
                CircuitType type = plan.Circuit is CircuitType.Wide ? CircuitType.Wide : CircuitType.Conventional;
                return commands.Add(CircuitStart.Runway, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (plan.From == From.LongFinal)
            {
                CircuitType type = plan.Circuit is CircuitType.Overhead or CircuitType.Sfo ? CircuitType.Conventional : plan.Circuit;
                return commands.Add(CircuitStart.LongFinal, type, plan.Side, plan.End, kind, callsign, false, out added);
            }
            if (plan.From is From.InCircuit or "IN CCT")
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
            plans.Add(PlanOfRows());
            callsignBox.Text = "";
            ShowPlans();
        }

        private static string PlanText(TowerPlan p)
        {
            string side = p.Side is CircuitSide s ? (s == CircuitSide.Left ? " L" : " R") : "";
            string circuit = TowerCommands.Circuits.FirstOrDefault(c => c.Type == p.Circuit).Short ?? "";
            string end = TowerCommands.Ends.FirstOrDefault(e => e.End == p.End).Short ?? "";
            string entry = p.From is From.Runway ? "T/O" : p.From is From.LongFinal ? "FINAL" : p.Entry.ToString().ToUpperInvariant();
            string height = p.HeightFt is double hf ? $" {hf:0} ft" : "";
            return $"{(p.Callsign.Length > 0 ? p.Callsign : "(auto)")} {p.Class}  {p.From}{height} → {entry} {circuit}{side} {end}  +{p.DelayMinutes:0.#}'";
        }

        /// <summary>The rows of the plans, built again (after a change).</summary>
        private void ShowPlans()
        {
            planList.Children.Clear();
            planRows.Clear();
            foreach (TowerPlan plan in plans)
            {
                TowerPlan p = plan;
                Grid row = new() { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel texts = new();
                texts.Children.Add(new TextBlock { Text = PlanText(p), Foreground = TextBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                TextBlock status = new() { Foreground = DimBrush, FontSize = 11 };
                texts.Children.Add(status);
                row.Children.Add(texts);
                Button hold = SmallButton("⏸", () => { p.Now = p.Now == TowerPlan.State.Held ? (p.Due > 0 ? TowerPlan.State.Counting : TowerPlan.State.Waiting) : p.Now is TowerPlan.State.Flying ? p.Now : TowerPlan.State.Held; UpdatePlans(); });
                StackPanel buttons = new() { Orientation = Orientation.Horizontal };
                buttons.Children.Add(SmallButton("▶", () => StartPlan(p)));
                buttons.Children.Add(hold);
                buttons.Children.Add(SmallButton("⧉", () => DuplicatePlan(p)));
                buttons.Children.Add(SmallButton("✕", () => { plans.Remove(p); ShowPlans(); }));
                Grid.SetColumn(buttons, 1);
                row.Children.Add(buttons);
                planList.Children.Add(row);
                planRows.Add((p, status, hold));
            }
            if (plans.Count == 0) planList.Children.Add(new TextBlock { Text = "No plans: choose in the rows above, then TO PLANS.", Foreground = DimBrush, FontSize = 11 });
            UpdatePlans();
        }

        private static Button SmallButton(string text, Action action)
        {
            Button button = ActionButton(text, action);
            button.MinWidth = 24;
            button.Padding = new Thickness(4, 0, 4, 0);
            button.Margin = new Thickness(2, 0, 0, 0);
            return button;
        }

        /// <summary>The state of each plan on its row; starts the plans whose time has come; frees those whose aircraft is gone.</summary>
        private void UpdatePlans()
        {
            double time = engine.Time;
            foreach (TowerPlan plan in plans)
            {
                if (plan.Now == TowerPlan.State.Flying && engine.Get(plan.Flying) == null) { plan.Now = TowerPlan.State.Waiting; plan.Due = 0; }
                if (plan.Now == TowerPlan.State.Counting && time >= plan.Due) StartPlan(plan);
            }
            foreach ((TowerPlan plan, TextBlock status, Button hold) in planRows)
            {
                status.Text = plan.Now switch
                {
                    TowerPlan.State.Counting => $"starts in {(int)(Math.Max(0, plan.Due - time) / 60)}:{(int)(Math.Max(0, plan.Due - time) % 60):00}",
                    TowerPlan.State.Held => "held (⏸ again to go on)",
                    TowerPlan.State.Flying => $"flying: {plan.Flying}",
                    _ => "waiting"
                };
                hold.Content = plan.Now == TowerPlan.State.Held ? "⏵" : "⏸";
            }
        }

        private void StartPlan(TowerPlan plan)
        {
            if (plan.Now == TowerPlan.State.Flying) return;
            string? error = Launch(plan, out string added);
            if (error != null)
            {
                Say($"{PlanText(plan)}: {error}");
                plan.Now = TowerPlan.State.Held;
            }
            else
            {
                plan.Now = TowerPlan.State.Flying;
                plan.Flying = added;
            }
            UpdatePlans();
        }

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
            if (plans.Count > 0 && MessageBox.Show(this, $"Remove the {plans.Count} flight plans (the aircraft flying go on)?", "TWR Trainer", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            plans.Clear();
            ShowPlans();
        }

        private void SaveScenario()
        {
            if (plans.Count == 0) { Say("No flight plans to save."); return; }
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
                new TowerScenario { Airport = airfield.Code, Runway = airfield.Runway?.Runway ?? "", Plans = plans.Select(p => p.Copy()).ToList() }.Save(dialog.FileName);
                Say("Scenario saved.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"The scenario could not be saved: {ex.Message}");
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
                    ? $"Scenario of {scenario.Airport}, not of {airfield.Code}: its gates may be missing here." : null);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Say($"The scenario could not be read: {ex.Message}");
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
            items.Add((From.InCircuit, "CIRC"));
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
            ShowEntries();
            view.Gates = airfield.Gates;
            view.InvalidateVisual();
        }

        /// <summary>The entries that apply to where it starts: none from the long final or the runway; in the circuit: DW, IP, HK.</summary>
        private void ShowEntries()
        {
            string from = fromRow.Value ?? "";
            entryRow.IsEnabled = from is not (From.LongFinal or From.Runway);
            bool circuit = from == From.InCircuit;
            entryRow.SetEnabled(CircuitEntry.Base, !circuit);
            entryRow.SetEnabled(CircuitEntry.Final, !circuit);
            if (circuit && entryRow.Value is CircuitEntry.Base or CircuitEntry.Final) entryRow.Show(CircuitEntry.Downwind);
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
                if (name == "CIRC" && Enum.TryParse(value, out CircuitType circuit)) commands.NextCircuit(callsign, circuit, engine.SideFor(circuit));
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
                    commands.Orbit(callsign);
                    break;
                case "CONTINUE":
                    commands.Continue(callsign);
                    break;
                case "GA":
                    commands.GoAround(callsign);
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
            else Say("Select an aircraft first (click it on the map).");
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
                orbitButton.IsEnabled = inCircuit && leg is "DOWNWIND" or "EXT DOWNWIND" or "UPWIND" or "CROSSWIND" or "JOIN";
                orbitForeverButton.IsEnabled = a.OrbitForever || orbitButton.IsEnabled;
                orbitForeverButton.Content = a.OrbitForever ? "CONTINUE" : "ORBIT ∞";
                orbitForeverButton.ToolTip = Tips[a.OrbitForever ? "CONTINUE" : "ORBIT ∞"];
                orbitForeverButton.BorderBrush = a.OrbitForever ? Brushes.Gold : Brushes.Transparent;
                orbitForeverButton.BorderThickness = new Thickness(a.OrbitForever ? 2 : 1);
                goAroundButton.IsEnabled = leg == "FINAL";
                sideLeftButton.IsEnabled = sideRightButton.IsEnabled = wideButton.IsEnabled = inCircuit;
                rejoinButton.IsEnabled = a.Contact == GroundContact.Airborne && (a.Circuit == null || leg is "MANUAL" or "GO AROUND");
                breakupButton.IsEnabled = leg is "DOWNWIND" or "EXT DOWNWIND" or "ORBIT" or "ORBITS" or "BASE";
            }
            selectedText.Text = a == null ? "None: click an aircraft on the map."
                : $"{a.Callsign}  {a.PerformanceClass}\n{Math.Round(a.AltitudeFt - airfield.ElevationFt):0} ft  {Math.Round(a.GroundSpeedKt):0} kt  {TowerView.LegText(a)}";
        }
    }
}
