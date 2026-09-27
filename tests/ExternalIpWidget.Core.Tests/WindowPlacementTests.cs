using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class WindowPlacementTests
{
    private static readonly PixelRect Desktop = new(0, 0, 1920, 1040);

    [Fact]
    public void Keeps_a_window_that_is_already_on_the_desktop()
    {
        var window = new PixelRect(100, 80, 460, 360);

        var placed = WindowPlacement.MoveIntoView(window, [Desktop]);

        Assert.Equal(window, placed);
    }

    [Fact]
    public void Centers_a_window_parked_outside_the_screen()
    {
        var parked = new PixelRect(-32000, -32000, 460, 360);

        var placed = WindowPlacement.MoveIntoView(parked, [Desktop]);

        Assert.Equal((1920 - 460) / 2, placed.X);
        Assert.Equal((1040 - 360) / 2, placed.Y);
    }

    [Fact]
    public void Centers_a_window_when_only_a_sliver_is_visible()
    {
        var sliver = new PixelRect(1900, 100, 460, 360);

        var placed = WindowPlacement.MoveIntoView(sliver, [Desktop]);

        Assert.Equal((1920 - 460) / 2, placed.X);
        Assert.False(WindowPlacement.HasUsefulOverlap(sliver, Desktop));
    }

    [Fact]
    public void Prefers_the_first_work_area_when_the_window_is_off_screen()
    {
        var secondary = new PixelRect(1920, 0, 1280, 800);
        var parked = new PixelRect(-32000, -32000, 400, 300);

        var placed = WindowPlacement.MoveIntoView(parked, [secondary, Desktop]);

        Assert.True(placed.X >= secondary.X);
        Assert.True(placed.Right <= secondary.Right);
    }

    [Fact]
    public void Fits_a_window_that_is_larger_than_the_work_area()
    {
        var huge = new PixelRect(-20000, -20000, 4000, 3000);

        var placed = WindowPlacement.MoveIntoView(huge, [Desktop]);

        Assert.Equal(new PixelRect(0, 0, Desktop.Width, Desktop.Height), placed);
    }
}
