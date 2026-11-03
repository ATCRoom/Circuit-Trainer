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
        private readonly ChoiceRow<CircuitStart> startRow = new(TowerCommands.Starts.Select(s => (s.Start, s.Short)));
        private readonly ChoiceRow<CircuitType> circuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)));
        private readonly ChoiceRow<CircuitSide?> sideRow = new([(null, "AD"), (CircuitSide.Left, "L"), (CircuitSide.Right, "R")]);
        private ChoiceRow<string> classRow = new([]);
        private readonly StackPanel classHolder = new();
        private readonly ChoiceRow<EndAction> endRow = new(TowerCommands.Ends.Select(e => (e.End, e.Short)));
        private readonly TextBox callsignBox = new() { Width = 90 };
        private readonly CheckBox holdBox = new() { Content = "Hold at the High Key (SFO)" };
        private readonly TextBlock selectedText = new() { TextWrapping = TextWrapping.Wrap };
        // The selected aircraft: each button an order at once.
        private readonly ChoiceRow<CircuitType> selCircuitRow = new(TowerCommands.Circuits.Select(c => (c.Type, c.Short)));
        private readonly ChoiceRow<CircuitSide> selSideRow = new([(CircuitSide.Left, "L"), (CircuitSide.Right, "R")]);
        private readonly ChoiceRow<EndAction> selEndRow = new(TowerCommands.Ends.Select(e => (e.End, e.Short)));
        private readonly ChoiceRow<BreakPoint> selBreakRow = new(TowerCommands.Breaks.Select(b => (b.Break, b.Short)));
        private readonly StackPanel selectedPanel = new();
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
            root.Children.Add(view);
            Content = root;

            view.SelectionChanged += _ => ShowSelected();
            view.FieldClicked += LabelField;
            view.FieldWheel += LabelWheel;
            view.MenuAsked += callsign => OpenMenu(Menu(callsign));
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
            classRow = new ChoiceRow<string>(engine.Classes.Select(c => (c.Name, c.Name.ToUpperInvariant())));
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
                if (pendingCallsign != null && pendingCallsign == view.Selected && engine.Get(pendingCallsign)?.Circuit != chosen)
                {
                    commands.FlyCircuit(pendingCallsign, chosen, engine.SideFor(chosen));
                    ShowSelected();
                }
                pendingCallsign = null;
            }
            if (++ticks % 10 == 0) ShowSelectedValues();
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
            Header("Aerodrome");
            panel.Children.Add(mapText);
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
            panel.Children.Add(Labelled(("Conventional", conventionalHeight), ("Wide", wideHeight), ("Close", closeHeight)));
            panel.Children.Add(Labelled(("Initial (OVH)", initialHeight), ("Break (OVH)", breakHeight)));
            panel.Children.Add(Labelled(("High Key (SFO)", highKeyHeight), ("Low Key (SFO)", lowKeyHeight)));
            Buttons(("Save heights", SaveHeights));

            Row("METAR (wind)", metarBox);
            Buttons(("Apply METAR", () => ApplyMetar(quiet: false)));
            panel.Children.Add(windText);

            Header("Simulation");
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
            panel.Children.Add(layersBox);

            Header("New aircraft");
            Note("Click along the rows, then ADD (the choices stay for the next one). AD: the aerodrome's side. Overhead starts at IP, SFO at HK.");
            Row("Start", startRow, null, 62);
            Row("Circuit", circuitRow, null, 62);
            Row("Side", sideRow, null, 62);
            Row("Class", classHolder, null, 62);
            Row("At the end", endRow, null, 62);
            startRow.Show(CircuitStart.Downwind);
            circuitRow.Show(CircuitType.Conventional);
            sideRow.Show(null);
            endRow.Show(EndAction.TouchAndGo);
            Row("Callsign", Inline(callsignBox, new TextBlock { Text = "(optional)", Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center }), null, 62);
            holdBox.Foreground = TextBrush;
            Row("", holdBox, null, 62);
            Buttons(("ADD", AddAircraft));

            Header("Selected aircraft");
            panel.Children.Add(selectedText);
            panel.Children.Add(selectedPanel);
            Note("Each button is an order at once; they can be chained (CLS then R: the close pattern on the right).", selectedPanel);
            Row("Circuit", selCircuitRow, selectedPanel, 62);
            // Another circuit: flown at once on the side of that circuit at this runway (then L / R to change it).
            selCircuitRow.Picked += type => WithSelected(cs =>
            {
                CircuitSide side = engine.SideFor(type);
                selSideRow.Show(side);
                commands.FlyCircuit(cs, type, side);
            });
            Row("Side", selSideRow, selectedPanel, 62);
            selSideRow.Picked += side => WithSelected(cs => commands.FlyCircuit(cs, selCircuitRow.HasValue ? selCircuitRow.Value : CircuitType.Conventional, side));
            Row("At the end", selEndRow, selectedPanel, 62);
            selEndRow.Picked += end => WithSelected(cs => commands.SetEnd(cs, end));
            Row("Break", selBreakRow, selectedPanel, 62);
            selBreakRow.Picked += where => WithSelected(cs => commands.SetBreak(cs, where));
            Row("", Inline(ActionButton("PNT", () => WithSelected(cs =>
                    view.AskPoint($"{cs}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(cs, x, y)))),
                ActionButton("BREAK NOW", () => WithSelected(commands.BreakNow))), selectedPanel, 62);
            WrapPanel actions = new();
            foreach ((string text, Action action) in new (string, Action)[]
            {
                ("ORB", () => WithSelected(commands.Orbit)),
                ("G/A", () => WithSelected(commands.GoAround)),
                ("HOLD", () => WithSelected(cs => commands.HoldAtHighKey(cs, true))),
                ("START", () => WithSelected(cs => commands.HoldAtHighKey(cs, false))),
                ("PAD", () => WithSelected(cs => view.AskPoint($"{cs}: click the helipad  -  Esc: cancel", (x, y) => commands.Helipad(cs, x, y)))),
                ("REMOVE", () => WithSelected(commands.Remove))
            }) actions.Children.Add(ActionButton(text, action));
            Row("Do", actions, selectedPanel, 62);
            Note("ORB orbit, G/A go around, HOLD / START the SFO at the High Key, PAD the helicopter's helipad (click it). Right click on an aircraft: all its commands.", selectedPanel);
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

        private void Header(string text) => panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 4),
            Foreground = TextBrush
        });

        private void Note(string text, Panel? into = null) => (into ?? panel).Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 2)
        });

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
            (into ?? panel).Children.Add(grid);
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

        private void Buttons(params (string Text, Action Action)[] buttons) => Buttons(panel, buttons);

        private static void Buttons(Panel into, params (string Text, Action Action)[] buttons)
        {
            WrapPanel row = new() { Margin = new Thickness(0, 3, 0, 3) };
            foreach ((string text, Action action) in buttons)
            {
                Button button = new() { Content = text, Margin = new Thickness(0, 0, 5, 4), Padding = new Thickness(8, 2, 8, 2) };
                button.Click += (_, _) => action();
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

        private void AddAircraft()
        {
            string? error = commands.Add(startRow.Value, circuitRow.Value, sideRow.Value, endRow.Value,
                classRow.Value, callsignBox.Text, holdBox.IsChecked == true, out string added);
            Say(error);
            if (error != null) return;
            callsignBox.Text = "";
            view.SetTraffic(engine.Snapshot(), engine.Time);
            view.Select(added);
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
                if (name == "CIRC" && Enum.TryParse(value, out CircuitType circuit)) commands.FlyCircuit(callsign, circuit, engine.SideFor(circuit));
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
                CircuitType now = view.PendingCircuit ?? a.Circuit ?? CircuitType.Conventional;
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
            return TowerCommands.Circuits.Select(c => Item(c.Text, () => { commands.FlyCircuit(callsign, c.Type, engine.SideFor(c.Type)); ShowSelected(); }, a?.Circuit == c.Type)).ToList();
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
            MenuItem circuit = new() { Header = "Circuit" };
            foreach (MenuItem item in CircuitItems(callsign)) circuit.Items.Add(item);
            MenuItem side = new() { Header = "Side" };
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
            items.Add(Item("Orbit", () => commands.Orbit(callsign)));
            items.Add(Item("Go around", () => commands.GoAround(callsign)));
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
                selCircuitRow.Show(a.Circuit ?? CircuitType.Conventional);
                selSideRow.Show(a.CircuitSide);
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
                selCircuitRow.Refresh(a.Circuit ?? CircuitType.Conventional);
                selSideRow.Refresh(a.CircuitSide);
                selEndRow.Refresh(a.EndAction);
                selBreakRow.Refresh(a.Break);
            }
            selectedText.Text = a == null ? "None: click an aircraft on the map."
                : $"{a.Callsign}  {a.PerformanceClass}\n{Math.Round(a.AltitudeFt - airfield.ElevationFt):0} ft  {Math.Round(a.GroundSpeedKt):0} kt  {TowerView.LegText(a)}";
        }
    }
}
