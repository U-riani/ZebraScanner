using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;

namespace ZebraSCannerTest1.ViewModels;

public partial class DetailsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly ClipboardService _clipboard;
    private int _originalQuantity;
    [ObservableProperty]
    private bool isLoading;
    private readonly string _currentSection;



    public ObservableCollection<ScanLog> Logs { get; } = new();

    [ObservableProperty]
    private bool hasUnsavedChanges = false;


    [ObservableProperty]
    private bool isReadOnly = false; // Default: editable

    public DetailsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
        _currentSection = Preferences.Get("CurrentSection", null);

        SaveCommand = new AsyncRelayCommand(() => SaveUpdatedDetailsAsync(isAutoSave: false, previousValue: null));
        LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
    }
    // ✅ Computed property
    public int Difference => ScannedQuantity - InitialQuantity;
    // Core product fields
    [ObservableProperty] private string productBarcode;
    [ObservableProperty] private int scannedQuantity;
    [ObservableProperty] private int initialQuantity;

    // Product info
    [ObservableProperty] private string productName;
    [ObservableProperty] private string productColor;
    [ObservableProperty] private string productSize;
    [ObservableProperty] private decimal productPrice;
    [ObservableProperty] private string productArticCode;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand LoadLogsCommand { get; }

    // === Quantity Adjustment Commands ===
    [RelayCommand]
    private void Increment()
    {
        ScannedQuantity++;
        UpdateUnsavedState();


    }

    [RelayCommand]
    private void Decrement()
    {
        if(ScannedQuantity > 0)
        {
            ScannedQuantity--;
            UpdateUnsavedState();

        }
    }

    [RelayCommand]
    private async Task ManualEditAsync()
    {
        string input = await Shell.Current.DisplayPromptAsync(
            "Edit Quantity",
            "Enter new scanned quantity:",
            "OK", "Cancel", "e.g. 10",
            maxLength: 5,
            keyboard: Keyboard.Numeric,
            initialValue: ScannedQuantity.ToString());

        if (int.TryParse(input, out int newQty) && newQty >= 0 && newQty != ScannedQuantity)
        {
            ScannedQuantity = newQty;
            UpdateUnsavedState();

        }
    }



    // === Shared logic for saving and logging every quantity change ===

    // === Unified save logic (used by Save button + auto logging) ===
    public async Task SaveUpdatedDetailsAsync(bool isAutoSave = false, int? previousValue = null)
    {
        if (string.IsNullOrWhiteSpace(ProductBarcode)) return;

        var now = DateTime.UtcNow.ToString("o");
        int previousQty = previousValue ?? 0;

        // Get last known quantity (if not passed)
        if (previousValue == null)
        {
            using var checkCmd = _conn.CreateCommand();
            checkCmd.CommandText = "SELECT ScannedQuantity FROM Products WHERE Barcode = $b";
            checkCmd.Parameters.AddWithValue("$b", ProductBarcode);
            var result = checkCmd.ExecuteScalar();
            previousQty = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        int incrementBy = ScannedQuantity - previousQty;

        // ✅ Skip saving & logging if nothing changed
        if (incrementBy == 0)
        {
            if (!isAutoSave)
                await Shell.Current.DisplayAlert("No Changes", "Scanned quantity is unchanged.", "OK");
            return;
        }

        // Upsert into Products
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO Products 
    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
VALUES 
    ($barcode,$initial,$scanned,$created,$updated,$name,$color,$size,$price,$artic)
ON CONFLICT(Barcode) DO UPDATE SET
    ScannedQuantity = $scanned,
    InitialQuantity = $initial,
    UpdatedAt = $updated,
    Name = $name,
    Color = $color,
    Size = $size,
    Price = $price,
    ArticCode = $artic;";
            cmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            cmd.Parameters.AddWithValue("$initial", InitialQuantity);
            cmd.Parameters.AddWithValue("$scanned", ScannedQuantity);
            cmd.Parameters.AddWithValue("$created", now);
            cmd.Parameters.AddWithValue("$updated", now);
            cmd.Parameters.AddWithValue("$name", ProductName ?? "");
            cmd.Parameters.AddWithValue("$color", ProductColor ?? "");
            cmd.Parameters.AddWithValue("$size", ProductSize ?? "");
            cmd.Parameters.AddWithValue("$price", ProductPrice);
            cmd.Parameters.AddWithValue("$artic", ProductArticCode ?? "");
            cmd.ExecuteNonQuery();
        }

        // Add log record
        using (var log = _conn.CreateCommand())
        {
            log.CommandText = @"
                INSERT INTO ScanLogs (Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
                VALUES ($barcode, $was, $inc, $isValue, $updated, $isManual, $section)";
            log.Parameters.AddWithValue("$barcode", ProductBarcode);
            log.Parameters.AddWithValue("$was", previousQty);
            log.Parameters.AddWithValue("$inc", incrementBy);
            log.Parameters.AddWithValue("$isValue", ScannedQuantity);
            log.Parameters.AddWithValue("$updated", now);
            log.Parameters.AddWithValue("$isManual", 1);
            log.Parameters.AddWithValue("$section", _currentSection);
            log.ExecuteNonQuery();
        }

        // Notify and refresh UI
        WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new Product
        {
            Barcode = ProductBarcode,
            InitialQuantity = InitialQuantity,
            ScannedQuantity = ScannedQuantity,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }));

        await LoadLogsAsync();

        if (!isAutoSave)
            await Shell.Current.DisplayAlert("Saved", "Product updated successfully.", "OK");

        // ✅ Reset baseline
        _originalQuantity = ScannedQuantity;
        HasUnsavedChanges = false;

    }

    private void UpdateUnsavedState()
    {
        HasUnsavedChanges = ScannedQuantity != _originalQuantity;
    }


    [RelayCommand]
    public async Task<bool> ConfirmLeaveAsync()
    {
        if (!HasUnsavedChanges)
            return true; // safe to leave

        bool stay = await Shell.Current.DisplayAlert(
            "Unsaved Changes",
            "You have unsaved changes.\n\nPress 'Save' to keep your changes, or 'Leave' to discard them.",
            "Stay", "Leave");

        if (!stay)
        {
            HasUnsavedChanges = false; // discard
            return true; // allow navigation
        }

        return false; // cancel navigation
    }

    partial void OnScannedQuantityChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(Difference)); // notify UI
    }

    partial void OnInitialQuantityChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(Difference)); // notify UI
    }


    private async Task LoadLogsAsync()
    {
        if (string.IsNullOrEmpty(ProductBarcode)) return;

        Logs.Clear();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
        SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section
        FROM ScanLogs
        WHERE Barcode = $b
        ORDER BY UpdatedAt DESC
        LIMIT 50";
        cmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var r = cmd.ExecuteReader();

        // 👇 detect if IsManual column exists in table
        bool hasIsManual = false;
        bool hasSection = false;

        try
        {
            var colCheck = _conn.CreateCommand();
            colCheck.CommandText = "PRAGMA table_info(ScanLogs)";
            using var info = colCheck.ExecuteReader();
            while (info.Read())
            {
                var colName = info.GetString(1);
                if (colName.Equals("IsManual", StringComparison.OrdinalIgnoreCase))
                    hasIsManual = true;
                if (colName.Equals("Section", StringComparison.OrdinalIgnoreCase))
                    hasSection = true;
            }
        }
        catch {
            hasIsManual = false;
            hasSection = false;
        }

        // if column exists, query again including it
        if (hasIsManual)
        {
            r.Close();
            cmd.CommandText = @"
            SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
            FROM ScanLogs
            WHERE Barcode = $b
            ORDER BY UpdatedAt DESC
            LIMIT 50";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$b", ProductBarcode);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int? isManual = null;
                if (!reader.IsDBNull(5))
                {
                    try { isManual = Convert.ToInt32(reader.GetValue(5)); }
                    catch { isManual = null; }
                }

                string sectionValue = null;
                if (hasSection && !reader.IsDBNull(6))
                {
                    sectionValue = reader.GetString(6);
                }

                Logs.Add(new ScanLog
                {
                    Barcode = reader.GetString(0),
                    Was = reader.GetInt32(1),
                    IncrementBy = reader.GetInt32(2),
                    IsValue = reader.GetInt32(3),
                    UpdatedAt = DateTime.Parse(reader.GetString(4)),
                    IsManual = isManual,
                    Section = sectionValue
                });
            }
        }
        else
        {
            while (r.Read())
            {
                Logs.Add(new ScanLog
                {
                    Barcode = r.GetString(0),
                    Was = r.GetInt32(1),
                    IncrementBy = r.GetInt32(2),
                    IsValue = r.GetInt32(3),
                    UpdatedAt = DateTime.Parse(r.GetString(4)),
                    IsManual = null
                });
            }
        }
    }


    public async Task LoadProductAsync()
    {
        if (string.IsNullOrEmpty(ProductBarcode)) return;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Name, Color, Size, Price, ArticCode, InitialQuantity, ScannedQuantity
            FROM Products
            WHERE Barcode = $b";
        cmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            ProductName = r.IsDBNull(0) ? "" : r.GetString(0);
            ProductColor = r.IsDBNull(1) ? "" : r.GetString(1);
            ProductSize = r.IsDBNull(2) ? "" : r.GetString(2);
            ProductPrice = r.IsDBNull(3) || string.IsNullOrWhiteSpace(r.GetString(3)) ? 0 : Convert.ToDecimal(r.GetString(3));
            ProductArticCode = r.IsDBNull(4) ? "" : r.GetString(4);
            InitialQuantity = r.IsDBNull(5) ? 0 : r.GetInt32(5);
            ScannedQuantity = r.IsDBNull(6) ? 0 : r.GetInt32(6);
        }

        // ✅ Save the starting quantity for later comparison
        _originalQuantity = ScannedQuantity;
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);
    }
}
