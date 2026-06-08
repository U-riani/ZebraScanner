using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IScanLogRepository
    {
        Task InsertAsync(ScanLog log, InventoryMode mode = InventoryMode.Standard);
        Task<IEnumerable<ScanLog>> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard);
        Task ClearAsync(InventoryMode mode = InventoryMode.Standard);
    }

}
