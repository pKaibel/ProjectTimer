namespace ProjectTimer.ViewModels;

internal static class QueryValue
{
    public static int GetInt(IDictionary<string, object> query, string key)
    {
        if (!query.TryGetValue(key, out var value))
        {
            return 0;
        }

        return value switch
        {
            int number => number,
            long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => 0
        };
    }
}
