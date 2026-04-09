using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

public partial class MainWindow : Window
{
    public ObservableCollection<Account> Accounts = new();
    private string path = "accounts.json";

    private Account selected;

    public MainWindow()
    {
        InitializeComponent();
        Load();
        AccountsList.ItemsSource = Accounts;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var acc = new Account
        {
            Login = "new_login",
            CreatedAt = DateTime.Now.ToString(),
            UpdatedAt = DateTime.Now.ToString(),
            Status = "Оффлайн"
        };

        Accounts.Add(acc);
        Save();
    }

    private void SelectAccount(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        selected = AccountsList.SelectedItem as Account;

        if (selected == null) return;

        LoginBox.Text = selected.Login;
        PasswordBox.Text = selected.Password;
        EmailBox.Text = selected.Email;
        EmailPassBox.Text = selected.EmailPass;
        SteamGuardBox.Text = selected.SteamGuard;
        MaFileBox.Text = selected.MaFile;

        ProxyBox.Text = selected.Proxy;
        StatusBox.Text = selected.Status;
        BalanceBox.Text = selected.Balance;

        CreatedBox.Text = selected.CreatedAt;
        UpdatedBox.Text = selected.UpdatedAt;
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Запуск аккаунта");
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Антидетект браузер (будет дальше)");
    }

    private void Save()
    {
        File.WriteAllText(path, JsonSerializer.Serialize(Accounts));
    }

    private void Load()
    {
        if (File.Exists(path))
        {
            var data = File.ReadAllText(path);
            Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(data) ?? new();
        }
    }
}

public class Account
{
    public string Login { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public string EmailPass { get; set; }
    public string SteamGuard { get; set; }
    public string MaFile { get; set; }

    public string Proxy { get; set; }
    public string Status { get; set; }
    public string Balance { get; set; }

    public string CreatedAt { get; set; }
    public string UpdatedAt { get; set; }
}
