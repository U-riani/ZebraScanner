using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.Views;

#if ANDROID
using Android.Media;
#endif

namespace ZebraSCannerTest1.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly SqliteConnection _conn;
        private readonly ExcelExportService _exportService;
        private readonly LogBufferService _logBuffer;
        private readonly DataImportService _dataImportService;

        private readonly Queue<string> _scanQueue = new();
        private readonly object _scanLock = new();
        private bool _isFlushingScans = false;

        private readonly Dictionary<string, Product> _cache = new();
        private readonly List<string> _recent = new(capacity: 8);
        private readonly SqliteCommand _upsertProductCmd;

        private string _currentBarcode;
        private string _showCurrentBarcode;
        private bool _isManualEntryVisible = true;
        private bool _isBusy;
        private double _importProgress;
        private string _importStatusText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<Product> NewProductAdded;

        public const int SlotCount = 8;

        public string ImportStatusText
        {
            get => _importStatusText;
            set { _importStatusText = value; OnPropertyChanged(); }
        }

        public string CurrentBarcode
        {
            get => _currentBarcode;
            set { _currentBarcode = value; OnPropertyChanged(); }
        }

        public string ShowCurrentBarcode
        {
            get => _showCurrentBarcode;
            set { _showCurrentBarcode = value; OnPropertyChanged(); }
        }

        public bool IsManualEntryVisible
        {
            get => _isManualEntryVisible;
            set { _isManualEntryVisible = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ProductSlot> Slots { get; } =
            new(Enumerable.Range(0, SlotCount).Select(_ => new ProductSlot()));

        public IAsyncRelayCommand ExportExcelCommand { get; }
        public IAsyncRelayCommand ImportDataCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand GoToDetailsCommand { get; }
        public ICommand GoToLogsCommand { get; }
        public ICommand GoToScannedProductsCommand { get; }
        public ICommand ToggleManualEntryCommand { get; }
        public ICommand ShowResultsCommand { get; }


        public MainViewModel(
            SqliteConnection conn,
            ExcelImportService importService,
            ExcelExportService exportService,
            LogBufferService logBuffer)
        {
            _conn = conn;
            _exportService = exportService;
            _logBuffer = logBuffer;
            _dataImportService = new DataImportService(conn);
            
    //            // ✅ Ensure required tables exist (especially ScanLogs)
    //        using (var cmd = _conn.CreateCommand())
    //{
    //    cmd.CommandText = @"
    //        CREATE TABLE IF NOT EXISTS ScanLogs (
    //            Id INTEGER PRIMARY KEY AUTOINCREMENT,
    //            Barcode TEXT NOT NULL,
    //            Quantity INTEGER DEFAULT 1,
    //            ScannedAt TEXT NOT NULL
                
    //        );

    //        CREATE TABLE IF NOT EXISTS Products (
    //            Barcode TEXT PRIMARY KEY,
    //            InitialQuantity INTEGER NOT NULL DEFAULT 0,
    //            ScannedQuantity INTEGER NOT NULL DEFAULT 0,
    //            CreatedAt TEXT NOT NULL,
    //            UpdatedAt TEXT NOT NULL,
    //            Name TEXT,
    //            Color TEXT,
    //            Size TEXT,
    //            Price TEXT,
    //            ArticCode TEXT
    //        );";
    //    cmd.ExecuteNonQuery();
    //}


            // SQL command for upserting
            _upsertProductCmd = _conn.CreateCommand();
            _upsertProductCmd.CommandText = @"
                INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                VALUES ($barcode,$initial,$scanned,$created,$updated)
                ON CONFLICT(Barcode) DO UPDATE SET
                    ScannedQuantity = $scanned,
                    UpdatedAt       = $updated;";
            _upsertProductCmd.Parameters.Add("$barcode", SqliteType.Text);
            _upsertProductCmd.Parameters.Add("$initial", SqliteType.Integer);
            _upsertProductCmd.Parameters.Add("$scanned", SqliteType.Integer);
            _upsertProductCmd.Parameters.Add("$created", SqliteType.Text);
            _upsertProductCmd.Parameters.Add("$updated", SqliteType.Text);

            LoadCache();
            LoadRecentIntoSlots();

            // Commands
            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ProductSlot>(OnSlotTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            GoToScannedProductsCommand = new AsyncRelayCommand(OnGoToScannedProductsAsync);
            ImportDataCommand = new AsyncRelayCommand(OnImportDataAsync);
            ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);
            ExportExcelCommand = new AsyncRelayCommand(OnExportExcelAsync);

            // Messenger to refresh slots
            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                _cache[m.Product.Barcode] = m.Product;

                int existing = _recent.IndexOf(m.Product.Barcode);
                if (existing >= 0) _recent.RemoveAt(existing);
                _recent.Insert(0, m.Product.Barcode);
                if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

                for (int i = 0; i < SlotCount; i++)
                {
                    if (i < _recent.Count)
                        UpdateSlotFromCache(i, _recent[i]);
                    else
                        ClearSlot(i);
                }
            });

            ShowResultsCommand = new AsyncRelayCommand(OnShowResultsAsync);

        }

        // ===== Loaders =====
        private void LoadCache()
        {
            _cache.Clear();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var p = new Product
                {
                    Barcode = r.GetString(0),
                    InitialQuantity = r.GetInt32(1),
                    ScannedQuantity = r.GetInt32(2),
                    CreatedAt = DateTime.Parse(r.GetString(3)),
                    UpdatedAt = DateTime.Parse(r.GetString(4))
                };
                _cache[p.Barcode] = p;
            }
        }

        private void LoadRecentIntoSlots()
        {
            _recent.Clear();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT Barcode FROM Products ORDER BY UpdatedAt DESC LIMIT 8";
            using var r = cmd.ExecuteReader();
            while (r.Read()) _recent.Add(r.GetString(0));

            for (int i = 0; i < SlotCount; i++)
            {
                if (i < _recent.Count)
                    UpdateSlotFromCache(i, _recent[i]);
                else
                    ClearSlot(i);
            }
        }

        private void UpdateSlotFromCache(int slotIndex, string barcode)
        {
            if (!_cache.TryGetValue(barcode, out var p)) return;

            var slot = Slots[slotIndex];
            MainThread.BeginInvokeOnMainThread(() =>
            {
                slot.Barcode = p.Barcode;
                slot.InitialQuantity = p.InitialQuantity;
                slot.ScannedQuantity = p.ScannedQuantity;
            });
        }

        private void ClearSlot(int slotIndex)
        {
            var slot = Slots[slotIndex];
            MainThread.BeginInvokeOnMainThread(() =>
            {
                slot.Barcode = string.Empty;
                slot.InitialQuantity = 0;
                slot.ScannedQuantity = 0;
            });
        }

        // ===== Scanning =====
        public async Task AddProductAsync(string scannedBarcode)
        {
            scannedBarcode = scannedBarcode?.Trim();
            if (string.IsNullOrEmpty(scannedBarcode)) return;

            lock (_scanLock) _scanQueue.Enqueue(scannedBarcode);
            ShowCurrentBarcode = scannedBarcode;

            if (!_isFlushingScans)
                _ = Task.Run(ProcessScanQueueAsync);
        }

        private async Task ProcessScanQueueAsync()
        {
            _isFlushingScans = true;
            try
            {
                while (true)
                {
                    string nextBarcode = null;
                    lock (_scanLock)
                    {
                        if (_scanQueue.Count > 0)
                            nextBarcode = _scanQueue.Dequeue();
                    }
                    if (nextBarcode == null) break;

                    if (!_cache.TryGetValue(nextBarcode, out var product))
                    {
#if ANDROID
                        var toneError = new ToneGenerator(Android.Media.Stream.System, 100);
                        toneError.StartTone(Tone.CdmaPip, 300);
#endif
                        bool addNew = await MainThread.InvokeOnMainThreadAsync(async () =>
                            await Shell.Current.DisplayAlert("Unknown Barcode",
                                $"Barcode {nextBarcode} not found.\nAdd it?", "Yes", "No"));
                        if (!addNew) continue;

                        product = new Product
                        {
                            Barcode = nextBarcode,
                            InitialQuantity = 0,
                            ScannedQuantity = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        using var cmd = _conn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO Products 
                            (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                            VALUES ($barcode,$initial,$scanned,$created,$updated)";
                        cmd.Parameters.AddWithValue("$barcode", product.Barcode);
                        cmd.Parameters.AddWithValue("$initial", product.InitialQuantity);
                        cmd.Parameters.AddWithValue("$scanned", product.ScannedQuantity);
                        cmd.Parameters.AddWithValue("$created", product.CreatedAt.ToString("o"));
                        cmd.Parameters.AddWithValue("$updated", product.UpdatedAt.ToString("o"));
                        cmd.ExecuteNonQuery();
                        _cache[nextBarcode] = product;
                    }

                    product.ScannedQuantity++;
                    product.UpdatedAt = DateTime.UtcNow;

                    _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                    _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                    _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                    _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                    _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                    _upsertProductCmd.ExecuteNonQuery();
                   
                    _logBuffer.AddLog(new ScanLog
                    {
                        Barcode = product.Barcode,
                        Was = product.ScannedQuantity - 1,
                        IncrementBy = 1,
                        IsValue = product.ScannedQuantity,
                        UpdatedAt = DateTime.UtcNow,
                        IsManual = null
                    });


#if ANDROID
                    var toneOk = new ToneGenerator(Android.Media.Stream.System, 100);
                    toneOk.StartTone(Tone.PropAck, 100);
#endif

                    WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
                }
            }
            finally { _isFlushingScans = false; }
        }

        // ===== Import with Progress =====
        private async Task OnImportDataAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select File to Import",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "*/*" } }
                    })
                });

                if (result == null) return;

                // ✅ Show popup before import begins
                await ShowingLongPopup.ShowAsync("Importing data...");


                string ext = Path.GetExtension(result.FileName).ToLowerInvariant();
                using var stream = await result.OpenReadAsync();
                if (ext != ".xlsx" && ext != ".json" && ext != ".db")
                {
                    await Shell.Current.DisplayAlert("Invalid File", "Select .xlsx, .json, or .db file.", "OK");
                    return;
                }


                if (ext == ".json")
                {
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var items = JsonSerializer.Deserialize<List<JsonProduct>>(json) ?? new();
                    int total = items.Count;

                    using var tx = _conn.BeginTransaction();
                    using var cmd = _conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Products
                        (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                        VALUES ($b,$i,$s,$c,$u,$n,$co,$si,$p,$a)";
                    cmd.Parameters.Add("$b", SqliteType.Text);
                    cmd.Parameters.Add("$i", SqliteType.Integer);
                    cmd.Parameters.Add("$s", SqliteType.Integer);
                    cmd.Parameters.Add("$c", SqliteType.Text);
                    cmd.Parameters.Add("$u", SqliteType.Text);
                    cmd.Parameters.Add("$n", SqliteType.Text);
                    cmd.Parameters.Add("$co", SqliteType.Text);
                    cmd.Parameters.Add("$si", SqliteType.Text);
                    cmd.Parameters.Add("$p", SqliteType.Text);
                    cmd.Parameters.Add("$a", SqliteType.Text);

                    foreach (var p in items)
                    {
                        cmd.Parameters["$b"].Value = p.Barcode ?? "";
                        cmd.Parameters["$i"].Value = p.InitialQuantity;
                        cmd.Parameters["$s"].Value = p.ScannedQuantity;
                        cmd.Parameters["$c"].Value = p.CreatedAt ?? DateTime.UtcNow.ToString("o");
                        cmd.Parameters["$u"].Value = p.UpdatedAt ?? DateTime.UtcNow.ToString("o");
                        cmd.Parameters["$n"].Value = p.Name ?? "";
                        cmd.Parameters["$co"].Value = p.Color ?? "";
                        cmd.Parameters["$si"].Value = p.Size ?? "";
                        cmd.Parameters["$p"].Value = p.Price ?? "";
                        cmd.Parameters["$a"].Value = p.ArticCode ?? "";
                        cmd.ExecuteNonQuery();

                    }

                    tx.Commit();
                }
                else if (ext == ".xlsx")
                {
                    ImportStatusText = "Importing Excel...";

                    // ✅ Copy to safe local storage before importing (Pixel fix)
                    var tempFile = Path.Combine(FileSystem.AppDataDirectory, result.FileName);

                    using (var localFile = File.Create(tempFile))
                        await stream.CopyToAsync(localFile);

                    // close SAF stream
                    await stream.DisposeAsync();

                    // reopen from local copy as normal FileStream (seekable)
                    using var localStream = File.OpenRead(tempFile);
                    await _dataImportService.ImportExcelAsync(localStream, result.FileName);
                }
                else if (ext == ".db")
                {
                    ImportStatusText = "Importing DB file...";
                    await _dataImportService.ImportDbAsync(stream);
                }

                ResetAndReload();

                ImportStatusText = "✅ Import Complete";

                // ✅ Close popup
                await ShowingLongPopup.CloseAsync();

                await Shell.Current.DisplayAlert("✅ Success", "Import finished successfully!", "OK");
            }
            catch (Exception ex)
            {
                await ShowingLongPopup.CloseAsync(); // close popup even on error
                await Shell.Current.DisplayAlert("❌ Import Error", ex.Message, "OK");
            }
        
        }

        // ===== Navigation & Reset =====
        private async Task OnSlotTappedAsync(ProductSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Barcode)) return;

            if (_cache.TryGetValue(slot.Barcode, out var product))
            {
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

        private async Task OnExportExcelAsync()
        {
            try
            {
                // ✅ Ask user for confirmation first
                bool confirm = await Shell.Current.DisplayAlert(
                    "Confirm Export",
                    "Are you sure you want to export all product data to Excel?",
                    "Yes", "No");

                if (!confirm)
                    return; // cancel export

                // 🌀 Show simple loading popup
                await ShowingLongPopup.ShowAsync("Exporting data...");

#if ANDROID
        var path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
                var path = FileSystem.AppDataDirectory;
#endif
                var name = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                var fullPath = Path.Combine(path, name);

                // 🧠 Perform export
                await _exportService.ExportProductsAsync(fullPath);

                // ✅ Dismiss popup
                await ShowingLongPopup.CloseAsync();

                await Shell.Current.DisplayAlert("✅ Export Complete", $"File saved:\n{name}", "OK");
            }
            catch (Exception ex)
            {
                await ShowingLongPopup.CloseAsync(); // ensure closed on error
                await Shell.Current.DisplayAlert("❌ Export Error", ex.Message, "OK");
            }
        }


        private async Task OnShowResultsAsync()
        {
            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = @"
            SELECT 
                SUM(InitialQuantity), 
                SUM(ScannedQuantity),
                COUNT(*) AS TotalBarcodes,
                SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END) AS ScannedBarcodes
            FROM Products";

                using var reader = cmd.ExecuteReader();

                int totalInitial = 0, totalScanned = 0, totalBarcodes = 0, scannedBarcodes = 0;

                if (reader.Read())
                {
                    totalInitial = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    totalScanned = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    totalBarcodes = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    scannedBarcodes = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                }

                int quantityDiff = totalScanned - totalInitial;
                int barcodeDiff = scannedBarcodes - totalBarcodes;

                string msg =
                    "📊 Inventory Summary\n\n" +
                    $"🔹 Quantities:\n" +
                    $"• Scanned Total Qty: {totalScanned:N0}\n" +
                    $"• Expected Total Qty: {totalInitial:N0}\n" +
                    $"• Difference: {quantityDiff:N0}\n\n" +
                    $"🔹 Barcodes:\n" +
                    $"• Scanned Barcodes: {scannedBarcodes:N0}\n" +
                    $"• Total Barcodes: {totalBarcodes:N0}\n" +
                    $"• Difference: {scannedBarcodes - totalBarcodes:N0}";

                await Shell.Current.DisplayAlert("📦 Inventory Results", msg, "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("❌ Error", ex.Message, "OK");
            }
        }



        private async Task OnGoToLogsAsync() =>
            await Shell.Current.GoToAsync(nameof(LogsPage));

        private async Task OnGoToScannedProductsAsync() =>
            await Shell.Current.GoToAsync(nameof(ScannedProductsPage));

        private void ResetAndReload()
        {
            _cache.Clear();
            _recent.Clear();
            foreach (var s in Slots)
            {
                s.Barcode = "";
                s.InitialQuantity = 0;
                s.ScannedQuantity = 0;
            }
            LoadCache();
            LoadRecentIntoSlots();
        }

        private void OnPropertyChanged([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class JsonProduct
    {
        public string Barcode { get; set; }
        public int InitialQuantity { get; set; }
        public int ScannedQuantity { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public string Price { get; set; }
        public string ArticCode { get; set; }
    }
}
