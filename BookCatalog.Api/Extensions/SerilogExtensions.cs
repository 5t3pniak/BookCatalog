using Serilog;

namespace BookCatalog.Api.Extensions;

internal static class SerilogExtensions
{
    internal static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }
    
    internal static WebApplication UseStructuredRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(o =>
        {
            o.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
            {
                diagCtx.Set("RequestHost",   httpCtx.Request.Host.Value);
                diagCtx.Set("RequestScheme", httpCtx.Request.Scheme);
                diagCtx.Set("UserAgent",     httpCtx.Request.Headers.UserAgent.ToString());
            };
        });

        return app;
    }
}
