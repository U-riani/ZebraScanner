using System.Collections.ObjectModel;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Core.Dtos;
using CommunityToolkit.Mvvm.Input;
using ZebraSCannerTest1.UI.Views;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.UI.ViewModels;

public class TasksViewModel
{
    private readonly ApiInventoryService _inventoryService = new();

    public ObservableCollection<PocketDocumentDto> Documents { get; set; }
        = new();

    public IRelayCommand<PocketDocumentDto> NavigateToScanningProcessCommand { get; }

    public TasksViewModel()
    {
        NavigateToScanningProcessCommand = new RelayCommand<PocketDocumentDto>(async (doc) =>
        {
            if (doc == null)
                return;

            InventoryMode mode = doc.scan_type == "loots"
                ? InventoryMode.Loots
                : InventoryMode.Standard;

            await Shell.Current.GoToAsync(nameof(InventorizationMenuPage),
                new Dictionary<string, object>
                {
                    ["Mode"] = mode,
                    ["DocumentId"] = doc.id,
                    ["ServerDbModule"] = doc.doc_module
                });
        });
    }
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