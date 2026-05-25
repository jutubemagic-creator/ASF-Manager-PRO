using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        
        private const byte VK_RETURN = 0x0D;
        private const byte VK_TAB = 0x09;
        
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private string dataPath = "";
        private string appDataFolder = "";
        private string configPath = "";
        private bool webViewReady = false;

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
            configPath = Path.Combine(appDataFolder, "config.json");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            LoadAccounts();
            LoadConfig();
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
                    await webView.EnsureCoreWebView2Async(env);
                    webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    webView.CoreWebView2.NavigateToString("<html><body style='background:#0a0a0f;color:white;padding:20px'><h1>index.html not found</h1><p>Path: " + htmlPath + "</p></body></html>");
                }
                
                webView.CoreWebView2.NavigationCompleted += (sender, e) =>
                {
                    webViewReady = true;
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
                else if (msg?.Action == "runSteam")
                {
                    var parts = msg.Data.Split('|');
                    if (parts.Length >= 2)
                    {
                        try
                        {
                            _ = RunSteamWithAccount(parts[0], parts[1]);
                        }
                        catch (Exception ex)
                        {
                            SendToJS("steamError", ex.Message);
                        }
                    }
                }
                else if (msg?.Action == "setSteamPath")
                {
                    SaveSteamPath(msg.Data);
                }
                else if (msg?.Action == "getSteamPath")
                {
                    string steamPath = GetSteamPath();
                    SendToJS("steamPath", steamPath);
                }
                else if (msg?.Action == "generateHWID")
                {
                    string hwid = GenerateHWID();
                    SendToJS("hwidGenerated", hwid);
                }
                else if (msg?.Action == "openSteamProfileFolder")
                {
                    OpenSteamProfileFolder(msg.Data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebMessage Error: {ex.Message}");
                SendToJS("error", ex.Message);
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
                        
                        Debug.WriteLine($"Загружено {Accounts.Count} аккаунтов");
                    }
                }
                else
                {
                    Accounts.Add(new Account { Login = "test_account", Password = "test123", Balance = "100 ₽" });
                    SaveAccounts();
                    Debug.WriteLine("Создан тестовый аккаунт");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load Error: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.SteamPath))
                            Properties.Settings.Default.SteamPath = config.SteamPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadConfig Error: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    SteamPath = Properties.Settings.Default.SteamPath
                };
                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveConfig Error: {ex.Message}");
            }
        }

        public void SaveAccounts()
        {
            try
            {
                string json = JsonSerializer.Serialize(Accounts, JsonOptions);
                File.WriteAllText(dataPath, json);
                Debug.WriteLine($"Сохранено {Accounts.Count} аккаунтов в {dataPath}");
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
            if (webView?.CoreWebView2 == null)
            {
                Debug.WriteLine("WebView не инициализирован");
                return;
            }
            
            try
            {
                string json = JsonSerializer.Serialize(new { type, data }, JsonOptions);
                string script = $"window.receiveFromCSharp({json});";
                webView.CoreWebView2.ExecuteScriptAsync(script);
                Debug.WriteLine($"Отправлено в JS: {type}");
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
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                string url = $"https://steamcommunity.com/inventory/{steamId}/{appId}/2?l=russian&count=200";
                string response = await client.GetStringAsync(url);
                var inventory = JsonSerializer.Deserialize<SteamInventory>(response);
                SendToJS("inventoryData", new { appId, data = inventory });
            }
            catch (Exception ex)
            {
                SendToJS("inventoryError", $"Не удалось загрузить инвентарь: {ex.Message}");
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
                    SendToJS("asfError", "ASF.exe не найден. Поместите ASF.exe в папку с программой");
                    return;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = asfPath,
                        Arguments = $"--command login {login}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();

                var account = GetAccountByLogin(login);
                if (account != null)
                {
                    account.Status = "ASF Online";
                    account.LastLogin = DateTime.Now.ToString("o");
                    SaveAccounts();
                    SendToJS("accounts", Accounts);
                }
                
                SendToJS("asfStarted", $"ASF запущен для {login}");
            }
            catch (Exception ex)
            {
                SendToJS("asfError", $"Ошибка запуска ASF: {ex.Message}");
            }
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
                            Arguments = $"--command login {account.Login}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    account.Status = "ASF Online";
                    account.LastLogin = DateTime.Now.ToString("o");
                    successCount++;
                }
                catch { }
            }
            SaveAccounts();
            SendToJS("accounts", Accounts);
            SendToJS("asfStarted", $"ASF запущен для {successCount} аккаунтов");
        }

        private void SimulateTyping(string text)
        {
            foreach (char c in text)
            {
                INPUT[] inputs = new INPUT[2];
                
                inputs[0] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE
                        }
                    }
                };
                
                inputs[1] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                        }
                    }
                };
                
                SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                System.Threading.Thread.Sleep(30);
            }
        }

        private void SimulateKeyPress(byte keyCode)
        {
            INPUT[] inputs = new INPUT[2];
            
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = KEYEVENTF_KEYDOWN
                    }
                }
            };
            
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = KEYEVENTF_KEYUP
                    }
                }
            };
            
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private async Task RunSteamWithAccount(string login, string password)
        {
            try
            {
                string steamPath = GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    SendToJS("steamError", "Steam не найден. Укажите путь к Steam.exe в настройках");
                    return;
                }

                var account = GetAccountByLogin(login);
                if (account == null)
                {
                    SendToJS("steamError", "Аккаунт не найден");
                    return;
                }

                // Формируем аргументы запуска Steam
                string args = "";
                
                if (account.UseIsolation)
                {
                    string userDataPath = Path.Combine(appDataFolder, "SteamProfiles", login);
                    Directory.CreateDirectory(userDataPath);
                    args += $" -userdata {userDataPath}";
                }
                
                switch (account.LaunchMode)
                {
                    case "bigpicture":
                        args += " -bigpicture";
                        break;
                    case "vr":
                        args += " -vr";
                        break;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = steamPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(steamPath) ?? ""
                };

                var process = Process.Start(startInfo);
                
                if (process != null)
                {
                    SendToJS("steamStarted", $"Steam запущен для {login}, ожидание окна входа...");
                    
                    await Task.Delay(5000);
                    
                    IntPtr steamWindow = FindWindow(null, "Steam - Вход");
                    if (steamWindow == IntPtr.Zero)
                    {
                        steamWindow = FindWindow(null, "Вход в Steam");
                    }
                    
                    if (steamWindow != IntPtr.Zero)
                    {
                        SetForegroundWindow(steamWindow);
                        await Task.Delay(800);
                        
                        SimulateTyping(login);
                        await Task.Delay(500);
                        
                        SimulateKeyPress(VK_TAB);
                        await Task.Delay(300);
                        
                        SimulateTyping(password);
                        await Task.Delay(500);
                        
                        SimulateKeyPress(VK_RETURN);
                        
                        account.Status = "Steam Online";
                        account.LastLogin = DateTime.Now.ToString("o");
                        account.LastSteamLaunch = DateTime.Now.ToString("o");
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                        
                        SendToJS("steamStarted", $"Автоматический вход выполнен для {login}");
                    }
                    else
                    {
                        SendToJS("steamError", "Не удалось найти окно входа в Steam");
                        
                        account.Status = "Steam Online";
                        account.LastLogin = DateTime.Now.ToString("o");
                        account.LastSteamLaunch = DateTime.Now.ToString("o");
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                    }
                }
                else
                {
                    SendToJS("steamError", "Не удалось запустить Steam");
                }
            }
            catch (Exception ex)
            {
                SendToJS("steamError", $"Ошибка: {ex.Message}");
            }
        }

        private string GetSteamPath()
        {
            if (Properties.Settings.Default.SteamPath != null && 
                File.Exists(Properties.Settings.Default.SteamPath))
            {
                return Properties.Settings.Default.SteamPath;
            }

            string[] commonPaths = {
                @"C:\Program Files (x86)\Steam\Steam.exe",
                @"C:\Program Files\Steam\Steam.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "Steam.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "Steam.exe"),
                @"D:\Steam\Steam.exe",
                @"E:\Steam\Steam.exe"
            };
            
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    Properties.Settings.Default.SteamPath = path;
                    Properties.Settings.Default.Save();
                    SaveConfig();
                    return path;
                }
            }
            
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string installPath = key.GetValue("InstallPath")?.ToString();
                        if (!string.IsNullOrEmpty(installPath))
                        {
                            string exePath = Path.Combine(installPath, "Steam.exe");
                            if (File.Exists(exePath))
                            {
                                Properties.Settings.Default.SteamPath = exePath;
                                Properties.Settings.Default.Save();
                                SaveConfig();
                                return exePath;
                            }
                        }
                    }
                }
            }
            catch { }
            
            return "";
        }

        private void SaveSteamPath(string path)
        {
            if (File.Exists(path))
            {
                Properties.Settings.Default.SteamPath = path;
                Properties.Settings.Default.Save();
                SaveConfig();
                SendToJS("steamPathSaved", path);
            }
            else
            {
                SendToJS("steamError", "Указанный файл Steam.exe не найден");
            }
        }

        private string GenerateHWID()
        {
            string timestamp = DateTime.Now.Ticks.ToString();
            string random = Guid.NewGuid().ToString().Substring(0, 8);
            string hwid = $"HWID-{timestamp.Substring(timestamp.Length - 8)}-{random}".ToUpper();
            return hwid;
        }

        private void OpenSteamProfileFolder(string login)
        {
            try
            {
                string profilePath = Path.Combine(appDataFolder, "SteamProfiles", login);
                if (Directory.Exists(profilePath))
                {
                    Process.Start("explorer.exe", profilePath);
                }
                else
                {
                    Directory.CreateDirectory(profilePath);
                    Process.Start("explorer.exe", profilePath);
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", $"Ошибка открытия папки: {ex.Message}");
            }
        }

        private void DeleteAccount(string accountId)
        {
            var account = GetAccountById(accountId);
            if (account != null)
            {
                string profilePath = Path.Combine(appDataFolder, "SteamProfiles", account.Login);
                if (Directory.Exists(profilePath))
                {
                    try
                    {
                        Directory.Delete(profilePath, true);
                    }
                    catch { }
                }
                
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
        public string LastSteamLaunch { get; set; } = "";
        public int CardsRemaining { get; set; } = 0;
        public int GamesCount { get; set; } = 0;
        
        public string SteamConfigPath { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public bool UseIsolation { get; set; } = false;
        public string LaunchMode { get; set; } = "normal";
        public string LastIP { get; set; } = "";
        public string LastCountry { get; set; } = "";
        public string SteamLevel { get; set; } = "0";
        public int TotalGames { get; set; } = 0;
        public int HoursPlayed { get; set; } = 0;
        public bool TwoFactorEnabled { get; set; } = false;
        public string TradeLink { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

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

    public class AppConfig
    {
        public string SteamPath { get; set; } = "";
    }
}

namespace ASFManagerPRO.Properties
{
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default => defaultInstance;
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string SteamPath
        {
            get { return ((string)(this["SteamPath"])); }
            set { this["SteamPath"] = value; }
        }
    }
}
