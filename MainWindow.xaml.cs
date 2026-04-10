using System;
using System.Collections.ObjectModel;
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
        private readonly string asfPath = "ASF.exe"; // Путь к ASF

        public MainWindow()
        {
            InitializeComponent();
            LoadAccounts();
            _ = InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string webViewDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASF_Manager_PRO_WebView");
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
                            Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data) ?? new();
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

                    case "stopASF":
                        StopASF(msg.Data);
                        break;

                    case "updateLastLogin":
                        UpdateLastLogin(msg.Data);
                        break;

                    case "copyToClipboard":
                        Clipboard.SetText(msg.Data);
                        SendToJS("copyResult", new { success = true });
                        break;
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", ex.Message);
            }
        }

        private async Task GetInventory(string steamId)
        {
            try
            {
                // Используем Steam API для получения инвентаря (пример для CS:GO/CS2)
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                string url = $"https://steamcommunity.com/inventory/{steamId}/730/2?l=russian&count=100";
                string response = await client.GetStringAsync(url);
                
                var inventory = JsonSerializer.Deserialize<SteamInventory>(response);
                SendToJS("inventoryData", inventory);
            }
            catch
            {
                SendToJS("inventoryError", "Не удалось загрузить инвентарь. Возможно, профиль приватный.");
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

        private void StopASF(string login)
        {
            foreach (var process in Process.GetProcessesByName("ASF"))
            {
                try { process.Kill(); } catch { }
            }
            SendToJS("asfStopped", $"ASF остановлен для {login}");
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

        private Account GetAccountById(string id)
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
                    Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(json) ?? new();
                }
            }
            catch { Accounts = new(); }
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
        public string SteamId { get; set; } = "";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
        public string LastLogin { get; set; } = "";
        public int CardsRemaining { get; set; } = 0;
        public int GamesCount { get; set; } = 0;
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }

    public class SteamInventory
    {
        public bool success { get; set; }
        public SteamInventoryItem[] assets { get; set; }
        public SteamInventoryDescription[] descriptions { get; set; }
    }

    public class SteamInventoryItem
    {
        public string assetid { get; set; }
        public string classid { get; set; }
        public int amount { get; set; }
    }

    public class SteamInventoryDescription
    {
        public string classid { get; set; }
        public string name { get; set; }
        public string market_hash_name { get; set; }
        public string icon_url { get; set; }
        public string type { get; set; }
        public string rarity { get; set; }
    }
}
