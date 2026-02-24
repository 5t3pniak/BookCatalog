using System.Net;
using System.Net.Http.Headers;
using BookCatalog.Integrations.OpenBooks.HttpClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookCatalog.Integrations.OpenBooks;

public static class OpenBooksExtensions
{
   public static IServiceCollection AddOpenBooksIntegration(
      this IServiceCollection services,
      IConfiguration configuration)
   {
      services.Configure<OpenBooksOptions>(configuration.GetSection(OpenBooksOptions.SectionName));

      services.AddHttpClient<IOpenBooksClient, OpenBooksClient>()
         .ConfigureHttpClient((sp, http) =>
         {
            var options = sp.GetRequiredService<IOptions<OpenBooksOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
         })
         .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
         {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
         })
         .AddStandardResilienceHandler();

      return services;
   }
}
