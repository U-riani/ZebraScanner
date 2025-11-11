using Microsoft.Data.Sqlite;
using System.Text;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    public class ServerImportService : IServerImportService
    {
        private readonly IApiService _apiService;
        private readonly IDataImportService _importer;
        private readonly IServiceProvider _provider; // To rebuild repos on reload

        public ServerImportService(IApiService apiService, IDataImportService importer, IServiceProvider provider)
        {
            _apiService = apiService;
            _importer = importer;
            _provider = provider;
        }

        public async Task<int> ImportJsonFromServerAsync(InventoryMode mode = InventoryMode.Standard)
        {
            Console.WriteLine($"[SERVER IMPORT] Downloading {mode} JSON data...");

            string json = await _apiService.DownloadInventoryJsonAsync("/download-inventory");
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("No JSON data received from server.");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            int imported = await _importer.ImportJsonAsync(stream, mode);

            // 💣 Delete and rebuild DB file to flush caches completely
            var dbFile = Path.Combine(FileSystem.AppDataDirectory,
                mode == InventoryMode.Loots ? "zebraScanner_loots.db" : "zebraScanner_standard.db");
            Console.WriteLine($"[SERVER IMPORT] ✅ DB at {dbFile} refreshed for {mode}");

            // 🔁 Force new connection globally
            var newConn = DatabaseInitializer.GetConnection(mode);
            DatabaseInitializer.Initialize(newConn, mode);

            // Replace singleton connection in DI container (optional if needed)
            if (_provider.GetService(typeof(SqliteConnection)) is SqliteConnection oldConn)
            {
                try { oldConn.Close(); oldConn.Dispose(); } catch { }
            }

            Console.WriteLine($"[SERVER IMPORT] ✅ Imported {imported} rows for {mode}");
            return imported;
        }
    }
}
