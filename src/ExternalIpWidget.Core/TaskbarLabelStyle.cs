namespace ExternalIpWidget.Core;

/// <summary>
/// Крупный шрифт для двух строк адреса. Блок выше тонкой панели задач, чтобы цифры стали больше.
/// </summary>
public static class TaskbarLabelStyle
{
    public const int PreferredBlockPx = 60;
    public const int MinimumFontPx = 16;
    public const int MaximumFontPx = 26;

    public static int BlockHeight(int barThickness)
    {
        if (barThickness < 16)
            return 16;
        return Math.Max(barThickness - 2, PreferredBlockPx);
    }

    public static int FontPx(int barThickness, Func<int, int> lineHeight)
    {
        var target = BlockHeight(barThickness) - 6;
        for (var px = MaximumFontPx; px > MinimumFontPx; px--)
        {
            var height = Math.Max(1, lineHeight(px));
            if (height * 2 + 1 <= target)
                return px;
        }

        return MinimumFontPx;
    }
}
