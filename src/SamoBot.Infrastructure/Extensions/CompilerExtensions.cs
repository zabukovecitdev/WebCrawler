using SqlKata;

namespace SamoBot.Infrastructure.Extensions;

public static class CompilerExtensions
{
    public static Query ForUpdateSkipLocked(this Query query)
    {
        return query.AddComponent("suffix", new RawClause("FOR UPDATE SKIP LOCKED"));
    }
}

public class RawClause : AbstractClause
{
    public string Expression { get; set; }

    public RawClause(string expression)
    {
        Expression = expression;
    }

    public override AbstractClause Clone()
    {
        return new RawClause(Expression)
        {
            Engine = Engine,
            Component = Component
        };
    }
}

