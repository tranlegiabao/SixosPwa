using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SixosPwa.Models;

[Table("DMChuDe")]
public class DMChuDe
{
    [Key]
    public long ID { get; set; }
    
    [StringLength(20)]
    public string? LoaiND { get; set; }
    
    public int? Active { get; set; }
    
    [StringLength(20)]
    public string? TenChuDe { get; set; }
}
