using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YessBackend.Domain.Entities;

namespace YessBackend.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация для Transaction entity
/// Соответствует индексам и ограничениям из Python модели
/// </summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        // Индексы
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.PartnerId);
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => new { t.UserId, t.Status, t.CreatedAt }).HasDatabaseName("idx_transaction_user_status");
        builder.HasIndex(t => new { t.Type, t.Status, t.CreatedAt }).HasDatabaseName("idx_transaction_type_status");
        builder.HasIndex(t => new { t.CreatedAt, t.Status }).HasDatabaseName("idx_transaction_date_range");
        builder.HasIndex(t => new { t.PartnerId, t.CreatedAt }).HasDatabaseName("idx_transaction_partner");

        // Уникальный индекс для номера транзакции
        builder.Property(t => t.TransactionNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(t => t.TransactionNumber)
            .IsUnique()
            .HasDatabaseName("idx_transaction_number_unique");

        // Check constraint
        builder.HasCheckConstraint("check_positive_amount", "amount > 0");

        // Relationships
        builder.HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Partner)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.PartnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order relationship is configured in OrderConfiguration.cs
        // Transaction.Order is inverse navigation property (FK is in Order.TransactionId)
    }
}