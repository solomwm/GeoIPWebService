using System;

namespace DatabaseUpdater
{
    [Serializable]
    public class Config
    {
        public string ConnectionString { get; set; }
        public string CashFolder { get; set; }
        public string TempFolder { get; set; }
        public string Locations_CSV_FileName { get; set; }
        public string BlocksIPv4_CSV_FileName { get; set; }
        public string BlocksIPv6_CSV_FileName { get; set; }
        public string MD5FileUrl { get; set; }
        public string DataFileUrl { get; set; }
    }
}
