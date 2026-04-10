using System.Windows;

namespace ASFManagerPRO
{
    public partial class App : Application
    {
        public App()
        {
            // Это помогает при запуске
            DispatcherUnhandledException += (sender, e) =>
            {
                MessageBox.Show("Ошибка запуска: " + e.Exception.Message, "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }
}
