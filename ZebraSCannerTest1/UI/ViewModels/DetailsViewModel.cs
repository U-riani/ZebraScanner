using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(BoxId), "BoxId")]
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
    [ObservableProperty] private InventoryMode currentMode = InventoryMode.Standard;
    [ObservableProperty] private string boxId = string.Empty;
    [ObservableProperty] private int totalScannedQuantity;
    [ObservableProperty] private int totalInitialQuantity;

    [ObservableProperty] private string allBoxesInfo = string.Empty;


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
        if (ScannedQuantity > 0)
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
        var table = CurrentMode == InventoryMode.Loots ? "LootsProducts" : "Products";

        using var conn = DatabaseInitializer.GetConnection(CurrentMode);

        // read last quantity if not given
        if (previousValue == null)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = CurrentMode == InventoryMode.Loots
                ? $"SELECT ScannedQuantity FROM {table} WHERE Barcode = $b AND Box_Id = $box"
                : $"SELECT ScannedQuantity FROM {table} WHERE Barcode = $b";
            checkCmd.Parameters.AddWithValue("$b", ProductBarcode);
            if (CurrentMode == InventoryMode.Loots)
                checkCmd.Parameters.AddWithValue("$box", BoxId);
            var result = checkCmd.ExecuteScalar();
            previousQty = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        int incrementBy = ScannedQuantity - previousQty;
        if (incrementBy == 0)
        {
            if (!isAutoSave)
                await Shell.Current.DisplayAlert("No Changes", "Scanned quantity is unchanged.", "OK");
            return;
        }

        using (var cmd = conn.CreateCommand())
        {
            if (CurrentMode == InventoryMode.Loots)
            {
                // manually check if record exists
                cmd.CommandText = "SELECT COUNT(*) FROM LootsProducts WHERE Barcode = $barcode AND Box_Id = $box";
                cmd.Parameters.AddWithValue("$barcode", ProductBarcode?.Trim());
                cmd.Parameters.AddWithValue("$box", BoxId ?? "");
                long count = (long)cmd.ExecuteScalar();

                cmd.Parameters.Clear();

                if (count > 0)
                {
                    // update existing
                    cmd.CommandText = @"
UPDATE LootsProducts
SET ScannedQuantity = $scanned,
    InitialQuantity = $initial,
    UpdatedAt = $updated,
    Name = $name,
    Color = $color,
    Size = $size,
    Price = $price,
    ArticCode = $artic
WHERE Barcode = $barcode AND Box_Id = $box;";
                }
                else
                {
                    // insert new
                    cmd.CommandText = @"
INSERT INTO LootsProducts
    (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, 
     Name, Color, Size, Price, ArticCode)
VALUES
    ($barcode, $box, $initial, $scanned, $created, $updated,
     $name, $color, $size, $price, $artic);";
                    cmd.Parameters.AddWithValue("$created", now);
                }

                cmd.Parameters.AddWithValue("$barcode", ProductBarcode?.Trim());
                cmd.Parameters.AddWithValue("$box", BoxId ?? "");
                cmd.Parameters.AddWithValue("$initial", InitialQuantity);
                cmd.Parameters.AddWithValue("$scanned", ScannedQuantity);
                cmd.Parameters.AddWithValue("$updated", now);
                cmd.Parameters.AddWithValue("$name", ProductName ?? "");
                cmd.Parameters.AddWithValue("$color", ProductColor ?? "");
                cmd.Parameters.AddWithValue("$size", ProductSize ?? "");
                cmd.Parameters.AddWithValue("$price", ProductPrice);
                cmd.Parameters.AddWithValue("$artic", ProductArticCode ?? "");
                cmd.ExecuteNonQuery();
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO Products 
    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, 
     Name, Color, Size, Price, ArticCode)
VALUES 
    ($barcode,$initial,$scanned,$created,$updated,$name,$color,$size,$price,$artic)
ON CONFLICT(Barcode) DO UPDATE SET
    ScannedQuantity=$scanned,
    InitialQuantity=$initial,
    UpdatedAt=$updated,
    Name=$name,
    Color=$color,
    Size=$size,
    Price=$price,
    ArticCode=$artic;";
                cmd.Parameters.AddWithValue("$barcode", ProductBarcode?.Trim());
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

        }

        // ✅ log only for standard inventory
        if (CurrentMode == InventoryMode.Standard)
        {
            using var log = conn.CreateCommand();
            log.CommandText = @"
        INSERT INTO ScanLogs (Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
        VALUES ($barcode, $was, $inc, $isValue, $updated, $isManual, $section)";
            log.Parameters.AddWithValue("$barcode", ProductBarcode);
            log.Parameters.AddWithValue("$was", previousQty);
            log.Parameters.AddWithValue("$inc", incrementBy);
            log.Parameters.AddWithValue("$isValue", ScannedQuantity);
            log.Parameters.AddWithValue("$updated", now);
            log.Parameters.AddWithValue("$isManual", 1);
            log.Parameters.AddWithValue("$section",
                string.IsNullOrEmpty(_currentSection) ? (object)DBNull.Value : _currentSection);
            log.ExecuteNonQuery();

        }
        else if (CurrentMode == InventoryMode.Loots)
        {
            using var log = conn.CreateCommand();
            log.CommandText = @"
        INSERT INTO LootsScanLogs 
        (Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
        VALUES 
        ($barcode, $box, $was, $inc, $isValue, $updated, $isManual, $section);";

            log.Parameters.AddWithValue("$barcode", ProductBarcode);
            log.Parameters.AddWithValue("$box", BoxId ?? "");
            log.Parameters.AddWithValue("$was", previousQty);
            log.Parameters.AddWithValue("$inc", incrementBy);
            log.Parameters.AddWithValue("$isValue", ScannedQuantity);
            log.Parameters.AddWithValue("$updated", now);
            log.Parameters.AddWithValue("$isManual", 1);
            log.Parameters.AddWithValue("$section",
                string.IsNullOrEmpty(_currentSection) ? (object)DBNull.Value : _currentSection);

            log.ExecuteNonQuery();
        }


        //if (CurrentMode == InventoryMode.Loots)
        //{
        //    RecalculateTotals(conn);
        //}

        WeakReferenceMessenger.Default.Send(
                new ProductUpdatedMessage(new Product
                {
                    Barcode = ProductBarcode,
                    InitialQuantity = InitialQuantity,
                    ScannedQuantity = ScannedQuantity,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }));

        if (!isAutoSave)
            await Shell.Current.DisplayAlert("Saved", "Product updated successfully.", "OK");

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
        if (CurrentMode == InventoryMode.Loots && !string.IsNullOrEmpty(ProductBarcode))
        {
            // reflect the visual total immediately (optimistic)
            var delta = newValue - oldValue;
            TotalScannedQuantity += delta;
        }
    }

    partial void OnInitialQuantityChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(Difference)); // notify UI
    }


    private async Task LoadLogsAsync()
    {
        if (string.IsNullOrEmpty(ProductBarcode))
            return;

        Logs.Clear();
        string table = CurrentMode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";

        using var conn = DatabaseInitializer.GetConnection(CurrentMode);

        // Check column availability dynamically
        bool hasIsManual = false;
        bool hasSection = false;
        bool hasBoxId = CurrentMode == InventoryMode.Loots;

        try
        {
            using var pragma = conn.CreateCommand();
            pragma.CommandText = $"PRAGMA table_info({table});";
            using var info = pragma.ExecuteReader();
            while (info.Read())
            {
                string col = info.GetString(1);
                if (col.Equals("IsManual", StringComparison.OrdinalIgnoreCase))
                    hasIsManual = true;
                if (col.Equals("Section", StringComparison.OrdinalIgnoreCase))
                    hasSection = true;
            }
        }
        catch { /* ignore */ }

        // Build proper select
        var columns = new List<string> { "Barcode", "Was", "IncrementBy", "IsValue", "UpdatedAt" };
        if (hasIsManual) columns.Add("IsManual");
        if (hasSection) columns.Add("Section");
        if (hasBoxId) columns.Add("Box_Id");

        string columnList = string.Join(", ", columns);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
        SELECT {columnList}
        FROM {table}
        WHERE Barcode = $b
        ORDER BY UpdatedAt DESC
        LIMIT 50;";
        cmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var log = new ScanLog
            {
                Barcode = reader.GetString(0),
                Was = reader.GetInt32(1),
                IncrementBy = reader.GetInt32(2),
                IsValue = reader.GetInt32(3),
                UpdatedAt = DateTime.Parse(reader.GetString(4))
            };

            int colIndex = 5;

            if (hasIsManual)
            {
                log.IsManual = reader.IsDBNull(colIndex) ? null : reader.GetInt32(colIndex);
                colIndex++;
            }

            if (hasSection)
            {
                log.Section = reader.IsDBNull(colIndex) ? null : reader.GetString(colIndex);
                colIndex++;
            }

            if (hasBoxId)
            {
                log.Box_Id = reader.IsDBNull(colIndex) ? null : reader.GetString(colIndex);
            }

            Logs.Add(log);
        }
    }



    public async Task LoadProductAsync()
    {
        if (string.IsNullOrEmpty(ProductBarcode))
            return;

        var table = CurrentMode == InventoryMode.Loots ? "LootsProducts" : "Products";
        using var conn = DatabaseInitializer.GetConnection(CurrentMode);

        using var cmd = conn.CreateCommand();

        if (CurrentMode == InventoryMode.Loots)
        {
            // 1️⃣ Load product info for current box
            cmd.CommandText = @"
            SELECT Name, Color, Size, Price, ArticCode, InitialQuantity, ScannedQuantity, Box_Id
            FROM LootsProducts
            WHERE Barcode = $b AND Box_Id = $box";
            cmd.Parameters.AddWithValue("$b", ProductBarcode);
            cmd.Parameters.AddWithValue("$box", BoxId ?? "");
        }
        else
        {
            cmd.CommandText = @"
            SELECT Name, Color, Size, Price, ArticCode, InitialQuantity, ScannedQuantity
            FROM Products
            WHERE Barcode = $b";
            cmd.Parameters.AddWithValue("$b", ProductBarcode);
        }

        using var r = cmd.ExecuteReader();
        if (!r.Read())
        {
            // 🔒 Auto-create record if missing (Loots mode only)
            if (CurrentMode == InventoryMode.Loots)
            {
                using var insert = conn.CreateCommand();
                insert.CommandText = @"
                INSERT OR IGNORE INTO LootsProducts
                (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                VALUES ($b, $box, 0, 0, $now, $now)";
                insert.Parameters.AddWithValue("$b", ProductBarcode);
                insert.Parameters.AddWithValue("$box", BoxId ?? "");
                insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                insert.ExecuteNonQuery();
            }
            return;
        }

        // ✅ Hydrate current box data
        ProductName = r.IsDBNull(0) ? "" : r.GetString(0);
        ProductColor = r.IsDBNull(1) ? "" : r.GetString(1);
        ProductSize = r.IsDBNull(2) ? "" : r.GetString(2);
        ProductPrice = r.IsDBNull(3) ? 0 :
            Convert.ToDecimal(r.GetValue(3));

        ProductArticCode = r.IsDBNull(4) ? "" : r.GetString(4);
        InitialQuantity = r.IsDBNull(5) ? 0 : r.GetInt32(5);
        ScannedQuantity = r.IsDBNull(6) ? 0 : r.GetInt32(6);

        if (CurrentMode == InventoryMode.Loots && !r.IsDBNull(7))
            BoxId = r.GetString(7);

        r.Close();

        // 2️⃣ If in Loots mode — calculate totals across all boxes
        if (CurrentMode == InventoryMode.Loots)
        {
            int totalQty = 0;
            TotalInitialQuantity = 0;
            List<string> boxes = new();

            using var totalCmd = conn.CreateCommand();
            totalCmd.CommandText = @"
                SELECT TRIM(Box_Id), SUM(ScannedQuantity), SUM(InitialQuantity)
                FROM LootsProducts
                WHERE TRIM(Barcode) = TRIM($b)
                GROUP BY TRIM(Box_Id);";
            totalCmd.Parameters.AddWithValue("$b", ProductBarcode?.Trim());


            using var totalReader = totalCmd.ExecuteReader();
            while (totalReader.Read())
            {
                string box = totalReader.IsDBNull(0) ? "(none)" : totalReader.GetString(0);
                int scanned = totalReader.IsDBNull(1) ? 0 : totalReader.GetInt32(1);
                int initial = totalReader.IsDBNull(2) ? 0 : totalReader.GetInt32(2);

                totalQty += scanned;
                TotalInitialQuantity += initial; // ← accumulate initial quantity
                boxes.Add($"{box} ({scanned}/{initial})");
            }

            TotalScannedQuantity = totalQty;
            // if no multiple boxes were found, show current box info
            if (boxes.Count < 2)
            {
                AllBoxesInfo = $"{BoxId} ({ScannedQuantity}/{InitialQuantity})";
            }
            else
            {
                AllBoxesInfo = string.Join(", ", boxes);
            }
        }

        _originalQuantity = ScannedQuantity;
        HasUnsavedChanges = false;
    }

    private void RecalculateTotals(SqliteConnection conn)
    {
        if (CurrentMode != InventoryMode.Loots || string.IsNullOrEmpty(ProductBarcode))
            return;

        using var totalCmd = conn.CreateCommand();
        totalCmd.CommandText = @"
        SELECT 
            COALESCE(SUM(ScannedQuantity), 0),
            COALESCE(SUM(InitialQuantity), 0)
        FROM LootsProducts
        WHERE Barcode = $b;";
        totalCmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var reader = totalCmd.ExecuteReader();
        if (reader.Read())
        {
            TotalScannedQuantity = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            TotalInitialQuantity = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        }
    }


    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);
    }
}
