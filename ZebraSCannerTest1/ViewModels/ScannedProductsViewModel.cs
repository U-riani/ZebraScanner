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

    private void LoadProducts(string option, bool descending = true, string whereClause = "")
    {
        var temp = new List<StatsProduct>();
        using var cmd = _conn.CreateCommand();

        var direction = descending ? "DESC" : "ASC";
        var where = string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}";

        cmd.CommandText = $@"
        SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode
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
                UpdatedAt = DateTime.Parse(r.GetString(4)),
                Name = r.IsDBNull(5) ? null : r.GetString(5),
                Color = r.IsDBNull(6) ? null : r.GetString(6),
                Size = r.IsDBNull(7) ? null : r.GetString(7),
                Price = r.IsDBNull(8) ? null : r.GetString(8),
                ArticCode = r.IsDBNull(9) ? null : r.GetString(9)
            });
        }

        ScannedProductsStats = new ObservableCollection<StatsProduct>(temp);
        OnPropertyChanged(nameof(ScannedProductsStats));
    }

    // 🔹 Commands for XAML buttons
    [RelayCommand]
    private void Sort()
    {
        // toggle between ScannedQuantity ascending/descending for demo
        _currentSortField = "ScannedQuantity";
        _currentSortDescending = !_currentSortDescending;
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);

        var arrow = _currentSortDescending ? "↓" : "↑";
        CurrentSortDescription = $"Sort: Qty {arrow}";
    }

    [RelayCommand]
    private void Filter()
    {
        // Example: only show products with Scanned > 0
        _currentFilter = "ScannedQuantity > 0";
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
        CurrentFilterDescription = "Filter: Scanned";
    }

    [RelayCommand]
    private void ClearFilter()
    {
        _currentFilter = "";
        LoadProducts(_currentSortField, _currentSortDescending, _currentFilter);
        CurrentFilterDescription = "Filter: All";
    }

    [RelayCommand]
    private async Task CopyBarcode(string barcode) => await _clipboard.CopyAsync(barcode);
}
