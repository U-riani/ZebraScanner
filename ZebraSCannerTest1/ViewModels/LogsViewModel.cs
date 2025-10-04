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

    private string _currentFilter = "";

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
            cmd.CommandText = "SELECT COUNT(*) FROM ScanLogs";
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
                SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt
                FROM ScanLogs
                ORDER BY UpdatedAt DESC
                LIMIT $limit OFFSET $offset";
        }
        else
        {
            cmd.CommandText = @"
                SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt
                FROM ScanLogs
                WHERE Barcode LIKE $filter
                ORDER BY UpdatedAt DESC
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
                Was = r.GetInt32(1),
                IncrementBy = r.GetInt32(2),
                IsValue = r.GetInt32(3),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        }

        for (int i = 0; i < PageSize; i++)
        {
            if (i < rows.Count)
            {
                Slots[i].Barcode = rows[i].Barcode;
                Slots[i].Was = rows[i].Was;
                Slots[i].IncrementBy = rows[i].IncrementBy;
                Slots[i].IsValue = rows[i].IsValue;
                Slots[i].UpdatedAt = rows[i].UpdatedAt;
            }
            else
            {
                Slots[i].Barcode = string.Empty;
                Slots[i].Was = 0;
                Slots[i].IncrementBy = 0;
                Slots[i].IsValue = 0;
                Slots[i].UpdatedAt = DateTime.MinValue;
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

        if (input == null) return;
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
