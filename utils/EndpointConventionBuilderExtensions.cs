using Microsoft.AspNetCore.Builder;

namespace PoliceBackend.Utils;

public static class EndpointConventionBuilderExtensions
{
    public static TBuilder ApplyOptionalAuthorization<TBuilder>(
        this TBuilder builder,
        bool bypassAuthorization,
        string policy)
        where TBuilder : IEndpointConventionBuilder
    {
        if (!bypassAuthorization)
        {
            builder.RequireAuthorization(policy);
        }

        return builder;
    }
}
