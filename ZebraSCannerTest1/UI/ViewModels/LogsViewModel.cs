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

namespace ZebraSCannerTest1.UI.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly LogBufferService _logBuffer;
    private readonly ClipboardService _clipboard;

    [ObservableProperty]
    private bool isLoading;

    public const int PageSize = 10;

    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private int _totalPages = 1;
    public int TotalPages
    {
        get => _totalPages;
        set => SetProperty(ref _totalPages, value);
    }

    private string _currentFilter = "";

    public ObservableCollection<LogSlot> Slots { get; } =
        new(Enumerable.Range(0, PageSize).Select(_ => new LogSlot()));


    public LogsViewModel(SqliteConnection conn, LogBufferService logBuffer, ClipboardService clipboard)
    {
        _conn = conn;
        _logBuffer = logBuffer;
        _clipboard = clipboard;
    }

    // ✅ Filter by time (1, 2, 3, 5, 10, 15, or custom)
    [RelayCommand]
    private async Task FilterTime()
    {
        var choice = await Shell.Current.DisplayActionSheet(
            "Filter by Time", "Cancel", null,
            "Last 1 Minute",
            "Last 2 Minutes",
            "Last 3 Minutes",
            "Last 5 Minutes",
            "Last 10 Minutes",
            "Last 15 Minutes",
            "Custom (Enter Minutes)",
            "Filter by Section",
            "Manual Only",
            "Scanned Only"
        );

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
                var input = await Shell.Current.DisplayPromptAsync(
                    "Custom Filter", "Enter number of minutes:",
                    "OK", "Cancel", keyboard: Keyboard.Numeric);
                if (int.TryParse(input, out int mins) && mins > 0)
                    _currentFilter = $"TIME:{mins}";
                else
                    return;
                break;
            // Manual filters
            case "Manual Only":
                _currentFilter = "MANUAL";
                break;
            case "Scanned Only":
                _currentFilter = "SCANNED";
                break;
            // 🧭 NEW SECTION FILTER
            case "Filter by Section":
                var sections = new List<string>();

                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT Section FROM ScanLogs ORDER BY Section ASC";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            var value = reader.GetString(0).Trim();
                            if (!string.IsNullOrEmpty(value))
                                sections.Add(value);
                        }
                    }
                }

                // 🧠 Add "No Section" option at top
                sections.Insert(0, "(No Section)");

                var chosenSection = await Shell.Current.DisplayActionSheet(
                    "Select Section", "Cancel", null, sections.ToArray());

                if (string.IsNullOrEmpty(chosenSection) || chosenSection == "Cancel")
                    return;

                if (chosenSection == "(No Section)")
                    _currentFilter = "SECTION_NULL";
                else
                    _currentFilter = $"SECTION:{chosenSection}";

                break;

            default: return;
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

    // ✅ Load page with filters
    private async Task LoadPage(int page)
    {
        string whereClause = "";
        var cmd = _conn.CreateCommand();

        // --- FILTER HANDLING ---
        if (_currentFilter == "MANUAL")
        {
            whereClause = "WHERE IsManual = 1";
        }
        else if (_currentFilter == "SCANNED")
        {
            whereClause = "WHERE IsManual IS NULL";
        }
        else if (_currentFilter.StartsWith("TIME:"))
        {
            if (int.TryParse(_currentFilter.Split(':')[1], out int minutes))
            {
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-minutes);
                whereClause = "WHERE UpdatedAt >= $cutoff";
                cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("o"));
            }
        }
        else if (_currentFilter.StartsWith("SECTION:"))
        {
            string sectionName = _currentFilter.Substring("SECTION:".Length);
            whereClause = "WHERE Section = $section";
            cmd.Parameters.AddWithValue("$section", sectionName);
        }
        else if (_currentFilter == "SECTION_NULL")
        {
            whereClause = "WHERE Section IS NULL OR TRIM(Section) = ''";
        }
        else if (!string.IsNullOrEmpty(_currentFilter))
        {
            whereClause = "WHERE Barcode LIKE $filter";
            cmd.Parameters.AddWithValue("$filter", $"%{_currentFilter}%");
        }


        // --- COUNT TOTAL RECORDS ---
        int totalCount = 0;
        using (var countCmd = _conn.CreateCommand())
        {
            countCmd.CommandText = $"SELECT COUNT(*) FROM ScanLogs {whereClause}";
            foreach (SqliteParameter p in cmd.Parameters)
                countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
            totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        // --- FETCH PAGE DATA ---
        cmd.CommandText = $@"
            SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
            FROM ScanLogs
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
                rows.Add(new ScanLog
                {
                    Barcode = r.GetString(0),
                    Was = r.GetInt32(1),
                    IncrementBy = r.GetInt32(2),
                    IsValue = r.GetInt32(3),
                    UpdatedAt = DateTime.Parse(r.GetString(4)),
                    IsManual = !r.IsDBNull(5) ? r.GetInt32(5) : (int?)null,
                    Section = !r.IsDBNull(6) ? r.GetString(6) : null
                });
            }
        }

        // --- UPDATE UI ---
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


    public async Task InitializeAsync()
    {
        await LoadPage(CurrentPage);
    }
}
