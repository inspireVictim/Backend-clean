using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YessBackend.Application.DTOs.OrderPayment;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

public class OrderPaymentService : IOrderPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderPaymentService> _logger;

    public OrderPaymentService(
        ApplicationDbContext context,
        ILogger<OrderPaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentResponseDto> CreateOrderPaymentAsync(int orderId, int userId, OrderPaymentRequestDto request)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null) throw new InvalidOperationException("Заказ не найден");
            if (order.Status != OrderStatus.Pending || order.PaymentStatus == "paid")
                throw new InvalidOperationException("Заказ уже оплачен или недоступен");

            var transactionId = Guid.NewGuid().ToString();
            var status = "processing";
            string walletInfo = ""; // Добавочка для чека

            if (request.Method == Application.DTOs.OrderPayment.PaymentMethod.wallet)
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null) throw new InvalidOperationException("Кошелек не найден");
                if (wallet.Balance < order.FinalAmount) throw new InvalidOperationException("Недостаточно коинов");

                wallet.Balance -= order.FinalAmount;
                wallet.LastUpdated = DateTime.UtcNow;

                var transaction = new Transaction
                {
                    UserId = userId,
                    Amount = order.FinalAmount,
                    Type = "payment",
                    Status = "completed",
                    Description = $"Оплата заказа #{orderId}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                order.TransactionId = transaction.Id;
                order.PaymentMethod = "wallet";
                order.PaymentStatus = "paid";
                order.Status = OrderStatus.Paid;
                order.PaidAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                status = "success";

                // Формируем инфо об остатке
                walletInfo = $" Списано: {order.FinalAmount} коинов. Остаток: {wallet.Balance}.";
            }
            else
            {
                order.PaymentMethod = request.Method.ToString();
                order.PaymentStatus = "processing";
                await _context.SaveChangesAsync();
            }

            // ФОРМИРУЕМ ИТОГОВЫЙ ТЕКСТ ЧЕКА В MESSAGE
            string finalMessage = status == "success"
                ? $"[ЧЕК ОПЛАТЫ] Заказ №{orderId} оплачен успешно.{walletInfo} Дата: {DateTime.UtcNow:dd.MM.yyyy HH:mm}"
                : "Платеж создан и ожидает обработки";

            return new PaymentResponseDto
            {
                OrderId = orderId,
                TransactionId = transactionId,
                Status = status,
                Amount = order.FinalAmount,
                Commission = 0,
                Message = finalMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания платежа заказа");
            throw;
        }
    }

    public async Task<PaymentStatusResponseDto> GetPaymentStatusAsync(int orderId, int userId)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null) throw new InvalidOperationException("Заказ не найден");

            return new PaymentStatusResponseDto
            {
                OrderId = orderId,
                PaymentStatus = order.PaymentStatus,
                OrderStatus = order.Status.ToString(),
                Amount = order.FinalAmount,
                PaidAt = order.PaidAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статуса платежа");
            throw;
        }
    }
}