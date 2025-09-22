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

        // Try find existing scanned product
        int? existingId = null;
        using (var checkCmd = _conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT Id FROM ScannedProducts WHERE Barcode=$barcode LIMIT 1";
            checkCmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            var result = await checkCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                existingId = Convert.ToInt32(result);
        }

        if (existingId.HasValue)
        {
            // Update
            using var updateCmd = _conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE ScannedProducts
                SET Quantity=$qty, UpdatedAt=$updatedAt
                WHERE Id=$id";
            updateCmd.Parameters.AddWithValue("$qty", ScannedQuantity);
            updateCmd.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("o"));
            updateCmd.Parameters.AddWithValue("$id", existingId.Value);
            await updateCmd.ExecuteNonQueryAsync();

            // Log
            using var logCmd = _conn.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO ScanLogs (Barcode, Quantity, InitialQuantity, ScannedProductId, Timestamp)
                VALUES ($barcode,$qty,$init,$spid,$ts)";
            logCmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            logCmd.Parameters.AddWithValue("$qty", ScannedQuantity);
            logCmd.Parameters.AddWithValue("$init", InitialQuantity);
            logCmd.Parameters.AddWithValue("$spid", existingId.Value);
            logCmd.Parameters.AddWithValue("$ts", DateTime.Now.ToString("o"));
            await logCmd.ExecuteNonQueryAsync();

            WeakReferenceMessenger.Default.Send(
                new ProductUpdatedMessage(new ScannedProduct
                {
                    Id = existingId.Value,
                    Barcode = ProductBarcode,
                    Quantity = ScannedQuantity,
                    InitialQuantity = InitialQuantity,
                    UpdatedAt = DateTime.Now
                }));
        }
        else
        {
            // Insert new
            using var insertCmd = _conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO ScannedProducts (Barcode, Quantity, InitialQuantity, CreatedAt, UpdatedAt)
                VALUES ($barcode,$qty,$init,$created,$updated)";
            insertCmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            insertCmd.Parameters.AddWithValue("$qty", ScannedQuantity);
            insertCmd.Parameters.AddWithValue("$init", InitialQuantity);
            insertCmd.Parameters.AddWithValue("$created", DateTime.Now.ToString("o"));
            insertCmd.Parameters.AddWithValue("$updated", DateTime.Now.ToString("o"));
            await insertCmd.ExecuteNonQueryAsync();

            // ✅ fetch last inserted id
            long newId;
            using (var idCmd = _conn.CreateCommand())
            {
                idCmd.CommandText = "SELECT last_insert_rowid();";
                newId = (long)(await idCmd.ExecuteScalarAsync());
            }

            // Insert log
            using var logCmd = _conn.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO ScanLogs (Barcode, Quantity, InitialQuantity, ScannedProductId, Timestamp)
                VALUES ($barcode,$qty,$init,$spid,$ts)";
            logCmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            logCmd.Parameters.AddWithValue("$qty", ScannedQuantity);
            logCmd.Parameters.AddWithValue("$init", InitialQuantity);
            logCmd.Parameters.AddWithValue("$spid", (int)newId);
            logCmd.Parameters.AddWithValue("$ts", DateTime.Now.ToString("o"));
            await logCmd.ExecuteNonQueryAsync();

            WeakReferenceMessenger.Default.Send(
                new ProductUpdatedMessage(new ScannedProduct
                {
                    Id = (int)newId,
                    Barcode = ProductBarcode,
                    Quantity = ScannedQuantity,
                    InitialQuantity = InitialQuantity,
                    UpdatedAt = DateTime.Now
                }));
        }

        await Shell.Current.GoToAsync("..");
    }
}
