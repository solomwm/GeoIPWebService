﻿using Database;
using Database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tools;

namespace DatabaseUpdater
{
    static class Updater
    {
        const string block_IPv4_File_Header = "network,geoname_id,registered_country_geoname_id,represented_country_geoname_id,is_anonymous_proxy,is_satellite_provider,postal_code,latitude,longitude,accuracy_radius";
        const string location_File_Header = "geoname_id,locale_code,continent_code,continent_name,country_iso_code,country_name,subdivision_1_iso_code,subdivision_1_name,subdivision_2_iso_code,subdivision_2_name,city_name,metro_code,time_zone,is_in_european_union";

        //Пересоздаёт базу и заполняет таблицы с нуля (если esureCreated == false, иначе считается, что база только-что создана и таблицы пустые.)
        public static void DatabaseRebuild(GeoIPDbContext dbContext, string blockIPv4FileName, string locationFileName, 
                                           bool ensureCreated)
        {
            try
            {
                //Если база не пустая, пересоздаём её.
                if (!ensureCreated)
                {
                    dbContext.Database.EnsureDeleted();
                    dbContext.Database.EnsureCreated();
                }

                DateTime start, startNew, start100, finish;

                //Заполняем таблицу CityLocations.
                start = DateTime.Now;
                Console.WriteLine($"start parsing locations: {start}");
                List<CityLocation> locations = ParseAllLocations(locationFileName);
                locations.Add(new CityLocation { Geoname_Id = 0 }); //Необходимо добавить такую локацию для анонимных IP-адресов.
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing locations from: {finish - start}");

                //Заполняем таблицы BlocksIPv4 и IPsv4.
                start = DateTime.Now;
                Console.WriteLine($"start parsing block IP's: {start}");
                List<BlockIPv4> blocks = ParseAllBlocksIPv4(blockIPv4FileName, out List<IPv4> ip_s);
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing block IP's from: {finish - start}");

                //Обновляем данные в базе.
                start = DateTime.Now;
                Console.WriteLine($"start update Database: {start}");

                //Сохраняем локации
                dbContext.CityLocations.AddRange(locations);
                locations.Clear();
                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                finish = DateTime.Now;
                Console.WriteLine($"locations complete from: {finish - start}");

                //Сохраняем блоки
                startNew = DateTime.Now;
                var blocksRange = new List<BlockIPv4>(100000); 
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
                Console.WriteLine($"all blocks complete from: {finish - startNew}");
                
                //Сохраняем IP-адреса
                startNew = DateTime.Now;
                var ip_sRange = new List<IPv4>(100000);
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
                finish = DateTime.Now;
                Console.WriteLine($"IP's complete from: {finish - startNew}");
                Console.WriteLine($"finish update Database from: {finish - start}");
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        //Обновляет базу, проверяя сущесвующие данные на изменение и добавляя новые.
        public static void DatabaseUpdate(GeoIPDbContext dbContext, string blockIPv4FileName, string locationFileName)
        {
            try
            {
                DateTime start, startNew, start100, finish;

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
                List<BlockIPv4> blocks = ParseAllBlocksIPv4(blockIPv4FileName, out List<IPv4> ip_s);
                finish = DateTime.Now;
                Console.WriteLine($"finish parsing block IP's from: {finish - start}");

                //Обновляем данные в базе.
                start = DateTime.Now;
                Console.WriteLine($"start update Database: {start}");

                //Обновляем локации.
                Console.WriteLine("start update locations");
                CityLocation dbLocation;
                List<CityLocation> newLocations = new List<CityLocation>();

                foreach (CityLocation location in locations)
                {
                    dbLocation = dbContext.CityLocations.FirstOrDefault(l => l.Geoname_Id == location.Geoname_Id);
                    if (dbLocation == null)
                    {
                        newLocations.Add(location);
                    }
                    else if (!location.Equals(dbLocation))
                    {
                        dbContext.Entry(dbLocation).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        dbContext.Update(location);
                    }
                }
                locations = null; //Освобождаем память.
                dbContext.CityLocations.AddRange(newLocations);
                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                finish = DateTime.Now;
                Console.WriteLine($"locations complete from: {finish - start}. New locations added: {newLocations.Count}");

                //Обновляем блоки IP.
                startNew = DateTime.Now;
                Console.WriteLine($"start update blocks: {startNew}");
                BlockIPv4 dbBlock;
                IPv4 ip_v4;
                List<BlockIPv4> newBlocks = new List<BlockIPv4>();
                List<IPv4> newIPs = new List<IPv4>();

                foreach (BlockIPv4 block in blocks)
                {
                    dbBlock = dbContext.Blocks_IPv4.FirstOrDefault(b => b.Network == block.Network);
                    if (dbBlock == null)
                    {
                        newBlocks.Add(block);
                        ip_v4 = ip_s.FirstOrDefault(ip => ip.BlockIPv4_Id == block.Network);
                        newIPs.Add(ip_v4);
                    }
                    else if (!block.Equals(dbBlock))
                    {
                        dbContext.Entry(dbBlock).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        dbContext.Update(block);
                    }
                }
                blocks = null;
                ip_s = null; //Освобождаем память.
                dbContext.SaveChanges(); //Сохраняем изменения в базе.
                finish = DateTime.Now;
                Console.WriteLine($"blocks update complete from: {finish - start}");

                //Сохраняем новые блоки.
                startNew = DateTime.Now;
                var newBlocksRange = new List<BlockIPv4>(100000);
                Console.WriteLine($"start saving new blocks: {startNew}");
                int remainder;
                int newBlocksCount = remainder = newBlocks.Count; 

                while (newBlocks.Count > 0)
                {
                    start100 = DateTime.Now;
                    if (newBlocks.Count > 100000)
                    {
                        newBlocksRange = newBlocks.GetRange(0, 100000);
                        newBlocks.RemoveRange(0, 100000);
                        dbContext.Blocks_IPv4.AddRange(newBlocksRange);
                        remainder = newBlocksRange.Count;
                    }
                    else
                    {
                        dbContext.Blocks_IPv4.AddRange(newBlocks);
                        remainder = newBlocks.Count;
                        newBlocks.Clear();
                    }
                    dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                    finish = DateTime.Now;
                    Console.WriteLine($"{remainder} blocks complete from: {finish - start100}");
                }
                finish = DateTime.Now;
                Console.WriteLine($"{newBlocksCount} new blocks saved from: {finish - startNew}");

                //Сохраняем новые IP.
                startNew = DateTime.Now;
                var newIPRange = new List<IPv4>(100000);
                Console.WriteLine($"start saving new IP: {startNew}");

                while (newIPs.Count > 0)
                {
                    start100 = DateTime.Now;
                    if (newIPs.Count > 100000)
                    {
                        newIPRange = newIPs.GetRange(0, 100000);
                        newIPs.RemoveRange(0, 100000);
                        dbContext.IPs_v4.AddRange(newIPRange);
                        remainder = newBlocksRange.Count;
                    }
                    else
                    {
                        dbContext.IPs_v4.AddRange(newIPs);
                        remainder = newIPs.Count;
                        newIPs.Clear();
                    }
                    dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                    finish = DateTime.Now;
                    Console.WriteLine($"{remainder} IP's complete from: {finish - start100}");
                }
                finish = DateTime.Now;
                Console.WriteLine($"{newBlocksCount} new blocks saved from: {finish - startNew}");
                Console.WriteLine($"All update operations complete from: {finish - start}");
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

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
    }
}