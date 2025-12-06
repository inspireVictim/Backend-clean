using Microsoft.Extensions.DependencyInjection;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Services;

namespace YessBackend.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddYessBackendServices(this IServiceCollection services)
    {
        // 🔐 Auth
        services.AddScoped<IAuthService, AuthService>();

        // 👛 Wallet
        services.AddScoped<IWalletService, WalletService>();

        // 🏪 Partner
        services.AddScoped<IPartnerService, PartnerService>();

        // 🛒 Orders
        services.AddScoped<IOrderService, OrderService>();

        // ❤️ Health
        services.AddScoped<IHealthService, HealthService>();

        // 📍 Location
        services.AddScoped<ILocationService, LocationService>();

        // 📦 Storage
        services.AddScoped<IStorageService, StorageService>();

        // 📲 QR
        services.AddScoped<IQRService, QRService>();

        // 🎥 Stories
        services.AddScoped<IStoryService, StoryService>();

        // 🛍 Partner products
        services.AddScoped<IPartnerProductService, PartnerProductService>();

        // 💳 Payments
        services.AddScoped<IOrderPaymentService, OrderPaymentService>();
        services.AddScoped<IPaymentProviderService, PaymentProviderService>();

        // 🔔 Notifications
        services.AddScoped<INotificationService, NotificationService>();

        // 🏅 Achievements
        services.AddScoped<IAchievementService, AchievementService>();

        // 🎯 Promotions
        services.AddScoped<IPromotionService, PromotionService>();

        // 🏦 Bank
        services.AddScoped<IBankService, BankService>();

        // Optima / Finik
        services.AddScoped<IOptimaPaymentService, OptimaPaymentService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
