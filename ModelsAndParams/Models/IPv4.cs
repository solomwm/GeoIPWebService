using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Models
{
    public class IPv4
    {
        [Key]
        public string IP { get; set; }

        [ForeignKey("BlockIPv4")]
        [Required]
        public string BlockIPv4_Id { get; set; }

        public virtual BlockIPv4 BlockIPv4 { get; set; }
    }
}
