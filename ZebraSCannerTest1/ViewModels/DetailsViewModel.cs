using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

[QueryProperty(nameof(ProductBarcode), "Barcode")]
[QueryProperty(nameof(ScannedQuantity), "Quantity")]
[QueryProperty(nameof(InitialQuantity), "InitialQuantity")]
public partial class DetailsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;

    public DetailsViewModel(SqliteConnection conn)
    {
        _conn = conn;
        SaveCommand = new AsyncRelayCommand(SaveUpdatedDetailsAsync);
    }

    [ObservableProperty] private string productBarcode;
    [ObservableProperty] private int scannedQuantity;
    [ObservableProperty] private int initialQuantity;

    public IAsyncRelayCommand SaveCommand { get; }

    public async Task SaveUpdatedDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductBarcode)) return;

        var now = DateTime.UtcNow.ToString("o");

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
VALUES ($barcode,$initial,$scanned,$created,$updated)
ON CONFLICT(Barcode) DO UPDATE SET
    ScannedQuantity = $scanned,
    InitialQuantity = $initial,
    UpdatedAt = $updated;";
            cmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            cmd.Parameters.AddWithValue("$initial", InitialQuantity);
            cmd.Parameters.AddWithValue("$scanned", ScannedQuantity);
            cmd.Parameters.AddWithValue("$created", now);
            cmd.Parameters.AddWithValue("$updated", now);
            cmd.ExecuteNonQuery();
        }

        using (var log = _conn.CreateCommand())
        {
            log.CommandText = @"
INSERT INTO ScanLogs (Barcode, ScannedQuantity, Timestamp)
VALUES ($barcode,$scanned,$ts)";
            log.Parameters.AddWithValue("$barcode", ProductBarcode);
            log.Parameters.AddWithValue("$scanned", ScannedQuantity);
            log.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            log.ExecuteNonQuery();
        }

        WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new Product
        {
            Barcode = ProductBarcode,
            InitialQuantity = InitialQuantity,
            ScannedQuantity = ScannedQuantity,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }));

        await Shell.Current.GoToAsync("..");
    }
}
