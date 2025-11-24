using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YessBackend.Domain.Entities;

/// <summary>
/// Сотрудники партнеров
/// Соответствует таблице partner_employees в PostgreSQL
/// </summary>
public class PartnerEmployee
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int PartnerId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [MaxLength(100)]
    public string? Position { get; set; }
    
    public DateTime HiredAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("PartnerId")]
    public virtual Partner Partner { get; set; } = null!;
    
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
