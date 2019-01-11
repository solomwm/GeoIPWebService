using Database;
using Database.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Tools;
using Timer = System.Timers.Timer;

namespace DatabaseUpdater
{
    static class Updater
    {
        const string block_IPv4_File_Header = "network,geoname_id,registered_country_geoname_id,represented_country_geoname_id,is_anonymous_proxy,is_satellite_provider,postal_code,latitude,longitude,accuracy_radius";
        const string location_File_Header = "geoname_id,locale_code,continent_code,continent_name,country_iso_code,country_name,subdivision_1_iso_code,subdivision_1_name,subdivision_2_iso_code,subdivision_2_name,city_name,metro_code,time_zone,is_in_european_union";

        //Пересоздаёт базу и заполняет таблицы с нуля (если esureCreated == false, иначе считается, что база только-что создана и таблицы пустые.)
        public static void DatabaseRebuild(GeoIPDbContext dbContext, string blockIPv4FileName, string locationFileName, 
                                           bool createNew)
        {
            try
            {
                DateTime start, startNew, finish;

                //Парсим файл локаций. Создаём сущности CityLocation.
                start = DateTime.Now;
                Console.WriteLine($"start parsing locations: {start}");
                List<CityLocation> locations = ParseAllLocations(locationFileName);
                locations.Add(new CityLocation { Geoname_Id = 0 }); //Необходимо добавить такую локацию для анонимных IP-адресов.
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing locations from: {finish - start}");

                //Парсим файл блоков IP. Создаём сущности BlocksIPv4 и IPv4.
                start = DateTime.Now;
                Console.WriteLine($"start parsing block IP's: {start}");
                List<BlockIPv4> blocks = ParseAllBlocksIPv4(blockIPv4FileName, out List<IPv4> ip_s);
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing block IP's from: {finish - start}");

                //Обновляем данные в базе.
                start = DateTime.Now;
                Console.WriteLine($"start update Database: {start}");

                //Если указано, пересоздаём базу.
                if (createNew)
                {
                    dbContext.Database.EnsureDeleted();
                    dbContext.Database.EnsureCreated();

                    //Сохраняем локации
                    dbContext.CityLocations.AddRange(locations);
                    locations.Clear();
                    dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                    finish = DateTime.Now;
                    Console.WriteLine($"locations complete from: {finish - start}");

                    //Сохраняем блоки
                    startNew = DateTime.Now;
                    SaveBlocks100(dbContext, blocks);
                    Console.WriteLine($"all blocks complete from: {finish - startNew}");

                    //Сохраняем IP-адреса
                    startNew = DateTime.Now;
                    SaveIPs100(dbContext, ip_s);
                    finish = DateTime.Now;
                    Console.WriteLine($"IP's complete from: {finish - startNew}");
                }
                else //иначе обновляем существующие таблицы.
                {
                    //dbContext.Database.
                }

                finish = DateTime.Now;
                Console.WriteLine($"finish update Database from: {finish - start}");
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        //Обновляет базу, проверяя существующие данные на изменение и добавляя новые.
        public static void DatabaseUpdate(GeoIPDbContext dbContext, string blockIPv4FileName, string locationFileName, bool quickUpdate)
        {
            try
            {
                DateTime start, startNew, finish;
                Timer timer = new Timer(30000) { AutoReset = true };

                //Парсим файл локаций. Создаём сущности CityLocation.
                start = DateTime.Now;
                Console.WriteLine($"start parsing locations: {start}");
                List<CityLocation> locations = ParseAllLocations(locationFileName);

                if (!dbContext.CityLocations.Any(l => l.Geoname_Id == 0))
                {
                    locations.Add(new CityLocation { Geoname_Id = 0 }); //Необходимо добавить такую локацию для анонимных IP-адресов, если не сущесвует.
                }

                finish = DateTime.Now;
                Console.WriteLine($"finish parsing locations from: {finish - start}");

                //Парсим файл блоков IP. Создаём сущности BlocksIPv4 и IPv4.
                start = DateTime.Now;
                Console.WriteLine($"start parsing block IP's: {start}");
                List<BlockIPv4> blocks = ParseAllBlocksIPv4(blockIPv4FileName);
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing block IP's from: {finish - start}");

                //Обновляем данные в базе.
                start = DateTime.Now;
                Console.WriteLine($"start update Database: {start}");

                //Обновляем локации.
                //Console.WriteLine("start update locations");
                //CityLocation dbLocation;
                //List<CityLocation> newLocations = new List<CityLocation>();
                //int locCount;
                //int changedLocCount = locCount = 0;

                //timer.Start();
                //timer.Elapsed += delegate
                //{
                //    Console.WriteLine($"{locCount} locations from {locations.Count} complete, {newLocations.Count} " +
                //            $"new locations found, {changedLocCount} locations changed.");
                //    Console.CursorTop--;
                //};

                //foreach (CityLocation location in locations)
                //{
                //    dbLocation = dbContext.CityLocations.FirstOrDefault(l => l.Geoname_Id == location.Geoname_Id);
                //    if (dbLocation == null)
                //    {
                //        newLocations.Add(location);
                //    }
                //    else if (!location.Equals(dbLocation))
                //    {
                //        dbContext.Entry(dbLocation).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                //        dbContext.Update(location);
                //        changedLocCount++;
                //    }
                //    locCount++;
                //}
                //Console.WriteLine($"{locCount} locations from {locations.Count} complete, {newLocations.Count} " +
                //           $"new locations found, {changedLocCount} locations changed.");
                //timer.Stop();

                //locations = null; //Освобождаем память.
                //startNew = DateTime.Now;
                //Console.WriteLine($"\nsaving locations to database {startNew}");
                //dbContext.CityLocations.AddRange(newLocations);
                //dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                //finish = DateTime.Now;
                //Console.WriteLine($"locations complete from: {finish - start}. New locations added: {newLocations.Count}");

                //Обновляем блоки IP.
                BlockIPv4 dbBlock;
                List<BlockIPv4> newBlocks = new List<BlockIPv4>();
                List<IPv4> newIPs = new List<IPv4>();

                //Сортируем таблицы блоков для ускорения поиска.
                dbContext.Blocks_IPv4.OrderBy(b => b.Geoname_Id);
                blocks.OrderBy(b => b.Geoname_Id);

                if (!quickUpdate)
                {
                    startNew = DateTime.Now;
                    Console.WriteLine($"start update blocks: {startNew}");
                    int blocksCount;
                    int changedBlocksCount = blocksCount = 0;
                    timer = new Timer(30000) { AutoReset = true };
                    timer.Start();
                    timer.Elapsed += delegate
                    {
                        Console.WriteLine($"{blocksCount} blocks from {blocks.Count} complete, {newBlocks.Count} " +
                            $"new blocks found, {changedBlocksCount} blocks changed.");
                        Console.CursorTop--;
                    };

                    foreach (BlockIPv4 block in blocks)
                    {
                        dbBlock = dbContext.Blocks_IPv4.FirstOrDefault(b => b.Network == block.Network);
                        if (dbBlock == null)
                        {
                            newBlocks.Add(block);
                            newIPs.Add(ParseIP(block.Network));
                        }
                        else if (!block.Equals(dbBlock))
                        {
                            dbContext.Entry(dbBlock).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                            dbContext.Update(block);
                            changedBlocksCount++;
                        }
                        blocksCount++;
                    }
                    Console.WriteLine($"{blocksCount} blocks from {blocks.Count} complete, {newBlocks.Count} " +
                            $"new blocks found, {changedBlocksCount} blocks changed.");
                    timer.Stop();

                    blocks = null; //Освобождаем память.
                    startNew = DateTime.Now;
                    Console.WriteLine($"\nstart saving changes to database: {startNew}");
                    dbContext.SaveChanges(); //Сохраняем изменения в базе.
                    finish = DateTime.Now;
                    Console.WriteLine($"save changes complete: {finish - startNew}");
                    Console.WriteLine($"blocks update complete from: {finish - start}");
                }

                else //Не проверяем существующие блоки на изменения (потому, что очень долго!!!)
                {
                    startNew = DateTime.Now;
                    Console.WriteLine($"start search new blocks: {startNew}");
                    int blocksCount = 0;
                    timer.Start();
                    timer.Elapsed += delegate 
                    {
                        Console.WriteLine($"{blocksCount} blocks from {blocks.Count} complete, {newBlocks.Count} " +
                            $"new blocks found.");
                        Console.CursorTop--;
                    };

                    foreach (BlockIPv4 block in blocks)
                    {
                        if (!dbContext.Blocks_IPv4.Any(b => b.Network == block.Network))
                        {
                            newBlocks.Add(block);
                            newIPs.Add(ParseIP(block.Network));
                        }
                        blocksCount++;
                    }
                    Console.WriteLine($"{blocksCount} blocks from {blocks.Count} complete, {newBlocks.Count} " +
                           $"new blocks found.");
                    timer.Stop();

                    blocks = null; //Освобождаем память.
                    finish = DateTime.Now;
                    Console.WriteLine($"\nnew blocks search complete from: {finish - start}, new blocks found: {newBlocks.Count}");
                }

                //Сохраняем новые блоки.
                if (newBlocks.Count > 0)
                {
                    startNew = DateTime.Now;
                    Console.WriteLine($"start saving new blocks: {startNew}");
                    int remainder;
                    int newBlocksCount = remainder = newBlocks.Count;
                    SaveBlocks100(dbContext, newBlocks);
                    finish = DateTime.Now;
                    Console.WriteLine($"{newBlocksCount} new blocks saved from: {finish - startNew}");

                    //Сохраняем новые IP.
                    startNew = DateTime.Now;
                    Console.WriteLine($"start saving new IP: {startNew}");
                    SaveIPs100(dbContext, newIPs);
                    finish = DateTime.Now;
                    Console.WriteLine($"{newBlocksCount} new IP's saved from: {finish - startNew}");
                }
                else
                {
                    Console.WriteLine("0 new blocks found");
                }
                Console.WriteLine($"All update operations complete from: {finish - start}");
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        //Обновляет базу, используя ADO DataAdapter и DataTable.
        public static void DatabaseUpdateADO(string connectionString, string blockIPv4FileName, string locationFileName)
        {
            DateTime start, finish;
            start = DateTime.Now;
            Console.WriteLine($"Start now: {start}");
            //TO DO...
            finish = DateTime.Now;
            Console.WriteLine($"Complete at: {finish - start}");
        }

        //Создаёт коллекцию сущностей CityLocation из CSV-файла.
        public static List<CityLocation> ParseAllLocations(string locationFileName)
        {
            List<CityLocation> locations = new List<CityLocation>();
            CityLocation location;
            string[] bufferArr;

            using (var locationFileReader = new StreamReader(locationFileName))
            {
                string locationFileHeader = locationFileReader.ReadLine();
                if (!locationFileHeader.Equals(location_File_Header))
                {
                    throw new Exception($"Некорректный заголовок файла данных {locationFileName}");
                }
                bufferArr = locationFileReader.ReadToEnd().Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }

            locations.Capacity = bufferArr.Length;
            for (int i = 0; i < bufferArr.Length; i++)
            {
                location = ParseLocation(bufferArr[i]);
                if (location != null) locations.Add(location);
            }

            return locations;
        }

        //Создаёт коллекции сущностей BlockIPv4 и IPv4 из CSV-файла.
        public static List<BlockIPv4> ParseAllBlocksIPv4(string blockIPv4FileName, out List<IPv4> ipList)
        {
            BlockIPv4 block;
            IPv4 ip_v4;
            List<BlockIPv4> blocks = new List<BlockIPv4>();
            ipList = new List<IPv4>();

            using (var blockIPv4FileReader = new StreamReader(blockIPv4FileName))
            {
                string blockIPv4FileHeader = blockIPv4FileReader.ReadLine();
                if (!blockIPv4FileHeader.Equals(block_IPv4_File_Header))
                {
                    throw new Exception($"Некорректный заголовок файла данных {blockIPv4FileName}");
                }
                while (!blockIPv4FileReader.EndOfStream)
                {
                    block = ParseBlockIPv4(blockIPv4FileReader.ReadLine());
                    if (block != null)
                    {
                        blocks.Add(block);
                        ip_v4 = ParseIP(block.Network);
                        if (ip_v4 != null) ipList.Add(ip_v4);
                    }
                }
            }
            return blocks;
        }

        //Создаёт коллекцию сущностей BlockIPv4 из CSV-файла.
        public static List<BlockIPv4> ParseAllBlocksIPv4(string blockIPv4FileName)
        {
            BlockIPv4 block;
            List<BlockIPv4> blocks = new List<BlockIPv4>();

            using (var blockIPv4FileReader = new StreamReader(blockIPv4FileName))
            {
                string blockIPv4FileHeader = blockIPv4FileReader.ReadLine();
                if (!blockIPv4FileHeader.Equals(block_IPv4_File_Header))
                {
                    throw new Exception($"Некорректный заголовок файла данных {blockIPv4FileName}");
                }
                while (!blockIPv4FileReader.EndOfStream)
                {
                    block = ParseBlockIPv4(blockIPv4FileReader.ReadLine());
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                }
            }
            return blocks;
        }

        //Проверяет наличие новых обновлений на сервере.
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

        //Возвращает сущность последнего установленного обновления.
        public static UpdateInfo GetLastUpdateInfo(GeoIPDbContext dbContext)
        {
            return dbContext.Updates.OrderBy(u => u.Id).Last();
        }
        
        //Создаёт сущность CityLocation из CSV-строки.    
        private static CityLocation ParseLocation(string sourceData)
        {
            string[] sourceArr;
            if (sourceData.Contains(", "))
            {
                StringBuilder sourceDataBuilder = new StringBuilder(sourceData);
                sourceDataBuilder.Replace(", ", "@");
                sourceDataBuilder.Replace(",", ";");
                sourceDataBuilder.Replace("@", ", ");
                sourceArr = sourceDataBuilder.ToString().Split(new char[] { ';' }, StringSplitOptions.None);
            }
            else sourceArr = sourceData.Split(new char[] { ',' }, StringSplitOptions.None);

            CityLocation location;
            try
            {
                location = new CityLocation
                {
                    Geoname_Id = int.Parse(sourceArr[0]),
                    Local_Code = sourceArr[1],
                    Continent_Code = sourceArr[2],
                    Continent_Name = sourceArr[3],
                    Country_Iso_Code = sourceArr[4],
                    Country_Name = sourceArr[5],
                    Subdivision_1_Iso_Code = sourceArr[6],
                    Subdivision_1_Name = sourceArr[7],
                    Subdivision_2_Iso_Code = sourceArr[8],
                    Subdivision_2_Name = sourceArr[9],
                    City_Name = sourceArr[10],
                    Metro_Code = sourceArr[11],
                    Time_Zone = sourceArr[12],
                    Is_In_European_Union = Convert.ToBoolean(int.Parse(sourceArr[13]))
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(sourceData);
                location = null;
            }
            return location;
        }

        //Создаёт сущность BlockIPv4 из CSV-строки. 
        private static BlockIPv4 ParseBlockIPv4(string sourceData)
        {
            string[] sourceArr = sourceData.Split(new char[] { ',' }, StringSplitOptions.None);
            BlockIPv4 block;
            IFormatProvider provider = new System.Globalization.NumberFormatInfo { NumberDecimalSeparator = "."}; //Потому, что в файле вещественные числа имеют точку в качестве разделителя.
            try
            {
                block = new BlockIPv4
                {
                    Network = sourceArr[0],
                    Geoname_Id = sourceArr[1] == "" ? 0 : int.Parse(sourceArr[1]),
                    Registered_Country_Geoname_Id = sourceArr[2] == "" ? 0 : int.Parse(sourceArr[2]),
                    Represented_Country_Geoname_Id = sourceArr[3] == "" ? 0 : int.Parse(sourceArr[3]),
                    Is_Anonymous_Proxy = Convert.ToBoolean(int.Parse(sourceArr[4])),
                    Is_Satellite_Provider = Convert.ToBoolean(int.Parse(sourceArr[5])),
                    Postal_Code = sourceArr[6],
                    Latitude = sourceArr[7] == "" ? 0 : double.Parse(sourceArr[7], provider),
                    Longitude = sourceArr[8] == "" ? 0 : double.Parse(sourceArr[8], provider),
                    Accuracy_Radius = sourceArr[9] == "" ? 0 : int.Parse(sourceArr[9])
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(sourceData);
                block = null;
            }
            return block;
        }

        //Создаёт сущность IPv4.
        //В качестве sourceData передаётся значение первичного ключа BlockIPv4 (поле network из CSV-таблицы)
        private static IPv4 ParseIP(string sourceData) // sourceData == "1.0.76.0/22";
        {
            string[] sourceArr = sourceData.Split('/'); //sourceArr = { "1.0.76.0", "22" };
            if (Regex.IsMatch(sourceArr[0], GlobalParams.IPv4_Regex_Pattern))
                return new IPv4 { IP = sourceArr[0], BlockIPv4_Id = sourceData };
            else
            {
                Console.WriteLine(sourceData);
                return null;
            }
        }

        //Сохраняет блоки-IP в базе данных порциями по 100000.
        private static void SaveBlocks100(GeoIPDbContext dbContext, List<BlockIPv4> blocks)
        {
            List<BlockIPv4> blocksRange = new List<BlockIPv4>(100000);
            DateTime start100;
            DateTime finish;

            while (blocks.Count > 0)
            {
                start100 = DateTime.Now;
                if (blocks.Count > 100000)
                {
                    blocksRange = blocks.GetRange(0, 100000);
                    blocks.RemoveRange(0, 100000);
                    dbContext.Blocks_IPv4.AddRange(blocksRange);
                }
                else
                {
                    dbContext.Blocks_IPv4.AddRange(blocks);
                    blocks.Clear();
                }
                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                finish = DateTime.Now;
                Console.WriteLine($"{blocksRange.Count} blocks complete from: {finish - start100}");
            }
        }

        //Сохраняет IPv4 в базе данных порциями по 100000.
        private static void SaveIPs100(GeoIPDbContext dbContext, List<IPv4> ip_s)
        {
            List<IPv4> ip_sRange = new List<IPv4>(100000);
            DateTime start100;
            DateTime finish;

            while (ip_s.Count > 0)
            {
                start100 = DateTime.Now;
                if (ip_s.Count > 100000)
                {
                    ip_sRange = ip_s.GetRange(0, 100000);
                    ip_s.RemoveRange(0, 100000);
                    dbContext.IPs_v4.AddRange(ip_sRange);
                }
                else
                {
                    dbContext.IPs_v4.AddRange(ip_s);
                    ip_s.Clear();
                }
                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                finish = DateTime.Now;
                Console.WriteLine($"{ip_sRange.Count} IP's complete from: {finish - start100}");
            }
        }

        //Пакетное обновление таблицы базы данных.
        private static void BatchUpdateDbTable(string connectionString, string sqlSelect, DataTable dataTable, int batchSize)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                using (NpgsqlDataAdapter dataAdapter = new NpgsqlDataAdapter(sqlSelect, connection)) //Параметр sqlSelect связывает dataAdapter с определённой таблицей в базе данных. (Например: sqlSelect = "SELECT * FROM \"CitiLocations\";")
                {
                    using (NpgsqlCommandBuilder commandBuilder = new NpgsqlCommandBuilder(dataAdapter))
                    {
                        dataAdapter.DeleteCommand = commandBuilder.GetDeleteCommand();
                        dataAdapter.DeleteCommand.UpdatedRowSource = UpdateRowSource.None;
                        dataAdapter.UpdateCommand = commandBuilder.GetUpdateCommand();
                        dataAdapter.UpdateCommand.UpdatedRowSource = UpdateRowSource.None;
                        dataAdapter.InsertCommand = commandBuilder.GetInsertCommand();
                        dataAdapter.InsertCommand.UpdatedRowSource = UpdateRowSource.None;
                        dataAdapter.UpdateBatchSize = batchSize;
                        dataAdapter.Update(dataTable);
                    }
                }
            }
        }

        //Возвращает строку данных для CityLocations DataTable.
        private static object[] GetLocationDataRow(string csvDataStr)
        {
            object[] result;
            string[] csvDataArr; 

            if (csvDataStr.Contains(", "))
            {
                StringBuilder csvDataBuilder = new StringBuilder(csvDataStr);
                csvDataBuilder.Replace(", ", "@");
                csvDataBuilder.Replace(",", ";");
                csvDataBuilder.Replace("@", ", ");
                csvDataArr = csvDataBuilder.ToString().Split(new char[] { ';' }, StringSplitOptions.None);
            }
            else csvDataArr = csvDataStr.Split(new char[] { ',' }, StringSplitOptions.None);

            try
            {
                result = new object[]
                {
                    int.Parse(csvDataArr[0]), csvDataArr[1], csvDataArr[2], csvDataArr[3], csvDataArr[4], csvDataArr[5],
                    csvDataArr[6], csvDataArr[7], csvDataArr[8], csvDataArr[9], csvDataArr[10], csvDataArr[11], csvDataArr[12],
                    int.TryParse(csvDataArr[13], out int res) ? new { Value = Convert.ToBoolean(res) }  : null
                };
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        //Возвращает строку данных для BlocksIPv4 DataTable.
        private static object[] GetBlockIPv4DataRow(string csvDataStr)
        {
            string[] csvDataArr = csvDataStr.Split(new char[] { ',' }, StringSplitOptions.None);
            object[] result;
            IFormatProvider provider = new System.Globalization.NumberFormatInfo { NumberDecimalSeparator = "." }; //Потому, что в файле вещественные числа имеют точку в качестве разделителя.
            try
            {
                result = new object[]
                {
                    csvDataArr[0],
                    csvDataArr[1] == "" ? null : new {Value = int.Parse(csvDataArr[1])},
                    csvDataArr[2] == "" ? null : new {Value = int.Parse(csvDataArr[2])},
                    csvDataArr[3] == "" ? null : new {Value = int.Parse(csvDataArr[3])},
                    csvDataArr[4] == "" ? null : new {Value = Convert.ToBoolean(int.Parse(csvDataArr[4]))},
                    csvDataArr[5] == "" ? null : new {Value = Convert.ToBoolean(int.Parse(csvDataArr[5]))},
                    csvDataArr[6],
                    csvDataArr[7] == "" ? null : new {Value = double.Parse(csvDataArr[7], provider)},
                    csvDataArr[8] == "" ? null : new {Value = double.Parse(csvDataArr[8], provider)},
                    csvDataArr[9] == "" ? null : new {Value = int.Parse(csvDataArr[9])}
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            return result;
        }

        //Возвращает строку данных для IPsv4 DataTable.
        private static object[] GetIPv4DataRow(string sourceData) // sourceData == "1.0.76.0/22";
        {
            string[] sourceArr = sourceData.Split('/'); //sourceArr = { "1.0.76.0", "22" };
            if (Regex.IsMatch(sourceArr[0], GlobalParams.IPv4_Regex_Pattern))
                return new object[] { sourceArr[0], sourceData };
            else
            {
                return null;
            }
        }

        //Создаёт таблицу DataTable. dataColumns.Key = "columnName"; dataColumns.Value = "typeName";
        private static DataTable CreateDataTable(string tableName, KeyValuePair<string, string>[] dataColumns, bool pkAutoInc)
        {
            DataTable resultTable = new DataTable(tableName);
            DataColumn column;
            
            //Создаём столбцы таблицы.
            for (int i = 0; i < dataColumns.Length; i++)
            {
                column = new DataColumn(dataColumns[i].Key, Type.GetType(dataColumns[i].Value));
                resultTable.Columns.Add(column);
            }

            //Устанавливаем свойства первичного ключа.
            resultTable.Columns[0].Unique = true;
            resultTable.Columns[0].AllowDBNull = false;
            if (pkAutoInc)
            {
                resultTable.Columns[0].AutoIncrement = true;
                resultTable.Columns[0].AutoIncrementSeed = 1;
                resultTable.Columns[0].AutoIncrementStep = 1;
            }

            //Задаём первичный ключ и возвращаем результат.
            resultTable.PrimaryKey = new DataColumn[] { resultTable.Columns[0] };
            return resultTable;
        }
    }
}
