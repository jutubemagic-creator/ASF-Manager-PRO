using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private string dataPath;
        private string appDataFolder;
        private string exeFolder;
        private bool isClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Closing += Window_Closing;
            this.PreviewKeyDown += Window_PreviewKeyDown;
            
            // ========== КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ ==========
            // Получаем реальную папку, где находится EXE файл
            string executablePath = Assembly.GetExecutingAssembly().Location;
            exeFolder = Path.GetDirectoryName(executablePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            
            // Дополнительная проверка: если путь содержит временную папку .NET single-file
            // (обычно содержит ".exe" в пути и "temp" или "Temporary")
            if (exeFolder.Contains(".exe") && (exeFolder.Contains("temp", StringComparison.OrdinalIgnoreCase) || 
                exeFolder.Contains("Temporary", StringComparison.OrdinalIgnoreCase)))
            {
                // Пробуем получить путь из командной строки
                string? mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainModulePath) && File.Exists(mainModulePath))
                {
                    exeFolder = Path.GetDirectoryName(mainModulePath)!;
                }
                else
                {
                    // Используем текущую рабочую директорию
                    exeFolder = Environment.CurrentDirectory;
                }
            }
            
            // Папка для данных - РЯДОМ С EXE (не во временной!)
            appDataFolder = Path.Combine(exeFolder, "ASF_Data");
            
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);
            
            dataPath = Path.Combine(appDataFolder, "accounts.json");
            
            // Отладочный вывод (можно удалить после проверки)
            MessageBox.Show($"EXE папка: {exeFolder}\nДанные: {dataPath}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Создаём резервную копию если файл существует
            CreateBackupIfNeeded();
            
            // Загружаем аккаунты
            LoadAccounts();
            
            // Подписываемся на изменения
            Accounts.CollectionChanged += (s, e) => { if (!isClosing) SaveAccounts(); };
            Accounts.CollectionChanged += OnAccountsCollectionChanged;
            
            InitializeWebView();
        }

        private void CreateBackupIfNeeded()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    string backupPath = Path.Combine(appDataFolder, $"accounts_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Copy(dataPath, backupPath, overwrite: true);
                    
                    // Удаляем старые бэкапы (старше 7 дней)
                    var backupFiles = Directory.GetFiles(appDataFolder, "accounts_backup_*.json");
                    foreach (var file in backupFiles)
                    {
                        if (File.GetCreationTime(file) < DateTime.Now.AddDays(-7))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private void OnAccountsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (isClosing) return;
            
            if (e.NewItems != null)
            {
                foreach (Account item in e.NewItems)
                {
                    item.PropertyChanged += Account_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (Account item in e.OldItems)
                {
                    item.PropertyChanged -= Account_PropertyChanged;
                }
            }
        }

        private void Account_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!isClosing)
            {
                SaveAccounts();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendToJS("hotkey", "new");
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendToJS("hotkey", "save");
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendToJS("hotkey", "search");
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                SendToJS("hotkey", "delete");
                e.Handled = true;
            }
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
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                // Поиск index.html в нескольких местах
                string htmlContent = FindIndexHtml();
                
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    await webView.NavigateToStringAsync(htmlContent);
                }
                else
                {
                    string fallbackHtml = GetFallbackHtml();
                    await webView.NavigateToStringAsync(fallbackHtml);
                    MessageBox.Show("index.html не найден, используется встроенная версия", "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка WebView2: {ex.Message}\n\nУстановите WebView2 Runtime:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703", 
                    "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FindIndexHtml()
        {
            string[] possiblePaths = {
                Path.Combine(exeFolder, "index.html"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html"),
                Path.Combine(Environment.CurrentDirectory, "index.html"),
                Path.Combine(appDataFolder, "index.html"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "index.html")
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllText(path);
                    }
                    catch { }
                }
            }
            
            return "";
        }

        private string GetFallbackHtml()
        {
            return @"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>ASF Manager PRO</title><style>body{background:#0a0a0f;color:white;font-family:sans-serif;text-align:center;padding:50px;}</style></head><body><h1>ASF Manager PRO v3.3</h1><p>Не удалось загрузить index.html</p><p>Пожалуйста, убедитесь, что файл index.html находится в папке с программой.</p></body></html>";
        }

        private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                    
                    case "massUpdate":
                        MassUpdateAccounts(msg.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", ex.Message);
            }
        }

        private void MassUpdateAccounts(string data)
        {
            try
            {
                var updateData = JsonSerializer.Deserialize<MassUpdateData>(data);
                if (updateData?.AccountIds != null && updateData.Fields != null)
                {
                    foreach (var accountId in updateData.AccountIds)
                    {
                        var account = GetAccountById(accountId);
                        if (account != null)
                        {
                            foreach (var field in updateData.Fields)
                            {
                                switch (field.Key)
                                {
                                    case "Proxy":
                                        account.Proxy = field.Value;
                                        break;
                                    case "Notes":
                                        account.Notes = field.Value;
                                        break;
                                    case "Status":
                                        account.Status = field.Value;
                                        break;
                                }
                            }
                        }
                    }
                    SaveAccounts();
                    SendToJS("accounts", Accounts);
                    SendToJS("massUpdateComplete", new { count = updateData.AccountIds.Length });
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
            catch
            {
                SendToJS("inventoryError", "Не удалось загрузить инвентарь. Возможно, профиль приватный или неверный AppID.");
            }
        }

        private void RunASF(string login)
        {
            try
            {
                string asfPath = Path.Combine(exeFolder, "ASF.exe");
                
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
                    SendToJS("asfError", $"ASF.exe не найден. Поместите ASF в папку: {exeFolder}");
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
            string asfPath = Path.Combine(exeFolder, "ASF.exe");
            
            if (!File.Exists(asfPath))
            {
                SendToJS("asfError", $"ASF.exe не найден в папке: {exeFolder}");
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
                webView.CoreWebView2.ExecuteScriptAsync($"window.receiveFromCSharp({json});");
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
                        {
                            acc.PropertyChanged += Account_PropertyChanged;
                            Accounts.Add(acc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}\nПуть: {dataPath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void SaveAccounts()
        {
            try
            {
                // Создаём бэкап перед сохранением
                if (File.Exists(dataPath))
                {
                    string backupPath = Path.Combine(appDataFolder, $"accounts_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Copy(dataPath, backupPath, overwrite: true);
                }
                
                string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataPath, json);
                
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] Сохранено {Accounts.Count} аккаунтов в {dataPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}\nПуть: {dataPath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            isClosing = true;
            SaveAccounts();
            Thread.Sleep(100); // Даём время на запись
        }
    }

    public class Account : INotifyPropertyChanged
    {
        private string _id = "";
        private string _login = "";
        private string _password = "";
        private string _email = "";
        private string _emailPass = "";
        private string _proxy = "";
        private string _pin = "";
        private string _maFile = "";
        private string _notes = "";
        private string _status = "Offline";
        private string _balance = "0 ₽";
        private string _steamId = "";
        private string _createdAt = "";
        private string _lastLogin = "";
        private int _cardsRemaining = 0;
        private int _gamesCount = 0;

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Login { get => _login; set { _login = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        public string EmailPass { get => _emailPass; set { _emailPass = value; OnPropertyChanged(); } }
        public string Proxy { get => _proxy; set { _proxy = value; OnPropertyChanged(); } }
        public string Pin { get => _pin; set { _pin = value; OnPropertyChanged(); } }
        public string MaFile { get => _maFile; set { _maFile = value; OnPropertyChanged(); } }
        public string Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public string Balance { get => _balance; set { _balance = value; OnPropertyChanged(); } }
        public string SteamId { get => _steamId; set { _steamId = value; OnPropertyChanged(); } }
        public string CreatedAt { get => string.IsNullOrEmpty(_createdAt) ? DateTime.Now.ToString("o") : _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
        public string LastLogin { get => _lastLogin; set { _lastLogin = value; OnPropertyChanged(); } }
        public int CardsRemaining { get => _cardsRemaining; set { _cardsRemaining = value; OnPropertyChanged(); } }
        public int GamesCount { get => _gamesCount; set { _gamesCount = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
