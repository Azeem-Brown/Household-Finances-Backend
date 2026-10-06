using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HouseholdFinances.Api.Conventions;

/// <summary>
/// Applies the versioned route prefix (for example <c>api/v1</c>) to every MVC controller route.
/// Controllers therefore only declare their own resource segment, for example
/// <c>[Route("households")]</c>, and the prefix is applied consistently.
/// </summary>
/// <remarks>
/// Prefixing is idempotent: a route that already starts with the prefix is left unchanged, so a
/// controller that declares <c>[Route("api/v1/households")]</c> produces the same URL as one that
/// declares <c>[Route("households")]</c>. This keeps a route that is correct under the convention
/// from being double-prefixed to <c>api/v1/api/v1/...</c>.
/// </remarks>
public sealed class ApiRoutePrefixConvention : IApplicationModelConvention
{
    /// <summary>The versioned prefix applied to controller routes.</summary>
    public const string DefaultPrefix = "api/v1";

    private readonly string _prefixText;
    private readonly AttributeRouteModel _prefix;

    /// <summary>Creates the convention for the supplied route prefix.</summary>
    /// <param name="prefix">The prefix to apply, for example <c>api/v1</c>.</param>
    public ApiRoutePrefixConvention(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        _prefixText = prefix.Trim().Trim('/');
        _prefix = new AttributeRouteModel(new RouteAttribute(_prefixText));
    }

    /// <inheritdoc />
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (var controller in application.Controllers)
        {
            var routeSelectors = controller.Selectors
                .Where(selector => selector.AttributeRouteModel is not null)
                .ToList();

            // A controller with no attribute route gets the prefix as its route.
            if (routeSelectors.Count == 0)
            {
                controller.Selectors.Add(new SelectorModel { AttributeRouteModel = _prefix });
                continue;
            }

            foreach (var selector in routeSelectors)
            {
                if (HasPrefix(selector.AttributeRouteModel!.Template))
                {
                    continue;
                }

                selector.AttributeRouteModel =
                    AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }

    private bool HasPrefix(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var normalized = template.TrimStart('~', '/');

        return normalized.Equals(_prefixText, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{_prefixText}/", StringComparison.OrdinalIgnoreCase);
    }
}
