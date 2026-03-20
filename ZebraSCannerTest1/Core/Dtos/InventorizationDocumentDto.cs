using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class InventorizationDocumentDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public int warehouse_id { get; set; }
        public string warehouse_name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string doc_type { get; set; }
        public string status { get; set; }
        public DateTime created_at { get; set; }
    }
}
