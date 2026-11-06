using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The colours of the inside of the panels (the user, 6 Nov): DARK (as before) or LIGHT, chosen in Settings › Map layers,
    /// used from the next start (<see cref="Light"/> is set once, before the window is made). The frames of the panels (their
    /// bars with the title, their edges) and the map stay dark.
    /// </summary>
    internal static class Theme
    {
        /// <summary>The inside of the panels light (set at the start, from the settings).</summary>
        public static bool Light { get; set; }

        private static Brush B(byte r, byte g, byte b, byte a = 0xFF)
        {
            SolidColorBrush brush = new(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static readonly Brush DarkPanel = B(0x1A, 0x21, 0x28), LightPanel = B(0xF3, 0xF4, 0xF6);
        private static readonly Brush DarkText = B(0xD6, 0xE2, 0xEC), LightText = B(0x1F, 0x29, 0x33);
        private static readonly Brush DarkDim = B(0x8A, 0x98, 0xA4), LightDim = B(0x5F, 0x6B, 0x76);
        private static readonly Brush DarkWarn = B(0xFF, 0xB0, 0x50), LightWarn = B(0xB5, 0x65, 0x1D);
        private static readonly Brush DarkCallsign = B(0x8E, 0xC9, 0xF5), LightCallsign = B(0x1F, 0x5F, 0x9A);
        private static readonly Brush DarkChosen = B(0xFF, 0xD7, 0x00), LightChosen = B(0xB0, 0x78, 0x00);
        private static readonly Brush DarkSection = B(0x3B, 0x46, 0x50), LightSection = B(0xC9, 0xCE, 0xD3);
        private static readonly Brush DarkActionText = B(0xB9, 0xCB, 0xD6), LightActionText = B(0xFF, 0xFF, 0xFF);
        private static readonly Brush DarkOffBack = B(0x2A, 0x33, 0x3C), LightOffBack = B(0xDF, 0xE7, 0xEE);
        private static readonly Brush DarkOffText = B(0xA9, 0xB6, 0xC0), LightOffText = B(0x2F, 0x4A, 0x5C);
        private static readonly Brush DarkOffEdge = B(0x5F, 0x6B, 0x75), LightOffEdge = B(0x9F, 0xB0, 0xBE);
        private static readonly Brush DarkButtonText = B(0xC8, 0xD4, 0xDE);
        private static readonly Brush DarkMapBar = B(0x1A, 0x21, 0x28, 0xF0), LightMapBar = B(0xF3, 0xF4, 0xF6, 0xF0);

        /// <summary>The inside of a panel.</summary>
        public static Brush Panel => Light ? LightPanel : DarkPanel;
        public static Brush Text => Light ? LightText : DarkText;
        /// <summary>Notes, units, what is less important.</summary>
        public static Brush Dim => Light ? LightDim : DarkDim;
        /// <summary>The messages (warnings, what happened).</summary>
        public static Brush Warn => Light ? LightWarn : DarkWarn;
        /// <summary>The callsigns of the Traffic List.</summary>
        public static Brush Callsign => Light ? LightCallsign : DarkCallsign;
        /// <summary>The one chosen (the selected aircraft, the plan being changed).</summary>
        public static Brush Chosen => Light ? LightChosen : DarkChosen;
        /// <summary>The edge of the parts of a panel.</summary>
        public static Brush Section => Light ? LightSection : DarkSection;
        /// <summary>The text of the blue command buttons.</summary>
        public static Brush ActionText => Light ? LightActionText : DarkActionText;
        /// <summary>A choice not chosen, and a plain button: background, text, edge.</summary>
        public static Brush OffBack => Light ? LightOffBack : DarkOffBack;
        public static Brush OffText => Light ? LightOffText : DarkOffText;
        public static Brush OffEdge => Light ? LightOffEdge : DarkOffEdge;
        public static Brush ButtonText => Light ? LightOffText : DarkButtonText;
        /// <summary>The bar on the map (pause, speed, level).</summary>
        public static Brush MapBar => Light ? LightMapBar : DarkMapBar;
        /// <summary>Under the mouse: the edge of a button (and its text, only dark: on the light it stays readable as it is).</summary>
        public static Brush Over => Light ? LightText : Brushes.White;
    }
}
