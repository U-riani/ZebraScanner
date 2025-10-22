using ZebraSCannerTest1.Core.Interfaces;
using System.Diagnostics;

namespace ZebraSCannerTest1.UI.Services;

public class LoggerService<T> : ILoggerService<T>
{
    public void Info(string message) => Debug.WriteLine($"[INFO] {typeof(T).Name}: {message}");
    public void Warn(string message) => Debug.WriteLine($"[WARN] {typeof(T).Name}: {message}");
    public void Error(string message, Exception? ex = null)
        => Debug.WriteLine($"[ERROR] {typeof(T).Name}: {message}\n{ex}");
}
