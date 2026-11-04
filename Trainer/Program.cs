using System.Windows;

namespace ATCRoom.TwrTrainer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
            // Something unexpected: said, and the program goes on (rather than closing without a word).
            app.DispatcherUnhandledException += (_, e) =>
            {
                MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nTWR Trainer goes on; if it happens again, please tell us what you were doing.",
                    "TWR Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            // (Every button: one look, readable under the mouse: the user, 4 Nov.)
            app.Resources[typeof(System.Windows.Controls.Button)] = ButtonStyle.Make();
            app.Run(new MainWindow());
        }
    }
}
