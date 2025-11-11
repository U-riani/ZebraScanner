using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;

public partial class InventorizationByLootsViewModel : ObservableObject, IDisposable
{
    private readonly ILootsProductRepository _repo;

    [ObservableProperty] private string filterText = string.Empty;
    [ObservableProperty] private ObservableCollection<LootBoxSummary> filteredLoots = new();
    [ObservableProperty] private ObservableCollection<LootBoxSummary> loots = new();

    [ObservableProperty] private bool isBusy;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<string> OpenLootCommand { get; }
    public IRelayCommand ApplyFilterCommand { get; }


    public InventorizationByLootsViewModel(ILootsProductRepository repo)
    {
        _repo = repo;
        RefreshCommand = new AsyncRelayCommand(LoadLootsAsync);
        OpenLootCommand = new AsyncRelayCommand<string>(OpenLootAsync);
        ApplyFilterCommand = new RelayCommand(ApplyFilter);

        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(
            this, async (_, _) => await LoadLootsAsync());

        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilterText))
                ApplyFilter();
        };
    }

    public async Task LoadLootsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var items = await _repo.GetAllAsync();

            var grouped = items
                .GroupBy(p => p.Box_Id ?? "Unknown")
                .Select(g => new LootBoxSummary
                {
                    Box_Id = g.Key,
                    InitialQuantity = g.Sum(x => x.InitialQuantity),
                    ScannedQuantity = g.Sum(x => x.ScannedQuantity)
                })
                .OrderBy(x => x.Box_Id)
                .ToList();

            Loots.Clear();
            foreach (var g in grouped)
                Loots.Add(g);

            // ✅ Also refresh filtered view after reload
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load loots: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            // show all when entry is empty
            FilteredLoots = new ObservableCollection<LootBoxSummary>(Loots);
            return;
        }

        var lower = FilterText.Trim().ToLowerInvariant();
        var filtered = Loots
            .Where(l => !string.IsNullOrEmpty(l.Box_Id) && l.Box_Id.ToLowerInvariant().Contains(lower))
            .ToList();

        FilteredLoots = new ObservableCollection<LootBoxSummary>(filtered);
    }



    private async Task OpenLootAsync(string boxId)
    {
        if (string.IsNullOrWhiteSpace(boxId))
            return;

        // Navigate to main InventorizationPage but pass the boxId
        var parameters = new Dictionary<string, object>
        {
            ["BoxId"] = boxId
        };

        await Shell.Current.GoToAsync(nameof(LootsScanningPage), true,
            new Dictionary<string, object>
            {
                ["BoxId"] = boxId
            });


    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}

public class LootBoxSummary
{
    public string Box_Id { get; set; } = string.Empty;
    public int InitialQuantity { get; set; }
    public int ScannedQuantity { get; set; }

    public string TotalInfo => $"{ScannedQuantity} / {InitialQuantity}";
}


