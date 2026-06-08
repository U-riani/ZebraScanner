using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;

public partial class InventorizationByLootsViewModel : ObservableObject
{
    private readonly ILootsProductRepository _repo;

    [ObservableProperty] private string filterText = string.Empty;
    [ObservableProperty] private ObservableCollection<LootBoxSummary> filteredLoots = new();
    [ObservableProperty] private ObservableCollection<LootBoxSummary> loots = new();

    [ObservableProperty] private bool isBusy;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<string> OpenLootCommand { get; }
    public IRelayCommand ApplyFilterCommand { get; }
    public IAsyncRelayCommand OpenNewLootCommand { get; }


    public InventorizationByLootsViewModel(ILootsProductRepository repo)
    {
        _repo = repo;
        RefreshCommand = new AsyncRelayCommand(LoadLootsAsync);
        OpenLootCommand = new AsyncRelayCommand<string>(OpenLootAsync);
        OpenNewLootCommand = new AsyncRelayCommand(OpenNewLootAsync);
        ApplyFilterCommand = new RelayCommand(ApplyFilter);

        PropertyChanged += (_, e) =>
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
            var grouped = await Task.Run(async () => (await _repo.GetBoxSummariesAsync()).ToList());

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Loots.Clear();
                foreach (var g in grouped)
                    Loots.Add(g);

                ApplyFilter();
            });
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

    // Example: when navigating to Settings from LootsScanningPage
    private async Task GoToSettings()
    {
        var vm = new SettingsViewModel(); // or resolve from DI
        vm.SetLootsMode(true); // ← set true because we are in Loots mode

        await Shell.Current.GoToAsync(nameof(SettingsPage), true, new Dictionary<string, object>
        {
            ["BindingContext"] = vm // optional if using DI
        });
    }

    private async Task OpenLootAsync(string boxId)
    {
        if (string.IsNullOrWhiteSpace(boxId))
            return;

        await Shell.Current.GoToAsync(nameof(LootsScanningPage), false,
            new Dictionary<string, object>
            {
                ["BoxId"] = boxId.Trim()
            });
    }

    private async Task OpenNewLootAsync()
    {
        await Shell.Current.GoToAsync(nameof(LootsScanningPage), false,
            new Dictionary<string, object>
            {
                ["BoxId"] = string.Empty,
                ["OpenSetBox"] = true
            });
    }

}
