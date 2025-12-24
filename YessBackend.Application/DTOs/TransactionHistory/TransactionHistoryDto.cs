using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YessBackend.Application.DTOs;

public class TransactionHistoryDto
{
    public string Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Currency { get; set; }
}