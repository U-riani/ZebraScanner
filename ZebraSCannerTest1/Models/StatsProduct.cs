using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Models
{
    public class StatsProduct : INotifyPropertyChanged
    {
        private string? _barcode;
        private int _scannedQuantity;
        private int _initialQuantity;
        private DateTime _createdAt;
        private DateTime _updatedAt;
        private bool _isHighlighted;

        public string? Barcode
        {
            get => _barcode;
            set { if (_barcode == value) return; _barcode = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { if (_createdAt == value) return; _createdAt = value; OnPropertyChanged(); }
        }

        public int InitialQuantity
        {
            get => _initialQuantity;
            set
            {
                if (_initialQuantity == value) return;
                _initialQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Difference)); // depends on InitialQuantity
            }
        }

        public int ScannedQuantity
        {
            get => _scannedQuantity;
            set
            {
                if (_scannedQuantity == value) return;
                _scannedQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Difference)); // depends on ScannedQuantity
            }
        }

        // Computed read-only property
        public int Difference => _scannedQuantity - _initialQuantity;

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { if (_updatedAt == value) return; _updatedAt = value; OnPropertyChanged(); }
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { if (_isHighlighted == value) return; _isHighlighted = value; OnPropertyChanged(); }
        }

        // Static product info
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public string? Price { get; set; }
        public string? ArticCode { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}