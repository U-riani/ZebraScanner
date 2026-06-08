using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using ZebraSCannerTest1.Core.Enums;
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
            _currentBoxId = string.IsNullOrWhiteSpace(boxId) ? null : boxId.Trim();
        }

        public void Enqueue(string barcode)
        {
            if (!_scanQueue.IsAddingCompleted)
                _scanQueue.Add(barcode);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_processingTask != null && !_processingTask.IsCompleted)
                return _processingTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = Task.Run(ProcessQueueAsync, _cts.Token);
            return _processingTask;
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _processingTask?.Wait(200);
            }
            catch (Exception ex)
            {
                _logger.Warn("Stop error" + ex);
            }
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                foreach (var rawBarcode in _scanQueue.GetConsumingEnumerable(_cts!.Token))
                {
                    var barcode = NormalizeBarcode(rawBarcode);
                    if (string.IsNullOrWhiteSpace(barcode))
                        continue;

                    await ProcessAsync(barcode);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal app/page shutdown path.
            }
        }

        private async Task ProcessAsync(string barcode)
        {
            try
            {
                var product = await _products.IncrementScanAsync(
                    barcode,
                    _mode,
                    _currentBoxId,
                    _mode == InventoryMode.Loots ? null : CurrentSection,
                    createIfMissing: false);

                if (product is null)
                {
#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() => toneError.StartTone(Tone.CdmaPip, 200));
#endif
                    var addNew = await MainThread.InvokeOnMainThreadAsync(async () =>
                        await _dialogs.ConfirmAsync(
                            "Unknown Barcode",
                            $"Barcode {barcode} not found.\nAdd it?",
                            "Yes",
                            "No"));

                    if (!addNew)
                    {
                        _logger.Warn($"Unknown barcode skipped: {barcode}");
                        return;
                    }

                    product = await _products.IncrementScanAsync(
                        barcode,
                        _mode,
                        _currentBoxId,
                        _mode == InventoryMode.Loots ? null : CurrentSection,
                        createIfMissing: true);
                }

                if (product is null)
                    return;

#if ANDROID
                MainThread.BeginInvokeOnMainThread(() => toneOk.StartTone(Tone.PropAck, 60));
#endif
                await PublishProductUpdatedAsync(product, _mode);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing barcode [{barcode}] in {_mode} mode", ex);
            }
        }

        private static string NormalizeBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return string.Empty;

            return barcode.Trim();
        }

        private static Task PublishProductUpdatedAsync(Product product, InventoryMode mode)
        {
            return MainThread.InvokeOnMainThreadAsync(() =>
            {
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(product, mode));
            });
        }
    }
}
