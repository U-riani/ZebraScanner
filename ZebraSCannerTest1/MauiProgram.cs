using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.ViewModels;
using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1
{
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

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "zebraData.db");

            // //  Delete existing database for testing purposes
            //if (File.Exists(dbPath))
            //{
            //    File.Delete(dbPath);
            //    Console.WriteLine("Database deleted.");
            //}

            builder.Services.AddSingleton<AppDbContext>(s =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={dbPath}")
                    .Options;
                var db = new AppDbContext(options);
                db.Database.EnsureCreated(); // optional: create DB if not exists
                return db;
            });

            builder.Services.AddSingleton<MainViewModel>();

            builder.Services.AddTransient<DetailsViewModel>();

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<DetailsPage>();




#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
