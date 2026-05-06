using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Common.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permission in GetPermissionValues())
            {
                options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        return services;
    }

    private static IReadOnlyCollection<string> GetPermissionValues()
    {
        var values = new List<string>();
        CollectValues(typeof(Permissions), values);
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectValues(Type type, ICollection<string> values)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.FieldType == typeof(string) && field.GetValue(null) is string value)
            {
                values.Add(value);
            }
        }

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            CollectValues(nestedType, values);
        }
    }
}