using System.Linq;
using System.Text.RegularExpressions;
using Database;
using Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace GeoIPWebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly GeoIPDbContext db;
        private const string IPv4_Regex_Pattern = @"((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)"; // Взято осюда: https://habr.com/post/123845/

        public LocationController(GeoIPDbContext dbContext)
        {
            db = dbContext;
        }

        //GET: api/location
        [HttpGet]
        public ActionResult<string> Get()
        {
            return Ok($"База данных содержит {db.Blocks_IPv4.Count()} IP-адресов, соответствующих {db.CityLocations.Count()} " +
                $"локациям.\n© 2018 - GeoIPWebService.");
        }

        //GET: api/location/192.168.1.1
        [HttpGet("{IP?}")]
        public ActionResult<BlockIPv4> Get(string IP)
        {
            if (!Regex.IsMatch(IP, IPv4_Regex_Pattern))
            {
                return BadRequest(new { error = "Invalid IP: " + IP });
            }

            BlockIPv4 blockIPv4 = db.Blocks_IPv4.FirstOrDefault(bl => bl.Network.StartsWith(IP));

            if (blockIPv4 == null)
            {
                return NotFound(new { result = "IP: " + IP + " не найден." });
            }

            blockIPv4.Location = db.CityLocations.FirstOrDefault(l => l.Geoname_Id == blockIPv4.Geoname_Id);
            return Ok(blockIPv4);
        }
    }
}