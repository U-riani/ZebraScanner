using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;

namespace ZebraSCannerTest1.ViewModels
{
    public class LogsViewModel : INotifyPropertyChanged
    {
        private readonly SqliteConnection _conn;
        private readonly LogBufferService _logBuffer;

        public ObservableCollection<ScanLog> Logs { get; set; } = new();

        public LogsViewModel(SqliteConnection conn, LogBufferService logBuffer)
        {
            _conn = conn;
            _logBuffer = logBuffer;

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
            _logBuffer.FlushNow(); // ensure buffered logs are persisted

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                SELECT L.Id, L.Barcode, L.ScannedQuantity, 
                       IFNULL(P.InitialQuantity, 0) as InitialQuantity,
                       L.Timestamp
                FROM ScanLogs L
                LEFT JOIN Products P ON P.Barcode = L.Barcode
                ORDER BY L.Timestamp DESC
                LIMIT 500";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Logs.Add(new ScanLog
                {
                    Id = r.GetInt32(0),
                    Barcode = r.GetString(1),
                    ScannedQuantity = r.GetInt32(2),
                    InitialQuantity = r.GetInt32(3),
                    Timestamp = DateTime.Parse(r.GetString(4))
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
