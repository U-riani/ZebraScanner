using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class InventorizationDocumentLinesDto
    {
        public int item_id { get; set; }
        public int document_id { get; set; }

        public string barcode { get; set; }
        public string article_code { get; set; }
        public string product_name { get; set; }
        public string? color { get; set; }
        public string? size { get; set; }

        public float? price { get; set; }

        public string? box_id { get; set; }

        public int expected_qty { get; set; }
        public int? counted_qty { get; set; }

        public int? employee_id { get; set; }
    }
}
