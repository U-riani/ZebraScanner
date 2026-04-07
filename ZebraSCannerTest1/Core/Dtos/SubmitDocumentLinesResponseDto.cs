using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos;

public class SubmitDocumentLinesResponseDto
{
    public bool ok { get; set; }
    public int document_id { get; set; }
    public string module { get; set; } = string.Empty;
    public string? role { get; set; }
    public int processed_rows { get; set; }
    public int updated_lines { get; set; }
    public string assignment_status { get; set; } = string.Empty;
}