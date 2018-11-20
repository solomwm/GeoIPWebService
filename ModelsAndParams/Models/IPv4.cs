using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Database.Models
{
    public class IPv4
    {
        public int Id { get; set; }
        public string IP { get; set; }

        [ForeignKey("BlockIPv4")]
        public string BlockIPv4_Id { get; set; }

        public virtual BlockIPv4 BlockIPv4 { get; set; }
    }
}
