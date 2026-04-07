using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Helpers;

namespace ZebraSCannerTest1.Data;


    public class DbFactory : IDbFactory
    {
        public async Task<SqliteConnection> Inventorization(InventoryMode mode)
        {
            int userId = await SessionHelper.GetCurrentUserIdAsync();

            return DatabaseInitializer.GetConnection(userId, mode);
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

