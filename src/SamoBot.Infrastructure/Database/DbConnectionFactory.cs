using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Database;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseOptions _options;

    public DbConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_options.ConnectionString);
    }
}
