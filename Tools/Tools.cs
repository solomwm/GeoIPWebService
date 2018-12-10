using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Tools
{
    public static class Utilites
    {
        public static string GetMD5(Stream inputData)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            return BitConverter.ToString(md5.ComputeHash(inputData)).Replace("-", string.Empty);
        }

        public static bool DownloadFile(string urlStr, string destFilePath)
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
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception("download failed", e.InnerException);
            }
        }

        public static bool CheckMD5(string pattern, Stream inputData)
        {
            inputData.Position = 0;
            string hash = GetMD5(inputData);
            return pattern.ToUpper().Trim() == hash.ToUpper().Trim();
        }
    }
}
