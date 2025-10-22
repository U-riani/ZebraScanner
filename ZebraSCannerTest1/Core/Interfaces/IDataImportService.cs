namespace ZebraSCannerTest1.Core.Interfaces;

public interface IDataImportService
{
    Task ImportExcelAsync(Stream stream, string? fileName = null);
    Task ImportDbAsync(Stream dbStream);
    Task<int> ImportJsonAsync(Stream jsonStream);
}
