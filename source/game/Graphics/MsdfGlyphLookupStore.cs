using System;
using System.Threading.Tasks;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Text;

namespace Msdf.Game.Graphics;

// Routes ITexturedGlyphLookupStore.Get(fontName, character) to the MsdfFontStore for the
// family/weight encoded in fontName -- FontUsage.FontName, "{Family}-{Weight}[Italic]" (see
// FontUsage.cs) -- which is the exact string TextBuilder passes to any glyph store it's given.
// Each individual MsdfFontStore only serves its own family/weight (see MsdfFontStore.Get);
// working out which one to ask is this type's only job, via MsdfFontStoreCache.
public sealed class MsdfGlyphLookupStore : ITexturedGlyphLookupStore
{
    private readonly MsdfFontStoreCache fontStoreCache;
    private readonly IRenderer renderer;
    private readonly ShaderManager shaders;

    public MsdfGlyphLookupStore(MsdfFontStoreCache fontStoreCache, IRenderer renderer, ShaderManager shaders)
    {
        this.fontStoreCache = fontStoreCache;
        this.renderer = renderer;
        this.shaders = shaders;
    }

    public ITexturedCharacterGlyph? Get(string? fontName, char character)
    {
        if (string.IsNullOrEmpty(fontName) || !tryParseFontName(fontName, out string family, out string weight))
            return null;

        var store = fontStoreCache.GetOrCreate(renderer, shaders, family, weight);
        return ((ITexturedGlyphLookupStore)store).Get(fontName, character);
    }

    public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.FromResult(Get(fontName, character));

    // Atlas generation doesn't cover the italic axis yet (see spec-msdf-spritetext-parity.md
    // section 2/6) -- an italic fontName is treated as a missing lookup rather than routed to a
    // store that was never generated, matching the existing missing-glyph behaviour.
    private static bool tryParseFontName(string fontName, out string family, out string weight)
    {
        family = string.Empty;
        weight = string.Empty;

        int separatorIndex = fontName.IndexOf('-');
        if (separatorIndex < 0)
            return false;

        family = fontName[..separatorIndex];
        weight = fontName[(separatorIndex + 1)..];

        if (weight.EndsWith("Italic", StringComparison.Ordinal))
            return false;

        return family.Length > 0 && weight.Length > 0;
    }
}
