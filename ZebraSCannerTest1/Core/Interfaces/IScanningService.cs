namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IScanningService
    {
        void Enqueue(string barcode);
        Task StartAsync(CancellationToken cancellationToken = default);
        void Stop();
    }
}
