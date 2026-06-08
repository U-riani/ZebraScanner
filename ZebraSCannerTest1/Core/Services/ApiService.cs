using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Core.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://test-server-three-nu.vercel.app/";
        public ApiService()
        {
            // Use your local test server
            // For Android emulator use: http://10.0.2.2:3000
            // For physical device, use LAN IP like http://192.168.1.101:3000
            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        }

        public async Task UploadInventoryJsonAsync(string json, string endpoint = "/upload-inventory")
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);

                response.EnsureSuccessStatusCode();
                Console.WriteLine($"✅ JSON uploaded successfully: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload failed: {ex.Message}");
                throw;
            }
        }
        public async Task<string> DownloadInventoryJsonAsync(string endpoint = "/")
        {
            try
            {
                Console.WriteLine("++++++:" + _baseUrl + endpoint);
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ Downloaded inventory JSON: {json.Length} chars");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Download failed: {ex.Message}");
                throw;
            }
        }

    }
}
