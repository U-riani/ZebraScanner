using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Core.Models
{
    /// <summary>
    /// Represents a product row in the scanned product list with live-updating support.
    /// </summary>
    public class StatsProduct : INotifyPropertyChanged
    {
        private string? _barcode;
        private int _scannedQuantity;
        private int _initialQuantity;
        private DateTime _createdAt;
        private DateTime _updatedAt;
        private bool _isHighlighted;

        private string? _name;
        private string? _color;
        private string? _size;
        private string? _price;
        private string? _articCode;
        public string? _boxId;
        public string? Barcode
        {
            get => _barcode;
            set { if (_barcode == value) return; _barcode = value; OnPropertyChanged(); }
        }

        public string? Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        public string? Color
        {
            get => _color;
            set { if (_color == value) return; _color = value; OnPropertyChanged(); }
        }

        public string? Size
        {
            get => _size;
            set { if (_size == value) return; _size = value; OnPropertyChanged(); }
        }

        public string? Price
        {
            get => _price;
            set { if (_price == value) return; _price = value; OnPropertyChanged(); }
        }

        public string? ArticCode
        {
            get => _articCode;
            set { if (_articCode == value) return; _articCode = value; OnPropertyChanged(); }
        }

        public string? BoxId
        {
            get => _boxId;
            set { if (_boxId == value) return; _boxId = value; OnPropertyChanged(); }
        }

        public int InitialQuantity
        {
            get => _initialQuantity;
            set
            {
                if (_initialQuantity == value) return;
                _initialQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Difference)); // computed field depends on it
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
                OnPropertyChanged(nameof(Difference)); // computed field depends on it
            }
        }

        /// <summary>
        /// Computed property — automatically recalculated when ScannedQuantity or InitialQuantity changes.
        /// </summary>
        public int Difference => _scannedQuantity - _initialQuantity;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { if (_createdAt == value) return; _createdAt = value; OnPropertyChanged(); }
        }

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

        /// <summary>
        /// Helper to quickly update data from another instance (for live refresh or merging).
        /// </summary>
        public void UpdateFrom(StatsProduct other)
        {
            Barcode = other.Barcode;
            Name = other.Name;
            Color = other.Color;
            Size = other.Size;
            Price = other.Price;
            ArticCode = other.ArticCode;
            InitialQuantity = other.InitialQuantity;
            ScannedQuantity = other.ScannedQuantity;
            CreatedAt = other.CreatedAt;
            UpdatedAt = other.UpdatedAt;
            IsHighlighted = other.IsHighlighted;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
