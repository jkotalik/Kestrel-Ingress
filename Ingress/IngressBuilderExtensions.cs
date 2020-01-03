namespace Microsoft.AspNetCore.Builder
{
    public static class IngressBuilderExtensions
    {
        public static IApplicationBuilder UseProxyEndpoints(this IApplicationBuilder builder)
        {
            return builder;
        }
    }
}