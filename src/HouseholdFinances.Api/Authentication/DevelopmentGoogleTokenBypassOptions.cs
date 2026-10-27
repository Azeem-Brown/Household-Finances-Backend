namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Configuration for the development-only Google token bypass. The bypass is off by default so it
/// cannot be activated by the host environment name alone; a developer opts in explicitly, per
/// developer, through user secrets or an environment variable. The setting is never committed as
/// enabled, so a deployed host cannot inherit it from a settings file.
/// </summary>
public sealed class DevelopmentGoogleTokenBypassOptions
{
    /// <summary>The configuration section that binds these options.</summary>
    public const string SectionName = "Authentication:Google:DevelopmentBypass";

    /// <summary>
    /// Whether the development Google token bypass is enabled. Defaults to <see langword="false"/>
    /// and is only honored in the Development environment; enabling it elsewhere fails startup.
    /// </summary>
    public bool Enabled { get; set; }
}
