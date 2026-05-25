using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ASFManagerPRO
{
    public partial class App : Application
    {
        private static string logFile = "";
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Настройка логирования
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string appDataFolder = Path.Combine(exeFolder, "ASF_Data");
            
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);
            
            logFile = Path.Combine(appDataFolder, "asf_manager_log.txt");
            
            // Запись запуска в лог
            Log($"ASF Manager PRO v3.3 запущен {DateTime.Now}");
            Log($"OS: {Environment.OSVersion}");
            Log($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            Log($"Working Directory: {Environment.CurrentDirectory}");
            
            // Обработка необработанных исключений
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                string errorMsg = $"КРИТИЧЕСКАЯ ОШИБКА: {ex?.Message}\n{ex?.StackTrace}";
                Log(errorMsg);
                
                MessageBox.Show($"Критическая ошибка: {ex?.Message}\n\nЛог сохранен в: {logFile}", 
                    "ASF Manager PRO", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            };
            
            DispatcherUnhandledException += (sender, args) =>
            {
                Log($"Dispatcher ошибка: {args.Exception.Message}\n{args.Exception.StackTrace}");
                
                MessageBox.Show($"Ошибка: {args.Exception.Message}\n\nЛог сохранен в: {logFile}", 
                    "ASF Manager PRO", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                args.Handled = true;
            };
            
            // Настройка производительности
            SetProcessPerformance();
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            Log($"ASF Manager PRO завершен {DateTime.Now}");
            Log("----------------------------------------");
            
            // Принудительно сохраняем всё при выходе из приложения
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                try
                {
                    mainWindow.SaveAccounts();
                }
                catch (Exception ex)
                {
                    Log($"Ошибка сохранения при выходе: {ex.Message}");
                }
            }
            base.OnExit(e);
        }
        
        private void SetProcessPerformance()
        {
            try
            {
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    // Устанавливаем высокий приоритет для плавной работы
                    currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                    
                    // Оптимизация для работы с WebView2
                    currentProcess.MaxWorkingSet = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка настройки производительности: {ex.Message}");
            }
        }
        
        private void Log(string message)
        {
            try
            {
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                
                // Ограничиваем размер лога 5 МБ
                FileInfo logInfo = new FileInfo(logFile);
                if (logInfo.Exists && logInfo.Length > 5 * 1024 * 1024)
                {
                    string archiveLog = Path.Combine(Path.GetDirectoryName(logFile) ?? "", "asf_manager_log_old.txt");
                    if (File.Exists(archiveLog))
                        File.Delete(archiveLog);
                    File.Move(logFile, archiveLog);
                }
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }
        
        public static void LogMessage(string message)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string logPath = Path.Combine(exeFolder, "ASF_Data", "asf_manager_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
