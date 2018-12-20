using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        
        public bool Equals(CityLocation obj) //В целях оптимизации быстродействия. Первые пять полей не сравниваем, потому как очень вряд ли они могут измениться.
        {
            return Country_Name == obj.Country_Name && Subdivision_1_Iso_Code == obj.Subdivision_1_Iso_Code && 
                Subdivision_1_Name == obj.Subdivision_1_Name && Subdivision_2_Iso_Code == obj.Subdivision_2_Iso_Code && 
                Subdivision_2_Name == obj.Subdivision_2_Name && City_Name == obj.City_Name && Metro_Code == obj.Metro_Code && 
                Time_Zone == obj.Time_Zone && Is_In_European_Union == obj.Is_In_European_Union;
        }
    }
}
