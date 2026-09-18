using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Messages
{
    /// <summary>
    /// Sent only for a successful barcode scan so the scan page can show
    /// Hall/WRH feedback without reacting to unrelated product edits.
    /// </summary>
    public class ScanFeedbackMessage
    {
        public Product Product { get; }

        public ScanFeedbackMessage(Product product)
        {
            Product = product;
        }
    }
}
