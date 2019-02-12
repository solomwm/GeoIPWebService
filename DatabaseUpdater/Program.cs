using Database;
using System;
using System.IO;
using System.Xml.Serialization;
using Tools;

namespace DatabaseUpdater
{
    class Program
    {
        const string connectionString_Default = "Server=127.0.0.1; Port=5432; Database=GeoIPStore; User ID=postgres; Password=admin";
        const string cashFolder_Default = "cash";
        const string tempFolder_Default = "tmp";
        const string locations_CSV_FileName_Default = "GeoLite2-City-Locations-ru.csv";
        const string blocksIPv4_CSV_FileName_Default = "GeoLite2-City-Blocks-IPv4.csv";
        const string blocksIPv6_CSV_FileName_Default = "GeoLite2-City-Blocks-IPv6.csv";
        const string md5FileUrl_Default = "http://geolite.maxmind.com/download/geoip/database/GeoLite2-City-CSV.zip.md5";
        const string dataFileUrl_Deafault = "http://geolite.maxmind.com/download/geoip/database/GeoLite2-City-CSV.zip";

        static void Main(string[] args)
        {
            string configFileName = Directory.GetCurrentDirectory() + @"\Config.xml";
            Config config = ApplicationInitialize(configFileName);

            if (config != null)
            {
                try
                {
                    using (GeoIPDbContext db = new GeoIPDbContext(config.ConnectionString))
                    {
                        Console.WriteLine("Подключение к базе...");
                        var dbCreated = db.Database.EnsureCreated();
                        Console.WriteLine($"Подключение установлено: {db.Database.ProviderName}");
                        if (CheckUpdates(db, config))
                        {
                            Console.WriteLine("Обновить сейчас? (y/n)");
                            int answ = Console.Read();
                            if (answ == 'y' || answ == 'Y')
                            {
                                DoOperations(db, config);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
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
                    BlocksIPv4_CSV_FileName = blocksIPv4_CSV_FileName_Default,
                    BlocksIPv6_CSV_FileName = blocksIPv6_CSV_FileName_Default,
                    MD5FileUrl = md5FileUrl_Default,
                    DataFileUrl = dataFileUrl_Deafault
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

        static void DoOperations(GeoIPDbContext dbContext, Config config)
        {
            DateTime start = DateTime.Now;
            DateTime finish;
            DateTime startNext;

            //download updates;
            string dataFileName, md5Hash;
            startNext = DateTime.Now;
            Console.WriteLine($"Загрузка обновлений: {startNext}");
            if (Utilites.DownloadFile(config.MD5FileUrl, config.TempFolder, out string md5FileName))
            {
                using (StreamReader md5Reader = new StreamReader(md5FileName))
                {
                    md5Hash = md5Reader.ReadLine();
                }
                if (Utilites.DownloadFile(config.DataFileUrl, config.TempFolder, md5Hash, out dataFileName, out bool checkRes))
                {
                    Console.WriteLine($"Check MD5: {(checkRes ? "OK" : "failed")}");
                }
                else return;
            }
            else return;
            finish = DateTime.Now;
            Console.WriteLine($"Загрузка завершена: {finish - startNext}");

            //extract updates;
            startNext = DateTime.Now;
            Console.WriteLine($"Распаковка обновлений: {startNext}");
            string[] extractedFiles = Utilites.ExtractFromZip(dataFileName, config.CashFolder,
                        new string[] { config.Locations_CSV_FileName, config.BlocksIPv4_CSV_FileName, config.BlocksIPv6_CSV_FileName });
            finish = DateTime.Now;
            Console.WriteLine($"Распаковка завершена: {finish - startNext}");

            //remove temporary files and ordering data;
            startNext = DateTime.Now;
            Console.WriteLine($"Удаление временных файлов: {startNext}");
            string blocksIPv4FileName, blocksIPv6FileName;
            string locationsFileName = blocksIPv4FileName = blocksIPv6FileName = string.Empty;

            for (int i = 0; i < extractedFiles.Length; i++)
            {
                if (extractedFiles[i].EndsWith(config.Locations_CSV_FileName)) locationsFileName = extractedFiles[i];
                else if (extractedFiles[i].EndsWith(config.BlocksIPv4_CSV_FileName)) blocksIPv4FileName = extractedFiles[i];
                else if (extractedFiles[i].EndsWith(config.BlocksIPv6_CSV_FileName)) blocksIPv6FileName = extractedFiles[i];
            }

            string cashedFilesPath = Path.GetDirectoryName(locationsFileName);
            File.Move(md5FileName, Path.Combine(cashedFilesPath, Path.GetFileName(md5FileName)));
            File.Move(dataFileName, Path.Combine(cashedFilesPath, Path.GetFileName(dataFileName)));
            finish = DateTime.Now;
            Console.WriteLine($"Удаление завершено: {finish - startNext}");

            //install updates;
            startNext = DateTime.Now;
            Console.WriteLine($"Установка обновлений: {startNext}");
            bool updRes = Updater.DatabaseUpdate(config.ConnectionString, blocksIPv4FileName, blocksIPv6FileName, locationsFileName);
            if (updRes)
            {
                dbContext.Updates.Add(new Database.Models.UpdateInfo { Hash = md5Hash, DateTime = DateTime.Now });
                dbContext.SaveChanges();
            }
            finish = DateTime.Now;
            Console.WriteLine($"{(updRes ? "Установка успешно завершена: " : "Установка завершена с ошибками: ")} " +
                $"{finish - startNext}");
            Console.WriteLine($"Затраченное время: {finish - start}");
        }

        static bool CheckUpdates(GeoIPDbContext dbContext, Config config)
        {
            DateTime start = DateTime.Now;
            Console.WriteLine($"Выполняется проверка обновлений: {start}");
            bool checkResult = Updater.CheckUpdate(Updater.GetLastUpdateInfo(dbContext), config.MD5FileUrl, out string message);
            DateTime finish = DateTime.Now;
            Console.WriteLine($"Проверка обновлений завершена за {finish - start} с сообщением: {message}");
            return checkResult;
        }
    }
}
