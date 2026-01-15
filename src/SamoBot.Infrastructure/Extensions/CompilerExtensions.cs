using SqlKata;
using SqlKata.Compilers;
using System.Reflection;

namespace SamoBot.Infrastructure.Extensions;

public static class CompilerExtensions
{
    private const string ForUpdateSkipLockedSuffix = " FOR UPDATE SKIP LOCKED";

    public static SqlResult CompileForUpdateSkipLocked(this Compiler compiler, Query query)
    {
        EnsurePostgresSelect(compiler, query);

        var res = compiler.Compile(query);

        // Get the actual SQL string - PostgreSQL with named parameters may store it in different properties
        // Try Sql first, then RawSql, then use reflection to get PlaceholderValue if it exists
        var baseSql = GetSqlString(res);
        
        if (string.IsNullOrEmpty(baseSql))
        {
            throw new InvalidOperationException("Failed to extract SQL from compiled query result");
        }

        var sql = baseSql + ForUpdateSkipLockedSuffix;
        var rawSql = (res.RawSql ?? baseSql) + ForUpdateSkipLockedSuffix;

        return new SqlResult(sql, rawSql)
        {
            Bindings = res.Bindings,
            NamedBindings = res.NamedBindings
        };
    }

    private static string GetSqlString(SqlResult result)
    {
        // First try the standard Sql property
        if (!string.IsNullOrEmpty(result.Sql))
        {
            return result.Sql;
        }

        // Then try RawSql
        if (!string.IsNullOrEmpty(result.RawSql))
        {
            return result.RawSql;
        }

        // For PostgreSQL with named parameters, the SQL might be in a property accessed via reflection
        // Try common property names that might contain the parameterized SQL
        var resultType = result.GetType();
        
        // Try PlaceholderValue property (common in some SqlKata versions)
        var placeholderValueProp = resultType.GetProperty("PlaceholderValue", BindingFlags.Public | BindingFlags.Instance);
        if (placeholderValueProp != null)
        {
            var value = placeholderValueProp.GetValue(result) as string;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        // Try Placeholder property
        var placeholderProp = resultType.GetProperty("Placeholder", BindingFlags.Public | BindingFlags.Instance);
        if (placeholderProp != null)
        {
            var value = placeholderProp.GetValue(result) as string;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        // Last resort: try ToString() which might return the SQL
        var toString = result.ToString();
        if (!string.IsNullOrEmpty(toString) && toString != resultType.FullName)
        {
            return toString;
        }

        return string.Empty;
    }

    private static void EnsurePostgresSelect(Compiler compiler, Query query)
    {
        if (compiler is not PostgresCompiler)
        {
            throw new InvalidOperationException("FOR UPDATE SKIP LOCKED requires PostgresCompiler.");
        }

        if (!string.Equals(query.Method, "select", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("FOR UPDATE is valid only for SELECT.");
        }
    }
}
