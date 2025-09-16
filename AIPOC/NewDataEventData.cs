using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIPOC.Models
{
    public class NewDataEventData
    {
        public required string Type { get; set; }
        public required string Name { get; set; }
        public required string Format { get; set; }

        public required object Data { get; set; }
        public required DateTime Timestamp { get; set; }
    }
}
