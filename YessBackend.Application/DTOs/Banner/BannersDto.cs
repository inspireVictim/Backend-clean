using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BannersDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageFileName { get; set; } = string.Empty; // Только имя файла: "promo.webp"
    public int? CityId { get; set; }
    public int? PartnerId { get; set; }
    public bool IsActive { get; set; } = true;
}
