using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Core.Enums;


#if ANDROID
using Android.Media;
#endif

namespace ZebraSCannerTest1.Core.Services
{
    public class ScanningService : IScanningService
    {
        private readonly IProductRepository _products;
        private readonly IScanLogRepository _logs;
        private readonly IDialogService _dialogs;
        private readonly ILoggerService<ScanningService> _logger;

        private string CurrentSection => Preferences.Get("CurrentSection", string.Empty);

        private InventoryMode _mode = InventoryMode.Standard;
        private string? _currentBoxId;

        private readonly BlockingCollection<string> _scanQueue = new();
        private Task? _processingTask;
        private CancellationTokenSource? _cts;

#if ANDROID
        private static readonly ToneGenerator toneOk = new(Android.Media.Stream.System, 100);
        private static readonly ToneGenerator toneError = new(Android.Media.Stream.System, 100);
#endif

        public ScanningService(
            IProductRepository products,
            IScanLogRepository logs,
            IDialogService dialogs,
            ILoggerService<ScanningService> logger)
        {
            _products = products;
            _logs = logs;
            _dialogs = dialogs;
            _logger = logger;
        }

        public void SetMode(InventoryMode mode, string? boxId = null)
        {
            _mode = mode;
            _currentBoxId = boxId;
            _logger.Info($"ScanningService mode set → {_mode} (Box: {_currentBoxId ?? "none"})");
        }

        public void Enqueue(string barcode)
        {
            if (!_scanQueue.IsAddingCompleted)
                _scanQueue.Add(barcode);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_processingTask != null && !_processingTask.IsCompleted)
                return _processingTask; // already running

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = Task.Run(ProcessQueueAsync, _cts.Token);
            return _processingTask;
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _scanQueue.CompleteAdding();
                _processingTask?.Wait(200); // ensure graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.Warn("Stop error" + ex);
            }

        }

        private async Task ProcessQueueAsync()
        {
            foreach (var barcode in _scanQueue.GetConsumingEnumerable(_cts!.Token))
            {
                await ProcessAsync(barcode);
            }
        }

        private async Task ProcessAsync(string barcode)
        {
            try
            {
                _logger.Info($"[SCAN] Mode={_mode}, Box={_currentBoxId ?? "null"} Barcode={barcode}");

                var product = await _products.FindAsync(barcode, _mode, _currentBoxId);

                if (product == null)
                {
#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() => toneError.StartTone(Tone.CdmaPip, 200));
#endif
                    bool addNew = await MainThread.InvokeOnMainThreadAsync(async () =>
                        await _dialogs.ConfirmAsync("Unknown Barcode",
                            $"Barcode {barcode} not found.\nAdd it?",
                            "Yes", "No"));

                    if (addNew)
                        await AddNewProductAsync(barcode);
                    else
                        _logger.Warn($"Unknown barcode skipped: {barcode}");
                }
                else
                {
#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() => toneOk.StartTone(Tone.PropAck, 80));
#endif
                    await UpdateProductAsync(product);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing barcode [{barcode}] in {_mode} mode", ex);
            }
        }

        private async Task AddNewProductAsync(string barcode)
        {
            var product = new Product
            {
                Barcode = barcode,
                Box_Id = _mode == InventoryMode.Loots ? _currentBoxId : null,
                InitialQuantity = 0,
                ScannedQuantity = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _products.AddAsync(product, _mode);

            await _logs.InsertAsync(new ScanLog
            {
                Barcode = product.Barcode,
                Was = product.ScannedQuantity - 1,
                IncrementBy = 1,
                IsValue = product.ScannedQuantity,
                UpdatedAt = DateTime.UtcNow,
                Section = _mode == InventoryMode.Loots ? null : CurrentSection,
                Box_Id = _mode == InventoryMode.Loots
        ? (!string.IsNullOrWhiteSpace(_currentBoxId) ? _currentBoxId : "Unassigned")
        : null
            }, _mode == InventoryMode.Loots ? InventoryMode.Loots : InventoryMode.Standard);


            _logger.Info($"New product added ({_mode}) {barcode}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
            });

        }

        private async Task UpdateProductAsync(Product product)
        {
            product.ScannedQuantity++;
            product.UpdatedAt = DateTime.UtcNow;

            await _products.UpdateAsync(product, _mode);

            await _logs.InsertAsync(new ScanLog
            {
                Barcode = product.Barcode,
                Was = product.ScannedQuantity - 1,
                IncrementBy = 1,
                IsValue = product.ScannedQuantity,
                UpdatedAt = DateTime.UtcNow,
                Section = _mode == InventoryMode.Loots ? null : CurrentSection,
                Box_Id = _mode == InventoryMode.Loots
        ? (!string.IsNullOrWhiteSpace(_currentBoxId) ? _currentBoxId : "Unassigned")
        : null
            }, _mode == InventoryMode.Loots ? InventoryMode.Loots : InventoryMode.Standard);


            _logger.Info($"Product scanned ({_mode}) {product.Barcode}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
            });

        }

    }
}
