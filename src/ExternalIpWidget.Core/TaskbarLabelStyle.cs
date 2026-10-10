namespace ExternalIpWidget.Core;

/// <summary>
/// Крупный шрифт для двух строк адреса. Блок выше тонкой панели задач, чтобы цифры стали больше.
/// Размер из настроек, если он задан, заменяет подобранный автоматически.
/// </summary>
public static class TaskbarLabelStyle
{
    public const int PreferredBlockPx = 60;
    public const int MinimumFontPx = 16;
    public const int MaximumFontPx = 26;
    public const int AutoFontPx = 0;
    public const int MinimumCustomFontPx = 10;
    public const int MaximumCustomFontPx = 40;

    /// <summary>0 и отрицательные значения означают «Авто», остальные прижимаются к допустимому диапазону.</summary>
    public static int NormalizeCustomFontPx(int fontPx)
    {
        if (fontPx <= AutoFontPx)
            return AutoFontPx;
        return Math.Clamp(fontPx, MinimumCustomFontPx, MaximumCustomFontPx);
    }

    public static int FontPx(int barThickness, int customFontPx, Func<int, int> lineHeight)
    {
        var custom = NormalizeCustomFontPx(customFontPx);
        return custom == AutoFontPx ? FontPx(barThickness, lineHeight) : custom;
    }

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
