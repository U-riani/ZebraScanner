using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics; // for Colors


namespace ZebraSCannerTest1.Models
{
    public class ScannedProduct : INotifyPropertyChanged
    {
        [Key]
        public int Id { get; set; }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set { 
                _quantity = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityBackgroundColor)); // update color
            }
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
        private int _initialQuantity; // <-- add this
        public int InitialQuantity
        {
            get => _initialQuantity;
            set { _initialQuantity = value; OnPropertyChanged(); }

        }


        public bool IsBelowInitial => Quantity < InitialQuantity;
        public bool IsEqualInitial => Quantity == InitialQuantity;
        public bool IsAboveInitial => Quantity > InitialQuantity;


        public Color QuantityBackgroundColor
        {
            get
            {
                if (Quantity < InitialQuantity)
                    return Colors.Orange;
                else if (Quantity == InitialQuantity)
                    return Colors.Green;
                else
                    return Colors.OrangeRed;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
