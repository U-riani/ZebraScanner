using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Models;

public class Product : INotifyPropertyChanged
{
    private int _scannedQuantity;
    private int _initialQuantity;
    private DateTime _updatedAt;
    private bool _isHighlighted;

    public string Barcode { get; set; }
    public DateTime CreatedAt { get; set; }

    public int InitialQuantity
    {
        get => _initialQuantity;
        set { _initialQuantity = value; OnPropertyChanged(); }
    }

    public int ScannedQuantity
    {
        get => _scannedQuantity;
        set { _scannedQuantity = value; OnPropertyChanged(); }
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set { _updatedAt = value; OnPropertyChanged(); }
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set { _isHighlighted = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
