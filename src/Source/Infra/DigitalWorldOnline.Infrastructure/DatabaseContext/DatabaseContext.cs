
using DigitalWorldOnline.Commons.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DigitalWorldOnline.Infrastructure
{
    public partial class DatabaseContext : DbContext
    {
        private const string DatabaseConnectionString = "Database:Connection";
        private readonly IConfiguration _configuration;
        private readonly bool _cliInitialization;

        public DatabaseContext()
        {
            _cliInitialization = true;
        }

        public DatabaseContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            try
            {
                optionsBuilder
                    .UseSqlServer("Server=192.168.0.100;Database=DMOX;User Id=sa;Password=Tb6!kV7-yM!z7FS#BevB;TrustServerCertificate=True")
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            }
            catch (SqlException ex)
            {
                Console.WriteLine("Error connecting to the database:\n" + ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error configuring the database connection:\n" + ex.Message);
                throw;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            SharedEntityConfiguration(modelBuilder);
            AccountEntityConfiguration(modelBuilder);
            AssetsEntityConfiguration(modelBuilder);
            CharacterEntityConfiguration(modelBuilder);
            ConfigEntityConfiguration(modelBuilder);
            DigimonEntityConfiguration(modelBuilder);
            EventEntityConfiguration(modelBuilder);
            SecurityEntityConfiguration(modelBuilder);
            ShopEntityConfiguration(modelBuilder);
            MechanicsEntityConfiguration(modelBuilder);
            RoutineEntityConfiguration(modelBuilder);
            ArenaEntityConfiguration(modelBuilder);
        }
    }
}