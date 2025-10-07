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

    private string _currentSortField = "UpdatedAt";
    private bool _currentSortDescending = true;
    private string _currentFilter = "ScannedQuantity > 0";


    [ObservableProperty] private string currentSortDescription = "Sort: Updated ↓";
    [ObservableProperty] private string currentFilterDescription = "Filter: Scanned";

    public ObservableCollection<StatsProduct> ScannedProductsStats { get; private set; } = new();

    public ScannedProductsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
    }

    private void LoadProducts(string orderBy, bool descending = true, string whereClause = "")
    {
        var temp = new List<StatsProduct>();
        using var cmd = _conn.CreateCommand();

        var dir = descending ? "DESC" : "ASC";
        var where = string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}";

        cmd.CommandText = $@"
            SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode
            FROM Products
            {where}
            ORDER BY {orderBy} {dir}";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            temp.Add(new StatsProduct
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
        }

        ScannedProductsStats = new ObservableCollection<StatsProduct>(temp);
        OnPropertyChanged(nameof(ScannedProductsStats));
    }

    // 🔹 SORT popup: ask which column and order
    [RelayCommand]
    private async Task Sort()
    {
        string[] fields = new[] { "Barcode", "ScannedQuantity", "InitialQuantity", "Difference", "UpdatedAt", "CreatedAt" };
        string fieldChoice = await Shell.Current.DisplayActionSheet("Sort by:", "Cancel", null, fields);
        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel")
            return;

        string orderChoice = await Shell.Current.DisplayActionSheet("Order:", "Cancel", null, "Ascending", "Descending");
        if (string.IsNullOrEmpty(orderChoice) || orderChoice == "Cancel")
            return;

        _currentSortField = fieldChoice switch
        {
            "Difference" => "(ScannedQuantity - InitialQuantity)",
            _ => fieldChoice
        };

        _currentSortDescending = orderChoice == "Descending";
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);

        var arrow = _currentSortDescending ? "↓" : "↑";
        CurrentSortDescription = $"Sort: {fieldChoice} {arrow}";
    }

    // 🔹 FILTER popup: same categories as before
    [RelayCommand]
    private async Task Filter()
    {
        var fieldChoice = await Shell.Current.DisplayActionSheet(
            "Choose filter", "Cancel", null,
            // --- Quantity-based filters ---
            "All Products",
            "Scanned > 0",
            "Unscanned (Scanned = 0)",
            "Shortage (Scanned < Initial)",
            "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)",
            "Equal And Scanned (Scanned = Initial And Scanned > 0)",
            "Zero Initial (Inital == 0)",
            "Manual Changed",
            "Automatic Only",
            "",
            // --- Product attribute filters ---
            "Missing Name",
            "Missing Color",
            "Missing Size",
            "Has Price",
            "No Price",
            "Missing Info (Name or Color or Size)",
            "",
            // --- Date-based filters ---
            "Updated Today",
            "Not Updated Recently (7+ days)",
            "Created Today",
            "",
            // --- Search filters ---
            "Search by Barcode",
            "Search by Name",
            "Search by ArticCode",
            "Manual Filter(Custom Builder)"
        );

        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel") return;

        switch (fieldChoice)
        {
            // --- Quantity filters ---
            case "All Products":
                _currentFilter = "";
                CurrentFilterDescription = "Filter: All";
                break;
            case "Scanned > 0":
                _currentFilter = "ScannedQuantity > 0";
                CurrentFilterDescription = "Filter: Scanned";
                break;
            case "Unscanned (Scanned = 0)":
                _currentFilter = "ScannedQuantity = 0";
                CurrentFilterDescription = "Filter: Unscanned";
                break;
            case "Shortage (Scanned < Initial)":
                _currentFilter = "ScannedQuantity < InitialQuantity";
                CurrentFilterDescription = "Filter: Shortage";
                break;
            case "Overstock (Scanned > Initial)":
                _currentFilter = "ScannedQuantity > InitialQuantity";
                CurrentFilterDescription = "Filter: Overstock";
                break;
            case "Equal (Scanned = Initial)":
                _currentFilter = "ScannedQuantity = InitialQuantity";
                CurrentFilterDescription = "Filter: Equal";
                break;
            case "Equal And Scanned (Scanned = Initial And Scanned > 0)":
                _currentFilter = "ScannedQuantity = InitialQuantity AND ScannedQuantity > 0";
                CurrentFilterDescription = "Filter: Equal & Scanned";
                break;
            case "Zero Initial (Inital == 0)":
                _currentFilter = "InitialQuantity = 0";
                CurrentFilterDescription = "Filter: Zero Init";
                break;
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



            // --- Product info filters ---
            case "Missing Name":
                _currentFilter = "Name IS NULL OR Name = ''";
                CurrentFilterDescription = "Filter: No Name";
                break;
            case "Missing Color":
                _currentFilter = "Color IS NULL OR Color = ''";
                CurrentFilterDescription = "Filter: No Color";
                break;
            case "Missing Size":
                _currentFilter = "Size IS NULL OR Size = ''";
                CurrentFilterDescription = "Filter: No Size";
                break;
            case "Has Price":
                _currentFilter = "Price IS NOT NULL AND Price != ''";
                CurrentFilterDescription = "Filter: Has Price";
                break;
            case "No Price":
                _currentFilter = "Price IS NULL OR Price = ''";
                CurrentFilterDescription = "Filter: No Price";
                break;
            case "Missing Info (Name or Color or Size)":
                _currentFilter = "(Name IS NULL OR Name = '' OR Color IS NULL OR Color = '' OR Size IS NULL OR Size = '')";
                CurrentFilterDescription = "Filter: Missing Info";
                break;

            // --- Date-based filters ---
            case "Updated Today":
                _currentFilter = "DATE(UpdatedAt) = DATE('now')";
                CurrentFilterDescription = "Filter: Updated Today";
                break;
            case "Not Updated Recently (7+ days)":
                _currentFilter = "UpdatedAt < DATETIME('now', '-7 day')";
                CurrentFilterDescription = "Filter: Old Updates";
                break;
            case "Created Today":
                _currentFilter = "DATE(CreatedAt) = DATE('now')";
                CurrentFilterDescription = "Filter: Created Today";
                break;

            // --- Search filters ---
            case "Search by Barcode":
                var barcode = await Shell.Current.DisplayPromptAsync("Search", "Enter part of barcode:", "OK", "Cancel", "12345");
                if (string.IsNullOrWhiteSpace(barcode)) return;
                _currentFilter = $"Barcode LIKE '%{barcode}%'";
                CurrentFilterDescription = $"Filter: Code~{barcode}";
                break;
            case "Search by Name":
                var name = await Shell.Current.DisplayPromptAsync("Search", "Enter part of name:", "OK", "Cancel", "e.g. Jeans");
                if (string.IsNullOrWhiteSpace(name)) return;
                _currentFilter = $"Name LIKE '%{name}%'";
                CurrentFilterDescription = $"Filter: Name~{name}";
                break;
            case "Search by ArticCode":
                var artic = await Shell.Current.DisplayPromptAsync("Search", "Enter ArticCode:", "OK", "Cancel", "e.g. A123");
                if (string.IsNullOrWhiteSpace(artic)) return;
                _currentFilter = $"ArticCode LIKE '%{artic}%'";
                CurrentFilterDescription = $"Filter: Artic~{artic}";
                break;
            default:
                _currentFilter = "";
                CurrentFilterDescription = "Filter: All";
                break;
        }

        // ✅ Reload with selected filter
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
    }


    [RelayCommand]
    private void ClearFilter()
    {
        _currentFilter = "ScannedQuantity > 0";
        _currentSortField = "UpdatedAt";
        _currentSortDescending = true;

        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);

        CurrentFilterDescription = "Filter: Scanned";
        CurrentSortDescription = "Sort: Updated ↓";
    }


    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);
    }

    public void ApplyManualFilter(string filter)
    {
        _currentFilter = filter;
        CurrentFilterDescription = $"Filter: Manual ({filter})";
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(StatsProduct product)
    {
        if (product == null)
            return;

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
            ["IsReadOnly"] = false  // 👈 NEW FLAG
        };


        await Shell.Current.GoToAsync(nameof(DetailsPage), query);
    }

}
