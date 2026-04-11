using System.Windows;

namespace ASFManagerPRO
{
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            // Принудительно сохраняем всё при выходе из приложения
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.SaveAccounts();
            }
            base.OnExit(e);
        }
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Обработка необработанных исключений
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Критическая ошибка: {args.ExceptionObject}", "ASF Manager PRO", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Ошибка: {args.Exception.Message}", "ASF Manager PRO", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
