using Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Database
{
    public class GeoIPDbContext: DbContext
    {
        private readonly string connectionString;

        public DbSet<CityLocation> CityLocations { get; set; }
        public DbSet<BlockIPv4> Blocks_IPv4 { get; set; }
        public DbSet<IPv4> IPs_v4 { get; set; }

        // Используется консольным приложением.
        public GeoIPDbContext(string connection): base()
        {
            connectionString = connection;
        }

        // Используется веб-сервисом.
        public GeoIPDbContext(DbContextOptions<GeoIPDbContext> options) : base(options)
        {
            // Провайдер и строка подключения передаются в options.
        }

        // Если консольное приложение - конфигурируем провайдера.
        // Если веб-сервис - не делаем ничего (провайдер уже сконфигурирован).
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
           if (connectionString != null) optionsBuilder.UseNpgsql(connectionString);
           optionsBuilder.EnableSensitiveDataLogging();
        }
    }
}
