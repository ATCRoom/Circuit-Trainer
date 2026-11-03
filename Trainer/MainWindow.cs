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
        private readonly ComboBox startBox = Combo(TowerCommands.Starts);
        private readonly ComboBox circuitBox = Combo(TowerCommands.Circuits);
        private readonly ComboBox sideBox = Combo<CircuitSide?>([(null, "Aerodrome's"), (CircuitSide.Left, "Left"), (CircuitSide.Right, "Right")]);
        private readonly ComboBox classBox = new();
        private readonly ComboBox endBox = Combo(TowerCommands.Ends);
        private readonly TextBox callsignBox = new() { Width = 90 };
        private readonly CheckBox holdBox = new() { Content = "Hold at the High Key (SFO)" };
        private readonly TextBlock selectedText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly ComboBox selCircuitBox = Combo(TowerCommands.Circuits);
        private readonly ComboBox selSideBox = Combo<CircuitSide>([(CircuitSide.Left, "Left"), (CircuitSide.Right, "Right")]);
        private readonly ComboBox selEndBox = Combo(TowerCommands.Ends);
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
            foreach (PerformanceClass kind in engine.Classes)
            {
                classBox.Items.Add(kind.Name);
                heightClassBox.Items.Add(kind.Name);
            }
            classBox.SelectedItem = engine.Classes.Any(c => c.Name == PerformanceClass.DefaultName) ? PerformanceClass.DefaultName : classBox.Items.Count > 0 ? classBox.Items[0] : null;
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
            Row("Start", startBox);
            Row("Circuit", circuitBox);
            Row("Side", sideBox);
            Row("Class", classBox);
            Row("At the end", endBox);
            Row("Callsign (optional)", callsignBox);
            holdBox.Foreground = TextBrush;
            Row("", holdBox);
            startBox.SelectedIndex = circuitBox.SelectedIndex = sideBox.SelectedIndex = endBox.SelectedIndex = 0;
            Note("Overhead starts at the initial point, SFO at the High Key (from elsewhere they fly the close pattern).");
            Buttons(("Add", AddAircraft));

            Header("Selected aircraft");
            panel.Children.Add(selectedText);
            panel.Children.Add(selectedPanel);
            Row("Circuit", selCircuitBox, selectedPanel);
            // Another circuit chosen: the side of that circuit at this runway (it can still be changed before flying it).
            selCircuitBox.SelectionChanged += (_, _) =>
            {
                if (!updating) SelectTag(selSideBox, engine.SideFor(TagOf<CircuitType>(selCircuitBox)));
            };
            Row("Side", selSideBox, selectedPanel);
            Buttons(selectedPanel, ("Fly this circuit", () => WithSelected(cs =>
                commands.FlyCircuit(cs, TagOf<CircuitType>(selCircuitBox), TagOf<CircuitSide>(selSideBox)))));
            Row("At the end", selEndBox, selectedPanel);
            selEndBox.SelectionChanged += (_, _) =>
            {
                if (!updating) WithSelected(cs => commands.SetEnd(cs, TagOf<EndAction>(selEndBox)));
            };
            Note("Overhead: where it breaks.", selectedPanel);
            Buttons(selectedPanel, TowerCommands.Breaks.Select(b => ((string, Action))(b.Text, () => WithSelected(cs => commands.SetBreak(cs, b.Break)))).ToArray());
            Buttons(selectedPanel, ("Point on the map", () => WithSelected(cs =>
                view.AskPoint($"{cs}: click where it breaks (along the runway)  -  Esc: cancel", (x, y) => commands.SetBreakAt(cs, x, y)))),
                ("BREAK NOW", () => WithSelected(commands.BreakNow)));
            Buttons(selectedPanel, ("Orbit", () => WithSelected(commands.Orbit)), ("Go around", () => WithSelected(commands.GoAround)));
            Note("SFO: holding over the field at the High Key, or start.", selectedPanel);
            Buttons(selectedPanel, ("Hold at High Key", () => WithSelected(cs => commands.HoldAtHighKey(cs, true))),
                ("Start SFO", () => WithSelected(cs => commands.HoldAtHighKey(cs, false))));
            Buttons(selectedPanel, ("Helipad on the map", () => WithSelected(cs =>
                view.AskPoint($"{cs}: click the helipad  -  Esc: cancel", (x, y) => commands.Helipad(cs, x, y)))),
                ("Remove", () => WithSelected(commands.Remove)));
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

        private void Row(string label, UIElement control, Panel? into = null)
        {
            Grid grid = new() { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
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
            string? error = commands.Add(TagOf<CircuitStart>(startBox), TagOf<CircuitType>(circuitBox), TagOf<CircuitSide?>(sideBox), TagOf<EndAction>(endBox),
                classBox.SelectedItem as string, callsignBox.Text, holdBox.IsChecked == true, out string added);
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
            switch (field)
            {
                case "CCT":
                    OpenMenu(CircuitItems(callsign));
                    break;
                case "SIDE":
                    commands.FlyCircuit(callsign, type, a.CircuitSide == CircuitSide.Left ? CircuitSide.Right : CircuitSide.Left);
                    break;
                case "NEXT":
                    OpenMenu(EndItems(callsign, a.EndAction));
                    break;
                case "BRK":
                    OpenMenu(BreakItems(callsign, a.Break));
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

        /// <summary>The wheel on NEXT or BRK: the next (or the previous) value.</summary>
        private void LabelWheel(string callsign, string field, int step)
        {
            AircraftState? a = engine.Get(callsign);
            if (a == null) return;
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
            updating = true;
            if (a != null)
            {
                SelectTag(selCircuitBox, a.Circuit ?? CircuitType.Conventional);
                SelectTag(selSideBox, a.CircuitSide);
                SelectTag(selEndBox, a.EndAction);
            }
            updating = false;
            ShowSelectedValues();
        }

        private void ShowSelectedValues()
        {
            AircraftState? a = view.Selected is string cs ? engine.Get(cs) : null;
            selectedText.Text = a == null ? "None: click an aircraft on the map."
                : $"{a.Callsign}  {a.PerformanceClass}\n{Math.Round(a.AltitudeFt - airfield.ElevationFt):0} ft  {Math.Round(a.GroundSpeedKt):0} kt  {TowerView.LegText(a)}";
        }
    }
}
