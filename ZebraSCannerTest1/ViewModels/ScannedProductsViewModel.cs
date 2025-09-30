using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

public partial class ScannedProductsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;

    public ObservableCollection<StatsProduct> ScannedProductsStats { get; } = new();

    public ScannedProductsViewModel(SqliteConnection conn)
    {
        _conn = conn;
        LoadProducts();
    }

    private void LoadProducts()
    {
        ScannedProductsStats.Clear();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt
            FROM Products
            ORDER BY UpdatedAt DESC";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            ScannedProductsStats.Add(new StatsProduct
            {
                Barcode = r.GetString(0),
                InitialQuantity = r.GetInt32(1),
                ScannedQuantity = r.GetInt32(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        }
    }
}
