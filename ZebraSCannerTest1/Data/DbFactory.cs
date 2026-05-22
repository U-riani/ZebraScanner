using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Helpers;

namespace ZebraSCannerTest1.Data;

public class DbFactory : IDbFactory
{
    public async Task<SqliteConnection> Inventorization(InventoryMode mode)
    {
        int userId = await SessionHelper.GetCurrentUserIdAsync();
        var context = SessionHelper.GetCurrentScanContext(mode);

        if (context == null)
        {
            // Legacy/manual fallback. Normal Pocket document scanning should set context from InventorizationMenuViewModel.
            return DatabaseInitializer.GetConnection(userId, mode);
        }

        return DatabaseInitializer.GetConnection(
            userId,
            mode,
            serverKey: context.ServerKey,
            documentId: context.DocumentId,
            documentModule: context.Module,
            assignmentRole: context.Role);
    }

    public SqliteConnection Sales()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "zebraScanner_Sales.db");

        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        SalesDatabaseInitializer.InitializeConnection(conn);
        return conn;
    }
}
