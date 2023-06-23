namespace Mistilteinn;

public static class Enumerates
{
    public static T? FirstOrDefault<T>(this IEnumerable<T> enumerable, Func<T, bool> func) where T : struct
    {
        foreach (var t in enumerable)
        {
            if (func(t))
                return t;
        }

        return null;
    }
    
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

    public static IEnumerable<int> Enumerate(this Range range)
    {
        foreach (var i in range)
        {
            yield return i;
        }
    }
    
    public static bool Contains(this Range range, int value)
    {
        return range.Start.Value <= value && value < range.End.Value;
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

public static class TextExtensions
{
    public static bool TryParseChineseNumeral(char ch, out int n)
    {
        var arr = new []{'一', '二', '三', '四', '五', '六', '七', '八', '九'};
        if (Array.IndexOf(arr, ch) is not -1 and var i)
        {
            n = i + 1;
            return true;
        }

        n = 0;
        return false;
    }
    
    public static char ToChineseNumeral(this int i)
    {
        return i switch
        {
            1 => '一',
            2 => '二',
            3 => '三',
            4 => '四',
            5 => '五',
            6 => '六',
            7 => '七',
            8 => '八',
            9 => '九',
            10 => '十',
            _ => throw new ArgumentOutOfRangeException(nameof(i))
        };
    }

    public static bool TryParseMoveDirection(char ch, out MoveTowards direction)
    {
        switch (ch)
        {
            case '进':
                direction = MoveTowards.Forward;
                return true;
            case '退':
                direction = MoveTowards.Backward;
                return true;
            case '平':
                direction = MoveTowards.Horizontal;
                return true;
            default:
                direction = 0;
                return false;
        }
    }
}