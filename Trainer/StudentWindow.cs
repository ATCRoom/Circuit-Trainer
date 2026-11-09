using System.Globalization;
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
    /// The student console (the user, 6 and 9 Nov): the top view of the aerodrome with the traffic (callsign, ICAO type,
    /// flight rules, altitude, speed), the runway, UTC time and the METAR line; the student's own strip bay (his notes: the
    /// instructor does not see them); NORTH / RWY. No commands to the aircraft.
    /// On the same PC (a second window, for a second monitor) it is fed by the instructor's window; on another PC (a lesson
    /// joined with its code) by the link: the map comes in pieces at the start (kept in memory only), then the photo of each
    /// second; IVAO's traffic from the student's own Aurora (the user, 9 Nov).
    /// </summary>
    internal sealed class StudentWindow : Window
    {
        private readonly TrainerSettings settings;
        private readonly ScrollViewer column;
        private readonly DockablePart bayPart;
        private readonly Button northButton, runwayButton;
        private readonly Button? ivaoButton;
        private string mapKey = "";
        private double course = double.NaN;

        // ----- On another PC: the link, the map in pieces, Aurora.
        private readonly LessonLink? link;
        private readonly string appVersion;
        private readonly DispatcherTimer? linkTimer;
        private readonly Dictionary<int, string> pieces = [];
        private string piecesId = "";
        private int piecesCount;
        private string mapId = "";
        private MapFile? map;
        private LessonPlane? plane;
        private InstructorMessage? lastMessage;
        private readonly Aurora aurora = new();
        private bool polling;
        private string ivaoCallsign = "", ivaoVid = "", vidFor = "";
        private DateTime callsignAt = DateTime.MinValue;
        private int seconds;

        private sealed record LessonPlane(double Latitude, double Longitude, double Heading)
        {
            public LocalPlane Make() => new(Latitude, Longitude, Heading);
        }

        /// <summary>The student's view (zoom and moves his own).</summary>
        public TowerView View { get; } = new() { Student = true };

        /// <summary>The student's strip bay (his notes, his order of the strips).</summary>
        public StripBay Bay { get; } = new() { Margin = new Thickness(4) };

        /// <summary>Linked to another PC (a lesson joined), not fed by the window of the instructor on this PC.</summary>
        public bool Remote => link != null;

        public StudentWindow(TrainerSettings settings, Func<StripKind, Brush> colours, LessonLink? link = null, string appVersion = "")
        {
            this.settings = settings;
            this.link = link;
            this.appVersion = appVersion;
            Title = link != null ? $"TWR Trainer - Student (lesson {link.Code})" : "TWR Trainer - Student";
            Width = 1200;
            Height = 800;
            Background = Theme.Panel;
            Foreground = Theme.Text;
            if (Application.Current?.MainWindow?.Icon is ImageSource icon) Icon = icon;

            StackPanel panel = new() { Margin = new Thickness(10) };
            column = new ScrollViewer { Content = panel, Width = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Panel };
            DockPanel root = new();
            DockPanel.SetDock(column, Dock.Right);
            root.Children.Add(column);
            Grid mapGrid = new();
            mapGrid.Children.Add(View);
            Canvas layer = new();
            mapGrid.Children.Add(layer);
            root.Children.Add(mapGrid);
            Content = root;

            // NORTH / RWY (the user, 9 Nov), and IVAO from the student's own Aurora when linked to another PC.
            StackPanel bar = new() { Orientation = Orientation.Horizontal };
            northButton = MainWindow.ActionButton("NORTH", () => SetTurn(0));
            runwayButton = MainWindow.ActionButton("RWY", () => SetTurn(settings.StudentMapTurn == 1 ? 2 : 1));
            northButton.ToolTip = "The map north up.";
            runwayButton.ToolTip = "The runway in use across the screen; again: turned round.";
            bar.Children.Add(northButton);
            bar.Children.Add(runwayButton);
            if (link != null)
            {
                ivaoButton = MainWindow.ActionButton("IVAO", ToggleIvao);
                ivaoButton.Margin = new Thickness(12, 0, 0, 4);
                bar.Children.Add(ivaoButton);
            }
            Border barBorder = new()
            {
                Child = bar,
                Background = Theme.MapBar,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5D, 0x7A, 0x99)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 0)
            };
            layer.Children.Add(barBorder);
            void PlaceBar()
            {
                Canvas.SetLeft(barBorder, Math.Max(0, (layer.ActualWidth - barBorder.ActualWidth) / 2));
                Canvas.SetTop(barBorder, 8);
            }
            layer.SizeChanged += (_, _) => PlaceBar();
            barBorder.SizeChanged += (_, _) => PlaceBar();

            Bay.ColourOf = colours;
            settings.Parts ??= [];
            // (Its strip bay floating on the right by default; resizable, closed to a small box, or in the column.)
            if (!settings.Parts.TryGetValue("Student strip bay", out PartPlacement? place))
                settings.Parts["Student strip bay"] = place = new PartPlacement { Floating = true, Width = 280, Height = 520, Left = 5000, Top = 60 };
            bayPart = new DockablePart("Strip bay", "▤", Bay, panel, layer, place, () =>
            {
                settings.Save();
                ShowColumn();
            });
            ShowColumn();
            ShowTurn();
            ShowIvaoButton();
            Loaded += (_, _) => View.ShowLevel(1);
            KeyDown += (_, e) =>
            {
                if (e.Key is Key.F1 or Key.F2)
                {
                    View.ShowLevel(e.Key == Key.F1 ? 1 : 2);
                    e.Handled = true;
                }
            };

            if (link != null)
            {
                View.Status = $"Lesson {link.Code}: waiting for the instructor...";
                link.Received += json => Dispatcher.BeginInvoke(() => Receive(json));
                linkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                linkTimer.Tick += (_, _) => LinkSecond();
                linkTimer.Start();
                Closed += (_, _) =>
                {
                    linkTimer?.Stop();
                    link.Dispose();
                    aurora.Close();
                };
            }
        }

        /// <summary>The column on the right only when the strip bay is in it.</summary>
        private void ShowColumn() => column.Visibility = bayPart.IsFloating ? Visibility.Collapsed : Visibility.Visible;

        private void SetTurn(int turn)
        {
            settings.StudentMapTurn = turn;
            settings.Save();
            ShowTurn();
        }

        /// <summary>NORTH / RWY as on the instructor's map: north up, or the runway in use across the screen.</summary>
        private void ShowTurn()
        {
            int turn = double.IsNaN(course) ? 0 : settings.StudentMapTurn;
            View.Rotation = turn == 0 ? 0 : course - 90 + (turn == 2 ? 180 : 0);
            View.InvalidateVisual();
            foreach ((Button button, bool on) in new[] { (northButton, turn == 0), (runwayButton, turn != 0) })
            {
                button.Background = on ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A)) : Theme.OffBack;
                button.Foreground = on ? new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)) : Theme.OffText;
                button.BorderBrush = button.Background;
                button.Tag = on ? "on" : null;
            }
        }

        /// <summary>The aerodrome of the lesson: the runway, gates, exits, helipads, the categories shown; the map when it changed.</summary>
        public void SetAerodrome(LessonAerodrome ad, MapFile? shown, LocalPlane localPlane)
        {
            View.LengthNM = ad.Length;
            View.ElevationFt = ad.Elevation;
            View.Qfe = ad.Qfe;
            View.CentrelineNM = ad.Centreline;
            View.LongFinalNM = ad.LongFinal;
            View.Gates = ad.Gates.Select(p => (p.Name, p.X, p.Y)).ToList();
            View.Exits = ad.Exits.Select(p => (p.Name, p.X, p.Y)).ToList();
            View.Helipads = ad.Helipads.Select(p => (p.Name, p.X, p.Y)).ToList();
            if (!View.CategoriesOn.SetEquals(ad.Categories))
            {
                View.CategoriesOn.Clear();
                View.CategoriesOn.UnionWith(ad.Categories);
            }
            string key = ad.MapKey + (shown != null ? "|map" : "|none");
            bool turned = !View.Course.Equals(ad.Course);
            View.Course = ad.Course;
            course = ad.Course;
            if (key != mapKey)
            {
                bool first = mapKey.Length == 0 || !mapKey.EndsWith("|map", StringComparison.Ordinal);
                mapKey = key;
                View.SetMap(shown, localPlane);
                if (first) View.ShowLevel(1);
            }
            if (turned) ShowTurn();
        }

        /// <summary>The photo of the lesson of this second.</summary>
        public void Show(LessonSnapshot s, string link = "")
        {
            View.Status = $"{s.Aerodrome}  RWY {(s.Runway.Length > 0 ? s.Runway : "-")}   UTC {s.Utc:HH:mm:ss}" + (s.Paused ? "   PAUSED" : "") + link;
            View.Metar = s.Metar;
            if (!Remote) View.Network = s.Network;
            View.SetTargets(s.Traffic, s.Time);
            Bay.Show(s.Coordination, s.Contact);
            bayPart.SummaryText = $"{s.Coordination.Count} + {s.Contact.Count}";
            ShowColumn();
        }

        // ----- Linked to another PC ----------------------------------------------------------------------------------

        /// <summary>A message of the instructor: the photo of the second, or a piece of the map.</summary>
        private void Receive(string json)
        {
            // (A message that cannot be used is dropped, never an error on the screen; the review of 9 Nov.)
            try
            {
                ReceiveMessage(json);
            }
            catch (Exception ex) when (ex is NullReferenceException or ArgumentException or InvalidOperationException or FormatException or IndexOutOfRangeException)
            {
            }
        }

        private void ReceiveMessage(string json)
        {
            switch (LessonMessages.KindOf(json))
            {
                case "snap" when LessonMessages.Read<InstructorMessage>(json) is InstructorMessage message && message.Ad != null:
                    message.Traffic ??= [];
                    message.Coordination ??= [];
                    message.Contact ??= [];
                    message.Ad.Gates ??= [];
                    message.Ad.Exits ??= [];
                    message.Ad.Helipads ??= [];
                    message.Ad.Categories ??= [];
                    lastMessage = message;
                    foreach (string[] pair in message.Rekeys ?? []) if (pair is [string old, string now]) Bay.Rekey(old, now);
                    plane = new LessonPlane(message.Ad.Latitude, message.Ad.Longitude, message.Ad.Heading);
                    SetAerodrome(message.Ad, message.Ad.MapId == mapId ? map : null, plane.Make());
                    Show(new LessonSnapshot(message.Time, message.Utc, message.Paused, message.Aerodrome, message.Runway, message.Metar,
                        message.Traffic, [], message.Coordination.Select(LessonMessages.FromLink).ToList(), message.Contact.Select(LessonMessages.FromLink).ToList()),
                        LinkText());
                    break;
                case "map" when LessonMessages.Read<MapPiece>(json) is MapPiece { Id: not null, Data: not null } piece:
                    TakePiece(piece);
                    break;
            }
        }

        /// <summary>A piece of the map: kept until all are here, then the map read (in memory only, never saved).</summary>
        private void TakePiece(MapPiece piece)
        {
            if (piece.Id.Length == 0 || piece.Id == mapId || piece.Count <= 0 || piece.Index < 0 || piece.Index >= piece.Count) return;
            if (piece.Id != piecesId || piece.Count != piecesCount)
            {
                pieces.Clear();
                piecesId = piece.Id;
                piecesCount = piece.Count;
            }
            pieces[piece.Index] = piece.Data;
            if (pieces.Count < piecesCount) return;
            MapFile? read = LessonMessages.Unpack(Enumerable.Range(0, piecesCount).Select(i => pieces[i]));
            pieces.Clear();
            piecesId = "";
            if (read == null) return;
            map = read;
            mapId = piece.Id;
            if (lastMessage is InstructorMessage message && plane != null && message.Ad.MapId == mapId) SetAerodrome(message.Ad, map, plane.Make());
        }

        /// <summary>The state of the link on the status line.</summary>
        private string LinkText()
        {
            if (link == null) return "";
            if (!link.OtherPresent) return link.EverHeard ? "   LINK LOST" : "";
            if (lastMessage is InstructorMessage message && message.Ad.MapId.Length > 0 && message.Ad.MapId != mapId)
                return piecesCount > 0 ? $"   receiving the map {pieces.Count}/{piecesCount}" : "   receiving the map";
            return "   LINK ●";
        }

        /// <summary>Each second: the student's message (his map, callsign and VID), the state of the link, IVAO.</summary>
        private void LinkSecond()
        {
            if (link == null) return;
            // (The callsign: Aurora's when connected to IVAO, else the one written in Settings; the user, 9 Nov.)
            bool onIvao = settings.StudentIvao && ivaoCallsign.Length > 0;
            link.Send(LessonMessages.Write(new StudentMessage
            {
                Map = mapId,
                Callsign = onIvao ? ivaoCallsign : settings.MyCallsign,
                Vid = onIvao && ivaoVid.Length > 0 ? ivaoVid : settings.InstructorVid
            }));
            if (!link.OtherPresent)
            {
                View.Status = link.EverHeard ? $"Lesson {link.Code}: LINK LOST (the instructor is not heard)" : $"Lesson {link.Code}: waiting for the instructor...";
                View.InvalidateVisual();
            }
            if (settings.StudentIvao && ++seconds % 2 == 0) _ = PollIvao();
        }

        private void ToggleIvao()
        {
            settings.StudentIvao = !settings.StudentIvao;
            settings.Save();
            callsignAt = DateTime.MinValue;
            if (!settings.StudentIvao)
            {
                ivaoCallsign = ivaoVid = vidFor = "";
                View.Network = [];
                View.InvalidateVisual();
            }
            ShowIvaoButton();
        }

        private void ShowIvaoButton()
        {
            if (ivaoButton == null) return;
            bool on = settings.StudentIvao, linked = on && aurora.Connected, network = linked && ivaoCallsign.Length > 0;
            ivaoButton.Content = !on ? "IVAO" : linked ? "● IVAO" : "IVAO ?";
            ivaoButton.Background = !on ? Theme.OffBack : network ? new SolidColorBrush(Color.FromRgb(0x63, 0x99, 0x22)) : new SolidColorBrush(Color.FromRgb(0xEF, 0x9F, 0x27));
            ivaoButton.Foreground = !on ? Theme.OffText : new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A));
            ivaoButton.BorderBrush = ivaoButton.Background;
            ivaoButton.Tag = on ? "on" : null;
            ivaoButton.ToolTip = !on ? "The traffic of IVAO from Aurora on this PC: off. Click: on (only shown)."
                : !linked ? "Aurora not found on this PC: open Aurora (with its link for other programs on). Click: off."
                : network ? $"Aurora on IVAO as {ivaoCallsign}: its traffic on the map (light blue). Click: off."
                : "Aurora found, but not connected to IVAO: no traffic yet. Click: off.";
        }

        /// <summary>IVAO's traffic within 60 NM from the student's own Aurora (every 2 s at most), his callsign and VID.</summary>
        private async Task PollIvao()
        {
            if (polling || !settings.StudentIvao) return;
            polling = true;
            try
            {
                if (!aurora.Connected && !await aurora.TryConnect())
                {
                    ivaoCallsign = "";
                    View.Network = [];
                    return;
                }
                if (DateTime.UtcNow - callsignAt > TimeSpan.FromSeconds(30))
                {
                    callsignAt = DateTime.UtcNow;
                    ivaoCallsign = await aurora.GetConnectedCallsign() ?? "";
                    if (ivaoCallsign.Length > 0 && ivaoCallsign != vidFor)
                    {
                        vidFor = ivaoCallsign;
                        ivaoVid = await IvaoDirectory.FindVidAsync(ivaoCallsign, appVersion) ?? "";
                    }
                }
                List<(string, double, double, double, double, double)> shown = [];
                if (plane != null)
                {
                    LocalPlane local = plane.Make();
                    foreach (string callsign in (await aurora.GetTrafficList()).Take(80))
                    {
                        if (!settings.StudentIvao) break;
                        if (await aurora.GetTrafficPosition(callsign) is not IvaoAircraft a) continue;
                        (double x, double y) = local.FromGeo(a.Latitude, a.Longitude);
                        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Sqrt(x * x + y * y) > 60) continue;
                        shown.Add((a.Callsign, x, y, a.AltitudeFt, a.SpeedKt, a.Track));
                    }
                }
                if (!settings.StudentIvao) return;
                View.Network = shown;
                View.InvalidateVisual();
            }
            catch (Exception)
            {
                // (Anything unexpected from the network: tried again at the next turn.)
            }
            finally
            {
                polling = false;
                ShowIvaoButton();
            }
        }
    }
}
