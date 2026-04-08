using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

public partial class MainWindow : Window
{
    public ObservableCollection<Account> Accounts = new();

    private string path = "accounts.json";

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
            Name = NameBox.Text,
            Login = LoginBox.Text,
            Password = PasswordBox.Text,
            Proxy = ProxyBox.Text
        };

        Accounts.Add(acc);
        Save();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Здесь будет запуск ботов");
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Здесь будет антидетект браузер");
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
            Accounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(data);
        }
    }
}

public class Account
{
    public string Name { get; set; }
    public string Login { get; set; }
    public string Password { get; set; }
    public string Proxy { get; set; }
}
