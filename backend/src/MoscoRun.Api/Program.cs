using MoscoRun.Api.Commands;
using MoscoRun.Api.Hosting;

// Comandi operativi usati dal container (nessuna shell nell'immagine chiseled).
if (args is ["healthcheck", ..])
{
    return await HealthcheckCommand.RunAsync(args);
}

if (args is ["generate-data-protection-certificate"])
{
    return DataProtectionCertificateCommand.Run(Console.Out);
}

var builder = WebApplication.CreateBuilder(args);
builder.AddMoscoRun();

var app = builder.Build();

if (args is ["migrate", ..])
{
    return await MigrateCommand.RunAsync(app.Services, args);
}

app.UseMoscoRun();
await app.RunAsync();
return 0;
