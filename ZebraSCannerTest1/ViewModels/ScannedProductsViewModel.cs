using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1.ViewModels;

public partial class ScannedProductsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;

    [ObservableProperty] private int totalProducts;
    [ObservableProperty] private int scannedProducts;
    [ObservableProperty] private int shortageProducts;
    [ObservableProperty] private int overstockProducts;
    [ObservableProperty] private int zeroInitialProducts;

    public ScannedProductsViewModel(SqliteConnection conn)
    {
        _conn = conn;
        LoadStats();
    }

    private void LoadStats()
    {
        using var cmd = _conn.CreateCommand();

        // Total products
        cmd.CommandText = "SELECT COUNT(*) FROM Products";
        TotalProducts = Convert.ToInt32(cmd.ExecuteScalar());

        // Scanned products (at least 1 scan)
        cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE ScannedQuantity > 0";
        ScannedProducts = Convert.ToInt32(cmd.ExecuteScalar());

        // Shortage (scanned < initial)
        cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE ScannedQuantity < InitialQuantity";
        ShortageProducts = Convert.ToInt32(cmd.ExecuteScalar());

        // Overstock (scanned > initial)
        cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE ScannedQuantity > InitialQuantity";
        OverstockProducts = Convert.ToInt32(cmd.ExecuteScalar());

        // Zero initial
        cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE InitialQuantity = 0";
        ZeroInitialProducts = Convert.ToInt32(cmd.ExecuteScalar());
    }
}
