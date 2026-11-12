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
                // (In the log of the session too, the user 8 Nov.)
                try { MainWindow.Current?.ProgramProblem(e.Exception, fatal: false); } catch (Exception) { }
                MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nCircuit Trainer goes on; if it happens again, please tell us what you were doing.",
                    "Circuit Trainer", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            // A crash it cannot go on from (another thread): written in the log of the session, which is closed, before it ends.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    if (e.ExceptionObject is Exception error) MainWindow.Current?.ProgramProblem(error, fatal: e.IsTerminating);
                }
                catch (Exception) { }
            };
            // A task gone wrong without anyone waiting for it: written (an error, the program goes on).
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try { MainWindow.Current?.ProgramProblem(e.Exception, fatal: false); } catch (Exception) { }
                e.SetObserved();
            };
            // (Every button: one look, readable under the mouse: the user, 4 Nov.)
            // (The inside of the panels dark or light, from the settings: before anything is made, the user 6 Nov.)
            Theme.Light = TrainerSettings.Load().LightPanels;
            app.Resources[typeof(System.Windows.Controls.Button)] = ButtonStyle.Make();
            app.Run(new MainWindow());
        }
    }
}
