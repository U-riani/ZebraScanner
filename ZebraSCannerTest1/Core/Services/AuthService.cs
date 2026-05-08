using System.Net.Http.Json;

namespace ZebraSCannerTest1.UI.Services;

public class AuthService
{
    private readonly HttpClient _http;

    public AuthService()
    {
        _http = new HttpClient();
        //_http.BaseAddress = new Uri("https://dev-scanmate.gtexshop.ge/api/");
        //_http.BaseAddress = new Uri("http://192.168.1.132:8000/api/");
        _http.BaseAddress = new Uri("http://10.0.2.2:8000/api/");
        //_http.BaseAddress = new Uri("https://scanmate-admin-panel.onrender.com/api/");
    }

    public async Task<LoginResponse?> Login(string username, string password)
    {
        try
        {
            var request = new
            {
                username = username,
                password = password
            };

            Console.WriteLine("--------------------------login clicked------------------");

            var response = await _http.PostAsJsonAsync("pocket-users/login", request);

            Console.WriteLine("STATUS: " + response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine(body);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<LoginResponse>();
        }
        catch (Exception)
        {
            Console.WriteLine("COnnection errrrrrrror");
            return null;
        }
    }
}

public class LoginResponse
{
    public string access_token { get; set; }
    public int user_id { get; set; }
    public string username { get; set; }
    public Dictionary<string, bool> modules { get; set; }
}