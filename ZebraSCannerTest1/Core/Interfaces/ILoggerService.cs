namespace ZebraSCannerTest1.Core.Interfaces;

public interface ILoggerService<T>
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
