using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.UI.Helpers;
using ZebraSCannerTest1.UI.Views;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(Mode), "Mode")]
[QueryProperty(nameof(BoxId), "BoxId")]
public partial class LogsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly LogBufferService _logBuffer;
    private readonly ClipboardService _clipboard;

    [ObservableProperty] private bool isLoading;  
    [ObservableProperty] private InventoryMode mode = InventoryMode.Standard;
    [ObservableProperty] private string? boxId;

    public const int PageSize = 10;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private string _currentFilter = "";

    public int CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }
    public int TotalPages { get => _totalPages; set => SetProperty(ref _totalPages, value); }



    public ObservableCollection<LogSlot> Slots { get; } =
        new(Enumerable.Range(0, PageSize).Select(_ => new LogSlot()));


    public LogsViewModel(SqliteConnection conn, LogBufferService logBuffer, ClipboardService clipboard)
    {
        _conn = conn;
        _logBuffer = logBuffer;
        _clipboard = clipboard;
    }

    public async Task InitializeAsync()
    {
        await LoadPage(CurrentPage);
    }

    private async Task LoadPage(int page)
    {
        string whereClause = "";

        // ✅ open correct database
        var dbName = Mode == InventoryMode.Loots
            ? "zebraScanner_loots.db"
            : "zebraScanner_standard.db";

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        var table = Mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
        var hasBox = Mode == InventoryMode.Loots;

        using var cmd = conn.CreateCommand();

        var column = Mode == InventoryMode.Loots ? "Box_Id" : "Section";


        // --- BASE CLAUSE ---
        List<string> conditions = new();

        // Default filter for Loots mode (only when user has NOT manually chosen another Box)
        if (Mode == InventoryMode.Loots && !_currentFilter.StartsWith("BOX:"))
        {
            if (string.IsNullOrWhiteSpace(BoxId))
            {
                conditions.Add("(Box_Id IS NULL OR TRIM(Box_Id) = '')");
            }
            else
            {
                conditions.Add("(Box_Id = $boxId OR Box_Id IS NULL OR TRIM(Box_Id) = '')");
                cmd.Parameters.AddWithValue("$boxId", BoxId);
            }
        }




        // Apply filters (can combine with Loots condition)
        if (!string.IsNullOrEmpty(_currentFilter))
        {
            if (_currentFilter == "MANUAL")
            {
                conditions.Add("IsManual = 1");
            }
            else if (_currentFilter == "SCANNED")
            {
                conditions.Add("IsManual IS NULL");
            }
            else if (_currentFilter.StartsWith("TIME:"))
            {
                if (int.TryParse(_currentFilter.Split(':')[1], out int minutes))
                {
                    DateTime cutoff = DateTime.UtcNow.AddMinutes(-minutes);
                    conditions.Add("UpdatedAt >= $cutoff");
                    cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("o"));
                }
            }
            else if (_currentFilter.StartsWith("SECTION:"))
            {
                string sectionName = _currentFilter.Substring("SECTION:".Length);
                conditions.Add("Section = $section");
                cmd.Parameters.AddWithValue("$section", sectionName);
            }
            else if (_currentFilter == "SECTION_NULL")
            {
                conditions.Add("(Section IS NULL OR TRIM(Section) = '')");
            }
            else if (_currentFilter.StartsWith("BOX:"))
            {
                string boxName = _currentFilter.Substring("BOX:".Length);
                conditions.Add("Box_Id = $box");
                cmd.Parameters.AddWithValue("$box", boxName);
            }
            else if (_currentFilter == "BOX_NULL")
            {
                conditions.Add("(Box_Id IS NULL OR TRIM(Box_Id) = '')");
            }

            else
            {
                conditions.Add("Barcode LIKE $filter");
                cmd.Parameters.AddWithValue("$filter", $"%{_currentFilter}%");
            }
        }

        // Combine all filters into one WHERE
        if (conditions.Count > 0)
            whereClause = "WHERE " + string.Join(" AND ", conditions);

        // COUNT + QUERY as before
        int totalCount = 0;

        // COUNT
        using (var countCmd = conn.CreateCommand())   // ✅ not _conn
        {
            countCmd.CommandText = $"SELECT COUNT(*) FROM {table} {whereClause}";
            foreach (SqliteParameter p in cmd.Parameters)
            {
                Console.WriteLine($"----------ameter to countCmd: {p.ParameterName} = {p.Value}");
                countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);

            }
            totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
        }


        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        cmd.CommandText = hasBox
    ? $@"
        SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section, Box_Id
        FROM {table}
        {whereClause}
        ORDER BY UpdatedAt DESC
        LIMIT $limit OFFSET $offset"
    : $@"
        SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
        FROM {table}
        {whereClause}
        ORDER BY UpdatedAt DESC
        LIMIT $limit OFFSET $offset";



        cmd.Parameters.AddWithValue("$limit", PageSize);
        cmd.Parameters.AddWithValue("$offset", (CurrentPage - 1) * PageSize);


        var rows = new List<ScanLog>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var log = new ScanLog
                {
                    Barcode = r.GetString(0),
                    Was = r.GetInt32(1),
                    IncrementBy = r.GetInt32(2),
                    IsValue = r.GetInt32(3),
                    UpdatedAt = DateTime.Parse(r.GetString(4)),
                    IsManual = !r.IsDBNull(5) ? r.GetInt32(5) : (int?)null,
                    Section = !r.IsDBNull(6) ? r.GetString(6) : null
                };

                if (hasBox)
                    log.Box_Id = !r.IsDBNull(7) ? r.GetString(7) : null;

                rows.Add(log);
            }

        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            for (int i = 0; i < PageSize; i++)
            {
                if (i < rows.Count)
                {
                    var src = rows[i];
                    var dst = Slots[i];
                    dst.Barcode = src.Barcode;
                    dst.Was = src.Was;
                    dst.IncrementBy = src.IncrementBy;
                    dst.IsValue = src.IsValue;
                    dst.UpdatedAt = src.UpdatedAt;
                    dst.IsManual = src.IsManual;
                    if (Mode == InventoryMode.Loots)
                        dst.Section = src.Box_Id;
                    else
                        dst.Section = src.Section;


                }
                else
                {
                    var dst = Slots[i];
                    dst.Barcode = string.Empty;
                    dst.Was = 0;
                    dst.IncrementBy = 0;
                    dst.IsValue = 0;
                    dst.UpdatedAt = DateTime.MinValue;
                    dst.IsManual = null;
                    dst.Section = string.Empty;
                }
            }
        });
    }



    // ✅ Filter by time (1, 2, 3, 5, 10, 15, or custom)
    [RelayCommand]
    private async Task FilterTime()
    {
        var baseMenu = new List<string>
    {
        "Last 1 Minute",
        "Last 2 Minutes",
        "Last 3 Minutes",
        "Last 5 Minutes",
        "Last 10 Minutes",
        "Last 15 Minutes",
        "Custom (Enter Minutes)",
        Mode == InventoryMode.Loots ? "Filter by Box" : "Filter by Section",
        "Manual Only",
        "Scanned Only"
    };

        var choice = await Shell.Current.DisplayActionSheet("Filter Options", "Cancel", null, baseMenu.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel")
            return;

        switch (choice)
        {
            case "Last 1 Minute": _currentFilter = "TIME:1"; break;
            case "Last 2 Minutes": _currentFilter = "TIME:2"; break;
            case "Last 3 Minutes": _currentFilter = "TIME:3"; break;
            case "Last 5 Minutes": _currentFilter = "TIME:5"; break;
            case "Last 10 Minutes": _currentFilter = "TIME:10"; break;
            case "Last 15 Minutes": _currentFilter = "TIME:15"; break;

            case "Custom (Enter Minutes)":
                var input = await Shell.Current.DisplayPromptAsync("Custom Filter", "Enter number of minutes:", "OK", "Cancel", keyboard: Keyboard.Numeric);
                if (int.TryParse(input, out int mins) && mins > 0)
                    _currentFilter = $"TIME:{mins}";
                else
                    return;
                break;

            case "Manual Only":
                _currentFilter = "MANUAL";
                break;

            case "Scanned Only":
                _currentFilter = "SCANNED";
                break;

            case "Filter by Box":
            case "Filter by Section":
                var labels = new List<string>();
                var table = Mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
                var column = Mode == InventoryMode.Loots ? "Box_Id" : "Section";

                using (var conn = new SqliteConnection($"Data Source={Path.Combine(FileSystem.AppDataDirectory,
                Mode == InventoryMode.Loots ? "zebraScanner_loots.db" : "zebraScanner_standard.db")}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT DISTINCT {column} FROM {table} WHERE TRIM({column}) != '' ORDER BY {column} ASC";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var val = reader.GetString(0).Trim();
                        if (!string.IsNullOrEmpty(val))
                            labels.Add(val);
                    }
                }

                if (labels.Count == 0)
                {
                    await Shell.Current.DisplayAlert("No Data", Mode == InventoryMode.Loots ? "No boxes found." : "No sections found.", "OK");
                    return;
                }

                labels.Insert(0, Mode == InventoryMode.Loots ? "(No Box)" : "(No Section)");

                var chosen = await Shell.Current.DisplayActionSheet(
                    Mode == InventoryMode.Loots ? "Select Box" : "Select Section",
                    "Cancel", null, labels.ToArray());

                if (string.IsNullOrEmpty(chosen) || chosen == "Cancel")
                    return;

                if (Mode == InventoryMode.Loots)
                    _currentFilter = chosen == "(No Box)" ? "BOX_NULL" : $"BOX:{chosen}";
                else
                    _currentFilter = chosen == "(No Section)" ? "SECTION_NULL" : $"SECTION:{chosen}";
                break;

            default:
                return;
        }

        await LoadPage(1);
    }

    // ✅ Filter by barcode
    [RelayCommand]
    private async Task FilterByBarcode()
    {
        var barcode = await Shell.Current.DisplayPromptAsync(
            "Search Logs", "Enter part of barcode:",
            "OK", "Cancel");

        if (string.IsNullOrWhiteSpace(barcode))
            return;

        _currentFilter = barcode.Trim();
        await LoadPage(1);
    }


    
    [RelayCommand]
    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
            await LoadPage(CurrentPage + 1);
    }

    [RelayCommand]
    private async Task PrevPage()
    {
        if (CurrentPage > 1)
            await LoadPage(CurrentPage - 1);
    }

    [RelayCommand]
    private async Task ClearFilter()
    {
        _currentFilter = "";
        await LoadPage(1);
    }

    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);
    }

    [RelayCommand]
    private async Task ShowFullSectionText(string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var popup = new SectionPopup(section);

            Shell.Current.CurrentPage.ShowPopup(popup);

            popup.PopupFrame.Scale = 0.8;
            popup.PopupFrame.FadeTo(1, 150, Easing.CubicIn);
            popup.PopupFrame.ScaleTo(1, 150, Easing.CubicOut);
        });
    }


    
}
