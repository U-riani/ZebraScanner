using System.ComponentModel.DataAnnotations;


    namespace ZebraSCannerTest1.Models;
    
        public class ScanLog
        {
            public int Id { get; set; }
            public string Barcode { get; set; }
            public int Was { get; set; }
            public int IncrementBy { get; set; }
            public int IsValue { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    


