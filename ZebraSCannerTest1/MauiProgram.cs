using CommunityToolkit.Maui;
using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Infrastructure.Repositories;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1;

public static class MauiProgram
{
    public static IServiceProvider ServiceProvider { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // === Database connection ===
        builder.Services.AddTransient<IDbFactory, DbFactory>();


        // === Core services & repositories ===
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddTransient<IProductRepository, ProductRepository>();
        builder.Services.AddTransient<IScanLogRepository, ScanLogRepository>();
        builder.Services.AddTransient<ILootsProductRepository, LootsProductRepository>();
        builder.Services.AddSingleton<SalesRepository>();
        builder.Services.AddTransient<IDataImportService, DataImportService>();
        builder.Services.AddTransient<IExcelExportService, ExcelExportService>();
        builder.Services.AddTransient<IExcelExportLogsService, ExcelExportLogsService>(); // ✅ Add this line
        builder.Services.AddSingleton<LogBufferService>();
        builder.Services.AddSingleton<ClipboardService>();
        builder.Services.AddTransient<IScanningService, ScanningService>();
        builder.Services.AddTransient<IProductService, ProductService>();
        builder.Services.AddTransient<IMenuService, MenuService>();
        builder.Services.AddTransient<IJsonExportService, JsonExportService>();
        builder.Services.AddTransient<IJsonExportLogsService, JsonExportLogsService>();
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddTransient<IServerImportService, ServerImportService>();
        builder.Services.AddSingleton<SalesExcelImportService>();
        builder.Services.AddSingleton<ApiInventoryService>();

        // === UI helpers ===
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));
        builder.Services.AddSingleton<ZebraSCannerTest1.UI.Services.PopupService>(); // ✅ Add this line


        // === ViewModels ===
        builder.Services.AddTransient<InventorizationViewModel>();
        builder.Services.AddTransient<DetailsViewModel>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<ScannedProductsViewModel>();
        builder.Services.AddTransient<ShellViewModel>();
        builder.Services.AddTransient<InventorizationByLootsMenuViewModel>();
        builder.Services.AddTransient<InventorizationByLootsViewModel>();
        builder.Services.AddTransient<LootsScanningViewModel>();
        builder.Services.AddTransient<InventorizationMenuViewModel>();
        builder.Services.AddTransient<SalesMenuViewModel>();
        builder.Services.AddTransient<SalesViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();


        // === Views ===
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoginPage>();

        builder.Services.AddTransient<InventorizationPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<ScannedProductsPage>();
        builder.Services.AddTransient<InventorizationByLootsMenuPage>();
        builder.Services.AddTransient<InventorizationByLootsPage>();
        builder.Services.AddTransient<LootsScanningPage>();
        builder.Services.AddTransient<InventorizationMenuPage>();
        builder.Services.AddTransient<SalesMenuPage>();
        builder.Services.AddTransient<SalesPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TasksPage>();

        var app = builder.Build();

        // 🔹 This line exposes the container globally
        ServiceProvider = app.Services;

        return app;
    }
}
