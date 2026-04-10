using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private readonly string dataPath = "accounts.json";
        private bool isWebViewInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Убираем обработку ошибки с иконкой
            try
            {
                LoadAccounts();
                _ = InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске: {ex.Message}", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Создаём папку для данных WebView2, если её нет
                string webViewDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASF_Manager_PRO_WebView");
                var env = await CoreWebView2Environment.CreateAsync(null, webViewDataPath);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                isWebViewInitialized = true;

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
                if (File.Exists(htmlPath))
                {
                    string html = await File.ReadAllTextAsync(htmlPath);
                    webView.NavigateToString(html);
                }
                else
                {
                    MessageBox.Show($"index.html не найден по пути: {htmlPath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации WebView2: {ex.Message}\n\nУстановите WebView2 Runtime: https://go.microsoft.com/fwlink/p/?LinkId=2124703", 
                    "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                if (msg?.Action == "saveAccounts" && msg.Data != null)
                {
                    var updatedAccounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data);
                    if (updatedAccounts != null)
                    {
                        Accounts = updatedAccounts;
                        SaveAccounts();
                    }
                }
                else if (msg?.Action == "getAccounts")
                {
                    SendToJS("accounts", Accounts);
                }
                else if (msg?.Action == "runASF")
                {
                    MessageBox.Show($"Запуск ASF для аккаунта ID: {msg.Data}\n\nФункция в разработке", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (msg?.Action == "openBrowser")
                {
                    MessageBox.Show($"Открытие браузера для аккаунта ID: {msg.Data}\n\nФункция в разработке", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки: {ex.Message}", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendToJS(string type, object data)
        {
            if (!isWebViewInitialized || webView?.CoreWebView2 == null) return;
            
            try
            {
                string json = JsonSerializer.Serialize(new { type, data });
                webView.CoreWebView2.ExecuteScriptAsync($"if(window.receiveFromCSharp) window.receiveFromCSharp({json});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendToJS error: {ex.Message}");
            }
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    string json = File.ReadAllText(dataPath);
                    Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(json) ?? new ObservableCollection<Account>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки accounts.json: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Accounts = new ObservableCollection<Account>();
            }
        }

        private void SaveAccounts()
        {
            try
            {
                string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения accounts.json: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Account
    {
        public string Id { get; set; } = "";
        public string Login { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmailPass { get; set; } = "";
        public string Proxy { get; set; } = "";
        public string Pin { get; set; } = "";
        public string MaFile { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "Offline";
        public string Balance { get; set; } = "0 ₽";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }
}
