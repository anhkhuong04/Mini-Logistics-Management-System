using Microsoft.AspNetCore.DataProtection;
using MiniLogistics.Infrastructure.PartnerApi;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class DataProtectionPersistenceTests
{
    [Fact]
    public void SharedKeyDirectory_AllowsAnotherReplicaToDecryptExistingSecret()
    {
        var keyDirectory = Path.Combine(
            Path.GetTempPath(),
            "MiniLogistics-DataProtectionTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyDirectory);

        try
        {
            var firstProvider = DataProtectionProvider.Create(
                new DirectoryInfo(keyDirectory),
                builder => builder.SetApplicationName("MiniLogistics-Test"));
            var firstProtector = new DataProtectionSecretProtector(firstProvider);
            var protectedSecret = firstProtector.Protect("a-secret-that-must-survive-replica-replacement");

            var secondProvider = DataProtectionProvider.Create(
                new DirectoryInfo(keyDirectory),
                builder => builder.SetApplicationName("MiniLogistics-Test"));
            var secondProtector = new DataProtectionSecretProtector(secondProvider);

            Assert.Equal(
                "a-secret-that-must-survive-replica-replacement",
                secondProtector.Unprotect(protectedSecret));
        }
        finally
        {
            if (Directory.Exists(keyDirectory)
                && keyDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(keyDirectory, recursive: true);
            }
        }
    }
}
