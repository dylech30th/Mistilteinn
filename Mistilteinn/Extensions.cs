namespace Mistilteinn;

public static class EnumerableExtensions
{
    // enumeration support for ranges
    public static IEnumerator<int> GetEnumerator(this Range range)
    {
        for (var i = range.Start.Value; i < range.End.Value; i++)
        {
            yield return i;
        }
    }
}

public static class Functions
{
    public static T Identity<T>(T t) => t;

    public static T Block<T>(Func<T> block) => block();
}