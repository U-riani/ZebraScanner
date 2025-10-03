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

    [ObservableProperty] private string currentSortDescription = "Sort: Upd ↓";
    [ObservableProperty] private string currentFilterDescription = "Filter: Scanned";


    public ObservableCollection<StatsProduct> ScannedProductsStats { get; private set; } = new();

    public ScannedProductsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
    }

    [RelayCommand]
    private async Task Sort()
    {
        var fieldChoice = await Shell.Current.DisplayActionSheet(
            "Choose sort field", "Cancel", null,
            "Barcode", "ScannedQuantity", "InitialQuantity", "Difference", "UpdatedAt");

        if (fieldChoice == "Cancel") return;

        var directionChoice = await Shell.Current.DisplayActionSheet(
            "Sort direction", "Cancel", null, "Ascending", "Descending");

        if (directionChoice == "Cancel") return;

        _currentSortField = fieldChoice switch
        {
            "Barcode" => "Barcode",
            "ScannedQuantity" => "ScannedQuantity",
            "InitialQuantity" => "InitialQuantity",
            "Difference" => "(ScannedQuantity - InitialQuantity)",
            "UpdatedAt" => "UpdatedAt",
            _ => "UpdatedAt"
        };

        _currentSortDescending = directionChoice == "Descending";

        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);

        // 🔹 Short label (arrow + short word)
        var arrow = _currentSortDescending ? "↓" : "↑";
        var shortField = fieldChoice switch
        {
            "Barcode" => "Code",
            "ScannedQuantity" => "Qty",
            "InitialQuantity" => "Init",
            "Difference" => "Diff",
            "UpdatedAt" => "Upd",
            _ => "Upd"
        };
        CurrentSortDescription = $"Sort: {shortField} {arrow}";
    }

    [RelayCommand]
    private async Task Filter()
    {
        var fieldChoice = await Shell.Current.DisplayActionSheet(
            "Choose filter", "Cancel", null,
            "All Products",
            "Scanned > 0",
            "Shortage (Scanned < Initial)",
            "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)",
            "Equal And Scanned (Scanned = Initial And Scanned > 0)",
            "Zero Initial",
            "Search by Barcode");

        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel") return;

        if (fieldChoice == "Search by Barcode")
        {
            // Prompt user for input
            var input = await Shell.Current.DisplayPromptAsync(
                "Search", "Enter part of barcode:", "OK", "Cancel", "12345");

            if (string.IsNullOrWhiteSpace(input)) return;

            _currentFilter = $"Barcode LIKE '%{input}%'";
            CurrentFilterDescription = $"Filter: Code~{input}";
        }
        else
        {
            _currentFilter = fieldChoice switch
            {
                "All Products" => "",
                "Scanned > 0" => "ScannedQuantity > 0",
                "Shortage (Scanned < Initial)" => "ScannedQuantity < InitialQuantity",
                "Overstock (Scanned > Initial)" => "ScannedQuantity > InitialQuantity",
                "Equal (Scanned = Initial)" => "ScannedQuantity = InitialQuantity",
                "Equal And Scanned (Scanned = Initial And Scanned > 0)" => "ScannedQuantity > 0 AND ScannedQuantity = InitialQuantity",
                "Zero Initial" => "InitialQuantity = 0",
                _ => ""
            };

            CurrentFilterDescription = fieldChoice switch
            {
                "All Products" => "Filter: All",
                "Scanned > 0" => "Filter: Scanned",
                "Shortage (Scanned < Initial)" => "Filter: Shortage",
                "Overstock (Scanned > Initial)" => "Filter: Over",
                "Equal (Scanned = Initial)" => "Filter: Equal",
                "Equal And Scanned (Scanned = Initial And Scanned > 0)" => "Filter: Equal & > 0",
                "Zero Initial" => "Filter: ZeroInit",
                _ => "No filter"
            };
        }

        // reload with combined sort+filter
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        _currentFilter = "ScannedQuantity > 0";
        _currentSortField = "UpdatedAt";
        _currentSortDescending = true;

        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);

        CurrentFilterDescription = "Filter: All Scanned";
        CurrentSortDescription = "Sort: Upd ↓";
    }

    private void LoadProducts(string option, bool descending = true, string whereClause = "")
    {
        var temp = new List<StatsProduct>();
        using var cmd = _conn.CreateCommand();

        var direction = descending ? "DESC" : "ASC";
        var where = string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}";

        cmd.CommandText = $@"
        SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt
        FROM Products
        {where}
        ORDER BY {option} {direction}";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            temp.Add(new StatsProduct
            {
                Barcode = r.GetString(0),
                InitialQuantity = r.GetInt32(1),
                ScannedQuantity = r.GetInt32(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        }

        ScannedProductsStats = new ObservableCollection<StatsProduct>(temp);
        OnPropertyChanged(nameof(ScannedProductsStats));

        // ✅ Always refresh short info
        var arrow = descending ? "↓" : "↑";
        var shortField = option switch
        {
            "Barcode" => "Code",
            "ScannedQuantity" => "Qty",
            "InitialQuantity" => "Init",
            "(ScannedQuantity - InitialQuantity)" => "Diff",
            _ => "Upd"
        };
        CurrentSortDescription = $"Sort: {shortField} {arrow}";

        CurrentFilterDescription = string.IsNullOrWhiteSpace(whereClause) ? "Filter: All" :
            whereClause switch
            {
                "ScannedQuantity > 0" => "Filter: Scanned",
                "ScannedQuantity < InitialQuantity" => "Filter: Shortage",
                "ScannedQuantity > InitialQuantity" => "Filter: Over",
                "ScannedQuantity = InitialQuantity" => "Filter: Equal",
                "ScannedQuantity > 0 AND ScannedQuantity = InitialQuantity" => "Filter: Equal & > 0",
                "InitialQuantity = 0" => "Filter: ZeroInit",
                _ => "Filter: Custom"
            };
    }

    [RelayCommand]
    private async Task ShowOveralCommand()
    {
        var fieldChoice = await Shell.Current.DisplayPromptAsync("Overal Stats", "Cancel", null);
    }


    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);
    }
}
