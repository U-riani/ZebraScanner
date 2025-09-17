using System.Windows.Input;
using ZebraSCannerTest1.ViewModels;
using ZebraSCannerTest1.Data;   // <-- for AppDbContext
using ZebraSCannerTest1.Models; // <-- for Product model

namespace ZebraSCannerTest1.Views;

public partial class DetailsPage : ContentPage
{
    public ICommand SaveUpdatedDetailsCommand { get; }

    private readonly AppDbContext _db;

    public DetailsPage(DetailsViewModel vm, AppDbContext db)
    {
        InitializeComponent();
        BindingContext = vm;
        _db = db;

        // Expose the command to XAML
        SaveUpdatedDetailsCommand = new Command(SaveUpdatedDetails);
        this.BindingContextChanged += (s, e) =>
        {
            if (BindingContext is DetailsViewModel vmContext)
            {
                // attach command into VM if needed
                vmContext.SaveUpdatedDetailsCommand = SaveUpdatedDetailsCommand;
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DetailsViewModel vm)
            vm.Load();
    }

    private void SaveUpdatedDetails()
    {
        if (BindingContext is DetailsViewModel vm && vm.Product != null)
        {
            var existing = _db.Products.FirstOrDefault(p => p.Id == vm.Product.Id);
            if (existing != null)
            {
                existing.Name = vm.Product.Name;
                existing.Quantity = vm.Product.Quantity;
                _db.SaveChanges();
            }
        }
    }
}
