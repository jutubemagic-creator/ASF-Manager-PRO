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
using Microsoft.Web.WebView2.Core;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private readonly string dataPath = "accounts.json";
        private readonly string asfPath = "ASF.exe";

        public MainWindow()
        {
            InitializeComponent();
            LoadAccounts();
            Accounts.CollectionChanged += OnAccountsChanged;
            _ = InitializeWebViewAsync();
        }

        private void OnAccountsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SaveAccounts();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Используем папку рядом с EXE, а не LocalAppData
                string webViewDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2Data");
                if (!Directory.Exists(webViewDataPath))
                    Directory.CreateDirectory(webViewDataPath);
                    
                var env = await CoreWebView2Environment.CreateAsync(null, webViewDataPath);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
                if (File.Exists(htmlPath))
                {
                    string html = await File.ReadAllTextAsync(htmlPath);
                    webView.NavigateToString(html);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nУстановите WebView2 Runtime:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703", 
                    "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                switch (msg?.Action)
                {
                    case "saveAccounts":
                        if (msg.Data != null)
                        {
                            var newAccounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data);
                            if (newAccounts != null)
                            {
                                Accounts.Clear();
                                foreach (var acc in newAccounts)
                                    Accounts.Add(acc);
                            }
                            SaveAccounts();
                            SendToJS("accounts", Accounts);
                        }
                        break;

                    case "getAccounts":
                        SendToJS("accounts", Accounts);
                        break;

                    case "getInventory":
                        await GetInventory(msg.Data);
                        break;

                    case "runASF":
                        RunASF(msg.Data);
                        break;

                    case "runASFForAll":
                        RunASFForAll();
                        break;

                    case "updateLastLogin":
                        UpdateLastLogin(msg.Data);
                        break;

                    case "copyToClipboard":
                        Clipboard.SetText(msg.Data);
                        SendToJS("copyResult", new { success = true });
                        break;

                    case "deleteAllAccounts":
                        Accounts.Clear();
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                        break;
                        
                    case "deleteAccount":
                        DeleteAccount(msg.Data);
                        break;
                        
                    case "updateBalance":
                        UpdateBalance(msg.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", ex.Message);
            }
        }

        private async Task GetInventory(string parameters)
        {
            try
            {
                var parts = parameters.Split('|');
                string steamId = parts[0];
                string appId = parts.Length > 1 ? parts[1] : "730";
                
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                string url = $"https://steamcommunity.com/inventory/{steamId}/{appId}/2?l=russian&count=200";
                string response = await client.GetStringAsync(url);
                
                var inventory = JsonSerializer.Deserialize<SteamInventory>(response);
                SendToJS("inventoryData", new { appId, data = inventory });
            }
            catch (Exception ex)
            {
                SendToJS("inventoryError", "Не удалось загрузить инвентарь. Возможно, профиль приватный или неверный AppID.");
            }
        }

        private void RunASF(string login)
        {
            try
            {
                if (File.Exists(asfPath))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = asfPath,
                            Arguments = $"--command --cryptkey \"{login}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    SendToJS("asfStarted", $"ASF запущен для {login}");
                    
                    var account = GetAccountByLogin(login);
                    if (account != null)
                    {
                        account.Status = "Online";
                        account.LastLogin = DateTime.Now.ToString("o");
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                    }
                }
                else
                {
                    SendToJS("asfError", $"ASF.exe не найден. Поместите ASF в папку с программой");
                }
            }
            catch (Exception ex)
            {
                SendToJS("asfError", ex.Message);
            }
        }

        private void RunASFForAll()
        {
            int successCount = 0;
            foreach (var account in Accounts)
            {
                try
                {
                    if (File.Exists(asfPath))
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = asfPath,
                                Arguments = $"--command --cryptkey \"{account.Login}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        account.Status = "Online";
                        account.LastLogin = DateTime.Now.ToString("o");
                        successCount++;
                    }
                }
                catch { }
            }
            SaveAccounts();
            SendToJS("accounts", Accounts);
            SendToJS("asfStarted", $"ASF запущен для {successCount} аккаунтов");
        }
        
        private void DeleteAccount(string accountId)
        {
            var account = GetAccountById(accountId);
            if (account != null)
            {
                Accounts.Remove(account);
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }
        
        private void UpdateBalance(string data)
        {
            var parts = data.Split('|');
            string accountId = parts[0];
            string newBalance = parts[1];
            
            var account = GetAccountById(accountId);
            if (account != null)
            {
                account.Balance = newBalance;
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }

        private Account? GetAccountByLogin(string login)
        {
            foreach (var acc in Accounts)
                if (acc.Login == login) return acc;
            return null;
        }

        private void UpdateLastLogin(string accountId)
        {
            var account = GetAccountById(accountId);
            if (account != null)
            {
                account.LastLogin = DateTime.Now.ToString("o");
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }

        private Account? GetAccountById(string id)
        {
            foreach (var acc in Accounts)
                if (acc.Id == id) return acc;
            return null;
        }

        private void SendToJS(string type, object data)
        {
            if (webView?.CoreWebView2 == null) return;
            
            try
            {
                string json = JsonSerializer.Serialize(new { type, data });
                webView.CoreWebView2.ExecuteScriptAsync($"if(window.receiveFromCSharp) window.receiveFromCSharp({json});");
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
                    var loaded = JsonSerializer.Deserialize<ObservableCollection<Account>>(json);
                    if (loaded != null)
                    {
                        Accounts.Clear();
                        foreach (var acc in loaded)
                            Accounts.Add(acc);
                    }
                }
            }
            catch { }
        }

        private void SaveAccounts()
        {
            try
            {
                string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataPath, json);
            }
            catch { }
        }
    }

    public class Account : INotifyPropertyChanged
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
        
        private string _status = "Offline";
        public string Status 
        { 
            get => _status; 
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }
        
        private string _balance = "0 ₽";
        public string Balance 
        { 
            get => _balance; 
            set { _balance = value; OnPropertyChanged(nameof(Balance)); }
        }
        
        public string SteamId { get; set; } = "";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
        public string LastLogin { get; set; } = "";
        public int CardsRemaining { get; set; } = 0;
        public int GamesCount { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }

    public class SteamInventory
    {
        public bool success { get; set; }
        public SteamInventoryItem[]? assets { get; set; }
        public SteamInventoryDescription[]? descriptions { get; set; }
        public int total_inventory_count { get; set; }
    }

    public class SteamInventoryItem
    {
        public string assetid { get; set; } = "";
        public string classid { get; set; } = "";
        public int amount { get; set; }
    }

    public class SteamInventoryDescription
    {
        public string classid { get; set; } = "";
        public string name { get; set; } = "";
        public string market_hash_name { get; set; } = "";
        public string icon_url { get; set; } = "";
        public string type { get; set; } = "";
        public string rarity { get; set; } = "";
    }
}
