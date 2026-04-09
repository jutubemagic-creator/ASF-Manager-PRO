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
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);
            
            // Передаём данные в JavaScript
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            if (File.Exists(htmlPath))
            {
                string html = File.ReadAllText(htmlPath);
                webView.NavigateToString(html);
            }
            else
            {
                MessageBox.Show("index.html не найден в папке с exe!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                // Здесь будем обрабатывать команды от JS (добавление, сохранение и т.д.)
                // Пока просто показываем
                MessageBox.Show("Получено от JS: " + message);
            }
            catch { }
        }

        private void LoadAccounts()
        {
            if (File.Exists(dataPath))
            {
                string json = File.ReadAllText(dataPath);
                Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(json) ?? new();
            }
        }

        public void SaveAccounts()
        {
            string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataPath, json);
        }
    }

    // Класс аккаунта (расширенный)
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
        public string Status { get; set; } = "Offline";
        public string Balance { get; set; } = "0 ₽";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
