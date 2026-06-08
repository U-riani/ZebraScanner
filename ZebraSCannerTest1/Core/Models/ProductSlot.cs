using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Core.Models;

public class ProductSlot : INotifyPropertyChanged
{
    private string _barcode;
    private int _scannedQuantity;
    private int _initialQuantity;
    private int _currentBoxScannedQuantity;
    private int _barcodeTotalScannedQuantity;
    private int _barcodeTotalExpectedQuantity;
    private bool _usesBarcodeTotalFallback;
    private bool _hasKnownExpectedQuantity;

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

    public int CurrentBoxScannedQuantity
    {
        get => _currentBoxScannedQuantity;
        set
        {
            _currentBoxScannedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScannedDisplay));
        }
    }

    public int BarcodeTotalScannedQuantity
    {
        get => _barcodeTotalScannedQuantity;
        set
        {
            _barcodeTotalScannedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScannedDisplay));
            OnPropertyChanged(nameof(Difference));
            OnPropertyChanged(nameof(DifferenceDisplay));
        }
    }

    public int BarcodeTotalExpectedQuantity
    {
        get => _barcodeTotalExpectedQuantity;
        set
        {
            _barcodeTotalExpectedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InitialDisplay));
            OnPropertyChanged(nameof(Difference));
            OnPropertyChanged(nameof(DifferenceDisplay));
        }
    }

    public bool UsesBarcodeTotalFallback
    {
        get => _usesBarcodeTotalFallback;
        set
        {
            _usesBarcodeTotalFallback = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScannedDisplay));
            OnPropertyChanged(nameof(InitialDisplay));
            OnPropertyChanged(nameof(Difference));
            OnPropertyChanged(nameof(DifferenceDisplay));
        }
    }

    public bool HasKnownExpectedQuantity
    {
        get => _hasKnownExpectedQuantity;
        set
        {
            _hasKnownExpectedQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InitialDisplay));
            OnPropertyChanged(nameof(DifferenceDisplay));
        }
    }

    // 🔹 Computed property
    public int Difference => UsesBarcodeTotalFallback
        ? BarcodeTotalScannedQuantity - BarcodeTotalExpectedQuantity
        : ScannedQuantity - InitialQuantity;

    public string ScannedDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Barcode))
                return string.Empty;

            return UsesBarcodeTotalFallback
                ? $"{CurrentBoxScannedQuantity:N0} / {BarcodeTotalScannedQuantity:N0}"
                : ScannedQuantity.ToString("N0");
        }
    }

    public string InitialDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Barcode))
                return string.Empty;

            if (UsesBarcodeTotalFallback)
                return BarcodeTotalExpectedQuantity.ToString("N0");

            return HasKnownExpectedQuantity ? InitialQuantity.ToString("N0") : "Unknown";
        }
    }

    public string DifferenceDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Barcode))
                return string.Empty;

            if (!HasKnownExpectedQuantity)
                return "—";

            if (UsesBarcodeTotalFallback)
            {
                var remaining = BarcodeTotalExpectedQuantity - BarcodeTotalScannedQuantity;
                return remaining >= 0 ? $"{remaining:N0} left" : $"+{Math.Abs(remaining):N0}";
            }

            return Difference.ToString("N0");
        }
    }

    public void Set(string barcode, int scanned, int initial)
    {
        SetProgress(
            barcode: barcode,
            currentBoxScanned: scanned,
            currentBoxExpected: initial,
            barcodeTotalScanned: scanned,
            barcodeTotalExpected: initial,
            usesBarcodeTotalFallback: false,
            hasKnownExpectedQuantity: initial > 0);
    }

    public void SetProgress(
        string barcode,
        int currentBoxScanned,
        int currentBoxExpected,
        int barcodeTotalScanned,
        int barcodeTotalExpected,
        bool usesBarcodeTotalFallback,
        bool hasKnownExpectedQuantity)
    {
        _barcode = barcode;
        _currentBoxScannedQuantity = currentBoxScanned;
        _barcodeTotalScannedQuantity = barcodeTotalScanned;
        _barcodeTotalExpectedQuantity = barcodeTotalExpected;
        _usesBarcodeTotalFallback = usesBarcodeTotalFallback;
        _hasKnownExpectedQuantity = hasKnownExpectedQuantity;

        _scannedQuantity = usesBarcodeTotalFallback ? barcodeTotalScanned : currentBoxScanned;
        _initialQuantity = usesBarcodeTotalFallback ? barcodeTotalExpected : currentBoxExpected;

        OnPropertyChanged(nameof(Barcode));
        OnPropertyChanged(nameof(CurrentBoxScannedQuantity));
        OnPropertyChanged(nameof(BarcodeTotalScannedQuantity));
        OnPropertyChanged(nameof(BarcodeTotalExpectedQuantity));
        OnPropertyChanged(nameof(UsesBarcodeTotalFallback));
        OnPropertyChanged(nameof(HasKnownExpectedQuantity));
        OnPropertyChanged(nameof(ScannedQuantity));
        OnPropertyChanged(nameof(InitialQuantity));
        OnPropertyChanged(nameof(Difference));
        OnPropertyChanged(nameof(ScannedDisplay));
        OnPropertyChanged(nameof(InitialDisplay));
        OnPropertyChanged(nameof(DifferenceDisplay));
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
