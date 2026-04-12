using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private string dataPath;
        private string appDataFolder;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
            this.PreviewKeyDown += Window_PreviewKeyDown;   // ← теперь метод существует

            string exeFolder = GetRealExeFolder();
            appDataFolder = Path.Combine(exeFolder, "ASF_Data");
            dataPath = Path.Combine(appDataFolder, "accounts.json");

            LoadAccounts();
            Accounts.CollectionChanged += (s, e) => SaveAccounts();
            InitializeWebView();
        }

        private string GetRealExeFolder()
        {
            string? exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) SendToJS("hotkey", "new");
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) SendToJS("hotkey", "save");
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) SendToJS("hotkey", "search");
            else if (e.Key == Key.Delete) SendToJS("hotkey", "delete");
        }

        private async void InitializeWebView()
        {
            try
            {
                string webViewDataPath = Path.Combine(appDataFolder, "WebView2Data");
                if (!Directory.Exists(webViewDataPath)) Directory.CreateDirectory(webViewDataPath);

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, webViewDataPath);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                string exeFolder = GetRealExeFolder();
                string htmlPath = Path.Combine(exeFolder, "index.html");

                if (File.Exists(htmlPath))
                {
                    string html = File.ReadAllText(htmlPath);
                    webView.NavigateToString(html);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 Error: " + ex.Message);
            }
        }

        private async void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                if (msg?.Action == "saveAccounts" && !string.IsNullOrWhiteSpace(msg.Data))
                {
                    var list = JsonSerializer.Deserialize<List<Account>>(msg.Data, JsonOptions);
                    if (list != null)
                    {
                        Accounts.Clear();
                        foreach (var acc in list)
                            Accounts.Add(acc);
                    }
                }

                switch (msg?.Action)
                {
                    case "saveAccounts":
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                        break;

                    case "getAccounts":
                        SendToJS("accounts", Accounts);
                        break;

                    case "runASF": RunASF(msg.Data); break;
                    case "runASFForAll": RunASFForAll(); break;
                    case "deleteAllAccounts": Accounts.Clear(); SaveAccounts(); SendToJS("accounts", Accounts); break;
                    case "deleteAccount": DeleteAccount(msg.Data); break;
                    case "massUpdate": MassUpdateAccounts(msg.Data); break;
                    case "copyToClipboard": Clipboard.SetText(msg.Data); break;
                }
            }
            catch { }
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    string json = File.ReadAllText(dataPath);
                    var list = JsonSerializer.Deserialize<List<Account>>(json, JsonOptions);
                    if (list != null)
                    {
                        Accounts.Clear();
                        foreach (var acc in list) Accounts.Add(acc);
                    }
                }
            }
            catch { }
        }

        public void SaveAccounts()
        {
            try
            {
                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                string json = JsonSerializer.Serialize(Accounts, JsonOptions);
                File.WriteAllText(dataPath, json);
            }
            catch { }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveAccounts();
        }

        private void SendToJS(string type, object data)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new { type, data }, JsonOptions);
                webView.CoreWebView2.ExecuteScriptAsync($"window.receiveFromCSharp({json});");
            }
            catch { }
        }

        // Пустые заглушки, чтобы не было ошибок компиляции
        private void MassUpdateAccounts(string data) { }
        private async Task GetInventory(string parameters) { }
        private void RunASF(string login) { }
        private void RunASFForAll() { }
        private void DeleteAccount(string accountId) { }
        private void UpdateBalance(string data) { }
        private Account? GetAccountByLogin(string login) => null;
        private void UpdateLastLogin(string accountId) { }
        private Account? GetAccountById(string id) => null;
    }

    // ====================== МОДЕЛИ ======================
    public class Account : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
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
        public string SteamId { get; set; } = "";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
        public string LastLogin { get; set; } = "";
        public int CardsRemaining { get; set; } = 0;
        public int GamesCount { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WebMessage { public string Action { get; set; } = ""; public string Data { get; set; } = ""; }
    public class MassUpdateData { public string[] AccountIds { get; set; } = Array.Empty<string>(); public Dictionary<string, string> Fields { get; set; } = new(); }

    public class SteamInventory { public bool success { get; set; } public SteamInventoryItem[]? assets { get; set; } public SteamInventoryDescription[]? descriptions { get; set; } public int total_inventory_count { get; set; } }
    public class SteamInventoryItem { public string assetid { get; set; } = ""; public string classid { get; set; } = ""; public int amount { get; set; } }
    public class SteamInventoryDescription { public string classid { get; set; } = ""; public string name { get; set; } = ""; public string market_hash_name { get; set; } = ""; public string icon_url { get; set; } = ""; public string type { get; set; } = ""; public string rarity { get; set; } = ""; }
}
