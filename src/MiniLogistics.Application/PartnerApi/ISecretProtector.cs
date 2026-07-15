namespace MiniLogistics.Application.PartnerApi;

public interface ISecretProtector
{
    string Protect(string plaintextSecret);

    string Unprotect(string protectedSecret);

    bool IsProtected(string value);
}
