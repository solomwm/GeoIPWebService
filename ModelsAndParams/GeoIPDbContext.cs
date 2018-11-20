using Database.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Database
{
    public class GeoIPDbContext: DbContext
    {
        private readonly string connectionString;

        public DbSet<CityLocation> CityLocations { get; set; }
        public DbSet<BlockIPv4> Bocks_IPv4 { get; set; }
        public DbSet<IPv4> IPs_v4 { get; set; }

        public GeoIPDbContext(string connection): base()
        {
            connectionString = connection;
        }

        public GeoIPDbContext(DbContextOptions<GeoIPDbContext> options): base(options)
        { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}
