using Database;
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

                StreamReader blockIPv4FileReader = new StreamReader(blockIPv4FileName);
                StreamReader locationFileReader = new StreamReader(locationFileName); // Encoding UTF-8.
                //StreamReader locationFileReader = new StreamReader(locationFileName, Encoding.GetEncoding(1251)); // т.к. данные в файле храняться в кодировке ANSI-1251 (Кириллица - Windows).
                string blockIPv4Header = blockIPv4FileReader.ReadLine();
                string locationHeader = locationFileReader.ReadLine();

                if (!blockIPv4Header.Equals(block_IPv4_File_Header) || !locationHeader.Equals(location_File_Header))
                {
                    throw new Exception($"Некорректные заголовки файлов данных {blockIPv4FileName} и/или {locationFileName}");
                }

                //Заполняем таблицу CityLocations.
                List<string> bufferList = new List<string>();
                CityLocation location;
                string buffer = locationFileReader.ReadToEnd();
                locationFileReader.Close();
                bufferList.AddRange(buffer.Split(new string[] { "\n" } , StringSplitOptions.RemoveEmptyEntries));
                for (int i = 0; i < bufferList.Count; i++)
                {
                    location = ParseLocation(bufferList[i]);
                    if (location != null) dbContext.CityLocations.Add(location);
                }

                //Заполняем таблицы BlocksIPv4 и IPsv4.
                //bufferList = new List<string>();
                //BlockIPv4 block;
                //IPv4 ip_v4;
                //buffer = blockIPv4FileReader.ReadToEnd();
                blockIPv4FileReader.Close();
                //bufferList.AddRange(buffer.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
                //for (int i = 0; i < bufferList.Count; i++)
                //{
                //    block = ParseBlockIPv4(bufferList[i]);
                //    if (block != null)
                //    {
                //        dbContext.Blocks_IPv4.Add(block);
                //        ip_v4 = ParseIP(block.Network);
                //        if (ip_v4 != null) dbContext.IPs_v4.Add(ip_v4);
                //    }
                //}

                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void DatabaseUpdate(GeoIPDbContext dbContext, string blockIPv4FileName, string locationFileName)
        {
            try
            {
                StreamReader blockIPv4FileReader = new StreamReader(blockIPv4FileName);
                StreamReader locationFileReader = new StreamReader(locationFileName, Encoding.GetEncoding(1251)); // т.к. данные в файле храняться в кодировке ANSI-1251 (Кириллица - Windows).
                string blockIPv4Header = blockIPv4FileReader.ReadLine();
                string locationHeader = locationFileReader.ReadLine();

                if (!blockIPv4Header.Equals(block_IPv4_File_Header) || !locationHeader.Equals(location_File_Header))
                {
                    throw new Exception($"Некорректные заголовки файлов данных {blockIPv4FileName} и/или {locationFileName}");
                }
                
                //Здесь заполняем таблицы базы данных.
                StringBuilder buffer = new StringBuilder(100);
                CityLocation location, dbLocation;

                //Заполняем таблицу CityLocations.
                while (!locationFileReader.EndOfStream)
                {
                    buffer.Clear();
                    buffer.Insert(0, locationFileReader.ReadLine());
                    location = ParseLocation(buffer.ToString());
                    if (location != null)
                    {
                        dbLocation = dbContext.CityLocations.FirstOrDefault(l => l.Geoname_Id == location.Geoname_Id);
                        if (dbLocation == null)
                        {
                            dbContext.CityLocations.Add(location);
                        }
                        else if (!location.Equals(dbLocation))
                        {
                            dbContext.Entry(dbLocation).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                            dbContext.Update(location);
                        }
                    }
                }
                locationFileReader.Close();

                //Заполняем таблицы BlocksIPv4 и IPsv4.
                BlockIPv4 block, dbBlock;
                IPv4 ip_v4;
                while (!blockIPv4FileReader.EndOfStream)
                {
                    buffer.Clear();
                    buffer.Insert(0, blockIPv4FileReader.ReadLine());
                    block = ParseBlockIPv4(buffer.ToString());
                    if (block != null)
                    {
                        dbBlock = dbContext.Blocks_IPv4.FirstOrDefault(b => b.Network == block.Network);
                        if (dbBlock == null)
                        {
                            dbContext.Blocks_IPv4.Add(block);
                            ip_v4 = ParseIP(block.Network);
                            if (ip_v4 != null) dbContext.IPs_v4.Add(ip_v4);
                        }
                        else if (!block.Equals(dbBlock))
                        {
                            dbContext.Entry(dbBlock).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                            dbContext.Update(block);
                        }
                    }
                }
                blockIPv4FileReader.Close();
                dbContext.SaveChanges(); // Не забывам сохранить изменения в базе данных!!!
                Console.WriteLine("Обновление завершено.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        //Создаёт сущность CityLocation из CSV-строки.    
        private static CityLocation ParseLocation(string sourceData)
        {
            string[] sourceArr = sourceData.Split(new char[] { ',' }, StringSplitOptions.None);
            if (sourceArr.Count() < 14)
            {
                Console.WriteLine(sourceData);
                return null;
            }
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
            if (sourceArr.Count() != 10) return null;
            BlockIPv4 block;
            IFormatProvider provider = new System.Globalization.NumberFormatInfo { NumberDecimalSeparator = "."}; //Потому, что в файле вещественные числа имеют точку в качестве разделителя.
            try
            {
                block = new BlockIPv4
                {
                    Network = sourceArr[0],
                    Geoname_Id = int.Parse(sourceArr[1]),
                    Registered_Country_Geoname_Id = int.Parse(sourceArr[2]),
                    Represented_Country_Geoname_Id = sourceArr[3] == "" ? 0 : int.Parse(sourceArr[3]),
                    Is_Anonymous_Proxy = Convert.ToBoolean(int.Parse(sourceArr[4])),
                    Is_Satellite_Provider = Convert.ToBoolean(int.Parse(sourceArr[5])),
                    Postal_Code = sourceArr[6],
                    Latitude = double.Parse(sourceArr[7], provider),
                    Longitude = double.Parse(sourceArr[8], provider),
                    Accuracy_Radius = int.Parse(sourceArr[9])
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
