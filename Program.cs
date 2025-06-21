
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

namespace WebApplication1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);             

            // Configurazione Kestrel - LA MIGLIORE per produzione
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // === SICUREZZA ===
                serverOptions.AddServerHeader = false; // Nasconde versione server

                // === PERFORMANCE E LIMITI ===
                serverOptions.Limits.MaxConcurrentConnections = Environment.ProcessorCount * 50;
                serverOptions.Limits.MaxConcurrentUpgradedConnections = Environment.ProcessorCount * 20;
                serverOptions.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30MB
                serverOptions.Limits.MaxRequestHeaderCount = 100;
                serverOptions.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB
                serverOptions.Limits.MaxRequestLineSize = 8 * 1024; // 8KB
                serverOptions.Limits.MaxResponseBufferSize = 64 * 1024; // 64KB

                // === TIMEOUT OTTIMIZZATI ===
                serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(130);
                serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

                // === RATE LIMITING ===
                serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(
                    bytesPerSecond: 240, gracePeriod: TimeSpan.FromSeconds(5));
                serverOptions.Limits.MinResponseDataRate = new MinDataRate(
                    bytesPerSecond: 240, gracePeriod: TimeSpan.FromSeconds(5));

                // === HTTP/2 OTTIMIZZATO ===
                serverOptions.Limits.Http2.MaxStreamsPerConnection = 100;
                serverOptions.Limits.Http2.HeaderTableSize = 4096;
                serverOptions.Limits.Http2.MaxFrameSize = 16384;
                serverOptions.Limits.Http2.InitialConnectionWindowSize = 131072; // 128KB
                serverOptions.Limits.Http2.InitialStreamWindowSize = 98304; // 96KB

                // === ENDPOINT CONFIGURATION ===
                serverOptions.Listen(IPAddress.Any, 5000); // HTTP per health checks
                serverOptions.Listen(IPAddress.Any, 5001, listenOptions =>
                {
                    listenOptions.UseHttps(); // HTTPS principale
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                });

                // Disabilita operazioni sincrone per massime performance
                serverOptions.AllowSynchronousIO = false;
            });

            // === FORWARDED HEADERS (per reverse proxy) ===
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                          ForwardedHeaders.XForwardedProto |
                                          ForwardedHeaders.XForwardedHost;
                options.RequireHeaderSymmetry = false;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // === COMPRESSIONE RESPONSE ===
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
