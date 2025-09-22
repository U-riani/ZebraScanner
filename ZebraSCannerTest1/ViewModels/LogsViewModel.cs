using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

public class LogsViewModel : INotifyPropertyChanged
{
    private readonly SqliteConnection _conn;

    public ObservableCollection<ScanLog> Logs { get; set; } = new();

    public LogsViewModel(SqliteConnection conn)
    {
        _conn = conn;
        LoadLogs();

        WeakReferenceMessenger.Default.Register<NewScanLogMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Logs.Insert(0, m.Value);
            });
        });
    }

    public void LoadLogs()
    {
        Logs.Clear();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Barcode, Quantity, InitialQuantity, Timestamp FROM ScanLogs ORDER BY Timestamp DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Logs.Add(new ScanLog
            {
                Id = reader.GetInt32(0),
                Barcode = reader.GetString(1),
                Quantity = reader.GetInt32(2),
                InitialQuantity = reader.GetInt32(3),
                Timestamp = DateTime.Parse(reader.GetString(4))
            });
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
