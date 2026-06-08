using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Helpers;

public sealed class ScanDocumentContext
{
    public int DocumentId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string ServerKey { get; set; } = "prod";
    public InventoryMode Mode { get; set; } = InventoryMode.Standard;
}

public static class SessionHelper
{
    private const string CurrentScanDocumentIdKey = "scan_context_document_id";
    private const string CurrentScanModuleKey = "scan_context_module";
    private const string CurrentScanRoleKey = "scan_context_role";
    private const string CurrentScanServerKey = "scan_context_server_key";
    private const string CurrentScanModeKey = "scan_context_mode";

    public static async Task<int> GetCurrentUserIdAsync()
    {
        var value = await SecureStorage.GetAsync("user_id");

        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var userId))
            throw new InvalidOperationException("No logged in user found.");

        return userId;
    }

    public static Task SetCurrentScanContextAsync(
        int documentId,
        string module,
        InventoryMode mode,
        string? role = null,
        string serverKey = "prod")
    {
        if (documentId <= 0)
            throw new InvalidOperationException("DocumentId is required before opening a scan database.");

        if (string.IsNullOrWhiteSpace(module))
            throw new InvalidOperationException("Document module is required before opening a scan database.");

        Preferences.Set(CurrentScanDocumentIdKey, documentId);
        Preferences.Set(CurrentScanModuleKey, Normalize(module));
        Preferences.Set(CurrentScanServerKey, NormalizeOrDefault(serverKey, "prod"));
        Preferences.Set(CurrentScanModeKey, mode.ToString());

        if (string.IsNullOrWhiteSpace(role))
            Preferences.Remove(CurrentScanRoleKey);
        else
            Preferences.Set(CurrentScanRoleKey, Normalize(role));

        return Task.CompletedTask;
    }

    public static ScanDocumentContext? GetCurrentScanContext(InventoryMode? expectedMode = null)
    {
        var documentId = Preferences.Get(CurrentScanDocumentIdKey, 0);
        var module = Preferences.Get(CurrentScanModuleKey, string.Empty);

        if (documentId <= 0 || string.IsNullOrWhiteSpace(module))
            return null;

        var storedModeText = Preferences.Get(CurrentScanModeKey, string.Empty);
        var mode = InventoryMode.Standard;

        if (!string.IsNullOrWhiteSpace(storedModeText)
            && Enum.TryParse<InventoryMode>(storedModeText, ignoreCase: true, out var parsedMode))
        {
            mode = parsedMode;
        }

        if (expectedMode.HasValue && mode != expectedMode.Value)
            return null;

        return new ScanDocumentContext
        {
            DocumentId = documentId,
            Module = Normalize(module),
            Role = NormalizeNullable(Preferences.Get(CurrentScanRoleKey, string.Empty)),
            ServerKey = NormalizeOrDefault(Preferences.Get(CurrentScanServerKey, "prod"), "prod"),
            Mode = mode
        };
    }

    public static void ClearCurrentScanContext()
    {
        Preferences.Remove(CurrentScanDocumentIdKey);
        Preferences.Remove(CurrentScanModuleKey);
        Preferences.Remove(CurrentScanRoleKey);
        Preferences.Remove(CurrentScanServerKey);
        Preferences.Remove(CurrentScanModeKey);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : Normalize(value);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Normalize(value);
    }
}
