using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos;

public class SubmitDocumentLinesRequestDto
{
    public string current_status { get; set; } = string.Empty;
    public string? role { get; set; }
    public List<SubmitDocumentLineRowDto> rows { get; set; } = new();
}
