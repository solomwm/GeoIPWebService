using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Database.Models
{
    public class CityLocation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Geoname_Id { get; set; }

        public string Local_Code { get; set; }
        public string Continent_Code { get; set; }
        public string Continent_Name { get; set; }
        public string Country_Iso_Code { get; set; }
        public string Country_Name { get; set; }
        public string Subdivision_1_Iso_Code { get; set; }
        public string Subdivision_1_Name { get; set; }
        public string Subdivision_2_Iso_Code { get; set; }
        public string Subdivision_2_Name { get; set; }
        public string City_Name { get; set; }
        public string Metro_Code { get; set; }
        public string Time_Zone { get; set; }
        public bool Is_In_European_Union { get; set; }
    }
}
