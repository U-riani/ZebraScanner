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
        //_http.BaseAddress = new Uri("https://dev-scanmate.gtexshop.ge/api/");
        //_http.BaseAddress = new Uri("http://192.168.1.132:8000/api/");
        _http.BaseAddress = new Uri("http://10.0.2.2:8000/api/");
        //_http.BaseAddress = new Uri("https://scanmate-admin-panel.onrender.com/api/");

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
        string module,
        string? role = null)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = $"pocket-api/{doc_id}/lines?module={module}";

            if (!string.IsNullOrWhiteSpace(role))
                url += $"&role={Uri.EscapeDataString(role)}";

            var response = await _http.GetAsync(url);

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

    public async Task<DocumentStatusChangeResponseDto?> UpdateDocumentStatus(
        string token, 
        int documentId, 
        string module, 
        string currentStatus, 
        string? role = null)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                current_status = currentStatus,
                role = role
            };

            var response = await _http.PostAsJsonAsync(
                $"pocket-api/document/{documentId}/{module}/status-change",
                payload);

            Console.WriteLine("STATUS UPDATE: " + response.StatusCode);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DocumentStatusChangeResponseDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Status update error: " + ex.Message);
            return null;
        }
    }

    

    public async Task<DocumentStatusChangeResponseDto?> FinishScanning(
        string token,
        int documentId,
        string module,
        string currentStatus,
        string? role = null)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                current_status = currentStatus,
                role = role
            };

            var response = await _http.PostAsJsonAsync(
                $"pocket-api/document/{documentId}/{module}/finish-scanning",
                payload);

            Console.WriteLine("FINISH SCANNING STATUS: " + response.StatusCode);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DocumentStatusChangeResponseDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Finish scanning error: " + ex.Message);
            return null;
        }
    }
    public async Task<SubmitDocumentLinesResponseDto?> SubmitDocumentLines(
        string token,
        int documentId,
        string module,
        SubmitDocumentLinesRequestDto payload)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.PostAsJsonAsync(
                $"pocket-api/document/{documentId}/{module}/submit-lines",
                payload);

            Console.WriteLine("SUBMIT LINES STATUS: " + response.StatusCode);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<SubmitDocumentLinesResponseDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Submit lines error: " + ex.Message);
            return null;
        }
    }
}
