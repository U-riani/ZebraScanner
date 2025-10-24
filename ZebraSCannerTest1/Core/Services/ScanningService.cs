using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Messages;

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
        private readonly string _currentSection = Preferences.Get("CurrentSection", string.Empty);

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
                _scanQueue.CompleteAdding();
                _cts?.Cancel();
            }
            catch { /* ignore */ }
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
                var product = await _products.FindAsync(barcode);

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
                _logger.Error("Error processing barcode", ex);
            }
        }

        private async Task AddNewProductAsync(string barcode)
        {
            var product = new Product
            {
                Barcode = barcode,
                InitialQuantity = 0,
                ScannedQuantity = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _products.AddAsync(product);
            await _logs.InsertAsync(new ScanLog
            {
                Barcode = barcode,
                Was = 0,
                IncrementBy = 1,
                IsValue = 1,
                UpdatedAt = DateTime.UtcNow,
                Section = _currentSection
            });

            _logger.Info($"New product added: {barcode}");
            WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
        }

        private async Task UpdateProductAsync(Product product)
        {
            product.ScannedQuantity++;
            product.UpdatedAt = DateTime.UtcNow;
            await _products.UpdateAsync(product);

            await _logs.InsertAsync(new ScanLog
            {
                Barcode = product.Barcode,
                Was = product.ScannedQuantity - 1,
                IncrementBy = 1,
                IsValue = product.ScannedQuantity,
                UpdatedAt = DateTime.UtcNow,
                Section = _currentSection
            });

            _logger.Info($"Product scanned: {product.Barcode}");
            WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product));
        }
    }
}
