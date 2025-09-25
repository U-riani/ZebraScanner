using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace ZebraSCannerTest1.Models
{
    public class Product : INotifyPropertyChanged
    {
        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        private int _initialQuantity;
        public int InitialQuantity
        {
            get => _initialQuantity;
            set { _initialQuantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(QuantityBackgroundColor)); }
        }

        private int _scannedQuantity;
        public int ScannedQuantity
        {
            get => _scannedQuantity;
            set { _scannedQuantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(QuantityBackgroundColor)); }
        }

        private DateTime _createdAt = DateTime.UtcNow;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        private DateTime _updatedAt = DateTime.UtcNow;
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { _updatedAt = value; OnPropertyChanged(); }
        }

        public bool IsBelowInitial => ScannedQuantity < InitialQuantity;
        public bool IsEqualInitial => ScannedQuantity == InitialQuantity;
        public bool IsAboveInitial => ScannedQuantity > InitialQuantity;

        public Color QuantityBackgroundColor =>
            IsBelowInitial ? Colors.Orange :
            IsEqualInitial ? Colors.Green :
            Colors.OrangeRed;

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
