using osu.Framework.Graphics.Textures;
using osu.Framework.Text;

namespace Msdf.Game.Graphics;

// Adapts a single MsdfGlyph (raw msdf-atlas-gen metrics, in atlas-em units) to the
// ITexturedCharacterGlyph contract TextBuilder/TextBuilderGlyph expect from any glyph source.
//
// Every metric is normalised by the font's LineHeight, the same role ScaleAdjust plays for the
// framework's own TexturedCharacterGlyph (bitmap fonts) -- so that TextBuilderGlyph's later
// `* FontUsage.Size` reproduces exactly what MsdfSpriteText.layout() used to compute by hand via
// `pixelsPerEm = FontSize / LineHeight` before this type existed.
public readonly struct MsdfCharacterGlyph : ITexturedCharacterGlyph
{
    public Texture Texture { get; }

    public char Character { get; }

    public float Width { get; }

    public float Height { get; }

    public float XOffset { get; }

    public float YOffset { get; }

    public float XAdvance { get; }

    public float Baseline { get; }

    private readonly MsdfFontStore store;

    public MsdfCharacterGlyph(MsdfFontStore store, char character, MsdfGlyph glyph, Texture texture)
    {
        this.store = store;

        Character = character;
        Texture = texture;

        float lineHeight = store.LineHeight;
        var planeBounds = glyph.PlaneBounds ?? default;

        Width = planeBounds.Width / lineHeight;
        Height = planeBounds.Height / lineHeight;
        XOffset = planeBounds.Left / lineHeight;
        YOffset = (planeBounds.Top - store.Ascender) / lineHeight;
        XAdvance = glyph.Advance / lineHeight;
        Baseline = store.Ascender / lineHeight;
    }

    // Kerning is looked up from the same store as the rest of this glyph's metrics and
    // normalised by the same LineHeight factor -- consistent with, but not additionally scaled
    // by, the caller's FontUsage.Size, matching how TextBuilder applies ITexturedCharacterGlyph
    // kerning verbatim (see TextBuilder.AddCharacter) rather than re-scaling it per consumer.
    public float GetKerning<T>(T lastGlyph)
        where T : ICharacterGlyph
        => store.Kerning.TryGetValue((lastGlyph.Character, Character), out float kerning) ? kerning / store.LineHeight : 0;
}
