namespace dotnet_server.CustomCode;

public static class CustomCodeExtensions
{
    public static IServiceCollection AddCustomCode(this IServiceCollection services)
    {
        services.AddSingleton<BranchLocations>();
        services.AddSingleton<WestbankProjectPasswords>();
        return services;
    }

    public static IEndpointRouteBuilder MapCustomCode(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/locations", async (
            BranchLocations locations,
            CancellationToken cancellationToken) =>
                Results.Ok(await locations.GetAllAsync(cancellationToken)));

        return endpoints;
    }
}
