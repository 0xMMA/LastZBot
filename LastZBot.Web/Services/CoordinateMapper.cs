namespace LastZBot.Web.Services;

/// <summary>
/// Maps coordinates from the displayed image to device coordinates.
/// </summary>
public static class CoordinateMapper
{
    /// <summary>
    /// Maps a click on the displayed image to device coordinates.
    /// </summary>
    /// <param name="clickX">X coordinate relative to the image element (e.g. from MouseEventArgs.OffsetX).</param>
    /// <param name="clickY">Y coordinate relative to the image element (e.g. from MouseEventArgs.OffsetY).</param>
    /// <param name="displayWidth">Rendered width of the image element.</param>
    /// <param name="displayHeight">Rendered height of the image element.</param>
    /// <param name="deviceWidth">Android framebuffer width.</param>
    /// <param name="deviceHeight">Android framebuffer height.</param>
    /// <returns>Device coordinates (x, y), clamped to valid range.</returns>
    public static (int X, int Y) DisplayToDevice(
        double clickX, double clickY,
        double displayWidth, double displayHeight,
        int deviceWidth, int deviceHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return (0, 0);
        }

        var deviceX = (int)((clickX / displayWidth) * deviceWidth);
        var deviceY = (int)((clickY / displayHeight) * deviceHeight);

        deviceX = Math.Clamp(deviceX, 0, deviceWidth - 1);
        deviceY = Math.Clamp(deviceY, 0, deviceHeight - 1);

        return (deviceX, deviceY);
    }
}
