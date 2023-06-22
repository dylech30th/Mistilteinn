namespace Mistilteinn;

public static class Enumerates
{
    // enumeration support for ranges
    public static IEnumerator<int> GetEnumerator(this Range range)
    {
        for (var i = range.Start.Value; i < range.End.Value; i++)
        {
            yield return i;
        }
    }
    
    public static IEnumerable<int> Range(int start, int stop, int step = 1)
    {
        if (step == 0)
            throw new ArgumentException(nameof(step));

        return RangeIterator(start, stop, step);
    }
    
    private static IEnumerable<int> RangeIterator(int start, int stop, int step)
    {
        var x = start;

        do
        {
            yield return x;
            x += step;
            if (step < 0 && x <= stop || 0 < step && stop <= x)
                break;
        } 
        while (true);
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var t in enumerable) action(t);
    }
}

public static class Functions
{
    public static T Identity<T>(T t) => t;

    public static T Block<T>(Func<T> block) => block();
}