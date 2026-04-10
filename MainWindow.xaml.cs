using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
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
            _ = InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
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

                if (msg == null) return;

                if (msg.Action == "saveAccounts")
                {
                    Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data) ?? new();
                    SaveAccounts();
                }
                else if (msg.Action == "getAccounts")
                {
                    SendToJS("accounts", Accounts);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка в WebMessage: " + ex.Message, "Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendToJS(string type, object data)
        {
            var message = new { type, data };
            string json = JsonSerializer.Serialize(message);
            webView.CoreWebView2.ExecuteScriptAsync($"window.receiveFromCSharp({json});");
        }

        private void LoadAccounts()
        {
            if (File.Exists(dataPath))
            {
                string json = File.ReadAllText(dataPath);
                Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(json) ?? new();
            }
        }

        private void SaveAccounts()
        {
            string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataPath, json);
        }
    }

    public class Account
    {
        public string Id { get; set; } = "";
        public string Login { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string Proxy { get; set; } = "";
        public string Status { get; set; } = "Offline";
        public string Balance { get; set; } = "0 ₽";
        public string CreatedAt { get; set; } = "";
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }
}
