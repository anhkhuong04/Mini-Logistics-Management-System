using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

/// <summary>
/// Represents the Fee Rule domain entity.
/// </summary>
public sealed class FeeRule : AuditableEntity
{
    private FeeRule()
    {
        BaseFee = Money.Zero;
        ExtraStepFee = Money.Zero;
    }

    public FeeRule(
        RouteType routeType,
        decimal baseWeightKg,
        Money baseFee,
        decimal extraWeightStepKg,
        Money extraStepFee,
        DateTimeOffset createdAtUtc,
        decimal? minimumWeightKg = null,
        decimal? maximumWeightKg = null,
        int version = 1,
        decimal insuranceFreeThreshold = InsuranceFeePolicy.FreeInsuranceThreshold,
        decimal insuranceMaximumValue = InsuranceFeePolicy.MaximumInsuredValue,
        decimal insuranceRate = InsuranceFeePolicy.InsuranceRate,
        decimal returnFeeRate = 0.5m)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (version <= 0)
        {
            throw new DomainException("Fee rule version must be greater than zero.");
        }

        if (baseWeightKg <= 0)
        {
            throw new DomainException("Base weight must be greater than zero.");
        }

        if (extraWeightStepKg <= 0)
        {
            throw new DomainException("Extra weight step must be greater than zero.");
        }

        if (minimumWeightKg is <= 0)
        {
            throw new DomainException("Minimum weight must be greater than zero.");
        }

        if (maximumWeightKg is <= 0)
        {
            throw new DomainException("Maximum weight must be greater than zero.");
        }

        if (minimumWeightKg is not null && maximumWeightKg is not null && minimumWeightKg > maximumWeightKg)
        {
            throw new DomainException("Minimum weight cannot be greater than maximum weight.");
        }

        if (insuranceFreeThreshold < 0 || insuranceMaximumValue < 0 || insuranceRate < 0 || returnFeeRate < 0)
        {
            throw new DomainException("Fee policy values cannot be negative.");
        }

        RouteType = routeType;
        BaseWeightKg = decimal.Round(baseWeightKg, 3);
        BaseFee = baseFee;
        ExtraWeightStepKg = decimal.Round(extraWeightStepKg, 3);
        ExtraStepFee = extraStepFee;
        MinimumWeightKg = minimumWeightKg;
        MaximumWeightKg = maximumWeightKg;
        Version = version;
        InsuranceFreeThreshold = decimal.Round(insuranceFreeThreshold, 2);
        InsuranceMaximumValue = decimal.Round(insuranceMaximumValue, 2);
        InsuranceRate = decimal.Round(insuranceRate, 6);
        ReturnFeeRate = decimal.Round(returnFeeRate, 4);
        IsActive = true;
    }

    public RouteType RouteType { get; private set; }

    public decimal BaseWeightKg { get; private set; }

    public Money BaseFee { get; private set; }

    public decimal ExtraWeightStepKg { get; private set; }

    public Money ExtraStepFee { get; private set; }

    public decimal? MinimumWeightKg { get; private set; }

    public decimal? MaximumWeightKg { get; private set; }

    public int Version { get; private set; }

    public decimal InsuranceFreeThreshold { get; private set; }

    public decimal InsuranceMaximumValue { get; private set; }

    public decimal InsuranceRate { get; private set; }

    public decimal ReturnFeeRate { get; private set; }

    public bool IsActive { get; private set; }

    public bool AppliesTo(ShippingFeeRequest request)
    {
        if (!IsActive || RouteType != request.RouteType)
        {
            return false;
        }

        if (MinimumWeightKg is not null && request.ChargeableWeightKg < MinimumWeightKg)
        {
            return false;
        }

        if (MaximumWeightKg is not null && request.ChargeableWeightKg > MaximumWeightKg)
        {
            return false;
        }

        return true;
    }

    public ShippingFeeQuote Calculate(ShippingFeeRequest request)
    {
        var extraWeightKg = Math.Max(0, request.ChargeableWeightKg - BaseWeightKg);
        var extraBlocks = extraWeightKg == 0
            ? 0
            : (int)decimal.Ceiling(extraWeightKg / ExtraWeightStepKg);
        var extraFee = ExtraStepFee * extraBlocks;
        var insuranceFee = InsuranceFeePolicy.Calculate(
            request.DeclaredGoodsValue,
            InsuranceFreeThreshold,
            InsuranceMaximumValue,
            InsuranceRate);
        var zeroFee = new Money(0, BaseFee.Currency);
        var breakdown = new ShippingFeeBreakdown(
            BaseFee,
            extraFee,
            insuranceFee,
            zeroFee,
            ReturnFeeRate);

        return new ShippingFeeQuote(
            breakdown,
            request.ActualWeight.Kilograms,
            request.VolumetricWeightKg,
            request.ChargeableWeightKg,
            BaseWeightKg,
            ExtraWeightStepKg,
            extraBlocks);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        IsActive = true;
        MarkUpdated(updatedAtUtc);
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = false;
        MarkUpdated(updatedAtUtc);
    }
}
