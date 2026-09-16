using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace Msdf.Game.Graphics;

// Renders a single line of text using the MSDF technique proven in TestSceneMsdfGlyph:
// one child Sprite per glyph, cropped out of an MsdfFontStore atlas and drawn with the same
// fwidth-antialiased shader, laid out via the atlas's own advance/plane metrics.
//
// Each instance loads its own MsdfFontStore (see load() below) rather than sharing one
// cached at the Game level: ShaderManager/IRenderer are only resolvable once a drawable is
// loaded as part of the scene graph below the root Game object, not from Game's own
// [BackgroundDependencyLoader] -- confirmed by a DependencyNotRegisteredException when that
// was tried. A single demo instance re-uploading the atlas texture is a non-issue for a POC.
//
// Deliberately minimal for what it needs to prove (MSDF text stays crisp at any scale,
// unlike bitmap-font SpriteText): single line only, no wrapping, no kerning (the generated
// atlas has none to apply), and characters outside the atlas's charset are skipped rather
// than falling back to a placeholder glyph.
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

    private MsdfFontStore? fontStore;

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        fontStore = new MsdfFontStore(renderer, shaders);
        layout();
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        fontStore?.Dispose();
    }

    private void layout()
    {
        var store = fontStore!;

        ClearInternal();

        float baselineY = -store.Ascender * FontSize;
        float cursorX = 0.0f;

        foreach (char c in Text)
        {
            if (!store.Glyphs.TryGetValue(c, out var glyph))
                continue;

            if (glyph.PlaneBounds is RectangleF planeBounds && glyph.AtlasBounds is RectangleF atlasBounds)
            {
                var cropped = store.Atlas.Crop(atlasBounds);

                AddInternal(new MsdfGlyphSprite(cropped, store.Shader)
                {
                    Position = new Vector2(cursorX + planeBounds.Left * FontSize, baselineY + planeBounds.Top * FontSize),
                    Size = new Vector2(planeBounds.Width * FontSize, planeBounds.Height * FontSize),
                });
            }

            cursorX += glyph.Advance * FontSize;
        }

        Width = cursorX;
        Height = (store.Descender - store.Ascender) * FontSize;
    }

    private partial class MsdfGlyphSprite : Sprite
    {
        private readonly Texture glyphTexture;
        private readonly IShader glyphShader;

        public MsdfGlyphSprite(Texture glyphTexture, IShader glyphShader)
        {
            this.glyphTexture = glyphTexture;
            this.glyphShader = glyphShader;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Texture = glyphTexture;
            TextureShader = glyphShader;
        }
    }
}
