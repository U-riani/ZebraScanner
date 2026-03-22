using System.Net.Http.Headers;
using System.Net.Http.Json;
using ZebraSCannerTest1.Core.Dtos;

namespace ZebraSCannerTest1.UI.Services;

public class ApiInventoryService
{
    private readonly HttpClient _http;

    public ApiInventoryService()
    {
        _http = new HttpClient();
        _http.BaseAddress = new Uri("http://10.0.2.2:8000/api/");
    }

    public async Task<List<PocketDocumentDto>?> GetDocuments(string token)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("pocket-api/documents");

            Console.WriteLine("STATUS: " + response.StatusCode);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<List<PocketDocumentDto>>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Document fetch error: " + ex.Message);
            return null;
        }
    }

    public async Task<List<PocketDocumentLinesDto>?> GetDocumentLines(
     string token,
     int doc_id,
     string module)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync($"pocket-api/{doc_id}/lines?module={module}");

            Console.WriteLine("STATUS: " + response.StatusCode);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<List<PocketDocumentLinesDto>>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Document fetch error: " + ex.Message);
            return null;
        }
    }
}
