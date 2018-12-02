using System;

namespace Tools
{
    public static class GlobalParams
    {
        //public const string block_IPv4_File_Header = "network,geoname_id,registered_country_geoname_id,represented_country_geoname_id,is_anonymous_proxy,is_satellite_provider,postal_code,latitude,longitude,accuracy_radius";
        //public const string location_File_Header = "geoname_id,locale_code,continent_code,continent_name,country_iso_code,country_name,subdivision_1_iso_code,subdivision_1_name,subdivision_2_iso_code,subdivision_2_name,city_name,metro_code,time_zone,is_in_european_union";
        public const string IPv4_Regex_Pattern = @"((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)"; // Взято осюда: https://habr.com/post/123845/
    }
}
