using BookCatalog.Integrations.OpenBooks.HttpClient;
using BookCatalog.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace BookCatalog.Api.Tests.Infrastructure;

public sealed class BookCatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connStr;

    public BookCatalogApiFactory(string connStr) => _connStr = connStr;
    
    public BookCatalogDbContext OpenDbContext() =>
        new(new DbContextOptionsBuilder<BookCatalogDbContext>()
            .UseSqlServer(_connStr)
            .Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection([
                new("ConnectionStrings:BookCatalogDbContext", _connStr)
            ]));

        builder.ConfigureServices(services =>
        {
            foreach (var d in services
                         .Where(s => s.ServiceType == typeof(IHostedService))
                         .ToList())
                services.Remove(d);
            
            var existing = services.FirstOrDefault(
                d => d.ServiceType == typeof(IOpenBooksClient));
            if (existing != null) services.Remove(existing);

            var stub = Substitute.For<IOpenBooksClient>();
            stub.GetBooksAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<
                    BookCatalog.Integrations.OpenBooks.Contract.RemoteBookListItem>>([]));
            services.AddSingleton(stub);
        });

        builder.UseEnvironment("Testing");
    }
}
