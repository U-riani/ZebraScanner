using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos;

public class SubmitDocumentLineRowDto
{
    public int? line_id { get; set; }
    public string barcode { get; set; } = string.Empty;
    public int quantity { get; set; }
    public string? box_id { get; set; }
}