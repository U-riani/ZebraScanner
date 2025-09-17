using Microsoft.EntityFrameworkCore;
using ZebraSCannerTest1.Models;


namespace ZebraSCannerTest1.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<TestModel> Products { get; set; }
        //private string _dbPath;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            // Ensure database is created when app starts
            Database.EnsureCreated();
        }

        
    }
}
