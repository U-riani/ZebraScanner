using Microsoft.EntityFrameworkCore;
using ZebraSCannerTest1.Models;


namespace ZebraSCannerTest1.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<InitialProduct> InitialProducts { get; set; }
        public DbSet<ScannedProduct> ScannedProducts { get; set; }
        public DbSet<ScanLog> ScanLogs { get; set; }   


        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Optional: seed some initial data
            //modelBuilder.Entity<InitialProduct>().HasData(
            //    new InitialProduct { Id = 1, Name = "1234567890", Quantity = 10 },
            //    new InitialProduct { Id = 2, Name = "15060715", Quantity = 5 },
            //    new InitialProduct { Id = 3, Name = "10123456789012345672", Quantity = 8 }
            //);
        }

    }
}
