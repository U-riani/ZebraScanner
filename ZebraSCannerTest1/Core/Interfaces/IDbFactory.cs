using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IDbFactory
    {
        SqliteConnection Inventorization(InventoryMode mode);
        SqliteConnection Sales();
    }

}
