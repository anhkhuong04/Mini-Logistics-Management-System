using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class FeeRuleConfiguration : IEntityTypeConfiguration<FeeRule>
{
    private static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<FeeRule> builder)
    {
        builder.ToTable("FeeRules");

        builder.HasKey(feeRule => feeRule.Id);

        builder.Property(feeRule => feeRule.Id)
            .ValueGeneratedNever();

        builder.Property(feeRule => feeRule.RouteType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(feeRule => feeRule.BaseFee)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(feeRule => feeRule.BaseWeightKg)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(feeRule => feeRule.ExtraWeightStepKg)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(feeRule => feeRule.ExtraStepFee)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(feeRule => feeRule.MinimumWeightKg)
            .HasPrecision(10, 3);

        builder.Property(feeRule => feeRule.MaximumWeightKg)
            .HasPrecision(10, 3);

        builder.Property(feeRule => feeRule.Version)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(feeRule => feeRule.InsuranceFreeThreshold)
            .HasPrecision(18, 2)
            .HasDefaultValue(InsuranceFeePolicy.FreeInsuranceThreshold)
            .IsRequired();

        builder.Property(feeRule => feeRule.InsuranceMaximumValue)
            .HasPrecision(18, 2)
            .HasDefaultValue(InsuranceFeePolicy.MaximumInsuredValue)
            .IsRequired();

        builder.Property(feeRule => feeRule.InsuranceRate)
            .HasPrecision(10, 6)
            .HasDefaultValue(InsuranceFeePolicy.InsuranceRate)
            .IsRequired();

        builder.Property(feeRule => feeRule.ReturnFeeRate)
            .HasPrecision(10, 4)
            .HasDefaultValue(0.5m)
            .IsRequired();

        builder.Property(feeRule => feeRule.IsActive)
            .IsRequired();

        builder.Property(feeRule => feeRule.CreatedAtUtc)
            .IsRequired();

        builder.Property(feeRule => feeRule.UpdatedAtUtc);

        builder.HasIndex(feeRule => new { feeRule.RouteType, feeRule.IsActive });

        builder.HasData(
            CreateSeed(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                RouteType.IntraProvince,
                2.0m,
                20000,
                0.5m,
                3000),
            CreateSeed(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                RouteType.IntraRegion,
                0.5m,
                28000,
                0.5m,
                4000),
            CreateSeed(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                RouteType.InterRegion,
                0.5m,
                35000,
                0.5m,
                8000));
    }

    private static object CreateSeed(
        Guid id,
        RouteType routeType,
        decimal baseWeightKg,
        decimal baseFee,
        decimal extraWeightStepKg,
        decimal extraStepFee)
    {
        return new
        {
            Id = id,
            RouteType = routeType,
            BaseWeightKg = baseWeightKg,
            BaseFee = new Money(baseFee),
            ExtraWeightStepKg = extraWeightStepKg,
            ExtraStepFee = new Money(extraStepFee),
            MinimumWeightKg = (decimal?)null,
            MaximumWeightKg = (decimal?)null,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
            UpdatedAtUtc = (DateTimeOffset?)null,
            Version = 1,
            InsuranceFreeThreshold = InsuranceFeePolicy.FreeInsuranceThreshold,
            InsuranceMaximumValue = InsuranceFeePolicy.MaximumInsuredValue,
            InsuranceRate = InsuranceFeePolicy.InsuranceRate,
            ReturnFeeRate = 0.5m
        };
    }
}
