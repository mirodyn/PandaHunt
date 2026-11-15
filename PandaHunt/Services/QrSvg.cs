using System.Globalization;
using System.Text;
using QRCoder;

namespace PandaHunt.Services;

/// <summary>
/// Draws a QR code as a printable SVG: black modules on white with the green bamboo (the points icon) in the
/// middle. The highest error correction (H, ~30 %) lets phones read it even though the bamboo covers the centre.
/// </summary>
public static class QrSvg
{
    // The QR's quiet zone (white border) in modules; QRCoder adds it to the matrix.
    private const int QuietZone = 4;

    // Part of the QR's width cleared for the bamboo. Well below what level H can repair.
    private const double LogoShare = 0.24;

    // Same drawing as Components/Shared/Bamboo.razor (viewBox 7.8 0.5 15.5 23).
    private const string Bamboo =
        """
        <path d="M14.4 7.9C16 4.7 18.9 3 22.8 2.6 21.6 6.1 18.8 7.9 14.4 7.9Z" fill="#66bb6a"/>
        <path d="M14.8 7.6C17.2 5.9 19.6 4.4 22 3.4" stroke="#388e3c" stroke-width="0.7" stroke-linecap="round" fill="none"/>
        <rect x="9" y="1" width="5.6" height="6.6" rx="1.5" fill="#4caf50"/>
        <rect x="9" y="8.7" width="5.6" height="6.6" rx="1.5" fill="#4caf50"/>
        <rect x="9" y="16.4" width="5.6" height="6.6" rx="1.5" fill="#4caf50"/>
        <rect x="8.3" y="7" width="7" height="2.1" rx="1.05" fill="#2e7d32"/>
        <rect x="8.3" y="14.7" width="7" height="2.1" rx="1.05" fill="#2e7d32"/>
        <rect x="10.2" y="2.3" width="1.2" height="4.2" rx="0.6" fill="#a5d6a7"/>
        <rect x="10.2" y="10" width="1.2" height="4.2" rx="0.6" fill="#a5d6a7"/>
        <rect x="10.2" y="17.7" width="1.2" height="4.2" rx="0.6" fill="#a5d6a7"/>
        """;

    public static string Render(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.H);
        var matrix = data.ModuleMatrix;
        var size = matrix.Count; // includes the quiet zone on every side

        // Odd size for an odd matrix, so the cleared square sits exactly in the middle.
        var logo = (int)Math.Round((size - 2 * QuietZone) * LogoShare);
        if (logo % 2 == 0)
            logo++;
        var logoFrom = (size - logo) / 2;
        var logoTo = logoFrom + logo;
        bool InLogo(int x, int y) => x >= logoFrom && x < logoTo && y >= logoFrom && y < logoTo;

        // One path; each run of dark modules in a row is one rectangle.
        var path = new StringBuilder();
        for (var y = 0; y < size; y++)
        {
            var row = matrix[y];
            for (var x = 0; x < size; x++)
            {
                if (!row[x] || InLogo(x, y))
                    continue;
                var start = x;
                while (x + 1 < size && row[x + 1] && !InLogo(x + 1, y))
                    x++;
                path.Append(CultureInfo.InvariantCulture, $"M{start} {y}h{x - start + 1}v1H{start}z");
            }
        }

        // The bamboo, 90 % of the cleared square's height, centred.
        var height = logo * 0.9;
        var width = height * 15.5 / 23;
        var bambooX = (size - width) / 2;
        var bambooY = (size - height) / 2;

        return string.Create(CultureInfo.InvariantCulture,
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}" shape-rendering="crispEdges">
            <rect width="{size}" height="{size}" fill="#fff"/>
            <path d="{path}" fill="#000"/>
            <svg x="{bambooX:0.###}" y="{bambooY:0.###}" width="{width:0.###}" height="{height:0.###}" viewBox="7.8 0.5 15.5 23" shape-rendering="geometricPrecision">
            {Bamboo}
            </svg>
            </svg>
            """);
    }
}
