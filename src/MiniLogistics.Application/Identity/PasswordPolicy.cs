namespace MiniLogistics.Application.Identity;

public static class PasswordPolicy
{
    public const int RequiredLength = 8;
    public const int RequiredUniqueChars = 4;
    public const int MaximumLength = 100;
    public const string RequirementMessage =
        "Password must be at least 8 characters and include uppercase, lowercase, number, and special character.";

    public static bool MeetsComplexity(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < RequiredLength)
        {
            return false;
        }

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsLetterOrDigit(character));
    }
}
