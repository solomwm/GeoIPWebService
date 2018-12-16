using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace Tools
{
    public static class Utilites
    {
        //Возвращает хэш-MD5 исходных данных.
        public static string GetMD5(Stream inputData)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(md5.ComputeHash(inputData)).Replace("-", string.Empty);
        }

        //Возвращает хэш-MD5 исходных данных.
        public static string GetMD5(byte[] inputData)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(md5.ComputeHash(inputData)).Replace("-", string.Empty);
        }

        //Загружает файл с сервера. Возвращает true при успешной загрузке. Возвращает полный путь к загруженному файлу.
        public static bool DownloadFile(string urlStr, string destFilePath, out string loadedFileName)
        {
            string fileName = Path.GetFileName(urlStr);
            string destFilePathAndName = Path.Combine(destFilePath, fileName);

            try
            {
                WebRequest webRequest = WebRequest.Create(urlStr);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                loadedFileName = null;
                return false;
            }

            try
            {
                byte[] fileData;

                using (WebClient client = new WebClient())
                {
                    fileData = client.DownloadData(urlStr);
                }

                using (FileStream fs = new FileStream(destFilePathAndName, FileMode.OpenOrCreate))
                {
                    fs.Write(fileData, 0, fileData.Length);
                }
                loadedFileName = destFilePathAndName;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception("download failed", e.InnerException);
            }
        }

        //Загружает файл с сервера. Возвращает true при успешной загрузке. Проверяет загруженные данные на соответствие указанному хэш-MD5. Возвращает полный путь к загруженному файлу.
        public static bool DownloadFile(string urlStr, string destFilePath, string patternMD5, out string loadedFileName, out bool checkResult)
        {
            string fileName = Path.GetFileName(urlStr);
            string destFilePathAndName = Path.Combine(destFilePath, fileName);

            try
            {
                WebRequest webRequest = WebRequest.Create(urlStr);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                checkResult = false;
                loadedFileName = null;
                return false;
            }

            try
            {
                byte[] fileData;

                using (WebClient client = new WebClient())
                {
                    fileData = client.DownloadData(urlStr);
                    checkResult = CheckMD5(patternMD5, fileData);
                }

                using (FileStream fs = new FileStream(destFilePathAndName, FileMode.OpenOrCreate))
                {
                    fs.Write(fileData, 0, fileData.Length);
                }
                loadedFileName = destFilePathAndName;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception("download failed", e.InnerException);
            }
        }

        //Проверяет хэш-MD5 исходных данных на соответствие заданному образцу.
        public static bool CheckMD5(string pattern, Stream inputData)
        {
            inputData.Position = 0;
            string hash = GetMD5(inputData);
            return pattern.ToUpper().Trim() == hash.ToUpper().Trim();
        }

        //Проверяет хэш-MD5 исходных данных на соответствие заданному образцу.
        public static bool CheckMD5(string pattern, byte[] inputData)
        {
            string hash = GetMD5(inputData);
            return pattern.ToUpper().Trim() == hash.ToUpper().Trim();
        }

        //Распаковывает zip-архив в указанную папку.
        public static void ExtractFromZip(string zipFileName, string pathToExtract)
        {
            try
            {
                ZipFile.ExtractToDirectory(zipFileName, pathToExtract);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        //Извлекает из zip-архива указанные в параметре entries элементы. Возвращает в строковом массиве полные имена каждого извлечённого элемента.
        public static string[] ExtractFromZip(string zipFileName, string pathToExtract, string[] entries)
        {
            if (!pathToExtract.EndsWith(Path.DirectorySeparatorChar.ToString()))
                pathToExtract += Path.DirectorySeparatorChar;
            string destinationPath;
            List<string> result = new List<string>();

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFileName))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        for (int i = 0; i < entries.Length; i++)
                        {
                            if (entry.FullName.EndsWith(entries[i], StringComparison.OrdinalIgnoreCase))
                            {
                                destinationPath = Path.GetFullPath(Path.Combine(pathToExtract, entry.FullName));
                                if (destinationPath.StartsWith(pathToExtract, StringComparison.Ordinal))
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                    entry.ExtractToFile(destinationPath);
                                }
                                result.Add(destinationPath);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

            return result.ToArray();
        }
    }
}
