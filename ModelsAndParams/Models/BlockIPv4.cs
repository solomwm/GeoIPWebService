namespace Database.Models
{
    public class BlockIPv4: BlockIP
    {
        public bool Equals(BlockIPv4 obj) //В целях оптимизации быстродействия. Первые три поля не сравниваем, потому как очень вряд ли они могут измениться.
        {
            return Represented_Country_Geoname_Id == obj.Represented_Country_Geoname_Id && Is_Anonymous_Proxy == 
                obj.Is_Anonymous_Proxy && Is_Satellite_Provider == Is_Satellite_Provider && Postal_Code == obj.Postal_Code && 
                Latitude == obj.Latitude && Longitude == obj.Longitude && Accuracy_Radius == obj.Accuracy_Radius;
        }
    }
}
