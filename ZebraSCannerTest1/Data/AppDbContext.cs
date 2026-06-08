//using Microsoft.EntityFrameworkCore;
//using ZebraSCannerTest1.Models;

//namespace ZebraSCannerTest1.Data
//{
//    public class AppDbContext : DbContext
//    {
//        public DbSet<InitialProduct> InitialProducts { get; set; }
//        public DbSet<ScannedProduct> ScannedProducts { get; set; }
//        public DbSet<ScanLog> ScanLogs { get; set; }

//        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

//        protected override void OnModelCreating(ModelBuilder modelBuilder)
//        {
//            base.OnModelCreating(modelBuilder);

//            // Ensure barcode is indexed for faster lookups
//            modelBuilder.Entity<InitialProduct>()
//                .HasIndex(p => p.Barcode)
//                .IsUnique(false);

//            modelBuilder.Entity<ScannedProduct>()
//                .HasIndex(p => p.Barcode)
//                .IsUnique(false);
//        }
//    }
//}
