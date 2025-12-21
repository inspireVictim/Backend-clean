using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.DTOs.Order;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;

    public OrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderCalculateResponseDto> CalculateOrderAsync(
        int partnerId,
        List<OrderItemDto> items,
        int? userId = null)
    {
        var partner = await _context.Partners
            .FirstOrDefaultAsync(p => p.Id == partnerId);

        if (partner == null) throw new InvalidOperationException("Партнер не найден");
        if (!partner.IsActive) throw new InvalidOperationException("Партнер неактивен");

        decimal orderTotal = 0;
        decimal totalYessCoinsRequired = 0;

        foreach (var item in items)
        {
            var product = await _context.PartnerProducts
                .FirstOrDefaultAsync(p =>
                    p.Id == item.ProductId &&
                    p.PartnerId == partnerId &&
                    p.IsAvailable);

            if (product == null)
                throw new InvalidOperationException($"Товар {item.ProductId} не найден или недоступен");

            // ЛОГИКА: Коины к списанию = (OriginalPrice - Price) * Quantity
            if (product.OriginalPrice.HasValue && product.OriginalPrice.Value > product.Price)
            {
                totalYessCoinsRequired += (product.OriginalPrice.Value - product.Price) * item.Quantity;
            }

            var price = product.Price;
            var subtotal = price * item.Quantity;
            orderTotal += subtotal;
        }

        // Проверка баланса коинов
        decimal? userBalance = null;
        if (userId.HasValue)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId.Value);

            if (wallet != null && wallet.YescoinBalance < totalYessCoinsRequired)
            {
                throw new InvalidOperationException($"Недостаточно Yess!Coin. Нужно: {totalYessCoinsRequired}, у вас: {wallet.YescoinBalance}");
            }
            userBalance = wallet?.Balance;
        }

        return new OrderCalculateResponseDto
        {
            OrderTotal = orderTotal,
            Discount = totalYessCoinsRequired, // Используем поле Discount для отображения списания коинов
            FinalAmount = orderTotal,
            UserBalance = userBalance
        };
    }

    public async Task<Order> CreateOrderAsync(
        int userId,
        OrderCreateRequestDto orderRequest)
    {
        var idempotencyKey = orderRequest.IdempotencyKey ?? GenerateIdempotencyKey(
            userId, orderRequest.PartnerId, orderRequest.Items);

        var existingOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);

        if (existingOrder != null) return existingOrder;

        // Расчет коинов перед созданием
        var calculation = await CalculateOrderAsync(orderRequest.PartnerId, orderRequest.Items, userId);

        // 1. Списание коинов из кошелька (если они требуются)
        if (calculation.Discount > 0)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null) throw new InvalidOperationException("Кошелек не найден");

            wallet.YescoinBalance -= calculation.Discount;
            wallet.TotalSpent += calculation.Discount;
            wallet.LastUpdated = DateTime.UtcNow;

            // 2. Создание транзакции (чтобы база не ругалась, сумма всегда > 0 здесь)
            var transaction = new Transaction
            {
                UserId = userId,
                PartnerId = orderRequest.PartnerId,
                Type = "PAYMENT",
                Amount = calculation.Discount,
                Status = "SUCCESS",
                Description = $"Оплата заказа коинами (Скидка: {calculation.Discount})",
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<Transaction>().Add(transaction);
        }

        // 3. Создание заказа
        var order = new Order
        {
            UserId = userId,
            PartnerId = orderRequest.PartnerId,
            OrderTotal = calculation.OrderTotal,
            Discount = calculation.Discount,
            FinalAmount = calculation.FinalAmount,
            Status = OrderStatus.Pending,
            DeliveryType = orderRequest.DeliveryType ?? "pickup",
            PaymentStatus = "paid_by_yescoin",
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // 4. Добавление товаров в заказ
        foreach (var item in orderRequest.Items)
        {
            var product = await _context.PartnerProducts.FindAsync(item.ProductId);
            if (product == null) continue;

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                ProductPrice = product.Price,
                Quantity = item.Quantity,
                Subtotal = product.Price * item.Quantity,
                CreatedAt = DateTime.UtcNow
            };
            _context.OrderItems.Add(orderItem);

            if (product.StockQuantity.HasValue)
                product.StockQuantity -= item.Quantity;
        }

        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId, int? userId = null)
    {
        var query = _context.Orders.Include(o => o.Items).AsQueryable();
        if (userId.HasValue) query = query.Where(o => o.UserId == userId.Value);
        return await query.FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<List<Order>> GetUserOrdersAsync(int userId, int limit = 20, int offset = 0)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(offset).Take(limit).ToListAsync();
    }

    public string GenerateIdempotencyKey(int userId, int partnerId, List<OrderItemDto> items)
    {
        var itemsStr = string.Join(",", items.OrderBy(i => i.ProductId).Select(i => $"{i.ProductId}:{i.Quantity}"));
        var data = $"{userId}:{partnerId}:{itemsStr}:{DateTime.UtcNow:O}";
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}