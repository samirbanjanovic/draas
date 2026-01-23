using DRaaS.ControlPlane.Providers;
using Scalar.AspNetCore;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
namespace DRaaS.ControlPlane;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddNewtonsoftJson(); // Required for JsonPatch support


        builder.Services.AddSingleton<IDeserializer>(sp =>
                    new DeserializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                            .Build());

        builder.Services.AddSingleton(sp =>
                    new SerializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                            .Build());

        // Register configuration provider
        builder.Services.AddSingleton<IDrasiServerConfigurationProvider, DrasiServerConfigurationProvider>();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();


        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapControllers();

        app.Run();
    }
}
