namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IApiService
    {
        Task UploadInventoryJsonAsync(string json, string endpoint = "/upload-inventory");
        Task<string> DownloadInventoryJsonAsync(string endpoint = "/");
    }
}
