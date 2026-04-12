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
            this.PreviewKeyDown += Window_PreviewKeyDown;

            string exeFolder = GetRealExeFolder();
            appDataFolder = Path.Combine(exeFolder, "ASF_Data");
            dataPath = Path.Combine(appDataFolder, "accounts.json");

            MessageBox.Show($"Программа запущена\nПуть к файлу:\n{dataPath}", "DEBUG - Start");

            LoadAccounts();
            Accounts.CollectionChanged += (s, e) => SaveAccounts();
            InitializeWebView();
        }

        private string GetRealExeFolder()
        {
            string? exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
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
                    webView.NavigateToString(File.ReadAllText(htmlPath));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView Error: " + ex.Message);
            }
        }

        private async void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                if (msg?.Action == "saveAccounts")
                {
                    MessageBox.Show($"Получен saveAccounts от JS\nДлина данных: {msg.Data?.Length ?? 0}", "DEBUG - Save Received");

                    if (!string.IsNullOrWhiteSpace(msg.Data))
                    {
                        var list = JsonSerializer.Deserialize<List<Account>>(msg.Data, JsonOptions);
                        if (list != null)
                        {
                            Accounts.Clear();
                            foreach (var acc in list) Accounts.Add(acc);
                            MessageBox.Show($"Успешно добавлено {list.Count} аккаунтов в память", "DEBUG - Loaded to Memory");
                        }
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
            catch (Exception ex)
            {
                MessageBox.Show("WebMessage Error: " + ex.Message);
            }
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    string json = File.ReadAllText(dataPath);
                    MessageBox.Show($"Файл найден ({new FileInfo(dataPath).Length} байт)", "DEBUG - Load");

                    var list = JsonSerializer.Deserialize<List<Account>>(json, JsonOptions);
                    if (list != null && list.Count > 0)
                    {
                        Accounts.Clear();
                        foreach (var acc in list) Accounts.Add(acc);
                        MessageBox.Show($"Загружено {list.Count} аккаунтов из файла", "DEBUG - Load Success");
                    }
                    else
                    {
                        MessageBox.Show("Файл существует, но аккаунтов в нём 0", "DEBUG - Load Empty");
                    }
                }
                else
                {
                    MessageBox.Show("Файл accounts.json ещё не создан", "DEBUG - Load");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Load Error: " + ex.Message);
            }
        }

        public void SaveAccounts()
        {
            try
            {
                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                string json = JsonSerializer.Serialize(Accounts, JsonOptions);
                File.WriteAllText(dataPath, json);

                MessageBox.Show($"Сохранено {Accounts.Count} аккаунтов в файл\nПуть: {dataPath}", "DEBUG - Save Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save Error: " + ex.Message);
            }
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

        // ==================== Остальные методы ====================
        private void MassUpdateAccounts(string data) { }
        private async Task GetInventory(string parameters) { }
        private void RunASF(string login) { }
        private void RunASFForAll() { }
        private void DeleteAccount(string accountId) { }
        private void UpdateBalance(string data) { }
        private Account? GetAccountByLogin(string login) { return null; }
        private void UpdateLastLogin(string accountId) { }
        private Account? GetAccountById(string id) { return null; }
    }

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
