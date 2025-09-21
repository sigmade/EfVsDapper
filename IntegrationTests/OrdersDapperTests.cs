using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Data;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

public class OrdersDapperTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pgContainer;
    private readonly ITestOutputHelper _output;
    private WebApplicationFactory<API.Program> _factory = default!;
    private HttpClient _client = default!;
    private string _connectionString = default!;

    public OrdersDapperTests(ITestOutputHelper output)
    {
        _output = output;
        _pgContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("efvsdapper_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithPortBinding(0, 5432)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _output.WriteLine("Starting PostgreSQL container...");
            await _pgContainer.StartAsync();
            
            _connectionString = _pgContainer.GetConnectionString();
            _output.WriteLine($"Container started. Connection string: {_connectionString}");

            // ?????????????? ???????? ?????????? ???? ??????
            await WaitForDatabaseReady(_connectionString);

            // ?????????????? ???? ?????? ????????, ??? WebApplicationFactory
            await InitializeDatabase();

            _factory = new WebApplicationFactory<API.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.Sources.Clear(); // ??????? ??? ????????? ????????????
                        config.AddInMemoryCollection(new[]
                        {
                            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", _connectionString),
                            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Testing")
                        });
                    });

                    // ??????????????? ??????? ??? ????????????? ?????????? ?????? ???????????
                    builder.ConfigureServices(services =>
                    {
                        // ??????? ???????????? ??????????? DbContext
                        var dbContextDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                        if (dbContextDescriptor != null)
                        {
                            services.Remove(dbContextDescriptor);
                        }

                        var appDbContextDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(AppDbContext));
                        if (appDbContextDescriptor != null)
                        {
                            services.Remove(appDbContextDescriptor);
                        }

                        var dbConnectionDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(IDbConnection));
                        if (dbConnectionDescriptor != null)
                        {
                            services.Remove(dbConnectionDescriptor);
                        }

                        // ????????? ????? ??????????? ? ?????????? ??????? ???????????
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseNpgsql(_connectionString));
                        services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(_connectionString));
                    });
                    
                    builder.UseSetting("ASPNETCORE_URLS", "http://localhost");
                });

            // ??????? ??????
            _client = _factory.CreateClient();
            
            _output.WriteLine("Test initialization completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error during initialization: {ex}");
            throw;
        }
    }

    private async Task InitializeDatabase()
    {
        _output.WriteLine("Initializing database directly...");
        
        // ??????? DbContext ???????? ??? ?????????????
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);
        
        using var dbContext = new AppDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
        await SeedData.SeedAsync(dbContext);
        
        _output.WriteLine("Database initialized successfully");
    }

    private async Task WaitForDatabaseReady(string connectionString, int maxRetries = 10)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
                _output.WriteLine("Database is ready!");
                return;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Database not ready (attempt {i + 1}/{maxRetries}): {ex.Message}");
                if (i == maxRetries - 1) throw;
                await Task.Delay(2000);
            }
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
            _factory?.Dispose();
            await _pgContainer.DisposeAsync();
            _output.WriteLine("Test cleanup completed");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error during cleanup: {ex}");
        }
    }

    [Fact]
    public async Task GetTop10_Dapper_ReturnsOkAndModeDapper()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/top10?mode=dapper");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OrderListResponseContract>();
        
        Assert.NotNull(result);
        Assert.Equal("dapper", result.Mode);
        Assert.True(result.Orders.Count <= 10);
        Assert.True(result.ElapsedMs >= 0);
    }

    private record OrderListResponseContract(string Mode, List<OrderContract> Orders, long ElapsedMs);
    private record OrderContract(int Id, DateTime CreatedAt, int UserId, string UserName, decimal Total, List<OrderItemContract> Items);
    private record OrderItemContract(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice);
}
