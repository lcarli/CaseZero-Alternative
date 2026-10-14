namespace CaseZero.Progression;

public interface IRuleCondition<T> where T : IRuleCondition<T>
{
    string Type { get; }
    string? Operator { get; }
    IReadOnlyList<T>? Children { get; }
}

public static class RuleConditions
{
    public static bool Satisfied<T>(T condition, Func<T, bool> leafMatches, int depth = 0)
        where T : IRuleCondition<T>
    {
        if (depth > 16) throw new InvalidDataException("Rule condition nesting exceeds 16 levels.");
        if (condition.Type != "multiple_conditions") return leafMatches(condition);
        var children = condition.Children;
        if (children is null || children.Count == 0)
            throw new InvalidDataException("A compound rule requires conditions.");
        return condition.Operator?.ToUpperInvariant() switch
        {
            "AND" => children.All(c => Satisfied(c, leafMatches, depth + 1)),
            "OR" => children.Any(c => Satisfied(c, leafMatches, depth + 1)),
            _ => throw new InvalidDataException("A compound rule requires AND or OR.")
        };
    }

    public static IEnumerable<T> Leaves<T>(T condition, int depth = 0)
        where T : IRuleCondition<T>
    {
        if (depth > 16) throw new InvalidDataException("Rule condition nesting exceeds 16 levels.");
        if (condition.Type != "multiple_conditions")
        {
            yield return condition;
            yield break;
        }
        if (condition.Children is null || condition.Children.Count == 0)
            throw new InvalidDataException("A compound rule requires conditions.");
        foreach (var child in condition.Children)
            foreach (var leaf in Leaves(child, depth + 1))
                yield return leaf;
    }
}
