using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIPOC.Options
{
    public class DataSourceOptions
    {
        public required string Type { get; set; }
        public required string Name { get; set; }
        public required List<string> Modes { get; set; }
        public required Dictionary<string, string> Settings { get; set; }
    }
}
