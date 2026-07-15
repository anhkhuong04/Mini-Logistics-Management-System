using Microsoft.AspNetCore.DataProtection;
using MiniLogistics.Application.PartnerApi;

namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Prefix = "dp:v1:";
    private const string Purpose = "MiniLogistics.PartnerApi.WebhookSigningSecret.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintextSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextSecret);

        return Prefix + _protector.Protect(plaintextSecret);
    }

    public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        return IsProtected(protectedSecret)
            ? _protector.Unprotect(protectedSecret[Prefix.Length..])
            : protectedSecret;
    }

    public bool IsProtected(string value)
    {
        return value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
