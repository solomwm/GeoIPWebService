﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Database.Models
{
    public class BlockIPv4
    {
        public string Network { get; set; }
        public int Geoname_Id { get; set; }
        public int Registered_Country_Geoname_Id { get; set; }
        public int Represented_Country_Geoname_Id { get; set; }
        public bool Is_Anonymous_Proxy { get; set; }
        public bool Is_Satellite_Provider { get; set; }
        public string Postal_Code { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Accuracy_Radius { get; set; }

        public CityLocation Location { get; set; }
    }
}