using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;

namespace Msdf.Game.Graphics;

// Renders a single line of text using the MSDF technique proven in TestSceneMsdfGlyph:
// one child MsdfGlyphSprite per glyph, cropped out of an MsdfFontStore atlas and drawn with
// the same fwidth-antialiased shader, laid out via the atlas's own advance/plane/kerning
// metrics.
//
// The backing MsdfFontStore is resolved from MsdfFontStoreCache (cached at the Game level,
// see MsdfGameBase) rather than owned per-instance, so every MsdfSpriteText using the same
// family/weight shares one atlas texture/JSON instead of each reloading its own copy.
//
// Deliberately minimal for what it needs to prove (MSDF text stays crisp at any scale,
// unlike bitmap-font SpriteText): single line only, no wrapping.
public partial class MsdfSpriteText : CompositeDrawable
{
    public string Text
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (fontStore != null)
                layout();
        }
    } = string.Empty;

    public float FontSize
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (fontStore != null)
                layout();
        }
    } = 48.0f;

    public string Family
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (fontStore != null)
                reloadFontStore();
        }
    } = "Inter";

    public string Weight
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (fontStore != null)
                reloadFontStore();
        }
    } = "Regular";

    [Resolved]
    private MsdfFontStoreCache fontStoreCache { get; set; } = null!;

    private IRenderer renderer = null!;
    private ShaderManager shaders = null!;
    private MsdfFontStore? fontStore;

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        this.renderer = renderer;
        this.shaders = shaders;

        reloadFontStore();
    }

    private void reloadFontStore()
    {
        fontStore = fontStoreCache.GetOrCreate(renderer, shaders, Family, Weight);
        layout();
    }

    private void layout()
    {
        var store = fontStore!;

        ClearInternal();

        // Every glyph metric below is in em units (see MsdfFontStore.Ascender). Scaling by
        // FontSize directly would make FontSize mean "em size in pixels", producing a line
        // height of LineHeight (~1.2) times FontSize -- taller than intended and inconsistent
        // with the framework's own bitmap SpriteText, where FontUsage.Size is the line height
        // in pixels. Scaling by pixelsPerEm instead makes the two conventions match.
        float pixelsPerEm = FontSize / store.LineHeight;

        float baselineY = -store.Ascender * pixelsPerEm;
        float cursorX = 0.0f;
        char? previous = null;

        foreach (char c in Text)
        {
            if (!store.Glyphs.TryGetValue(c, out var glyph))
            {
                Logger.Log($"MsdfSpriteText: no glyph for character '{c}' (U+{(int)c:X4}) in {Family}-{Weight} atlas -- skipping.", LoggingTarget.Runtime, LogLevel.Debug);
                previous = null;
                continue;
            }

            if (previous is char prev && store.Kerning.TryGetValue((prev, c), out float kerning))
                cursorX += kerning * pixelsPerEm;

            if (glyph.PlaneBounds is RectangleF planeBounds && glyph.AtlasBounds is RectangleF atlasBounds)
            {
                var cropped = store.Atlas.Crop(atlasBounds);

                AddInternal(new MsdfGlyphSprite(cropped, store.Shader, store.DistanceRange)
                {
                    Position = new Vector2(cursorX + planeBounds.Left * pixelsPerEm, baselineY + planeBounds.Top * pixelsPerEm),
                    Size = new Vector2(planeBounds.Width * pixelsPerEm, planeBounds.Height * pixelsPerEm),
                });
            }

            cursorX += glyph.Advance * pixelsPerEm;
            previous = c;
        }

        Width = cursorX;
        Height = (store.Descender - store.Ascender) * pixelsPerEm;
    }
}
