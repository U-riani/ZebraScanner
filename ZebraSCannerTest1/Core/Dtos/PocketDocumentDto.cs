using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos;

public class PocketDocumentDto
{
    public int id { get; set; }
    public string name { get; set; }

    public string doc_module { get; set; }
    public string scan_type { get; set; }
    public string status { get; set; }

    public int? warehouse_id { get; set; }
    public string? warehouse_name { get; set; }

    public int? from_warehouse_id { get; set; }
    public string? from_warehouse_name { get; set; }

    public int? to_warehouse_id { get; set; }
    public string? to_warehouse_name { get; set; }

    public int? parent_document_id { get; set; }

    public string? description { get; set; }

    public List<int>? employees { get; set; }

    public DateTime created_at { get; set; }
    public DateTime? updated_at { get; set; }
}