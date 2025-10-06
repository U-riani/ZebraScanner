using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Models;

public class LogSlot : INotifyPropertyChanged
{
    private string _barcode;
    private int _was;
    private int _incrementBy;
    private int _isValue;
    private DateTime _updatedAt;
    private int? _isManual { get; set; }

    public string Barcode
    {
        get => _barcode;
        set { _barcode = value; OnPropertyChanged(); }
    }

    public int Was
    {
        get => _was;
        set { _was = value; OnPropertyChanged(); }
    }

    public int IncrementBy
    {
        get => _incrementBy;
        set { _incrementBy = value; OnPropertyChanged(); }
    }

    public int IsValue
    {
        get => _isValue;
        set { _isValue = value; OnPropertyChanged(); }
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set { _updatedAt = value; OnPropertyChanged(); }
    }

    public int? IsManual
    {
        get => _isManual;
        set { _isManual = value; OnPropertyChanged(); } // ✅ important
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
