using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class TaskbarLabelStyleTests
{
    [Fact]
    public void Picks_a_larger_font_than_the_old_single_line_cap()
    {
        var px = TaskbarLabelStyle.FontPx(48, line => line + 6);

        Assert.Equal(20, px);
        Assert.True(px > 16);
    }

    [Fact]
    public void Caps_the_font_on_a_tall_bar()
    {
        Assert.Equal(TaskbarLabelStyle.MaximumFontPx, TaskbarLabelStyle.FontPx(120, line => line));
    }

    [Fact]
    public void Keeps_the_previous_font_when_even_the_minimum_does_not_fit()
    {
        var px = TaskbarLabelStyle.FontPx(48, line => line + 40);

        Assert.Equal(TaskbarLabelStyle.MinimumFontPx, px);
    }

    [Fact]
    public void Picks_the_font_from_the_bar_when_the_size_is_auto()
    {
        var px = TaskbarLabelStyle.FontPx(48, TaskbarLabelStyle.AutoFontPx, line => line + 6);

        Assert.Equal(TaskbarLabelStyle.FontPx(48, line => line + 6), px);
    }

    [Fact]
    public void Uses_the_chosen_size_regardless_of_the_bar()
    {
        Assert.Equal(34, TaskbarLabelStyle.FontPx(48, 34, line => line + 40));
        Assert.Equal(12, TaskbarLabelStyle.FontPx(120, 12, line => line));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(3, TaskbarLabelStyle.MinimumCustomFontPx)]
    [InlineData(18, 18)]
    [InlineData(500, TaskbarLabelStyle.MaximumCustomFontPx)]
    public void Normalizes_the_saved_font_size(int saved, int expected)
    {
        Assert.Equal(expected, TaskbarLabelStyle.NormalizeCustomFontPx(saved));
    }
}
