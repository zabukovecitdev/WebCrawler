using System.Data;
using Dapper;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Extensions;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class UrlFetchRepository(QueryFactory queryFactory) : IUrlFetchRepository
{
    public async Task<UrlFetch?> GetById(int id, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), id)
            .FirstOrDefaultAsync<UrlFetch>(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<UrlFetch>> GetAll(CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.UrlFetches)
            .GetAsync<UrlFetch>(cancellationToken: cancellationToken);
    }

    public async Task<int> Insert(UrlFetch entity, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.UrlFetches)
            .InsertGetIdAsync<int>(new
            {
                entity.DiscoveredUrlId,
                FetchedAt = entity.FetchedAt.ToUniversalTime(),
                entity.StatusCode,
                entity.ContentType,
                entity.ContentLength,
                entity.ResponseTimeMs,
                entity.ObjectName,
                ParsedAt = entity.ParsedAt?.ToUniversalTime()
            }, cancellationToken: cancellationToken);
    }

    public async Task<bool> Update(UrlFetch entity, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), entity.Id)
            .UpdateAsync(new
            {
                entity.DiscoveredUrlId,
                FetchedAt = entity.FetchedAt.ToUniversalTime(),
                entity.StatusCode,
                entity.ContentType,
                entity.ContentLength,
                entity.ResponseTimeMs,
                entity.ObjectName,
                ParsedAt = entity.ParsedAt?.ToUniversalTime()
            }, cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), id)
            .DeleteAsync(cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<IEnumerable<UrlFetch>> GetUnparsedHtmlFetches(int limit, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (transaction?.Connection == null)
        {
            throw new InvalidOperationException("Transaction and connection must be provided for FOR UPDATE SKIP LOCKED");
        }

        var query = queryFactory.Query(TableNames.Database.UrlFetches)
            .WhereNull(nameof(UrlFetch.ParsedAt))
            .WhereNotNull(nameof(UrlFetch.ObjectName))
            .Where(q => q
                .Where(nameof(UrlFetch.ContentType), "text/html")
                .OrWhereLike(nameof(UrlFetch.ContentType), "text/html%"))
            .OrderBy(nameof(UrlFetch.FetchedAt))
            .Limit(limit)
            .ForUpdateSkipLocked();

        var sqlResult = queryFactory.Compiler.Compile(query);

        var command = new CommandDefinition(
            sqlResult.Sql,
            sqlResult.NamedBindings,
            transaction,
            cancellationToken: cancellationToken);

        return await transaction.Connection.QueryAsync<UrlFetch>(command);
    }
    
    public async Task<bool> MarkAsParsed(int id, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), id)
            .UpdateAsync(new
            {
                ParsedAt = DateTimeOffset.UtcNow
            }, cancellationToken: cancellationToken);

        return affected > 0;
    }
}