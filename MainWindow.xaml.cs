using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private string dataPath = "";
        private string appDataFolder = "";

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

            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            appDataFolder = Path.Combine(exeFolder, "ASF_Data");
            dataPath = Path.Combine(appDataFolder, "accounts.json");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            LoadAccounts();
            InitializeWebView();
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
                if (!Directory.Exists(webViewDataPath))
                    Directory.CreateDirectory(webViewDataPath);

                var env = await CoreWebView2Environment.CreateAsync(null, webViewDataPath);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string htmlPath = Path.Combine(exeFolder, "index.html");
                
                if (File.Exists(htmlPath))
                {
                    string html = File.ReadAllText(htmlPath);
                    webView.NavigateToString(html);
                }
                else
                {
                    webView.NavigateToString("<html><body style='background:#0a0a0f;color:white;padding:20px'><h1>index.html not found</h1><p>Path: " + htmlPath + "</p></body></html>");
                }
                
                webView.CoreWebView2.DOMContentLoaded += (sender, e) =>
                {
                    SendToJS("accounts", Accounts);
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json, JsonOptions);
                
                if (msg?.Action == "saveAccounts" && !string.IsNullOrWhiteSpace(msg.Data))
                {
                    var list = JsonSerializer.Deserialize<List<Account>>(msg.Data, JsonOptions);
                    if (list != null)
                    {
                        Accounts.Clear();
                        foreach (var acc in list)
                            Accounts.Add(acc);
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                    }
                }
                else if (msg?.Action == "getAccounts")
                {
                    SendToJS("accounts", Accounts);
                }
                else if (msg?.Action == "getInventory")
                {
                    _ = GetInventory(msg.Data);
                }
                else if (msg?.Action == "runASF")
                {
                    RunASF(msg.Data);
                }
                else if (msg?.Action == "runASFForAll")
                {
                    RunASFForAll();
                }
                else if (msg?.Action == "copyToClipboard")
                {
                    Clipboard.SetText(msg.Data ?? "");
                }
                else if (msg?.Action == "deleteAllAccounts")
                {
                    Accounts.Clear();
                    SaveAccounts();
                    SendToJS("accounts", Accounts);
                }
                else if (msg?.Action == "deleteAccount")
                {
                    DeleteAccount(msg.Data ?? "");
                }
                else if (msg?.Action == "massUpdate")
                {
                    MassUpdateAccounts(msg.Data ?? "");
                }
                else if (msg?.Action == "updateBalance")
                {
                    UpdateBalance(msg.Data ?? "");
                }
                else if (msg?.Action == "updateLastLogin")
                {
                    UpdateLastLogin(msg.Data ?? "");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebMessage Error: {ex.Message}");
            }
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
                        foreach (var acc in list)
                            Accounts.Add(acc);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load Error: {ex.Message}");
            }
        }

        public void SaveAccounts()
        {
            try
            {
                string json = JsonSerializer.Serialize(Accounts, JsonOptions);
                File.WriteAllText(dataPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save Error: {ex.Message}");
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            SaveAccounts();
        }

        private void SendToJS(string type, object data)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new { type, data }, JsonOptions);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendToJS Error: {ex.Message}");
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
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                string url = $"https://steamcommunity.com/inventory/{steamId}/{appId}/2?l=russian&count=200";
                string response = await client.GetStringAsync(url);
                var inventory = JsonSerializer.Deserialize<SteamInventory>(response);
                SendToJS("inventoryData", new { appId, data = inventory });
            }
            catch
            {
                SendToJS("inventoryError", "Не удалось загрузить инвентарь.");
            }
        }

        private void RunASF(string login)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string asfPath = Path.Combine(exeFolder, "ASF.exe");
                
                if (!File.Exists(asfPath))
                {
                    SendToJS("asfError", "ASF.exe не найден");
                    return;
                }

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
            catch { }
        }

        private void RunASFForAll()
        {
            int successCount = 0;
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string asfPath = Path.Combine(exeFolder, "ASF.exe");
            
            if (!File.Exists(asfPath))
            {
                SendToJS("asfError", "ASF.exe не найден");
                return;
            }

            foreach (var account in Accounts)
            {
                try
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

        private void MassUpdateAccounts(string data)
        {
            try
            {
                var updateData = JsonSerializer.Deserialize<MassUpdateData>(data, JsonOptions);
                if (updateData == null) return;

                foreach (var accountId in updateData.AccountIds)
                {
                    var account = GetAccountById(accountId);
                    if (account != null)
                    {
                        foreach (var field in updateData.Fields)
                        {
                            switch (field.Key)
                            {
                                case "Proxy": account.Proxy = field.Value; break;
                                case "Notes": account.Notes = field.Value; break;
                                case "Status": account.Status = field.Value; break;
                            }
                        }
                    }
                }
                SaveAccounts();
                SendToJS("accounts", Accounts);
                SendToJS("massUpdateComplete", new { count = updateData.AccountIds.Length });
            }
            catch { }
        }

        private void UpdateBalance(string data)
        {
            var parts = data.Split('|');
            if (parts.Length < 2) return;
            var account = GetAccountById(parts[0]);
            if (account != null)
            {
                account.Balance = parts[1];
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
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

        private Account? GetAccountByLogin(string login)
        {
            foreach (var acc in Accounts) if (acc.Login == login) return acc;
            return null;
        }

        private Account? GetAccountById(string id)
        {
            foreach (var acc in Accounts) if (acc.Id == id) return acc;
            return null;
        }
    }

    // Модели данных
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

    public class WebMessage 
    { 
        public string Action { get; set; } = ""; 
        public string Data { get; set; } = ""; 
    }
    
    public class MassUpdateData 
    { 
        public string[] AccountIds { get; set; } = Array.Empty<string>(); 
        public Dictionary<string, string> Fields { get; set; } = new(); 
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
