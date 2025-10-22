using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string message);
        Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
    }
}
