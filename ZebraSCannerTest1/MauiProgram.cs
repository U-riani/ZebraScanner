using CommunityToolkit.Maui;
//using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.ViewModels;
using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // ✅ Register CommunityToolkit
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // DB connection
        builder.Services.AddSingleton(sp => DatabaseInitializer.GetConnection());

        // Services
        builder.Services.AddSingleton<ExcelImportService>();
        builder.Services.AddSingleton<ExcelExportService>();
        builder.Services.AddSingleton<LogBufferService>();
        builder.Services.AddSingleton<ClipboardService>();


        // VMs
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<DetailsViewModel>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<ScannedProductsViewModel>();

        // Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<ScannedProductsPage>();

        return builder.Build();
    }
}
