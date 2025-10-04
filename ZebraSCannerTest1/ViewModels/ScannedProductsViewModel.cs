using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;

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
            "All Products",
            "Scanned > 0",
            "Unscanned (Scanned = 0)",
            "Shortage (Scanned < Initial)",
            "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)",
            "Equal And Scanned (Scanned = Initial And Scanned > 0)",
            "Zero Initial",
            "Search by Barcode");

        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel")
            return;

        switch (fieldChoice)
        {
            case "Search by Barcode":
                var input = await Shell.Current.DisplayPromptAsync(
                    "Search", "Enter part of barcode:", "OK", "Cancel", "12345");
                if (string.IsNullOrWhiteSpace(input)) return;

                _currentFilter = $"Barcode LIKE '%{input}%'";
                CurrentFilterDescription = $"Filter: Code~{input}";
                break;

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
                CurrentFilterDescription = "Filter: Eql&Scan";
                break;

            case "Zero Initial":
                _currentFilter = "InitialQuantity = 0";
                CurrentFilterDescription = "Filter: ZeroInit";
                break;
        }

        // ✅ Reload list with current sort & new filter
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
}
