using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SqliteConnection _conn;
        private readonly ExcelImportService _importService;
        private readonly ExcelExportService _exportService;
        private readonly LogBufferService _logBuffer;

        // scan queue for thread-safe fast ingestion
        private readonly Queue<string> _scanQueue = new();
        private readonly object _scanLock = new();
        private bool _isFlushingScans = false;

        // cache for all products by barcode
        private readonly Dictionary<string, Product> _cache = new();

        // recent list that feeds the 8 static slots (most recent first)
        private readonly List<string> _recent = new(capacity: 8);

        // prepared UPSERT command
        private readonly SqliteCommand _upsertProductCmd;

        private string _currentBarcode;
        private string _showCurrentBarcode;
        private bool _isManualEntryVisible = true;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<Product> NewProductAdded; // kept for compatibility, but not used for scrolling now

        public const int SlotCount = 8;

        // 8 static slots for the UI
        public ObservableCollection<ProductSlot> Slots { get; } = new(
            Enumerable.Range(0, SlotCount).Select(_ => new ProductSlot())
        );

        public IAsyncRelayCommand ExportExcelCommand { get; }

        public MainViewModel(SqliteConnection conn, ExcelImportService importService, ExcelExportService exportService, LogBufferService logBuffer)
        {
            _conn = conn;
            _importService = importService;
            _exportService = exportService;
            _logBuffer = logBuffer;

            // prepare UPSERT
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

            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ProductSlot>(OnSlotTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            GoToScannedProductsCommand = new AsyncRelayCommand(OnGoToScannedProductsAsync);
            ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);
            ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);

            // if details page updates something, refresh cache and slots
            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                _cache[m.Product.Barcode] = m.Product;

                // move updated product to top of recent list
                int existing = _recent.IndexOf(m.Product.Barcode);
                if (existing >= 0) _recent.RemoveAt(existing);
                _recent.Insert(0, m.Product.Barcode);
                if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

                // refresh all slots
                for (int i = 0; i < SlotCount; i++)
                {
                    if (i < _recent.Count)
                        UpdateSlotFromCache(i, _recent[i]);
                    else
                        ClearSlot(i);
                }
            });

            ExportExcelCommand = new AsyncRelayCommand(OnExportExcelAsync);
        }

        // =========== Bindables ===========
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

        // =========== Commands ===========
        public IAsyncRelayCommand ImportExcelCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand GoToDetailsCommand { get; }
        public ICommand GoToLogsCommand { get; }
        public ICommand GoToScannedProductsCommand { get; }
        public ICommand ToggleManualEntryCommand { get; }

        // =========== Data Loaders ===========
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
            if (!_cache.TryGetValue(barcode, out var p))
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products WHERE Barcode=$b";
                cmd.Parameters.AddWithValue("$b", barcode);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    p = new Product
                    {
                        Barcode = barcode,
                        InitialQuantity = r.GetInt32(0),
                        ScannedQuantity = r.GetInt32(1),
                        CreatedAt = DateTime.Parse(r.GetString(2)),
                        UpdatedAt = DateTime.Parse(r.GetString(3))
                    };
                    _cache[barcode] = p;
                }
                else
                {
                    ClearSlot(slotIndex);
                    return;
                }
            }

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

        // =========== Scanning ===========
        public async Task AddProductAsync(string scannedBarcode)
        {
            scannedBarcode = scannedBarcode?.Trim();
            if (string.IsNullOrEmpty(scannedBarcode)) return;

            lock (_scanLock) _scanQueue.Enqueue(scannedBarcode);

            ShowCurrentBarcode = scannedBarcode;

            if (!_isFlushingScans)
                _ = Task.Run(ProcessScanQueueAsync);
        }

        //private async Task ProcessScanQueueAsync()
        //{
        //    _isFlushingScans = true;
        //    try
        //    {
        //        while (true)
        //        {
        //            string nextBarcode = null;
        //            lock (_scanLock)
        //            {
        //                if (_scanQueue.Count > 0)
        //                    nextBarcode = _scanQueue.Dequeue();
        //            }
        //            if (nextBarcode == null) break;

        //            if (!_cache.TryGetValue(nextBarcode, out var product))
        //            {
        //                product = new Product
        //                {
        //                    Barcode = nextBarcode,
        //                    InitialQuantity = 0,
        //                    ScannedQuantity = 0,
        //                    CreatedAt = DateTime.UtcNow,
        //                    UpdatedAt = DateTime.UtcNow
        //                };
        //                _cache[nextBarcode] = product;
        //            }

        //            product.ScannedQuantity++;
        //            product.UpdatedAt = DateTime.UtcNow;

        //            _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
        //            _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
        //            _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
        //            _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
        //            _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
        //            _upsertProductCmd.ExecuteNonQuery();

        //            var log = new ScanLog
        //            {
        //                Barcode = product.Barcode,
        //                ScannedQuantity = product.ScannedQuantity,
        //                Timestamp = DateTime.UtcNow
        //            };
        //            _logBuffer.AddLog(log);
        //            WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));

        //            int existing = _recent.IndexOf(nextBarcode);
        //            if (existing >= 0) _recent.RemoveAt(existing);
        //            _recent.Insert(0, nextBarcode);
        //            if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

        //            for (int i = 0; i < SlotCount; i++)
        //            {
        //                if (i < _recent.Count)
        //                    UpdateSlotFromCache(i, _recent[i]);
        //                else
        //                    ClearSlot(i);
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        _isFlushingScans = false;
        //    }
        //}


        //        private async Task ProcessScanQueueAsync()
        //        {
        //            _isFlushingScans = true;
        //            try
        //            {
        //                while (true)
        //                {
        //                    string nextBarcode = null;
        //                    lock (_scanLock)
        //                    {
        //                        if (_scanQueue.Count > 0)
        //                            nextBarcode = _scanQueue.Dequeue();
        //                    }
        //                    if (nextBarcode == null) break;

        //                    Product product;

        //                    if (!_cache.TryGetValue(nextBarcode, out product))
        //                    {
        //                        // 🔹 Ask user if they want to add unknown barcode
        //                        bool addNew = await MainThread.InvokeOnMainThreadAsync(async () =>
        //                        {
        //                            return await Shell.Current.DisplayAlert(
        //                                "Unknown Barcode",
        //                                $"Barcode {nextBarcode} was not found in the database.\n\nDo you want to add it?",
        //                                "Yes", "No");
        //                        });

        //                        if (!addNew)
        //                            continue; // ❌ Skip this barcode if user said No

        //                        // ✅ Create and insert a new product
        //                        product = new Product
        //                        {
        //                            Barcode = nextBarcode,
        //                            InitialQuantity = 0,
        //                            ScannedQuantity = 0,
        //                            CreatedAt = DateTime.UtcNow,
        //                            UpdatedAt = DateTime.UtcNow
        //                        };
        //                        _cache[nextBarcode] = product;

        //                        using var insertCmd = _conn.CreateCommand();
        //                        insertCmd.CommandText = @"
        //INSERT OR IGNORE INTO Products 
        //(Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
        //VALUES ($barcode, $initial, $scanned, $created, $updated)";
        //                        insertCmd.Parameters.AddWithValue("$barcode", product.Barcode);
        //                        insertCmd.Parameters.AddWithValue("$initial", product.InitialQuantity);
        //                        insertCmd.Parameters.AddWithValue("$scanned", product.ScannedQuantity);
        //                        insertCmd.Parameters.AddWithValue("$created", product.CreatedAt.ToString("o"));
        //                        insertCmd.Parameters.AddWithValue("$updated", product.UpdatedAt.ToString("o"));
        //                        insertCmd.ExecuteNonQuery();
        //                    }

        //                    // 🔹 Increase scanned quantity
        //                    product.ScannedQuantity++;
        //                    product.UpdatedAt = DateTime.UtcNow;

        //                    _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
        //                    _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
        //                    _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
        //                    _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
        //                    _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
        //                    _upsertProductCmd.ExecuteNonQuery();

        //                    var log = new ScanLog
        //                    {
        //                        Barcode = product.Barcode,
        //                        ScannedQuantity = product.ScannedQuantity,
        //                        Timestamp = DateTime.UtcNow
        //                    };
        //                    _logBuffer.AddLog(log);
        //                    WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));

        //// success case
        //#if ANDROID
        //var toneOk = new ToneGenerator(Android.Media.Stream.System, 100);
        //toneOk.StartTone(Tone.PropAck, 100); // short beep for success
        //#endif

        //                                        int existing = _recent.IndexOf(nextBarcode);
        //                    if (existing >= 0) _recent.RemoveAt(existing);
        //                    _recent.Insert(0, nextBarcode);
        //                    if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

        //                    for (int i = 0; i < SlotCount; i++)
        //                    {
        //                        if (i < _recent.Count)
        //                            UpdateSlotFromCache(i, _recent[i]);
        //                        else
        //                            ClearSlot(i);
        //                    }
        //                }
        //            }
        //            finally
        //            {
        //                _isFlushingScans = false;
        //            }
        //        }

        // =========== Navigation & Import ===========

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

                    Product product;

                    if (!_cache.TryGetValue(nextBarcode, out product))
                    {
                        // 🔹 Ask user if they want to add unknown barcode
                        bool addNew = await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
#if ANDROID
                    // ❌ Play error beep
                    var toneError = new Android.Media.ToneGenerator(Android.Media.Stream.System, 100);
                    toneError.StartTone(Android.Media.Tone.CdmaPip, 1000);
#endif
                            return await Shell.Current.DisplayAlert(
                                "Unknown Barcode",
                                $"Barcode {nextBarcode} was not found in the database.\n\nDo you want to add it?",
                                "Yes", "No");
                        });

                        if (!addNew)
                        {
                            continue; // Skip this barcode
                        }

                        // ✅ Create and insert a new product
                        product = new Product
                        {
                            Barcode = nextBarcode,
                            InitialQuantity = 0,
                            ScannedQuantity = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _cache[nextBarcode] = product;

                        using var insertCmd = _conn.CreateCommand();
                        insertCmd.CommandText = @"
INSERT OR IGNORE INTO Products 
(Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
VALUES ($barcode, $initial, $scanned, $created, $updated)";
                        insertCmd.Parameters.AddWithValue("$barcode", product.Barcode);
                        insertCmd.Parameters.AddWithValue("$initial", product.InitialQuantity);
                        insertCmd.Parameters.AddWithValue("$scanned", product.ScannedQuantity);
                        insertCmd.Parameters.AddWithValue("$created", product.CreatedAt.ToString("o"));
                        insertCmd.Parameters.AddWithValue("$updated", product.UpdatedAt.ToString("o"));
                        insertCmd.ExecuteNonQuery();
                    }

                    // 🔹 Increase scanned quantity
                    product.ScannedQuantity++;
                    product.UpdatedAt = DateTime.UtcNow;

                    _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                    _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                    _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                    _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                    _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                    _upsertProductCmd.ExecuteNonQuery();

                    var log = new ScanLog
                    {
                        Barcode = product.Barcode,
                        ScannedQuantity = product.ScannedQuantity,
                        Timestamp = DateTime.UtcNow
                    };
                    _logBuffer.AddLog(log);
                    WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));

                    int existing = _recent.IndexOf(nextBarcode);
                    if (existing >= 0) _recent.RemoveAt(existing);
                    _recent.Insert(0, nextBarcode);
                    if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

                    for (int i = 0; i < SlotCount; i++)
                    {
                        if (i < _recent.Count)
                            UpdateSlotFromCache(i, _recent[i]);
                        else
                            ClearSlot(i);
                    }

#if ANDROID
            // ✅ Play short success beep
            var toneOk = new Android.Media.ToneGenerator(Android.Media.Stream.System, 100);
            toneOk.StartTone(Android.Media.Tone.PropAck, 100);
#endif
                }
            }
            finally
            {
                _isFlushingScans = false;
            }
        }


        private async Task OnImportExcelAsync()
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Excel File",
                FileTypes = FileTypes.Excel
            });
            if (result == null) return;

            await _importService.ImportExcelAsync(result.FullPath);
           
            // 🔥 Clear in-memory logs too
            _logBuffer.Clear();

            // 🔥 reset in-memory state for a truly fresh start
            _cache.Clear();
            _recent.Clear();
            foreach (var slot in Slots)
            {
                slot.Barcode = string.Empty;
                slot.InitialQuantity = 0;
                slot.ScannedQuantity = 0;
            }

            LoadCache();
            LoadRecentIntoSlots();
        }

        private async Task OnSlotTappedAsync(ProductSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Barcode)) return;

            // hydrate product from cache
            if (_cache.TryGetValue(slot.Barcode, out var product))
            {
                await Shell.Current.GoToAsync(
                    $"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.ScannedQuantity}&InitialQuantity={product.InitialQuantity}");
            }
        }

        private async Task OnExportExcelAsync()
        {
            // 🔹 Ask user first
            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Export",
                "Do you want to export products to Excel?",
                "Yes", "No");

            if (!confirm)
                return; // ❌ User canceled

#if ANDROID
    var downloadsPath = Android.OS.Environment
        .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)
        .AbsolutePath;

    var fileName = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
    var exportPath = Path.Combine(downloadsPath, fileName);
#else
            var fileName = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            var exportPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
#endif

            // ✅ Do the export
            await _exportService.ExportProductsAsync(exportPath);

            Console.WriteLine($"[DOTNET] ✅ Export complete. File saved at {exportPath}");

            await Shell.Current.DisplayAlert(
                "Export Complete",
                $"File saved in Downloads:\n{fileName}",
                "OK"
            );
        }

        private async Task OnGoToLogsAsync() =>
            await Shell.Current.GoToAsync(nameof(LogsPage));

        private async Task OnGoToScannedProductsAsync() =>
            await Shell.Current.GoToAsync(nameof(ScannedProductsPage));

        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
