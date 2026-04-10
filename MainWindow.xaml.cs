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

        public MainWindow()
        {
            InitializeComponent();
            LoadAccounts();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            if (File.Exists(htmlPath))
            {
                string html = await File.ReadAllTextAsync(htmlPath);
                webView.NavigateToString(html);
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                if (msg?.Action == "saveAccounts")
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data, options) ?? new();
                    SaveAccounts();
                }
                else if (msg?.Action == "getAccounts")
                {
                    SendToJS("accounts", Accounts);
                }
                else if (msg?.Action == "runASF" || msg?.Action == "openBrowser")
                {
                    MessageBox.Show($"Команда выполнена: {msg.Action}\nАккаунт: {msg.Data}", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void SendToJS(string type, object data)
        {
            string json = JsonSerializer.Serialize(new { type, data });
            webView.CoreWebView2.ExecuteScriptAsync($"window.receiveFromCSharp({json});");
        }

        private void LoadAccounts()
        {
            if (File.Exists(dataPath))
            {
                string json = File.ReadAllText(dataPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(json, options) ?? new();
            }
        }

        private void SaveAccounts()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            string json = JsonSerializer.Serialize(Accounts, options);
            File.WriteAllText(dataPath, json);
        }
    }

    public class Account
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Login { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmailPass { get; set; } = "";
        public string SteamGuard { get; set; } = "";
        public string MaFile { get; set; } = "";
        public string Proxy { get; set; } = "";
        public string Pin { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "Offline";
        public string Balance { get; set; } = "0 ₽";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }
}
