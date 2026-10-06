using HouseholdFinances.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies that the concrete Google token validator is selected by environment and that required
/// secrets are enforced outside Development. These are configuration-level checks, so they do not
/// start a host or contact the identity provider.
/// </summary>
public class AuthenticationRegistrationTests
{
    private const string SigningKey = "registration-test-signing-key-that-is-long-enough-1234567890";

    [Fact]
    public void Development_RegistersTheDevelopmentValidator()
    {
        var validator = ResolveValidator(Environments.Development, CompleteConfiguration());

        Assert.IsType<DevelopmentGoogleTokenValidator>(validator);
    }

    [Fact]
    public void Production_RegistersTheRealValidator()
    {
        var validator = ResolveValidator(Environments.Production, CompleteConfiguration());

        Assert.IsType<GoogleIdentityTokenValidator>(validator);
    }

    [Fact]
    public void Development_WithoutSigningKeyOrClientId_StillStarts()
    {
        // A developer with no Google credentials must still be able to run the API and use dev tokens.
        var validator = ResolveValidator(
            Environments.Development,
            new Dictionary<string, string?>());

        Assert.IsType<DevelopmentGoogleTokenValidator>(validator);
    }

    [Fact]
    public void Production_WithoutSigningKey_Throws()
    {
        var configuration = CompleteConfiguration();
        configuration.Remove("Authentication:Jwt:SigningKey");

        Assert.Throws<InvalidOperationException>(
            () => ResolveValidator(Environments.Production, configuration));
    }

    [Fact]
    public void Production_WithoutGoogleClientId_Throws()
    {
        var configuration = CompleteConfiguration();
        configuration.Remove("Authentication:Google:ClientId");

        Assert.Throws<InvalidOperationException>(
            () => ResolveValidator(Environments.Production, configuration));
    }

    [Fact]
    public void DevelopmentGoogleTokenValidator_CannotBeConstructedOutsideDevelopment()
    {
        var inner = new GoogleIdentityTokenValidator(new GoogleIdentityOptions());

        Assert.Throws<InvalidOperationException>(
            () => new DevelopmentGoogleTokenValidator(inner, new TestHostEnvironment(Environments.Production)));
    }

    private static IGoogleTokenValidator ResolveValidator(
        string environmentName,
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var environment = new TestHostEnvironment(environmentName);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddHouseholdFinancesAuthentication(configuration, environment);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IGoogleTokenValidator>();
    }

    private static Dictionary<string, string?> CompleteConfiguration() => new()
    {
        ["Authentication:Jwt:SigningKey"] = SigningKey,
        ["Authentication:Jwt:Issuer"] = "HouseholdFinances.Tests",
        ["Authentication:Jwt:Audience"] = "HouseholdFinances.Tests.Api",
        ["Authentication:Jwt:RefreshAudience"] = "HouseholdFinances.Tests.Api.Refresh",
        ["Authentication:Google:ClientId"] = "test-client-id.apps.googleusercontent.com",
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "HouseholdFinances.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
