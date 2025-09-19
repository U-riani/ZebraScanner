using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;
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

            //  Delete existing database for testing purposes
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
                Console.WriteLine("Database deleted.");
            }

            builder.Services.AddSingleton<AppDbContext>(s =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={dbPath}")
                    .Options;
                var db = new AppDbContext(options);
                db.Database.EnsureCreated(); // optional: create DB if not exists
                //db.Database.Migrate();

                // seeding initial data if table is empty
                //if (!db.InitialProducts.Any())
                //{
                //    db.InitialProducts.AddRange(
                //        new InitialProduct { Id = 1, Barcode = "1234567890", Quantity = 10 },
                //        new InitialProduct { Id = 2, Barcode = "15060715", Quantity = 5 },
                //        new InitialProduct { Id = 3, Barcode = "10123456789012345672", Quantity = 8 },
                //        new InitialProduct { Id = 4, Barcode = "0101234567891231", Quantity = 3 },
                //        new InitialProduct { Id = 5, Barcode = "1012345678912345678", Quantity = 11 }


                //    );
                //    db.SaveChanges();
                //}


                return db;
            });

            //builder.Services.AddSingleton<MainViewModel>();

            //builder.Services.AddTransient<DetailsViewModel>();

            //builder.Services.AddSingleton<MainPage>();
            //builder.Services.AddTransient<DetailsPage>();
            //builder.Services.AddSingleton<LogsViewModel>();

            // ViewModels
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<DetailsViewModel>();
            builder.Services.AddTransient<LogsViewModel>();

            // Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<DetailsPage>();
            builder.Services.AddTransient<LogsPage>();
            builder.Services.AddScoped<ExcelImportService>();



#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
