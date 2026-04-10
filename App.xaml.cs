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
    }
}
