using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Data
{
    public class DbFactory : IDbFactory
    {
        public SqliteConnection Inventorization(InventoryMode mode)
        {

            var dbName = mode == InventoryMode.Loots
                ? "zebraScanner_loots.db"
                : "zebraScanner_standard.db";

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);

            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            DatabaseInitializer.Initialize(conn, mode);
            return conn;
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

}
