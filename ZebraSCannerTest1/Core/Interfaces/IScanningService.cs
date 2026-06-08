using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IScanningService
    {
        void Enqueue(string barcode);
        Task StartAsync(CancellationToken cancellationToken = default);
        void Stop();
        void SetMode(InventoryMode mode, string? boxId = null);
    }
}
