using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IServerImportService
    {
        Task<int> ImportJsonFromServerAsync(InventoryMode mode = InventoryMode.Standard);
    }
}
