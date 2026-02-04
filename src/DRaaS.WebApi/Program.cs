using DRaaS.CoreLib.Extensions;
using DRaaS.CoreLib.Providers.Impl;
using DRaaS.CoreLib.Providers;
using DRaaS.CoreLib.Services;
using DRaaS.CoreLib.Services.Impl;

namespace DRaaS.WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDraas()
                        .AddInMemoryInstanceStore()
                        .AddDockerPlatform()
                        .AddBaremetalPlatform();

        // API Services
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
