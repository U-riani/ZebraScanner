using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Models;

public class LogSlot : INotifyPropertyChanged
{
    private string _barcode;
    private int _scannedQuantity;
    private int _initialQuantity;
    private DateTime _timestamp;

    public string Barcode
    {
        get => _barcode;
        set { _barcode = value; OnPropertyChanged(); }
    }

    public int ScannedQuantity
    {
        get => _scannedQuantity;
        set { _scannedQuantity = value; OnPropertyChanged(); }
    }

    public int InitialQuantity
    {
        get => _initialQuantity;
        set { _initialQuantity = value; OnPropertyChanged(); }
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
