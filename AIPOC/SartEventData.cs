using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AIPOC.Models
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class StartEventData
    {
        public required string ConfirmationNumber { get; set; }
        public required string Mode { get; set; }
    }
}
