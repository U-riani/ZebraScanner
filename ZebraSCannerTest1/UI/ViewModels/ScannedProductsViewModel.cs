using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;


[QueryProperty(nameof(CurrentBoxId), "BoxId")]
[QueryProperty(nameof(CurrentMode), "Mode")]
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

    [ObservableProperty] private string currentSortDescription = "Sort: Updated ↓";
    [ObservableProperty] private string currentFilterDescription = "Filter: Scanned";
    [ObservableProperty] private int rowCount;
    [ObservableProperty] private bool isInitialLoading;
    [ObservableProperty] private bool isLoadingMore;
    [ObservableProperty] private bool hasMoreRows;
    [ObservableProperty] private int totalRowCount;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool needsReload = true;
    [ObservableProperty] private string currentBoxId = string.Empty;
    [ObservableProperty] private InventoryMode currentMode = InventoryMode.Standard; // 👈 new

    public ObservableCollection<StatsProduct> ScannedProductsStats { get; private set; } = new();

    public ScannedProductsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
    }

    private async Task LoadProductsAsync(bool reset)
    {
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
        else IsLoadingMore = true;

        try
        {
            var where = string.IsNullOrWhiteSpace(_currentFilter) ? "" : $"WHERE {_currentFilter}";
            var dir = _currentSortDescending ? "DESC" : "ASC";

            // 🧠 choose table dynamically
            var table = CurrentMode == InventoryMode.Loots ? "LootsProducts" : "Products";

            // 🧠 add BoxId filter if in Loots mode and box specified
            if (CurrentMode == InventoryMode.Loots && !string.IsNullOrWhiteSpace(CurrentBoxId))
            {
                var extra = $"Box_Id = '{CurrentBoxId.Replace("'", "''")}'";
                where = string.IsNullOrWhiteSpace(where)
                    ? $"WHERE {extra}"
                    : $"{where} AND {extra}";
            }

            var (temp, total) = await Task.Run(() =>
            {
                var list = new List<StatsProduct>();

                using var conn = DatabaseInitializer.GetConnection(CurrentMode);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = CurrentMode == InventoryMode.Loots
                    ? $@"
                        SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                               Name, Color, Size, Price, ArticCode
                        FROM {table}
                        {where}
                        ORDER BY {_currentSortField} {dir}
                        LIMIT {PageSize} OFFSET {_offset};"
                    : $@"
                        SELECT Barcode, NULL as Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                               Name, Color, Size, Price, ArticCode
                        FROM {table}
                        {where}
                        ORDER BY {_currentSortField} {dir}
                        LIMIT {PageSize} OFFSET {_offset};";


                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException();


                    list.Add(new StatsProduct
                    {
                        Barcode = r.GetString(0),
                        BoxId = r.IsDBNull(1) ? "" : r.GetString(1),
                        InitialQuantity = r.GetInt32(2),
                        ScannedQuantity = r.GetInt32(3),
                        CreatedAt = DateTime.Parse(r.GetString(4)),
                        UpdatedAt = DateTime.Parse(r.GetString(5)),
                        Name = r.IsDBNull(6) ? "" : r.GetString(6),
                        Color = r.IsDBNull(7) ? "" : r.GetString(7),
                        Size = r.IsDBNull(8) ? "" : r.GetString(8),
                        Price = r.IsDBNull(9) ? "" : r.GetString(9),
                        ArticCode = r.IsDBNull(10) ? "" : r.GetString(10)
                    });


                }

                int totalCount;
                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table} {where}";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

                return (list, totalCount);
            }, token);

            if (token.IsCancellationRequested) return;

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


    [RelayCommand] private async Task LoadMoreAsync() => await LoadProductsAsync(false);

    public async Task LoadAsync(bool reset = true, CancellationToken token = default)
    {
        if (IsLoading) return;
        try
        {
            await Task.Yield();
            await LoadProductsAsync(reset);
        }
        catch (OperationCanceledException) { }
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
    private async Task Sort()
    {
        // 🧠 Add Box_Id when in Loots mode
        var fields = CurrentMode == InventoryMode.Loots
            ? new[] { "Box_Id", "Barcode", "ScannedQuantity", "InitialQuantity", "Difference",
                  "UpdatedAt", "ArticCode", "Name", "Color", "Size", "Price", "CreatedAt" }
            : new[] { "Barcode", "ScannedQuantity", "InitialQuantity", "Difference",
                  "UpdatedAt", "ArticCode", "Name", "Color", "Size", "Price", "CreatedAt" };

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
            // Loots specific (only when in Loots mode)
            CurrentMode == InventoryMode.Loots ? "All Products (All Boxes)" : null,
            CurrentMode == InventoryMode.Loots ? "Filter by Box ID" : null,
            // Common filters
            "All Products",
            "Scanned > 0",
            "Unscanned (Scanned = 0)",
            "Shortage (Scanned < Initial)",
            "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)",
            "Equal And Scanned (Scanned = Initial And Scanned > 0)",
            "Zero Initial (Initial == 0)",
            "Manual Changed",
            "Automatic Only",
            "",
            "Missing Name",
            "Missing Color",
            "Missing Size",
            "Has Price",
            "No Price",
            "Missing Info (Name or Color or Size)",
            "",
            "Updated Today",
            "Not Updated Recently (7+ days)",
            "Created Today",
            "",
            "Search by Barcode",
            "Search by Name",
            "Search by ArticCode"
        );


        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel")
            return;

        switch (fieldChoice)
        {
            // Quantity
            case "All Products": _currentFilter = ""; CurrentFilterDescription = "Filter: All"; break;
            case "Scanned > 0": _currentFilter = "ScannedQuantity > 0"; CurrentFilterDescription = "Filter: Scanned"; break;
            case "Unscanned (Scanned = 0)": _currentFilter = "ScannedQuantity = 0"; CurrentFilterDescription = "Filter: Unscanned"; break;
            case "Shortage (Scanned < Initial)": _currentFilter = "ScannedQuantity < InitialQuantity"; CurrentFilterDescription = "Filter: Shortage"; break;
            case "Overstock (Scanned > Initial)": _currentFilter = "ScannedQuantity > InitialQuantity"; CurrentFilterDescription = "Filter: Overstock"; break;
            case "Equal (Scanned = Initial)": _currentFilter = "ScannedQuantity = InitialQuantity"; CurrentFilterDescription = "Filter: Equal"; break;
            case "Equal And Scanned (Scanned = Initial And Scanned > 0)":
                _currentFilter = "ScannedQuantity = InitialQuantity AND ScannedQuantity > 0";
                CurrentFilterDescription = "Filter: Equal & Scanned";
                break;
            case "Zero Initial (Initial == 0)": _currentFilter = "InitialQuantity = 0"; CurrentFilterDescription = "Filter: Zero Init"; break;
            case "Manual Changed":
                _currentFilter = @"
                EXISTS (
                    SELECT 1 FROM ScanLogs sl
                    WHERE sl.Barcode = Products.Barcode
                      AND sl.IsManual = 1
                )";
                CurrentFilterDescription = "Filter: Manual Changed";
                break;
            case "Automatic Only":
                _currentFilter = @"
                ScannedQuantity > 0
                AND NOT EXISTS (
                    SELECT 1 FROM ScanLogs sl
                    WHERE sl.Barcode = Products.Barcode
                      AND sl.IsManual = 1
                )";
                CurrentFilterDescription = "Filter: Auto Only";
                break;

            // Info
            case "Missing Name": _currentFilter = "Name IS NULL OR Name = ''"; CurrentFilterDescription = "Filter: No Name"; break;
            case "Missing Color": _currentFilter = "Color IS NULL OR Color = ''"; CurrentFilterDescription = "Filter: No Color"; break;
            case "Missing Size": _currentFilter = "Size IS NULL OR Size = ''"; CurrentFilterDescription = "Filter: No Size"; break;
            case "Has Price": _currentFilter = "Price IS NOT NULL AND Price != ''"; CurrentFilterDescription = "Filter: Has Price"; break;
            case "No Price": _currentFilter = "Price IS NULL OR Price = ''"; CurrentFilterDescription = "Filter: No Price"; break;
            case "Missing Info (Name or Color or Size)":
                _currentFilter = "(Name IS NULL OR Name = '' OR Color IS NULL OR Color = '' OR Size IS NULL OR Size = '')";
                CurrentFilterDescription = "Filter: Missing Info";
                break;

            // Dates
            case "Updated Today": _currentFilter = "DATE(UpdatedAt) = DATE('now')"; CurrentFilterDescription = "Filter: Updated Today"; break;
            case "Not Updated Recently (7+ days)": _currentFilter = "UpdatedAt < DATETIME('now', '-7 day')"; CurrentFilterDescription = "Filter: Old Updates"; break;
            case "Created Today": _currentFilter = "DATE(CreatedAt) = DATE('now')"; CurrentFilterDescription = "Filter: Created Today"; break;

            // Search
            case "Search by Barcode":
                var barcode = await Shell.Current.DisplayPromptAsync("Search", "Enter part of barcode:", "OK", "Cancel");
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    _currentFilter = $"Barcode LIKE '%{barcode}%'";
                    CurrentFilterDescription = $"Filter: Code~{barcode}";
                }
                else return;
                break;

            case "Search by Name":
                var name = await Shell.Current.DisplayPromptAsync("Search", "Enter part of name:", "OK", "Cancel");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _currentFilter = $"Name LIKE '%{name}%'";
                    CurrentFilterDescription = $"Filter: Name~{name}";
                }
                else return;
                break;

            case "Search by ArticCode":
                var artic = await Shell.Current.DisplayPromptAsync("Search", "Enter ArticCode:", "OK", "Cancel");
                if (!string.IsNullOrWhiteSpace(artic))
                {
                    _currentFilter = $"ArticCode LIKE '%{artic}%'";
                    CurrentFilterDescription = $"Filter: Artic~{artic}";
                }
                else return;
                break;

            default: _currentFilter = ""; CurrentFilterDescription = "Filter: All"; break;
        }
        // === Loots-specific filters ===
        if (CurrentMode == InventoryMode.Loots)
        {
            switch (fieldChoice)
            {
                case "All Products (All Boxes)":
                    CurrentBoxId = string.Empty;
                    _currentFilter = "";
                    CurrentFilterDescription = "Filter: All Loots Boxes";
                    await LoadProductsAsync(reset: true);
                    return;

                case "Filter by Box ID":
                    var box = await Shell.Current.DisplayPromptAsync("Filter by Box", "Enter full or part of Box ID:", "OK", "Cancel");
                    if (!string.IsNullOrWhiteSpace(box))
                    {
                        _currentFilter = $"Box_Id LIKE '%{box.Replace("'", "''")}%'";
                        CurrentFilterDescription = $"Filter: Box~{box}";
                        await LoadProductsAsync(reset: true);
                    }
                    return;
            }
        }


        await LoadProductsAsync(reset: true);
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        _currentSortField = "UpdatedAt";
        _currentSortDescending = true;
        CurrentSortDescription = "Sort: Updated ↓";

        if (CurrentMode == InventoryMode.Loots)
        {
            _currentFilter = "";
            CurrentBoxId = string.Empty;
            CurrentFilterDescription = "Filter: All Loots Boxes";
        }
        else
        {
            _currentFilter = "ScannedQuantity > 0";
            CurrentFilterDescription = "Filter: Scanned";
        }

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
    public async Task OpenDetailsAsync(StatsProduct product)
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

    //public async Task LoadAsync(bool reset = true, CancellationToken token = default)
    //{
    //    // already loading? skip
    //    if (IsLoading)
    //        return;


    //    try
    //    {
    //        await Task.Yield(); // yield control so UI shows
    //        await LoadProductsAsync(reset);
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        // ignore cancellation
    //    }
    //    catch (Exception ex)
    //    {
    //        await MainThread.InvokeOnMainThreadAsync(() =>
    //            Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
    //    }
    //    finally
    //    {
    //        IsInitialLoading = false;
    //        IsLoadingMore = false;
    //    }
    //}



}
