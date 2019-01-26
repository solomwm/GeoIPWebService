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
using Timer = System.Timers.Timer;

namespace DatabaseUpdater
{
    static class Updater
    {
        const string block_IPv4_File_Header = "network,geoname_id,registered_country_geoname_id,represented_country_geoname_id,is_anonymous_proxy,is_satellite_provider,postal_code,latitude,longitude,accuracy_radius";
        const string location_File_Header = "geoname_id,locale_code,continent_code,continent_name,country_iso_code,country_name,subdivision_1_iso_code,subdivision_1_name,subdivision_2_iso_code,subdivision_2_name,city_name,metro_code,time_zone,is_in_european_union";
        
        //Обновляет базу, используя ADO DataAdapter и DataTable.
        public static bool DatabaseUpdateADO(string connectionString, string blockIPv4FileName, string locationFileName)
        {
            DateTime start, startNew, finish;
            start = DateTime.Now;
            Console.WriteLine($"Start update operations now: {start}");
            //Создаём таблицы DataTable.
            DataTable locationsTable = CreateDataTable("\"CityLocations\"", new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("Geoname_Id", "System.Int32"),
                new KeyValuePair<string, string>("Local_Code", "System.String"),
                new KeyValuePair<string, string>("Continent_Code", "System.String"),
                new KeyValuePair<string, string>("Continent_Name", "System.String"),
                new KeyValuePair<string, string>("Country_Iso_Code", "System.String"),
                new KeyValuePair<string, string>("Country_Name", "System.String"),
                new KeyValuePair<string, string>("Subdivision_1_Iso_Code", "System.String"),
                new KeyValuePair<string, string>("Subdivision_1_Name", "System.String"),
                new KeyValuePair<string, string>("Subdivision_2_Iso_Code", "System.String"),
                new KeyValuePair<string, string>("Subdivision_2_Name", "System.String"),
                new KeyValuePair<string, string>("City_Name", "System.String"),
                new KeyValuePair<string, string>("Metro_Code", "System.String"),
                new KeyValuePair<string, string>("Time_Zone", "System.String"),
                new KeyValuePair<string, string>("Is_In_European_Union","System.Boolean")
            }, false);
            DataTable blocksIPv4Table = CreateDataTable("\"Blocks_IPv4\"", new KeyValuePair<string, string>[] 
            {
                new KeyValuePair<string, string>("Network", "System.String"),
                new KeyValuePair<string, string>("Geoname_Id", "System.Int32"),
                new KeyValuePair<string, string>("Registered_Country_Geoname_Id", "System.Int32"),
                new KeyValuePair<string, string>("Represented_Country_Geoname_Id", "System.Int32"),
                new KeyValuePair<string, string>("Is_Anonymous_Proxy", "System.Boolean"),
                new KeyValuePair<string, string>("Is_Satellite_Provider", "System.Boolean"),
                new KeyValuePair<string, string>("Postal_Code", "System.String"),
                new KeyValuePair<string, string>("Latitude", "System.Double"),
                new KeyValuePair<string, string>("Longitude", "System.Double"),
                new KeyValuePair<string, string>("Accuracy_Radius", "System.Int32"),
            }, false);
            //Создаём DataSet.
            DataSet geoIPDataSet = new DataSet("GeoIPStore");
            geoIPDataSet.Tables.AddRange(new DataTable[] { locationsTable, blocksIPv4Table });

            ForeignKeyConstraint foreignKey = new ForeignKeyConstraint(locationsTable.Columns["Geoname_Id"], blocksIPv4Table.Columns["Geoname_Id"])
            {
                ConstraintName = "LocationIPForeignKey",
                DeleteRule = Rule.SetNull,
                UpdateRule = Rule.Cascade
            };

            geoIPDataSet.Tables["\"Blocks_IPv4\""].Constraints.Add(foreignKey);
            geoIPDataSet.EnforceConstraints = true;
            geoIPDataSet.Relations.Add("LocationIP", locationsTable.Columns["Geoname_Id"], blocksIPv4Table.Columns["Geoname_Id"]);

            //Заполняем таблицы DataTable данными из csv-файлов.
            //Заполняем locationsTable.
            startNew = DateTime.Now;
            Console.WriteLine($"Start parsing locations: {startNew}");

            using (StreamReader locationsReader = new StreamReader(locationFileName))
            {
                object[] location;
                string[] bufferArr;

                string locationFileHeader = locationsReader.ReadLine();
                if (!locationFileHeader.Equals(location_File_Header))
                {
                    throw new Exception($"Некорректный заголовок файла данных {locationFileName}");
                }
                bufferArr = locationsReader.ReadToEnd().Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                //Заполняем таблицу.
                for (int i = 0; i < bufferArr.Length; i++)
                {
                    location = GetLocationDataRow(bufferArr[i]);
                    if (location != null) locationsTable.Rows.Add(location);
                }
                locationsTable.Rows.Add(new object[] { 0 }); // Для IP, у которых отсутствует локация.
            }
            finish = DateTime.Now;
            Console.WriteLine($"Locations parsing complete at: {finish - startNew}");

            //Заполняем blocksIPv4Table.
            startNew = DateTime.Now;
            Console.WriteLine($"Start parsing blocks IPv4: {startNew}");

            using (StreamReader blocksReader = new StreamReader(blockIPv4FileName))
            {
                object[] block;

                string blockIPv4FileHeader = blocksReader.ReadLine();
                if (!blockIPv4FileHeader.Equals(block_IPv4_File_Header))
                {
                    throw new Exception($"Некорректный заголовок файла данных {blockIPv4FileName}");
                }
                //string[] bufferArr = blocksReader.ReadToEnd().Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries); //OutOfMemory Exeption.
                //Заполняем таблицу.
                int blocksCount = 0;
                Timer timer = new Timer(20000) { AutoReset = true };

                timer.Elapsed += delegate
                {
                    Console.WriteLine($"{blocksCount} records parsed.");
                    Console.CursorTop--;
                };

                timer.Start();
                while (!blocksReader.EndOfStream)
                {
                    block = GetBlockIPv4DataRow(blocksReader.ReadLine());
                    if (block != null)
                    {
                        blocksIPv4Table.Rows.Add(block);
                        blocksCount++;
                    }
                }
                timer.Stop();
                Console.WriteLine($"{blocksCount} records parsed.");
            }
            finish = DateTime.Now;
            Console.WriteLine($"Blocks IPv4 parsing complete at: {finish - startNew}");

            //Обновляем данные в базе.
            string sqlLocationsTableSelect = "SELECT * FROM \"CityLocations\";";
            string sqlBlocksIPv4TableSelect = "SELECT * FROM \"Blocks_IPv4\";";
            string sqlDataSetSelect = string.Concat(sqlLocationsTableSelect, sqlBlocksIPv4TableSelect);

            startNew = DateTime.Now;
            Console.WriteLine($"Start update database: {startNew}");
            bool result = BatchUpdateDbTable(connectionString, sqlDataSetSelect, geoIPDataSet);
            finish = DateTime.Now;
            Console.WriteLine($"{(result ? "Database update compleate at: " : "An error occurred while updating database: ")} " +
                $"{finish - startNew}");
            return result;
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
            if (dbContext.Updates.Count() > 0)
                return dbContext.Updates.OrderBy(u => u.Id).Last();
            else return null;
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

        //Пакетное обновление таблицы базы данных.
        private static bool BatchUpdateDbTable(string connectionString, string sqlSelect, DataSet dataSetSrc)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                NpgsqlTransaction transaction = connection.BeginTransaction();
                using (NpgsqlDataAdapter dataAdapter = new NpgsqlDataAdapter(sqlSelect, connection)) //Параметр sqlSelect связывает dataAdapter с определённой таблицей в базе данных. (Например: sqlSelect = "SELECT * FROM \"CitiLocations\";")
                {
                    dataAdapter.TableMappings.Add("Table", "\"CityLocations\"");
                    dataAdapter.TableMappings.Add("Table1", "\"Blocks_IPv4\"");
                    DataSet dataSetDb = new DataSet();
                    dataAdapter.Fill(dataSetDb);
                    //Выбираем строки для удаления по ItemArray[0] ("Geoname_Id"), если таких нет в обновлениях, 
                    //удаляем их из базы.
                    //IEnumerable<DataRow> rowsToDelInCityLoc = dataSetDb.Tables["\"CityLocations\""].AsEnumerable().TakeWhile
                    //    (r => !dataSetSrc.Tables["\"CityLocations\""].AsEnumerable().Any
                    //    (rs => rs.ItemArray[0] == r.ItemArray[0]));

                    //IEnumerable<DataRow> rowsToDelInBlocksIPv4 = dataSetDb.Tables["\"Blocks_IPv4\""].AsEnumerable().TakeWhile
                    //    (r => dataSetSrc.Tables["\"Blocks_IPv4\""].AsEnumerable().Any
                    //    (rs => rs.ItemArray[0] != r.ItemArray[0]));
                    //IEnumerable<DataRow> rowsToDelInBlocksIPv4 = from r in dataSetDb.Tables["\"Blocks_IPv4\""].AsEnumerable()
                    //                                             where r.ItemArray[0];

                    //foreach (DataRow row in rowsToDelInCityLoc)
                    //{
                    //    dataSetDb.Tables["\"CityLocations\""].Rows.Remove(row);
                    //}

                    //foreach (DataRow row in rowsToDelInBlocksIPv4)
                    //{
                    //    dataSetDb.Tables["\"Blocks_IPv4\""].Rows.Remove(row);
                    //}

                    DateTime start = DateTime.Now;
                    Console.WriteLine($"Merge1 start: {start}");
                    dataSetDb.Tables[0].Merge(dataSetSrc.Tables["\"CityLocations\""], true);
                    DateTime finish = DateTime.Now;
                    Console.WriteLine($"Merge1 complete at: {finish - start}");

                    start = DateTime.Now;
                    Console.WriteLine($"Merge2 start: {start}");
                    dataSetDb.Tables[1].Merge(dataSetSrc.Tables["\"Blocks_IPv4\""], true);
                    finish = DateTime.Now;
                    Console.WriteLine($"Merge2 complete at: {finish - start}");

                    using (NpgsqlCommandBuilder commandBuilder = new NpgsqlCommandBuilder(dataAdapter))
                    {
                        dataAdapter.DeleteCommand = commandBuilder.GetDeleteCommand();
                        dataAdapter.UpdateCommand = commandBuilder.GetUpdateCommand();
                        dataAdapter.InsertCommand = commandBuilder.GetInsertCommand();
                        dataAdapter.DeleteCommand.Transaction = transaction;
                        dataAdapter.UpdateCommand.Transaction = transaction;
                        dataAdapter.InsertCommand.Transaction = transaction;
                        int rowUpdatedCount = 0;
                        int allRowsCount = 0;
                        for (int i = 0; i < dataSetSrc.Tables.Count; i++)
                        {
                            allRowsCount += dataSetSrc.Tables[i].Rows.Count;
                        }

                        dataAdapter.RowUpdated += delegate(object sender, NpgsqlRowUpdatedEventArgs eventArgs) 
                        {
                            if (eventArgs.Status == UpdateStatus.ErrorsOccurred)
                            {
                                Console.WriteLine(eventArgs.Errors.Message);
                                eventArgs.Status = UpdateStatus.SkipCurrentRow;
                            }
                            rowUpdatedCount += eventArgs.RowCount;
                        };

                        Timer timer = new Timer(20000) { AutoReset = true };
                        timer.Elapsed += delegate
                        {
                            Console.WriteLine($"{rowUpdatedCount} rows from {allRowsCount} complete. " +
                                $"({rowUpdatedCount / allRowsCount * 100:f2} %)");
                            Console.CursorTop--;
                        };
                        timer.Start();

                        try
                        {
                            dataAdapter.Update(dataSetDb);
                            transaction.Commit();
                            timer.Stop();
                            Console.WriteLine($"{rowUpdatedCount} rows from {allRowsCount} complete. " +
                               $"({rowUpdatedCount / allRowsCount * 100:f2})");
                            return true;
                        }
                        catch (Exception e)
                        {
                            timer.Stop();
                            Console.WriteLine($"{rowUpdatedCount} rows from {allRowsCount} complete. " +
                               $"({rowUpdatedCount / allRowsCount * 100:f2})");
                            Console.WriteLine(e.Message);
                            transaction.Rollback();
                            return false;
                        }
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
                StringBuilder csvDataStrBuilder = new StringBuilder(csvDataStr);
                csvDataStrBuilder.Replace(", ", "@");
                csvDataStrBuilder.Replace(",", ";");
                csvDataStrBuilder.Replace("@", ", ");
                csvDataArr = csvDataStrBuilder.ToString().Split(new char[] { ';' }, StringSplitOptions.None);
            }
            else csvDataArr = csvDataStr.Split(new char[] { ',' }, StringSplitOptions.None);

            try
            {
                result = new object[]
                {
                    int.Parse(csvDataArr[0]), csvDataArr[1], csvDataArr[2], csvDataArr[3], csvDataArr[4], csvDataArr[5],
                    csvDataArr[6], csvDataArr[7], csvDataArr[8], csvDataArr[9], csvDataArr[10], csvDataArr[11], csvDataArr[12],
                    int.TryParse(csvDataArr[13], out int res) ? Convert.ToBoolean(res) : false
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
                    csvDataArr[1] == "" ? 0 : int.Parse(csvDataArr[1]),
                    csvDataArr[2] == "" ? 0 : int.Parse(csvDataArr[2]),
                    csvDataArr[3] == "" ? 0 : int.Parse(csvDataArr[3]),
                    Convert.ToBoolean(int.Parse(csvDataArr[4])),
                    Convert.ToBoolean(int.Parse(csvDataArr[5])),
                    csvDataArr[6],
                    csvDataArr[7] == "" ? 0 : double.Parse(csvDataArr[7], provider),
                    csvDataArr[8] == "" ? 0 : double.Parse(csvDataArr[8], provider),
                    csvDataArr[9] == "" ? 0 : int.Parse(csvDataArr[9])
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            return result;
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
