using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Models
{
    public class BlockIPv4
    {
        [Key]
        public string Network { get; set; }

        [ForeignKey("Location")]
        public int Geoname_Id { get; set; }

        public int? Registered_Country_Geoname_Id { get; set; }
        public int? Represented_Country_Geoname_Id { get; set; }
        public bool Is_Anonymous_Proxy { get; set; }
        public bool Is_Satellite_Provider { get; set; }
        public string Postal_Code { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Accuracy_Radius { get; set; }

        public virtual CityLocation Location { get; set; }

        public bool Equals(BlockIPv4 obj)
        {
            return Network == obj.Network && Geoname_Id == obj.Geoname_Id && Registered_Country_Geoname_Id ==
                obj.Registered_Country_Geoname_Id && Represented_Country_Geoname_Id == obj.Represented_Country_Geoname_Id &&
                Is_Anonymous_Proxy == obj.Is_Anonymous_Proxy && Is_Satellite_Provider == Is_Satellite_Provider && Postal_Code
                == obj.Postal_Code && Latitude == obj.Latitude && Longitude == obj.Longitude && Accuracy_Radius ==
                obj.Accuracy_Radius;
        }
    }
}
