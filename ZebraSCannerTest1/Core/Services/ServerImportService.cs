using System.Text;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    /// <summary>
    /// Downloads inventory data from backend and imports into SQLite via IDataImportService.
    /// </summary>
    public class ServerImportService : IServerImportService
    {
        private readonly IApiService _apiService;
        private readonly IDataImportService _importer;

        public ServerImportService(IApiService apiService, IDataImportService importer)
        {
            _apiService = apiService;
            _importer = importer;
        }

        public async Task<int> ImportJsonFromServerAsync(InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                Console.WriteLine($"[SERVER IMPORT] Downloading {mode} JSON data from backend...");

                string json = await _apiService.DownloadInventoryJsonAsync("/download-inventory");
                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidOperationException("No JSON data received from server.");

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                int imported = await _importer.ImportJsonAsync(stream, mode);

                // 🔁 Force full DB reconnection
                var newConn = DatabaseInitializer.GetConnection(mode);
                DatabaseInitializer.Initialize(newConn, mode);

                Console.WriteLine($"----[SERVER IMPORT] ✅ Imported {imported} items and refreshed DB connection for {mode}");
                return imported;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ImportJsonFromServerAsync failed: {ex.Message}");
                throw;
            }
        }

    }
}
