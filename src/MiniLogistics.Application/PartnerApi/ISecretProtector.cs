namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines the application contract for Secret Protector.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintextSecret);

    string Unprotect(string protectedSecret);

    bool IsProtected(string value);
}
