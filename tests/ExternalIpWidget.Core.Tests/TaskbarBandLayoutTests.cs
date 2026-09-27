using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class TaskbarBandLayoutTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Places_the_band_on_a_bottom_taskbar_just_left_of_the_tray()
    {
        var taskbar = new PixelRect(0, 1032, 1920, 48);
        var tray = new PixelRect(1760, 1032, 160, 48);

        var placed = TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, textWidth: 130, textHeight: 28, nudge: 0, out var bounds);

        Assert.True(placed);
        Assert.Equal(1626, bounds.X);
        Assert.Equal(130, bounds.Width);
        Assert.Equal(28, bounds.Height);
        Assert.True(bounds.Y > taskbar.Y && bounds.Bottom < taskbar.Bottom);
        Assert.True(bounds.Right <= tray.X);
    }

    [Fact]
    public void Nudge_moves_the_band_but_does_not_cover_the_start_button()
    {
        var taskbar = new PixelRect(0, 1032, 1920, 48);
        var tray = new PixelRect(1760, 1032, 160, 48);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, 130, 28, nudge: -40, out var nudged));
        Assert.Equal(1586, nudged.X);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, 130, 28, nudge: -5000, out var clamped));
        Assert.Equal(48, clamped.X);
    }

    [Fact]
    public void Places_the_band_to_the_right_of_a_left_side_tray()
    {
        var taskbar = new PixelRect(0, 0, 1920, 48);
        var tray = new PixelRect(0, 0, 160, 48);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, 100, 24, 0, out var bounds));

        Assert.True(bounds.X >= tray.Right);
        Assert.True(bounds.Bottom <= taskbar.Bottom);
    }

    [Fact]
    public void Hides_the_band_when_the_taskbar_is_collapsed()
    {
        var taskbar = new PixelRect(0, 1078, 1920, 2);

        Assert.False(TaskbarBandLayout.TryGetBounds(taskbar, null, Monitor, 100, 24, 0, out _));
    }

    [Fact]
    public void Attaches_a_readable_label_to_a_left_vertical_taskbar()
    {
        var taskbar = new PixelRect(0, 0, 48, 1080);
        var tray = new PixelRect(0, 980, 48, 100);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, 140, 24, 0, out var bounds));

        Assert.True(bounds.X >= taskbar.Right - 4);
        Assert.Equal(140, bounds.Width);
        Assert.True(bounds.Bottom <= tray.Y);
    }

    [Fact]
    public void Grows_a_two_line_label_inward_from_a_bottom_taskbar()
    {
        var taskbar = new PixelRect(0, 1032, 1920, 48);
        var tray = new PixelRect(1760, 1032, 160, 48);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, tray, Monitor, textWidth: 78, textHeight: 60, nudge: 0, out var bounds));

        Assert.Equal(78, bounds.Width);
        Assert.Equal(60, bounds.Height);
        Assert.Equal(1080, bounds.Bottom);
        Assert.True(bounds.Y < taskbar.Y);
        Assert.True(bounds.Right <= tray.X);
    }

    [Fact]
    public void Grows_a_two_line_label_inward_from_a_top_taskbar()
    {
        var taskbar = new PixelRect(0, 0, 1920, 48);

        Assert.True(TaskbarBandLayout.TryGetBounds(taskbar, null, Monitor, 78, 60, 0, out var bounds));

        Assert.Equal(0, bounds.Y);
        Assert.Equal(60, bounds.Height);
    }
}
