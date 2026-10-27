using HouseholdFinances.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies that the concrete Google token validator is selected by environment and the development
/// bypass opt-in, that the bypass cannot be enabled outside Development, and that required secrets
/// are enforced outside Development. Configuration-level checks do not start a host; the
/// outside-Development opt-in check builds the real host so it asserts the startup guard itself.
/// </summary>
public class AuthenticationRegistrationTests
{
    private const string SigningKey = "registration-test-signing-key-that-is-long-enough-1234567890";

    [Fact]
    public void Development_WithoutOptIn_RegistersTheRealValidator()
    {
        var validator = ResolveValidator(Environments.Development, CompleteConfiguration());

        Assert.IsType<GoogleIdentityTokenValidator>(validator);
    }

    [Fact]
    public void Development_WithOptIn_RegistersTheDevelopmentValidator()
    {
        var configuration = CompleteConfiguration();
        configuration[EnabledSetting] = "true";

        var validator = ResolveValidator(Environments.Development, configuration);

        Assert.IsType<DevelopmentGoogleTokenValidator>(validator);
    }

    [Fact]
    public void Production_WithoutOptIn_RegistersTheRealValidator()
    {
        var validator = ResolveValidator(Environments.Production, CompleteConfiguration());

        Assert.IsType<GoogleIdentityTokenValidator>(validator);
    }

    [Fact]
    public void Production_WithOptIn_ThrowsAtStartup()
    {
        // The opt-in fail-fast is the startup guard in Program.cs, so this hosts the real app in
        // Production with the opt-in enabled and asserts the guard throws naming the setting. Host
        // settings are used because Program reads configuration before a factory's app-configuration
        // sources are applied.
        using var factory = new ProductionWithBypassFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(DevelopmentGoogleTokenBypassOptions.SectionName, exception.Message);
    }

    [Fact]
    public void Development_WithOptIn_LogsAWarningAtStartup()
    {
        using var factory = new DevelopmentWithBypassFactory();

        // Starting the host logs the warning before CreateClient returns; the client is not used.
        using var client = factory.CreateClient();

        Assert.Contains(
            factory.Messages,
            message => message.Contains("development Google token bypass", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Development_WithoutSigningKeyOrClientId_StillStartsWithoutTheBypass()
    {
        // A developer with no Google credentials must still be able to run the API. The bypass stays
        // off unless it is explicitly opted in, so the real validator is selected.
        var validator = ResolveValidator(
            Environments.Development,
            new Dictionary<string, string?>());

        Assert.IsType<GoogleIdentityTokenValidator>(validator);
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
            () => new DevelopmentGoogleTokenValidator(
                inner,
                new TestHostEnvironment(Environments.Production),
                new DevelopmentGoogleTokenBypassOptions { Enabled = true }));
    }

    [Fact]
    public void DevelopmentGoogleTokenValidator_CannotBeConstructedWithoutTheOptIn()
    {
        var inner = new GoogleIdentityTokenValidator(new GoogleIdentityOptions());

        Assert.Throws<InvalidOperationException>(
            () => new DevelopmentGoogleTokenValidator(
                inner,
                new TestHostEnvironment(Environments.Development),
                new DevelopmentGoogleTokenBypassOptions { Enabled = false }));
    }

    private static string EnabledSetting =>
        $"{DevelopmentGoogleTokenBypassOptions.SectionName}:Enabled";

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

    /// <summary>
    /// Hosts the real application in Production with the development bypass opted in. Host settings
    /// are used (rather than app configuration) because Program reads its configuration before a
    /// factory's app-configuration sources are applied.
    /// </summary>
    private sealed class ProductionWithBypassFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Authentication:Jwt:SigningKey", SigningKey);
            builder.UseSetting("Authentication:Jwt:Issuer", "HouseholdFinances.Tests");
            builder.UseSetting("Authentication:Jwt:Audience", "HouseholdFinances.Tests.Api");
            builder.UseSetting("Authentication:Jwt:RefreshAudience", "HouseholdFinances.Tests.Api.Refresh");
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id.apps.googleusercontent.com");
            builder.UseSetting(EnabledSetting, "true");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=household_finances;");
        }
    }

    /// <summary>Hosts the real application in Development with the development bypass opted in.</summary>
    private sealed class DevelopmentWithBypassFactory : WebApplicationFactory<Program>
    {
        private readonly RecordingLoggerProvider _loggerProvider = new();

        public IReadOnlyCollection<string> Messages => _loggerProvider.Messages;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting(EnabledSetting, "true");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=household_finances;");
            builder.ConfigureLogging(logging => logging.AddProvider(_loggerProvider));
        }
    }

    /// <summary>Captures every log message written during host startup.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = new();

        public IReadOnlyCollection<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToArray();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly List<string> _messages;

            public RecordingLogger(List<string> messages) => _messages = messages;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (_messages)
                {
                    _messages.Add(formatter(state, exception));
                }
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "HouseholdFinances.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
