using Database;
using Database.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Timers;

namespace DatabaseUpdater
{
    static class Updater
    {
        //Обновляет базу.
        public static bool DatabaseUpdate(string connectionString, string blockIPv4FileName, string blockIPv6FileName, 
            string locationFileName)
        {
            DateTime start, startNew, finish;
            bool resultLoc = false;
            bool resultBlockv4 = false;
            bool resultBlockv6 = false;
            start = DateTime.Now;
            Console.WriteLine($"Start update operations now: {start}");

            try
            {
                startNew = DateTime.Now;
                Console.WriteLine($"Start clear data tables: {startNew}");
                ClearDbTables(connectionString);
                finish = DateTime.Now;
                Console.WriteLine($"Data tables clear complete at: {finish - startNew}");

                startNew = DateTime.Now;
                Console.WriteLine($"Start update locations: {startNew}");
                resultLoc = BatchUpdateDbTable(connectionString, "\"CityLocations\"", locationFileName);
                finish = DateTime.Now;
                Console.WriteLine($"Locations update complete at: {finish - startNew}");

                startNew = DateTime.Now;
                Console.WriteLine($"Start update blocks IP v4: {startNew}");
                resultBlockv4 = BatchUpdateDbTable(connectionString, "\"Blocks_IPv4\"", blockIPv4FileName);
                finish = DateTime.Now;
                Console.WriteLine($"Blocks IP v4 update complete at: {finish - startNew}");

                startNew = DateTime.Now;
                Console.WriteLine($"Start update blocks IP v6: {startNew}");
                resultBlockv6 = BatchUpdateDbTable(connectionString, "\"Blocks_IPv6\"", blockIPv6FileName);
                finish = DateTime.Now;
                Console.WriteLine($"Blocks IP v6 update complete at: {finish - startNew}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            bool result = resultLoc && resultBlockv4 && resultBlockv6;
            finish = DateTime.Now;
            Console.WriteLine(value: $"{(result ? "Database update compleate at: " : "An error occurred while updating database: ")} " +
                $"{finish - start}");
            return result;
        }

        //Удаляет базу и пересоздаёт её заново с пустыми таблицами.
        public static void DatabaseRebuild(GeoIPDbContext dbContext)
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        //Проверяет наличие новых обновлений на сервере. (+)
        public static bool CheckUpdate(UpdateInfo lastUpdate, string md5Url, out string message)
        {
            string md5Data = null;
            bool result;

            if (lastUpdate != null)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        md5Data = client.DownloadString(md5Url);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    message = "failed to load data from server";
                    return false;
                }
                result = !(lastUpdate.Hash.ToUpper().Trim() == md5Data.ToUpper().Trim());
            }
            else result = true;

            if (result) message = "new update available";
            else message = "actual version installed";
            return result;
        }

        //Возвращает сущность последнего установленного обновления. (+)
        public static UpdateInfo GetLastUpdateInfo(GeoIPDbContext dbContext)
        {
            if (dbContext.Updates.Count() > 0)
                return dbContext.Updates.OrderBy(u => u.Id).Last();
            else return null;
        }
        
        //Пакетное обновление таблицы базы данных. (++)
        private static bool BatchUpdateDbTable(string connectionString, string tableName, string csvFilePath)
        {
            string copyFromCommand = $"COPY {tableName} FROM STDIN WITH (FORMAT CSV, HEADER TRUE)";
            int allRowsCount = 0;
            int rowCount = 0;
            bool result = false;
            if (File.Exists(csvFilePath))
            {
                Timer timer = new Timer(8000) { AutoReset = true };
                timer.Elapsed += delegate
                {
                    Console.WriteLine($"{rowCount} rows from {allRowsCount} complete. ({(float)rowCount/allRowsCount:p2})");
                    Console.CursorTop--;
                };

                IEnumerable<string> allRows = File.ReadLines(csvFilePath).Where(rw => rw != string.Empty);
                allRowsCount = allRows.Count();
                connectionString = connectionString + "; Pooling=false; Keepalive=30;"; //Если строк в таблице очень много (более 250 000), writer.Close() не хватает стандартного времени до закрытия connection.
                timer.Start();

                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    try
                    {
                        TextWriter writer = connection.BeginTextImport(copyFromCommand);
                        int flushCount = 0;
                        foreach (string dataRow in allRows)
                        {
                            writer.WriteLine(dataRow);
                            rowCount++;
                            flushCount++;
                            if (flushCount >= 200000)
                            {
                                writer.Flush();
                                flushCount = 0;
                            }
                        }
                        timer.Stop();
                        Console.WriteLine($"{rowCount} rows from {allRowsCount} complete. (100%)");
                        DateTime start = DateTime.Now; 
                        Console.WriteLine("Commit data to server...");
                        writer.Close(); //"Exeption while reading from stream", if DataTable have to many rows and "Keepalive=default" in connectionString. See line 128. 
                        DateTime finish = DateTime.Now;
                        Console.WriteLine($"Complete at: {finish - start}");
                        result = true;
                    }
                    catch (Exception e)
                    {
                        timer.Stop();
                        Console.WriteLine(e.Message);
                        result = false;
                    }
                }
            }
            else throw new Exception($"File {csvFilePath} not found.");
            return result;
        }

        //Очищает таблицы в базе.
        private static void ClearDbTables(string connectionString)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                NpgsqlCommand clearTable = new NpgsqlCommand("TRUNCATE TABLE \"Blocks_IPv4\"", connection); //truncate практически мгновенно очищает таблицу.
                clearTable.ExecuteNonQuery();
                clearTable = new NpgsqlCommand("TRUNCATE TABLE \"Blocks_IPv6\"", connection);
                clearTable.ExecuteNonQuery();
                clearTable = new NpgsqlCommand("DELETE FROM \"CityLocations\"", connection); //delete работает гораздо медленее чем truncate, но truncate нельзя применять к таблицам, на которые ссылается внешний ключ. 
                clearTable.ExecuteNonQuery();
            }
        }
    }
}
