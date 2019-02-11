using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Models
{
    public class BlockIP
    {
        [Key]
        public string Network { get; set; }

        [ForeignKey("Location")]
        public int? Geoname_Id { get; set; }

        public int? Registered_Country_Geoname_Id { get; set; }
        public int? Represented_Country_Geoname_Id { get; set; }
        public bool? Is_Anonymous_Proxy { get; set; }
        public bool? Is_Satellite_Provider { get; set; }
        public string Postal_Code { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Accuracy_Radius { get; set; }

        public virtual CityLocation Location { get; set; }
    }
}
