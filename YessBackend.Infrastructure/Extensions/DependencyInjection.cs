using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

using YessBackend.Infrastructure.Data;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Infrastructure.Services;
using YessBackend.Application.Services;

namespace YessBackend.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        // ==========================
        // PostgreSQL DbContext
        // ==========================
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' не найден");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("YessBackend.Infrastructure");
                npgsql.CommandTimeout(30);
            });
        });

        // ==========================
        // Финансовые сервисы Finik
        // ==========================
        services.AddSingleton<IFinikSignatureService, FinikSignatureService>();
        services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();

        return services;
    }

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

        // 🛍 Products
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

        // Finik API
        services.AddScoped<IFinikService, FinikService>();

        // Email & reconciliation
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
