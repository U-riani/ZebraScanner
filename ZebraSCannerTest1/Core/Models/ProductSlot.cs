using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Core.Models;

public class ProductSlot : INotifyPropertyChanged
{
    private string _barcode;
    private int _scannedQuantity;
    private int _initialQuantity;

    public string Barcode
    {
        get => _barcode;
        set { _barcode = value; OnPropertyChanged(); }
    }

    public int ScannedQuantity
    {
        get => _scannedQuantity;
        set
        {
            _scannedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Difference)); // ✅ notify UI
        }
    }

    public int InitialQuantity
    {
        get => _initialQuantity;
        set
        {
            _initialQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Difference)); // ✅ notify UI
        }
    }

    // 🔹 Computed property
    public int Difference => ScannedQuantity - InitialQuantity;

    public void Set(string barcode, int scanned, int initial)
    {
        _barcode = barcode;
        _scannedQuantity = scanned;
        _initialQuantity = initial;
        OnPropertyChanged(nameof(Barcode));
        OnPropertyChanged(nameof(ScannedQuantity));
        OnPropertyChanged(nameof(InitialQuantity));
        OnPropertyChanged(nameof(Difference)); // ✅ notify when using Set()
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
