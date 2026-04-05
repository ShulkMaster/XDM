using PdfSharp.Fonts;

namespace DevelopServer;

public class SystemFontResolver : IFontResolver
{
    public string DefaultFontName => "DejaVu Sans Mono";

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var suffix = (isBold, isItalic) switch
        {
            (true, true) => "-BoldOblique",
            (true, false) => "-Bold",
            (false, true) => "-Oblique",
            _ => ""
        };

        var faceName = $"DejaVuSansMono{suffix}";
        return new FontResolverInfo(faceName);
    }

    public byte[]? GetFont(string faceName)
    {
        var path = $"/usr/share/fonts/TTF/{faceName}.ttf";
        if (File.Exists(path))
            return File.ReadAllBytes(path);

        return null;
    }
}
