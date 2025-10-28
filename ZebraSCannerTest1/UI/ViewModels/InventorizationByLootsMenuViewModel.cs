using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.UI.ViewModels;
public class InventorizationByLootsMenuViewModel : ObservableObject
{
    public IRelayCommand NavigateToContinueCommand { get; }

    public IRelayCommand NavigateToResultCommand { get; }
    public IRelayCommand ExportLootsCommand { get; }
    public IRelayCommand ImportLootsCommand { get; }
    public IRelayCommand ClearResultCommand { get; }

    private readonly ILootsProductRepository _repo;


    public InventorizationByLootsMenuViewModel(ILootsProductRepository repo)
    {
        _repo = repo;

        NavigateToContinueCommand = new RelayCommand(async () => await OnContinue());
        NavigateToResultCommand = new RelayCommand(async () => await OnResult());
        ExportLootsCommand = new RelayCommand(async () => await OnExport());
        ImportLootsCommand = new RelayCommand(async () => await OnImport());
        ClearResultCommand = new RelayCommand(async () => await OnClear());
    }

    private Task OnContinue() => Shell.Current.DisplayAlert("LOOTS", "Continue clicked", "OK");
    private Task OnResult() => Shell.Current.DisplayAlert("LOOTS", "Result clicked", "OK");
    private Task OnExport() => Shell.Current.DisplayAlert("LOOTS", "Export clicked", "OK");
    private async Task OnImport()
    {
        //try
        //{
        //    // Pick a file
        //    var result = await FilePicker.PickAsync(new PickOptions
        //    {
        //        PickerTitle = "Select Loots Excel File",
        //        FileTypes = FilePickerFileType.Excel
        //    });
        //    if (result == null) return;

        //    using var stream = await result.OpenReadAsync();
        //    using var package = new ExcelPackage(stream);
        //    var sheet = package.Workbook.Worksheets[0];

        //    // Simple Excel example
        //    for (int row = 2; sheet.Cells[row, 1].Value != null; row++)
        //    {
        //        var product = new LootProduct
        //        {
        //            Barcode = sheet.Cells[row, 1].Text.Trim(),
        //            Box_Id = sheet.Cells[row, 2].Text.Trim(),
        //            InitialQuantity = int.TryParse(sheet.Cells[row, 3].Text, out var q) ? q : 0,
        //            ScannedQuantity = 0,
        //            CreatedAt = DateTime.UtcNow,
        //            UpdatedAt = DateTime.UtcNow
        //        };

        //        await _repo.AddAsync(product);
        //    }

        //    await Shell.Current.DisplayAlert("Import Complete", "Loots data imported successfully!", "OK");
        //}
        //catch (Exception ex)
        //{
        //    await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        //}
    }
    private Task OnClear() => Shell.Current.DisplayAlert("LOOTS", "Clear clicked", "OK");
}