using System.Data;

namespace SamoBot.Infrastructure.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
