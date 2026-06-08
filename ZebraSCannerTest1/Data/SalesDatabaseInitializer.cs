using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Data
{
    public static class SalesDatabaseInitializer
    {
        private const string SalesDb = "zebraScanner_Sales.db";
        public static SqliteConnection GetConnection()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, SalesDb);
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            return conn;
        }

        public static void InitializeConnection(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Sales (
    Barcode TEXT PRIMARY KEY,
    Name TEXT,
    Color TEXT,
    Size TEXT,
    SaleType TEXT,
    OldPrice TEXT,
    NewPrice TEXT,
    ArticCode TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_sales_barcode ON Sales(Barcode);

";
            cmd.ExecuteNonQuery();
        }


    }
}
