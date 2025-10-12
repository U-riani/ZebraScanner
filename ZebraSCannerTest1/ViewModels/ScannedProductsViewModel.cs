using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1.ViewModels;

public partial class ScannedProductsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly ClipboardService _clipboard;
    private CancellationTokenSource? _loadCts;

    private string _currentSortField = "UpdatedAt";
    private bool _currentSortDescending = true;
    private string _currentFilter = "ScannedQuantity > 0";

    // Pagination
    private int _offset = 0;
    private const int PageSize = 50;
    private bool _hasMoreRows = false;

    [ObservableProperty] private string currentSortDescription = "Sort: Updated ↓";
    [ObservableProperty] private string currentFilterDescription = "Filter: Scanned";
    [ObservableProperty] private int rowCount;
    [ObservableProperty] private bool isInitialLoading;
    [ObservableProperty] private bool isLoadingMore;
    [ObservableProperty] private bool hasMoreRows;
    [ObservableProperty] private int totalRowCount;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool needsReload = true;

    public ObservableCollection<StatsProduct> ScannedProductsStats { get; private set; } = new();

    public ScannedProductsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
    }

    private async Task LoadProductsAsync(bool reset)
    {
        // Cancel any previous load
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        if (reset)
        {
            _offset = 0;
            ScannedProductsStats.Clear();
            TotalRowCount = 0;
            IsInitialLoading = true;
            IsLoadingMore = false;
        }
        else
        {
            IsLoadingMore = true;
        }

        try
        {
            var where = string.IsNullOrWhiteSpace(_currentFilter) ? "" : $"WHERE {_currentFilter}";
            var dir = _currentSortDescending ? "DESC" : "ASC";

            // Run DB operations fully off the UI thread
            var (temp, total) = await Task.Run(() =>
            {
                var list = new List<StatsProduct>();

                using var cmd = _conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM Products
                    {where}
                    ORDER BY {_currentSortField} {dir}
                    LIMIT {PageSize} OFFSET {_offset};";

                using var r = cmd.ExecuteReader();
                int batchCounter = 0;

                while (r.Read())
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException();

                    list.Add(new StatsProduct
                    {
                        Barcode = r.GetString(0),
                        InitialQuantity = r.GetInt32(1),
                        ScannedQuantity = r.GetInt32(2),
                        CreatedAt = DateTime.Parse(r.GetString(3)),
                        UpdatedAt = DateTime.Parse(r.GetString(4)),
                        Name = r.IsDBNull(5) ? "" : r.GetString(5),
                        Color = r.IsDBNull(6) ? "" : r.GetString(6),
                        Size = r.IsDBNull(7) ? "" : r.GetString(7),
                        Price = r.IsDBNull(8) ? "" : r.GetString(8),
                        ArticCode = r.IsDBNull(9) ? "" : r.GetString(9)
                    });

                    // Check for cancellation every 20 rows
                    if (++batchCounter % 20 == 0 && token.IsCancellationRequested)
                        throw new OperationCanceledException();
                }

                int totalCount;
                using (var countCmd = _conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM Products {where}";
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                return (list, totalCount);
            }, token);

            if (token.IsCancellationRequested) return;

            // Update UI on MainThread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var p in temp)
                    ScannedProductsStats.Add(p);

                _offset += temp.Count;
                RowCount = ScannedProductsStats.Count;
                TotalRowCount = total;
                HasMoreRows = _offset < total;
            });
        }
        catch (OperationCanceledException)
        {
            // silently ignore user cancellation
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
        }
        finally
        {
            IsInitialLoading = false;
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsInitialLoading || IsLoadingMore || !_hasMoreRows)
            return;
        await LoadProductsAsync(reset: false);
    }

    [RelayCommand]
    private async Task Sort()
    {
        string[] fields = new[]
        {
            "Barcode", "ScannedQuantity", "InitialQuantity", "Difference",
            "UpdatedAt", "ArticCode", "Name", "Color", "Size", "Price", "CreatedAt"
        };

        string fieldChoice = await Shell.Current.DisplayActionSheet("Sort by:", "Cancel", null, fields);
        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel") return;

        string orderChoice = await Shell.Current.DisplayActionSheet("Order:", "Cancel", null, "Ascending", "Descending");
        if (string.IsNullOrEmpty(orderChoice) || orderChoice == "Cancel") return;

        _currentSortField = fieldChoice switch
        {
            "Difference" => "(ScannedQuantity - InitialQuantity)",
            _ => fieldChoice
        };

        _currentSortDescending = orderChoice == "Descending";
        CurrentSortDescription = $"Sort: {fieldChoice} {(_currentSortDescending ? "↓" : "↑")}";

        await LoadProductsAsync(reset: true);
    }

    [RelayCommand]
    private async Task Filter()
    {
        var fieldChoice = await Shell.Current.DisplayActionSheet(
            "Choose filter", "Cancel", null,
            "All Products", "Scanned > 0", "Unscanned (Scanned = 0)",
            "Shortage (Scanned < Initial)", "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)", "Equal And Scanned (Scanned = Initial And Scanned > 0)",
            "Zero Initial (Initial == 0)", "Manual Changed", "Automatic Only"
        );

        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel") return;

        switch (fieldChoice)
        {
            case "All Products": _currentFilter = ""; break;
            case "Scanned > 0": _currentFilter = "ScannedQuantity > 0"; break;
            case "Unscanned (Scanned = 0)": _currentFilter = "ScannedQuantity = 0"; break;
            case "Shortage (Scanned < Initial)": _currentFilter = "ScannedQuantity < InitialQuantity"; break;
            case "Overstock (Scanned > Initial)": _currentFilter = "ScannedQuantity > InitialQuantity"; break;
            case "Equal (Scanned = Initial)": _currentFilter = "ScannedQuantity = InitialQuantity"; break;
            case "Equal And Scanned (Scanned = Initial And Scanned > 0)":
                _currentFilter = "ScannedQuantity = InitialQuantity AND ScannedQuantity > 0"; break;
            case "Zero Initial (Initial == 0)": _currentFilter = "InitialQuantity = 0"; break;
            case "Manual Changed":
                _currentFilter = @"EXISTS (SELECT 1 FROM ScanLogs sl WHERE sl.Barcode = Products.Barcode AND sl.IsManual = 1)";
                break;
            case "Automatic Only":
                _currentFilter = @"ScannedQuantity > 0 AND NOT EXISTS (SELECT 1 FROM ScanLogs sl WHERE sl.Barcode = Products.Barcode AND sl.IsManual = 1)";
                break;
        }

        CurrentFilterDescription = $"Filter: {fieldChoice}";
        await LoadProductsAsync(reset: true);
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        _currentFilter = "ScannedQuantity > 0";
        _currentSortField = "UpdatedAt";
        _currentSortDescending = true;
        CurrentFilterDescription = "Filter: Scanned";
        CurrentSortDescription = "Sort: Updated ↓";

        await LoadProductsAsync(reset: true);
    }

    [RelayCommand]
    private async Task CopyBarcode(string barcode) => await _clipboard.CopyAsync(barcode);

    public async void ApplyManualFilter(string filter)
    {
        _currentFilter = filter;
        CurrentFilterDescription = $"Filter: Manual ({filter})";
        await LoadProductsAsync(reset: true);
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(StatsProduct product)
    {
        if (product == null) return;

        var query = new Dictionary<string, object>
        {
            ["Barcode"] = product.Barcode,
            ["Quantity"] = product.ScannedQuantity,
            ["InitialQuantity"] = product.InitialQuantity,
            ["Name"] = product.Name ?? "",
            ["Color"] = product.Color ?? "",
            ["Size"] = product.Size ?? "",
            ["Price"] = decimal.TryParse(product.Price, out var p) ? p : 0,
            ["ArticCode"] = product.ArticCode ?? "",
            ["IsReadOnly"] = true
        };

        await Shell.Current.GoToAsync(nameof(DetailsPage), query);
    }

    public async Task LoadAsync(bool reset = true, CancellationToken token = default)
    {
        // already loading? skip
        if (IsLoading || IsInitialLoading)
            return;

        try
        {
            await Task.Yield(); // yield control so UI shows
            await LoadProductsAsync(reset);
        }
        catch (OperationCanceledException)
        {
            // ignore cancellation
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
        }
        finally
        {
            IsInitialLoading = false;
            IsLoadingMore = false;
        }
    }
}
