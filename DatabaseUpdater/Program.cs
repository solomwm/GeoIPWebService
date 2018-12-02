using Database;
using Database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DatabaseUpdater
{
    class Program
    {
        const string connectionString_Default = "Server=127.0.0.1; Port=5432; Database=GeoIPStore; User ID=postgres; Password=admin";
        const string cashFolder_Default = "cash";
        const string tempFolder_Default = "tmp";
        const string locations_CSV_FileName_Default = "GeoLite2-City-Locations-ru.csv";
        const string blocksIPv4_CSV_FileName_Default = "GeoLite2-City-Blocks-IPv4.csv";

        static void Main(string[] args)
        {
            string configFileName = Directory.GetCurrentDirectory() + @"\Config.xml";
            Config config = ApplicationInitialize(configFileName);

            if (config != null)
            {
                try
                {
                    using (var db = new GeoIPDbContext(config.ConnectionString))
                    {
                        var dbCreated = db.Database.EnsureCreated();
                        //Console.WriteLine(dbCreated);
                        string locationsFileName = string.Concat(config.TempFolder, @"\", config.Locations_CSV_FileName);
                        string blocksIPv4FileName = string.Concat(config.TempFolder, @"\", config.BlocksIPv4_CSV_FileName);
                        DateTime start = DateTime.Now;
                        Console.WriteLine($"Старт обновления: {start}");
                        //Updater.DatabaseUpdate(db, blocksIPv4FileName, locationsFileName);
                        Updater.DatabaseRebuild(db, blocksIPv4FileName, locationsFileName, dbCreated);
                        DateTime finish = DateTime.Now;
                        TimeSpan difference = finish - start;
                        Console.WriteLine($"Затраченное время: {difference}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Не удалось подключиться к базе данных.");
                    Console.WriteLine("Файл конфигурации: " + configFileName);
                    Console.WriteLine("Строка подключения: \"" + config.ConnectionString + "\"");
                }
            }
            else Console.WriteLine("failed to create application configuration");
        }

        static Config ApplicationInitialize(string configFileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            string currentDirectory = Directory.GetCurrentDirectory();
            Config config = null;

            if (File.Exists(configFileName))
            {
                try
                {
                    using (var fr = new StreamReader(configFileName))
                    {
                        config = serializer.Deserialize(fr) as Config;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }
            else
            {
                config = new Config()
                {
                    ConnectionString = connectionString_Default,
                    CashFolder = currentDirectory + @"\" + cashFolder_Default,
                    TempFolder = currentDirectory + @"\" + tempFolder_Default,
                    Locations_CSV_FileName = locations_CSV_FileName_Default,
                    BlocksIPv4_CSV_FileName = blocksIPv4_CSV_FileName_Default
                };
                try
                {
                    using (var fw = new StreamWriter(configFileName))
                    {
                        serializer.Serialize(fw, config);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }

            try
            {
                Directory.CreateDirectory(config.CashFolder);
                Directory.CreateDirectory(config.TempFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                config = null;
            }
            
            return config;
        }
    }
}
