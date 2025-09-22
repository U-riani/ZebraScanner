using Microsoft.Extensions.Logging;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.ViewModels;
using ZebraSCannerTest1.Views;
using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ✅ Initialize DB
        var conn = DatabaseInitializer.GetConnection();
        builder.Services.AddSingleton<SqliteConnection>(conn);

        // ✅ Register services
        builder.Services.AddSingleton<ExcelImportService>();

        // ✅ Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<DetailsViewModel>();
        builder.Services.AddTransient<LogsViewModel>();

        // ✅ Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LogsPage>();

        return builder.Build();
    }
}
