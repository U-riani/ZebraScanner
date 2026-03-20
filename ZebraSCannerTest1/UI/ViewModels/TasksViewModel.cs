using System.Collections.ObjectModel;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Core.Dtos;
using CommunityToolkit.Mvvm.Input;

namespace ZebraSCannerTest1.UI.ViewModels;

public class TasksViewModel
{
    private readonly ApiInventoryService _inventoryService = new();

    public ObservableCollection<InventorizationDocumentDto> Documents { get; set; }
        = new();

    public IRelayCommand NavigateToScanningProcessCommand { get; }
    public async Task LoadDocuments(string token)
    {
        var docs = await _inventoryService.GetDocuments(token);

        if (docs == null)
            return;

        Documents.Clear();

        foreach (var doc in docs)
            Documents.Add(doc);
    }
}