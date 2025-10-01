//using CommunityToolkit.Maui.Alerts;
//using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;

namespace ZebraSCannerTest1.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly LogBufferService _logBuffer;
    private readonly ClipboardService _clipboard;


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

    private string _currentFilter = ""; // 🔹 barcode filter

    public ObservableCollection<LogSlot> Slots { get; } =
        new(Enumerable.Range(0, PageSize).Select(_ => new LogSlot()));

    public LogsViewModel(SqliteConnection conn, LogBufferService logBuffer, ClipboardService clipboard)
    {
        _conn = conn;
        _logBuffer = logBuffer;
        _clipboard = clipboard;

        LoadPage(CurrentPage);

    }

    private int GetTotalCount()
    {
        using var cmd = _conn.CreateCommand();
        if (string.IsNullOrEmpty(_currentFilter))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ScanLogs";
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM ScanLogs WHERE Barcode LIKE $filter";
            cmd.Parameters.AddWithValue("$filter", $"%{_currentFilter}%");
        }
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void LoadPage(int page)
    {
        var totalCount = GetTotalCount();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        if (TotalPages == 0) TotalPages = 1;
        if (page < 1) page = 1;
        if (page > TotalPages) page = TotalPages;

        CurrentPage = page;

        using var cmd = _conn.CreateCommand();
        if (string.IsNullOrEmpty(_currentFilter))
        {
            cmd.CommandText = @"
                SELECT L.Barcode, L.ScannedQuantity,
                       IFNULL(P.InitialQuantity,0) as InitialQuantity,
                       L.Timestamp
                FROM ScanLogs L
                LEFT JOIN Products P ON P.Barcode = L.Barcode
                ORDER BY L.Timestamp DESC
                LIMIT $limit OFFSET $offset";
        }
        else
        {
            cmd.CommandText = @"
                SELECT L.Barcode, L.ScannedQuantity,
                       IFNULL(P.InitialQuantity,0) as InitialQuantity,
                       L.Timestamp
                FROM ScanLogs L
                LEFT JOIN Products P ON P.Barcode = L.Barcode
                WHERE L.Barcode LIKE $filter
                ORDER BY L.Timestamp DESC
                LIMIT $limit OFFSET $offset";
            cmd.Parameters.AddWithValue("$filter", $"%{_currentFilter}%");
        }

        cmd.Parameters.AddWithValue("$limit", PageSize);
        cmd.Parameters.AddWithValue("$offset", (page - 1) * PageSize);

        using var r = cmd.ExecuteReader();
        var rows = new List<ScanLog>();
        while (r.Read())
        {
            rows.Add(new ScanLog
            {
                Barcode = r.GetString(0),
                ScannedQuantity = r.GetInt32(1),
                InitialQuantity = r.GetInt32(2),
                Timestamp = DateTime.Parse(r.GetString(3))
            });
        }

        for (int i = 0; i < PageSize; i++)
        {
            if (i < rows.Count)
            {
                Slots[i].Barcode = rows[i].Barcode;
                Slots[i].ScannedQuantity = rows[i].ScannedQuantity;
                Slots[i].InitialQuantity = rows[i].InitialQuantity;
                Slots[i].Timestamp = rows[i].Timestamp;
            }
            else
            {
                Slots[i].Barcode = string.Empty;
                Slots[i].ScannedQuantity = 0;
                Slots[i].InitialQuantity = 0;
                Slots[i].Timestamp = DateTime.MinValue;
            }
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
            LoadPage(CurrentPage + 1);
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage > 1)
            LoadPage(CurrentPage - 1);
    }

    [RelayCommand]
    private async Task Filter()
    {
        var input = await Shell.Current.DisplayPromptAsync(
            "Filter Logs", "Enter barcode (partial allowed):",
            "OK", "Cancel", "Barcode...", maxLength: 50);

        if (input == null) return; // canceled
        _currentFilter = input.Trim();
        LoadPage(1);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        _currentFilter = "";
        LoadPage(1);
    }

    [RelayCommand]
    private async Task CopyBarcode(string barcode)
    {
        await _clipboard.CopyAsync(barcode);

    }
}
