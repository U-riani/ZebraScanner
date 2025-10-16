using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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
    public partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly ExcelExportService _exportService;
        private readonly LogBufferService _logBuffer;
        private readonly DataImportService _dataImportService;

        private readonly Queue<string> _scanQueue = new();
        private readonly object _scanLock = new();
        private int _isFlushingScans = 0;

        private readonly Dictionary<string, Product> _cache = new();
        private readonly List<string> _recent = new(capacity: 8);

        private string _currentBarcode;
        private string _showCurrentBarcode;
        private bool _isManualEntryVisible = true;
        private string _importStatusText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<Product> NewProductAdded;

        public const int SlotCount = 8;
        private bool _isNavigating;

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

        private string _currentSection;

        public string CurrentSection
        {
            get => _currentSection;
            set
            {
                if (_currentSection != value)
                {
                    _currentSection = value;
                    Preferences.Set("CurrentSection", value); // Save it persistently
                    OnPropertyChanged();
                }
            }
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
        public ICommand ChangeSectionCommand { get; }


#if ANDROID
        private static readonly ToneGenerator toneOk = new(Android.Media.Stream.System, 100);
        private static readonly ToneGenerator toneError = new(Android.Media.Stream.System, 100);
#endif

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
            _currentSection = Preferences.Get("CurrentSection", null);


            LoadCache();
            LoadRecentIntoSlots();

            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ProductSlot>(OnSlotTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            GoToScannedProductsCommand = new AsyncRelayCommand(OnGoToScannedProductsAsync);
            ImportDataCommand = new AsyncRelayCommand(OnImportDataAsync);
            ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);
            ExportExcelCommand = new AsyncRelayCommand(OnExportExcelAsync);
            ShowResultsCommand = new AsyncRelayCommand(OnShowResultsAsync);
            ChangeSectionCommand = new AsyncRelayCommand(OnChangeSectionAsync);


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

            if (Interlocked.Exchange(ref _isFlushingScans, 1) == 0)
                _ = Task.Run(ProcessScanQueueAsync);
        }

        private async Task ProcessScanQueueAsync()
        {
            System.Diagnostics.Debug.WriteLine("[SCAN QUEUE] Started");
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

                    try
                    {
                        if (!_cache.TryGetValue(nextBarcode, out var product))
                        {
#if ANDROID
                            MainThread.BeginInvokeOnMainThread(() =>
                                toneError.StartTone(Tone.CdmaPip, 200));
#endif
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                bool addNew = await Shell.Current.DisplayAlert(
                                    "Unknown Barcode",
                                    $"Barcode {nextBarcode} not found.\nAdd it?",
                                    "Yes", "No");
                                if (addNew)
                                    await AddNewProductAsync(nextBarcode);
                            });
                            continue;
                        }

                        await UpdateProductAsync(product);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SCAN ERROR] {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SCAN QUEUE FATAL] {ex}");
            }

            finally
            {
                Interlocked.Exchange(ref _isFlushingScans, 0);
                System.Diagnostics.Debug.WriteLine("[SCAN QUEUE] Reset complete");
            }
        }

        private async Task AddNewProductAsync(string barcode)
        {
            try
            {
                using var conn = new SqliteConnection(_conn.ConnectionString);
                conn.Open();

                var product = new Product
                {
                    Barcode = barcode,
                    InitialQuantity = 0,
                    ScannedQuantity = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO Products 
                    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                    VALUES ($b,$i,$s,$c,$u)";
                cmd.Parameters.AddWithValue("$b", product.Barcode);
                cmd.Parameters.AddWithValue("$i", product.InitialQuantity);
                cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
                cmd.Parameters.AddWithValue("$c", product.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();

                _cache[barcode] = product;
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ADD NEW PRODUCT ERROR] {ex}");
            }
        }

        private async Task UpdateProductAsync(Product product)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    product.ScannedQuantity++;
                    product.UpdatedAt = DateTime.UtcNow;

                    using var conn = new SqliteConnection(_conn.ConnectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                        VALUES ($barcode,$initial,$scanned,$created,$updated)
                        ON CONFLICT(Barcode) DO UPDATE SET
                            ScannedQuantity = $scanned,
                            UpdatedAt       = $updated;";
                    cmd.Parameters.AddWithValue("$barcode", product.Barcode);
                    cmd.Parameters.AddWithValue("$initial", product.InitialQuantity);
                    cmd.Parameters.AddWithValue("$scanned", product.ScannedQuantity);
                    cmd.Parameters.AddWithValue("$created", product.CreatedAt.ToString("o"));
                    cmd.Parameters.AddWithValue("$updated", product.UpdatedAt.ToString("o"));
                    cmd.ExecuteNonQuery();

                    _logBuffer.AddLog(new ScanLog
                    {
                        Barcode = product.Barcode,
                        Was = product.ScannedQuantity - 1,
                        IncrementBy = 1,
                        IsValue = product.ScannedQuantity,
                        UpdatedAt = DateTime.UtcNow,
                        IsManual = null,
                        Section = CurrentSection
                    });

#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() =>
                        toneOk.StartTone(Tone.PropAck, 80));
#endif
                    WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
                    break;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempts < 3)
                {
                    attempts++;
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UPDATE PRODUCT ERROR] {ex}");
                    break;
                }
            }
        }

        private async Task OnChangeSectionAsync()
        {
            string result = await Shell.Current.DisplayPromptAsync(
                "Set Section",
                "Enter section name (e.g. Warehouse A, Floor 2, Shelf 5):",
                "OK", "Cancel",
                initialValue: CurrentSection);

            if (!string.IsNullOrWhiteSpace(result))
            {
                CurrentSection = result.Trim();
                await Shell.Current.DisplayAlert("✅ Section Updated", $"Current section: {CurrentSection}", "OK");
            }
        }


        // ===== Import, Export, Navigation (unchanged) =====
        // ... (same as your previous working version) ...
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

                await ShowingLongPopup.ShowAsync("Preparing data...");
                try
                {
                    string ext = Path.GetExtension(result.FileName).ToLowerInvariant();
                    using var stream = await result.OpenReadAsync();
                    await Task.Run(async () =>
                    {
                        if (ext == ".json")
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await ShowingLongPopup.UpdateMessageAsync("Importing JSON data..."));
                            await _dataImportService.ImportJsonAsync(stream);
                            ImportStatusText = "✅ JSON import complete";
                        }
                        else if (ext == ".xlsx")
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await ShowingLongPopup.UpdateMessageAsync("Importing Excel data..."));
                            await _dataImportService.ImportExcelAsync(stream, result.FileName);
                            ImportStatusText = "✅ Excel import complete";
                        }
                        else if (ext == ".db")
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await ShowingLongPopup.UpdateMessageAsync("Importing database..."));
                            await _dataImportService.ImportDbAsync(stream);
                            ImportStatusText = "✅ Database import complete";
                        }
                        else
                        {
                            throw new InvalidOperationException("Select a valid .xlsx, .json, or .db file.");
                        }
                    });

                    ResetAndReload();
                }
                finally
                {
                    await ShowingLongPopup.CloseAsync();
                }
                await Shell.Current.DisplayAlert("✅ Success", "Import finished successfully!", "OK");
            }
            catch (Exception ex)
            {
                await ShowingLongPopup.CloseAsync();
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
                    ["IsReadOnly"] = false
                };
                await Shell.Current.GoToAsync(nameof(DetailsPage), query);
            }
        }

        private async Task OnExportExcelAsync()
        {
            try
            {
                var exportChoice = await Shell.Current.DisplayActionSheet(
                    "Export Type",
                    "Cancel", null,
                    "Export Products",
                    "Export Logs");

                if (string.IsNullOrEmpty(exportChoice) || exportChoice == "Cancel")
                    return;

                await ShowingLongPopup.ShowAsync("Preparing export...");

#if ANDROID
        var path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
                var path = FileSystem.AppDataDirectory;
#endif

                string name;
                string fullPath;
                var progress = new Progress<double>(p =>
                {
                    ImportStatusText = $"Exporting... {(int)(p * 100)}%";
                });

                if (exportChoice == "Export Products")
                {
                    name = $"ProductsExport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                    fullPath = Path.Combine(path, name);
                    await Task.Run(async () =>
                    {
                        await _exportService.ExportProductsAsync(fullPath, progress);
                    });
                }
                else // Export Logs
                {
                    name = $"LogsExport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                    fullPath = Path.Combine(path, name);
                    var logExport = new ExcelExportLogsService(_conn);
                    await Task.Run(async () =>
                    {
                        await logExport.ExportLogsAsync(fullPath, progress);
                    });
                }

                await ShowingLongPopup.CloseAsync();
                await Shell.Current.DisplayAlert("✅ Export Complete", $"File saved:\n{name}", "OK");
            }
            catch (Exception ex)
            {
                await ShowingLongPopup.CloseAsync();
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

        private async Task OnGoToLogsAsync()
        {
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                await Shell.Current.GoToAsync(nameof(LogsPage));
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private async Task OnGoToScannedProductsAsync()
        {
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                await Shell.Current.GoToAsync(nameof(ScannedProductsPage),
                    new Dictionary<string, object> { ["ForceReload"] = true });
            }
            finally
            {
                _isNavigating = false;
            }
        }

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

        public void Dispose()
        {
            _conn?.Dispose();
#if ANDROID
            toneOk.Release();
            toneError.Release();
#endif
        }
    }
}
