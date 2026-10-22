using Arco.Api.Infrastructure;

// CLI mode: `dotnet run --project src/Arco.Api -- hash-password <password>`
if (args is ["hash-password", ..])
{
    if (args.Length < 2 || string.IsNullOrEmpty(args[1]))
    {
        Console.Error.WriteLine("Usage: hash-password <password>");
        return 1;
    }
    Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(args[1], workFactor: 12));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddArco();

var app = builder.Build();
app.ApplyMigrations();
app.UseArco();
app.MapArco();
app.Run();
return 0;
