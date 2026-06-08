using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Core.Models
{
    public class Product : INotifyPropertyChanged
    {
        private int _scannedQuantity;
        private int _initialQuantity;
        private DateTime _updatedAt;
        private bool _isHighlighted;

        private string? _name;
        private string? _color;
        private string? _size;
        private string? _price;
        private string? _articCode;
        private string? _boxId;


        public string? Barcode { get; set; }
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

        // === New product fields ===
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public string Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); }
        }

        public string Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); }
        }

        public string ArticCode
        {
            get => _articCode;
            set { _articCode = value; OnPropertyChanged(); }
        }

        public string? Box_Id
        {
            get => _boxId;
            set { _boxId = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
