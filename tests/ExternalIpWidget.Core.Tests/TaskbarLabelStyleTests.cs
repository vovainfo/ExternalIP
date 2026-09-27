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
}
