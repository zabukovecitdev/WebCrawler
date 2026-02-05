using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace SamoBot.Infrastructure.Database;

/// <summary>
/// Dapper type handler so that JsonElement parameters are sent as PostgreSQL JSONB.
/// </summary>
internal sealed class DapperJsonElementTypeHandler : SqlMapper.TypeHandler<JsonElement>
{
    public override void SetValue(IDbDataParameter parameter, JsonElement value)
    {
        if (parameter is NpgsqlParameter npgsqlParam)
        {
            npgsqlParam.NpgsqlDbType = NpgsqlDbType.Jsonb;
            npgsqlParam.Value = value;
        }
        else
        {
            parameter.Value = value.GetRawText();
        }
    }

    public override JsonElement Parse(object value)
    {
        if (value is null or DBNull)
        {
            return default;
        }

        if (value is JsonElement element)
        {
            return element;
        }

        // When Npgsql returns JSONB as string, we cannot return a valid JsonElement without
        // keeping the JsonDocument alive. This handler is only used for writing; reading
        // in this app is done via dynamic + manual deserialization.
        return default;
    }
}
