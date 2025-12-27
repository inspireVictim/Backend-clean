using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.DTOs.Order;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;
using QuestPDF.Fluent;      // Добавлено
using QuestPDF.Helpers;     // Добавлено
using QuestPDF.Infrastructure; // Добавлено

namespace YessBackend.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;

    public OrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ... (Методы CalculateOrderAsync, CreateOrderAsync, GetOrderByIdAsync без изменений)

    public async Task<OrderCalculateResponseDto> CalculateOrderAsync(int partnerId, List<OrderItemDto> items, int? userId = null)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == partnerId);
        if (partner == null) throw new InvalidOperationException("Партнер не найден");
        if (!partner.IsActive) throw new InvalidOperationException("Партнер неактивен");

        decimal orderTotal = 0;
        decimal totalYessCoinsRequired = 0;

        foreach (var item in items)
        {
            var product = await _context.PartnerProducts.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.PartnerId == partnerId && p.IsAvailable);
            if (product == null) throw new InvalidOperationException($"Товар {item.ProductId} не найден или недоступен");
            if (product.OriginalPrice.HasValue && product.OriginalPrice > product.Price)
                totalYessCoinsRequired += (product.OriginalPrice.Value - product.Price) * item.Quantity;
            orderTotal += product.Price * item.Quantity;
        }

        decimal? userBalance = null;
        if (userId.HasValue)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet != null && wallet.YescoinBalance < totalYessCoinsRequired)
                throw new InvalidOperationException($"Недостаточно Yess!Coin. Нужно: {totalYessCoinsRequired}, у вас: {wallet.YescoinBalance}");
            userBalance = wallet?.YescoinBalance;
        }

        var cashbackRate = partner.CashbackRate > 0 ? partner.CashbackRate : 5m;
        var cashbackAmount = orderTotal * (cashbackRate / 100);
        return new OrderCalculateResponseDto { OrderTotal = orderTotal, Discount = totalYessCoinsRequired, FinalAmount = orderTotal, CashbackAmount = cashbackAmount, UserBalance = userBalance };
    }

    public async Task<Order> CreateOrderAsync(int userId, OrderCreateRequestDto orderRequest)
    {
        var idempotencyKey = orderRequest.IdempotencyKey ?? GenerateIdempotencyKey(userId, orderRequest.PartnerId, orderRequest.Items);
        var existingOrder = await _context.Orders.Include(o => o.Items).Include(o => o.Transaction).FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        if (existingOrder != null) return existingOrder;

        var calculation = await CalculateOrderAsync(orderRequest.PartnerId, orderRequest.Items, userId);
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            bool isYescoinPay = orderRequest.PaymentMethod?.ToLower() == "yescoin";
            Transaction? transaction = null;

            if (isYescoinPay && calculation.Discount > 0)
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null) throw new InvalidOperationException("Кошелек не найден");
                var oldBalance = wallet.YescoinBalance;
                wallet.YescoinBalance -= calculation.Discount;
                wallet.TotalSpent += calculation.Discount;

                transaction = new Transaction
                {
                    UserId = userId,
                    PartnerId = orderRequest.PartnerId,
                    Type = "YESCOIN_PAYMENT",
                    Amount = calculation.Discount,
                    YescoinUsed = calculation.Discount,
                    BalanceBefore = oldBalance,
                    BalanceAfter = wallet.YescoinBalance,
                    Status = "SUCCESS",
                    Description = "Оплата заказа коинами",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(transaction);
            }

            var order = new Order
            {
                UserId = userId,
                PartnerId = orderRequest.PartnerId,
                OrderTotal = calculation.OrderTotal,
                Discount = calculation.Discount,
                CashbackAmount = calculation.CashbackAmount,
                FinalAmount = calculation.FinalAmount,
                Status = isYescoinPay ? OrderStatus.Paid : OrderStatus.Pending,
                DeliveryAddress = orderRequest.DeliveryAddress,
                DeliveryType = orderRequest.DeliveryType ?? "pickup",
                PaymentMethod = orderRequest.PaymentMethod,
                PaymentStatus = isYescoinPay ? "paid" : "pending",
                Transaction = transaction,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow,
                PaidAt = isYescoinPay ? DateTime.UtcNow : null
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in orderRequest.Items)
            {
                var product = await _context.PartnerProducts.FindAsync(item.ProductId);
                if (product != null)
                {
                    _context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = product.Id, ProductName = product.Name, ProductPrice = product.Price, Quantity = item.Quantity, Subtotal = product.Price * item.Quantity, CreatedAt = DateTime.UtcNow });
                    if (product.StockQuantity.HasValue) product.StockQuantity -= item.Quantity;
                }
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            await _context.Entry(order).Collection(o => o.Items).LoadAsync();
            return order;
        }
        catch { await dbTransaction.RollbackAsync(); throw; }
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId, int? userId = null)
    {
        var query = _context.Orders.Include(o => o.Items).Include(o => o.Transaction).Include(o => o.Partner).AsQueryable();
        if (userId.HasValue) query = query.Where(o => o.UserId == userId.Value);
        return await query.FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<List<Order>> GetUserOrdersAsync(int userId, int limit = 20, int offset = 0)
    {
        return await _context.Orders.Include(o => o.Items).Include(o => o.Transaction).Include(o => o.Partner).Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).Skip(offset).Take(limit).ToListAsync();
    }

    public string GenerateIdempotencyKey(int userId, int partnerId, List<OrderItemDto> items)
    {
        var itemsStr = string.Join(",", items.OrderBy(i => i.ProductId).Select(i => $"{i.ProductId}:{i.Quantity}"));
        var data = $"{userId}:{partnerId}:{itemsStr}:{DateTime.UtcNow:O}";
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    // Реализация генерации PDF
    public async Task<byte[]> GenerateReceiptPdfAsync(Order order)
    {
        // Обязательно для новых версий
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(0.5f, Unit.Centimetre);

                // ИСПРАВЛЕНИЕ: В новых версиях используется ContinuousSize для чеков 
                // или стандартный PageSizes.A6 через метод Size
                page.Size(PageSizes.A6);

                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("YessGo").FontSize(16).Bold().FontColor(Colors.Blue.Medium);
                    col.Item().AlignCenter().Text(order.Partner?.Name ?? "Партнер").FontSize(10);
                    col.Item().PaddingVertical(4).LineHorizontal(1);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Заказ №{order.Id}");
                        row.RelativeItem().AlignRight().Text($"{order.CreatedAt:dd.MM.yy HH:mm}");
                    });
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        foreach (var item in order.Items)
                        {
                            table.Cell().Text(item.ProductName);
                            table.Cell().AlignCenter().Text($"{item.Quantity}");
                            table.Cell().AlignRight().Text($"{item.Subtotal:N0}");
                        }
                    });

                    // Разделитель: используем встроенный LineHorizontal вместо прямого рисования через SkiaSharp
                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    // Упрощенный вариант разделителя, если Canvas кажется сложным:
                    // col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().AlignRight().Text(x =>
                    {
                        x.Span("ИТОГО: ").Bold();
                        x.Span($"{order.FinalAmount:N0} сом").Bold();
                    });
                });

                page.Footer().AlignCenter().Text("Спасибо за покупку!").FontSize(8).Italic();
            });
        });

        return document.GeneratePdf();
    }
}