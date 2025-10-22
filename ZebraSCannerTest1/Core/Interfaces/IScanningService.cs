using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IScanningService
    {
        Task ProcessAsync(string barcode);
    }
}
