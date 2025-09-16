using System.Data;
using API.Data;
using Microsoft.EntityFrameworkCore;

namespace API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var configuration = builder.Configuration;

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Db + EF + Dapper
            // Force localhost host when running API outside Docker, ignoring any mistaken override to 'postgres'
            var configured = configuration.GetConnectionString("DefaultConnection");
            var connString = FixHost(configured ?? "Host=localhost;Port=5432;Database=efvsdapper_db;Username=postgres;Password=postgres");
            Console.WriteLine($"Using connection string: {connString}");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connString));
            builder.Services.AddScoped<IDbConnection>(_ => new Npgsql.NpgsqlConnection(connString));

            var app = builder.Build();

            // Apply migrations / create & seed
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // For demo we ensure created (could use migrations if added later)
                await db.Database.EnsureCreatedAsync();
                await Data.SeedData.SeedAsync(db);
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            await app.RunAsync();
        }

        private static string FixHost(string cs)
        {
            // If user accidentally set Host=postgres but API runs on host machine, replace with localhost.
            if (cs.Contains("Host=postgres" , StringComparison.OrdinalIgnoreCase))
            {
                return cs.Replace("Host=postgres", "Host=localhost", StringComparison.OrdinalIgnoreCase);
            }
            return cs;
        }
    }
}
