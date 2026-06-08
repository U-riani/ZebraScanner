using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos;

public class DocumentStatusChangeResponseDto
{
    public bool ok { get; set; }
    public string assignment_status { get; set; }
    public string document_status { get; set; }
    public string role { get; set; }
}