using Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var db = new GeoIPDbContext("Server=127.0.0.1; Port=5432; Database=GeoIPStore; User ID=postgres; Password=admin"))
            {
                var dbCreated = db.Database.EnsureCreated();
                Console.WriteLine(dbCreated);
            }
        }
    }
}
