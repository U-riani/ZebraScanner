using CommunityToolkit.Maui;
using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Infrastructure.Repositories;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1;

public static class MauiProgram
{
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
        builder.Services.AddSingleton<SqliteConnection>(_ => DatabaseInitializer.GetConnection());

        // === Core services & repositories ===
        builder.Services.AddSingleton<IProductRepository, ProductRepository>();
        builder.Services.AddSingleton<IScanLogRepository, ScanLogRepository>();
        builder.Services.AddSingleton<IDataImportService, DataImportService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();
        builder.Services.AddSingleton<IExcelExportLogsService, ExcelExportLogsService>(); // ✅ Add this line
        builder.Services.AddSingleton<LogBufferService>();
        builder.Services.AddSingleton<ClipboardService>();
        builder.Services.AddSingleton<IScanningService, ScanningService>();



        // === UI helpers ===
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));
        builder.Services.AddSingleton<ZebraSCannerTest1.UI.Services.PopupService>(); // ✅ Add this line


        // === ViewModels ===
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<DetailsViewModel>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<ScannedProductsViewModel>();

        // === Views ===
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<ScannedProductsPage>();

        return builder.Build();
    }
}
