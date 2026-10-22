using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using FinancialMonitoring.Core;
using FinancialMonitoring.Core.Data;
using FinancialMonitoring.Core.Entities;
using FinancialMonitoring.Core.Repositories;
using FinancialMonitoring.Core.Services;

namespace FinancialMonitoring.App
{
    public partial class MainWindow : Window
    {
        private readonly TransactionAnalyticsService _analytics;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        private string? _selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();

            _analytics = new TransactionAnalyticsService();

            string dbPath = Path.Combine(
                AppContext.BaseDirectory,
                "financial_monitoring.db");

            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            TxtStatus.Text = "Оберіть JSON-файл для імпорту";
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await using var context = CreateContext();
                await context.Database.EnsureCreatedAsync();
                await UpdateCountersAsync();
                TxtStatus.Text = "База даних готова до роботи";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Помилка підключення до бази даних";
                MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetActiveAnalyticsButton(Button? activeButton, bool showParameters)
        {
            foreach (var button in new[] { BtnLarge, BtnMerchants, BtnSuspicious, BtnStatistics })
                button.Background = new SolidColorBrush(Color.FromRgb(25, 25, 25));

            if (activeButton != null)
                activeButton.Background = new SolidColorBrush(Color.FromRgb(214, 40, 57));

            ParametersPanel.Visibility = showParameters ? Visibility.Visible : Visibility.Collapsed;
        }

        // Створення контексту бази даних
        private AppDbContext CreateContext()
        {
            return new AppDbContext(_dbOptions);
        }

        // 1. Вибір JSON-файлу
        private void BtnSelectJson_Click(
            object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Виберіть файл банківських транзакцій",
                Filter = "JSON files (*.json)|*.json",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;

                TxtFilePath.Text = _selectedFilePath;
                TxtStatus.Text = "Файл вибрано. Можна імпортувати.";
            }
        }

        // 2. Імпорт JSON до SQLite
        private async void BtnImport_Click(
            object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show(
                    "Спочатку виберіть JSON-файл!",
                    "Увага",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!string.Equals(Path.GetExtension(_selectedFilePath), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new JsonException("Оберіть файл із розширенням .json.");
                await using var stream = File.OpenRead(_selectedFilePath);
                using var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Кореневий елемент JSON має бути масивом.");
                foreach (var client in doc.RootElement.EnumerateArray())
                {
                    if (client.ValueKind != JsonValueKind.Object ||
                        !client.TryGetProperty("clientId", out var id) || id.ValueKind != JsonValueKind.String ||
                        !client.TryGetProperty("accounts", out var accounts) || accounts.ValueKind != JsonValueKind.Array)
                        throw new JsonException("Некоректна структура клієнта.");
                    foreach (var account in accounts.EnumerateArray())
                    {
                        if (account.ValueKind != JsonValueKind.Object ||
                            !account.TryGetProperty("iban", out var iban) || iban.ValueKind != JsonValueKind.String ||
                            !account.TryGetProperty("currency", out var currency) || currency.ValueKind != JsonValueKind.String ||
                            !account.TryGetProperty("transactions", out var transactions) || transactions.ValueKind != JsonValueKind.Array)
                            throw new JsonException("Некоректна структура рахунку.");
                        foreach (var tx in transactions.EnumerateArray())
                        {
                            if (tx.ValueKind != JsonValueKind.Object ||
                                !tx.TryGetProperty("txId", out var txId) || txId.ValueKind != JsonValueKind.String ||
                                !tx.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetDecimal(out _) ||
                                !tx.TryGetProperty("timestamp", out var timestamp) || timestamp.ValueKind != JsonValueKind.String || !timestamp.TryGetDateTime(out _) ||
                                !tx.TryGetProperty("merchant", out var merchant) || merchant.ValueKind != JsonValueKind.String)
                                throw new JsonException("Некоректна структура транзакції.");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                TxtStatus.Text = "Некоректний JSON";
                MessageBox.Show(ex.Message, "Помилка перевірки файлу", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnImport.IsEnabled = false;
            TxtStatus.Text = "Виконується імпорт даних...";

            try
            {
                await using var context = CreateContext();

                IFileLoader loader = new JsonFileLoader();

                var initializer = new DbInitializer(
                    context, loader);


                var mode = CmbImportMode.SelectedIndex == 1
                    ? ImportMode.Replace
                    : ImportMode.Add;

                // Підтвердження повної заміни даних
                if (mode == ImportMode.Replace)
                {
                    var confirmation = MessageBox.Show(
                        "Усі попередні клієнти, рахунки та транзакції " +
                        "будуть видалені й замінені даними нового JSON.\n\n" +
                        "Продовжити?",
                        "Підтвердження заміни",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmation != MessageBoxResult.Yes)
                        return;
                }

                // Використовуй назву об'єкта, яка вже є у твоєму методі
                var importResult = await initializer.InitializeAsync(
                    _selectedFilePath!,
                    mode);

                MessageBox.Show(
                    $"Імпорт завершено!\n\n" +
                    $"Нових клієнтів: {importResult.ClientsAdded}\n" +
                    $"Нових рахунків: {importResult.AccountsAdded}\n" +
                    $"Нових транзакцій: {importResult.TransactionsAdded}\n" +
                    $"Пропущено дублікатів: {importResult.DuplicatesSkipped}",
                    "Результат імпорту",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                TxtStatus.Text = "Імпорт завершено (наявні дані могли бути пропущені).";

                await UpdateCountersAsync();
                await ShowAllTransactionsAsync();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Помилка імпорту";

                MessageBox.Show(
                    ex.Message,
                    "Помилка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BtnImport.IsEnabled = true;
            }
        }

        // Оновлення лічильників
        private async Task UpdateCountersAsync()
        {
            await using var context = CreateContext();

            TxtClients.Text = (await context.Clients.CountAsync())
                .ToString();

            TxtAccounts.Text = (await context.Accounts.CountAsync())
                .ToString();

            TxtTransactions.Text =
                (await context.Transactions.CountAsync())
                .ToString();
        }

        // Отримання всіх транзакцій через репозиторій
        private async Task<List<TransactionEntity>>
            LoadTransactionsAsync()
        {
            await using var context = CreateContext();

            await context.Database.EnsureCreatedAsync();

            ITransactionRepository repository =
                new SqliteTransactionRepository(context);

            var catalog = new TransactionCatalog(repository);

            var clients = await catalog.GetClientsAsync();

            return clients
                .SelectMany(c => c.Accounts)
                .SelectMany(a => a.Transactions)
                .ToList();
        }

        // Виведення всіх транзакцій
        private async Task ShowAllTransactionsAsync()
        {
            var transactions = await LoadTransactionsAsync();

            ResultsGrid.ItemsSource = transactions
                .Select(t => new
                {
                    Операція = t.TxId,
                    Сума = t.Amount.ToString("N2"),
                    Валюта = t.Account.Currency,
                    Отримувач = t.Merchant,
                    Дата = t.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                    Рахунок = t.Account.Iban
                })
                .ToList();

            TxtTableTitle.Text = "Усі транзакції";
            SetActiveAnalyticsButton(null, false);
        }

        // Кнопка "Показати всі транзакції"
        private async void BtnShowAll_Click(
            object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowAllTransactionsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Помилка");
            }
        }

        // Отримання порогу та валюти
        private bool TryGetParameters(
            out decimal threshold, out string currency)
        {
            currency = (CmbCurrency.SelectedItem
                as ComboBoxItem)?.Content?.ToString() ?? "UAH";

            if (!decimal.TryParse(
                TxtThreshold.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out threshold) || threshold <= 0)
            {
                MessageBox.Show(
                    "Введіть коректний невід'ємний поріг!",
                    "Помилка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        // 3. Великі транзакції
        private async void BtnLarge_Click(
            object sender, RoutedEventArgs e)
        {
            SetActiveAnalyticsButton(BtnLarge, true);
            if (!TryGetParameters(
                out decimal threshold, out string currency))
                return;

            try
            {
                var transactions = await LoadTransactionsAsync();

                var result = _analytics.GetLargeTransactions(
                    transactions, threshold, currency);

                ResultsGrid.ItemsSource = result
                    .Select(t => new
                    {
                        Операція = t.TxId,
                        Сума = t.Amount.ToString("N2"),
                        Валюта = t.Account.Currency,
                        Отримувач = t.Merchant,
                        Дата = t.Timestamp.ToString("dd.MM.yyyy HH:mm")
                    })
                    .ToList();

                TxtTableTitle.Text = "Великі транзакції";
                SetActiveAnalyticsButton(BtnLarge, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Помилка");
            }
        }

        // 4. Серійні транзакції
        private async void BtnMerchants_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _analytics.GetRapidTransactions(await LoadTransactionsAsync(), 3, 10);
                ResultsGrid.ItemsSource = result.Select(x => new
                {
                    Рахунок = x.Iban,
                    Валюта = x.Currency,
                    Кількість = x.TransactionCount,
                    Загальна_сума = x.TotalAmount.ToString("N2"),
                    Початок = x.StartTime.ToString("dd.MM.yyyy HH:mm"),
                    Кінець = x.EndTime.ToString("dd.MM.yyyy HH:mm"),
                    Причина = x.Reason
                }).ToList();
                TxtTableTitle.Text = "Серійні транзакції (3 за 10 хвилин)";
                SetActiveAnalyticsButton(BtnMerchants, false);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
        }

        // 5. Можливе дроблення сум
        private async void BtnSuspicious_Click(object sender, RoutedEventArgs e)
        {
            SetActiveAnalyticsButton(BtnSuspicious, true);
            if (!TryGetParameters(out decimal threshold, out string currency)) return;
            try
            {
                var result = _analytics.GetStructuringTransactions(
                    await LoadTransactionsAsync(), threshold, currency);
                ResultsGrid.ItemsSource = result.Select(x => new
                {
                    Рахунок = x.Iban,
                    Валюта = x.Currency,
                    Дата = x.Date.ToString("dd.MM.yyyy"),
                    Кількість = x.TransactionCount,
                    Загальна_сума = x.TotalAmount.ToString("N2"),
                    Причина = x.Reason
                }).ToList();
                TxtTableTitle.Text = "Можливе дроблення сум";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
        }

        // 6. Загальна статистика
        private async void BtnStatistics_Click(
            object sender, RoutedEventArgs e)
        {
            try
            {
                var transactions = await LoadTransactionsAsync();

                var result = _analytics.GetOverallStatistics(
                    transactions);

                ResultsGrid.ItemsSource = result
                    .Select(x => new
                    {
                        Валюта = x.Currency,
                        Кількість = x.TransactionCount,
                        Загальна_сума = x.TotalAmount.ToString("N2"),
                        Середня_сума = x.AverageAmount.ToString("N2"),
                        Мінімум = x.MinAmount.ToString("N2"),
                        Максимум = x.MaxAmount.ToString("N2")
                    })
                    .ToList();

                TxtTableTitle.Text = "Загальна статистика";
                SetActiveAnalyticsButton(BtnStatistics, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Помилка");
            }
        }
        // Оптимальна ширина стовпців і повний текст у підказці.
        private void ResultsGrid_AutoGeneratingColumn(
            object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string name = e.PropertyName ?? string.Empty;
            e.Column.Header = name.Replace('_', ' ');

            // Довгі IBAN і пояснення прокручуються горизонтально,
            // не стискаючи інші стовпці.
            double width = name switch
            {
                "Рахунок" => 310,
                "Причина" => 420,
                "Операція" => 135,
                "Сума" => 145,
                "Загальна_сума" => 165,
                "Середня_сума" => 165,
                "Мінімум" => 145,
                "Максимум" => 145,
                "Кількість" => 120,
                "Валюта" => 100,
                "Дата" => 160,
                "Початок" => 170,
                "Кінець" => 170,
                "Отримувач" => 180,
                _ => 170
            };
            e.Column.Width = new DataGridLength(width);

            if (e.Column is DataGridTextColumn column)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,
                    TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,
                    VerticalAlignment.Center));
                style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,
                    new System.Windows.Data.Binding(name)));
                column.ElementStyle = style;
            }
        }

    }
}
