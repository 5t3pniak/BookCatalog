using System.Text.Json;
using BookCatalog.Api;
using BookCatalog.Api.Extensions;
using BookCatalog.ApplicationCore.DependencyInjection;
using BookCatalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();
    builder.AddObservability();

    builder.Services.AddOpenApi();
    builder.Services.AddPooledDbContextFactory<BookCatalogDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString(nameof(BookCatalogDbContext))));
    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddApplicationCore(builder.Configuration);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    using (var scope = app.Services.CreateScope())
        scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>().Database.EnsureCreated();

    app.UseApplicationCore();
    app.UseStructuredRequestLogging();
    app.UseExceptionHandler();
    app.UseHttpsRedirection();
    app.UseRouting();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "BookCatalog.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
