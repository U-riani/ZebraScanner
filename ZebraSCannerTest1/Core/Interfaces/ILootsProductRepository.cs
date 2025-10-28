using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface ILootsProductRepository
    {
        Task AddAsync(LootProduct product);
        Task<IEnumerable<LootProduct>> GetAllAsync();
        Task ClearAsync();
    }
}
