using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

public class LogsViewModel : INotifyPropertyChanged
{
    private readonly AppDbContext _db;

    public ObservableCollection<ScanLog> Logs { get; set; } = new();

    public LogsViewModel(AppDbContext db) // DI
    {
        _db = db;
        LoadLogs();

        // Subscribe to new ScanLog messages
        WeakReferenceMessenger.Default.Register<NewScanLogMessage>(this, (r, m) =>
        {
            //// Insert at the top
            //Logs.Insert(0, m.Value);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Insert at top
                Logs.Insert(0, m.Value);
            });
        });
    }

    public void LoadLogs()
    {
        Logs.Clear();
        var history = _db.ScanLogs
            .OrderByDescending(l => l.Timestamp) // most recent first
            .ToList();

        foreach (var item in history)
            Logs.Add(item);
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
