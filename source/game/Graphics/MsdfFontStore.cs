using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Msdf.Game.Resources;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Text;

namespace Msdf.Game.Graphics;

// Glyph metrics for a single character, converted from msdf-atlas-gen's JSON into
// framework-native rectangles. Null bounds mean the glyph has no ink (e.g. space) --
// only the advance applies.
public readonly record struct MsdfGlyph(float Advance, RectangleF? PlaneBounds, RectangleF? AtlasBounds);

// Caches the MSDF atlas texture, shader, and per-glyph metrics for a single family/weight.
// Instances are cached and shared across every MsdfSpriteText via MsdfFontStoreCache
// (see MsdfGameBase) -- there is no reason to reload the same texture/JSON per drawable.
//
// Atlas reproduction command (tools/Inter_24pt-Regular.ttf, no -charset/-chars means the
// default full printable ASCII set):
//   msdf-atlas-gen.exe -font tools/Inter_24pt-Regular.ttf -type msdf -format png -size 48
//     -pxrange 4 -yorigin top -imageout Inter-Regular.png -json Inter-Regular.json
// -yorigin top makes both atlasBounds and planeBounds Y-down, matching screen/texture space
// directly (see planeBounds/atlasBounds usage in MsdfSpriteText). pxrange is read back from
// atlas.distanceRange in the generated JSON and passed to sh_MsdfGlyph.fs as a uniform (see
// DistanceRange/MsdfGlyphSprite), so it never needs to match a shader constant by convention.
public sealed class MsdfFontStore : IDisposable, ITexturedGlyphLookupStore
{
    public Texture Atlas { get; }

    public IShader Shader { get; }

    // Ascender/Descender/LineHeight are in em units (emSize == 1 in the atlas JSON), matching
    // every other per-glyph metric -- consumers scale by FontSize / LineHeight, not FontSize
    // directly, so that FontSize means "line height in pixels", the same convention the
    // framework's own bitmap SpriteText uses for FontUsage.Size (see MsdfSpriteText.layout()).
    public float Ascender { get; }

    public float Descender { get; }

    public float LineHeight { get; }

    public float DistanceRange { get; }

    public IReadOnlyDictionary<char, MsdfGlyph> Glyphs { get; }

    public IReadOnlyDictionary<(char First, char Second), float> Kerning { get; }

    private readonly TextureStore textureStore;

    public MsdfFontStore(IRenderer renderer, ShaderManager shaders, string family, string weight)
    {
        var resources = new DllResourceStore(MsdfResourceAssemblyProvider.Assembly);

        textureStore = new TextureStore(
            renderer,
            new TextureLoaderStore(resources),
            useAtlas: false,
            filteringMode: TextureFilteringMode.Linear,
            manualMipmaps: true);

        string texturePath = $@"Fonts/Msdf/{family}/{family}-{weight}.png";
        string jsonPath = $@"Fonts/Msdf/{family}/{family}-{weight}.json";

        Atlas = textureStore.Get(texturePath);
        Shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");

        byte[] json = resources.Get(jsonPath) ?? throw new InvalidOperationException($"Could not find embedded resource '{jsonPath}'.");
        var atlasJson = parseAtlasJson(json);

        Ascender = atlasJson.Metrics.Ascender;
        Descender = atlasJson.Metrics.Descender;
        LineHeight = atlasJson.Metrics.LineHeight;
        DistanceRange = atlasJson.Atlas.DistanceRange;

        var glyphs = new Dictionary<char, MsdfGlyph>();

        foreach (var glyph in atlasJson.Glyphs)
        {
            glyphs[(char)glyph.Unicode] = new MsdfGlyph(
                glyph.Advance,
                toRectangle(glyph.PlaneBounds),
                toRectangle(glyph.AtlasBounds));
        }

        Glyphs = glyphs;

        var kerning = new Dictionary<(char, char), float>();

        foreach (var pair in atlasJson.Kerning)
            kerning[((char)pair.Unicode1, (char)pair.Unicode2)] = pair.Advance;

        Kerning = kerning;
    }

    // fontName is routed to this specific family/weight store already (see MsdfGlyphLookupStore)
    // -- nothing left to validate against it here, only the character lookup matters.
    ITexturedCharacterGlyph? ITexturedGlyphLookupStore.Get(string? fontName, char character)
    {
        if (!Glyphs.TryGetValue(character, out var glyph))
        {
            Logger.Log($"MsdfFontStore: no glyph for character '{character}' (U+{(int)character:X4}) in atlas -- skipping.", LoggingTarget.Runtime, LogLevel.Debug);
            return null;
        }

        var texture = glyph.AtlasBounds is RectangleF atlasBounds ? Atlas.Crop(atlasBounds) : Atlas;
        return new MsdfCharacterGlyph(this, character, glyph, texture);
    }

    Task<ITexturedCharacterGlyph?> ITexturedGlyphLookupStore.GetAsync(string fontName, char character)
        => Task.FromResult(((ITexturedGlyphLookupStore)this).Get(fontName, character));

    // Reads only atlas.distanceRange from an msdf-atlas-gen JSON, for consumers that render
    // directly from a raw atlas without going through a full MsdfFontStore (e.g. the
    // shader-only visual test scenes).
    public static float ReadDistanceRange(byte[] json) => parseAtlasJson(json).Atlas.DistanceRange;

    private static AtlasJson parseAtlasJson(byte[] json)
        => System.Text.Json.JsonSerializer.Deserialize<AtlasJson>(json) ?? throw new InvalidOperationException("Failed to parse MSDF atlas JSON.");

    private static RectangleF? toRectangle(BoundsJson? bounds)
        => bounds == null ? null : new RectangleF(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);

    public void Dispose() => textureStore.Dispose();

    private sealed class AtlasJson
    {
        [JsonPropertyName("atlas")]
        public AtlasMetaJson Atlas { get; set; } = new AtlasMetaJson();

        [JsonPropertyName("metrics")]
        public MetricsJson Metrics { get; set; } = new MetricsJson();

        [JsonPropertyName("glyphs")]
        public List<GlyphJson> Glyphs { get; set; } = new List<GlyphJson>();

        [JsonPropertyName("kerning")]
        public List<KerningJson> Kerning { get; set; } = new List<KerningJson>();
    }

    private sealed class AtlasMetaJson
    {
        [JsonPropertyName("distanceRange")]
        public float DistanceRange { get; set; }
    }

    private sealed class MetricsJson
    {
        [JsonPropertyName("ascender")]
        public float Ascender { get; set; }

        [JsonPropertyName("descender")]
        public float Descender { get; set; }

        [JsonPropertyName("lineHeight")]
        public float LineHeight { get; set; }
    }

    private sealed class GlyphJson
    {
        [JsonPropertyName("unicode")]
        public int Unicode { get; set; }

        [JsonPropertyName("advance")]
        public float Advance { get; set; }

        [JsonPropertyName("planeBounds")]
        public BoundsJson? PlaneBounds { get; set; }

        [JsonPropertyName("atlasBounds")]
        public BoundsJson? AtlasBounds { get; set; }
    }

    private sealed class BoundsJson
    {
        [JsonPropertyName("left")]
        public float Left { get; set; }

        [JsonPropertyName("top")]
        public float Top { get; set; }

        [JsonPropertyName("right")]
        public float Right { get; set; }

        [JsonPropertyName("bottom")]
        public float Bottom { get; set; }
    }

    private sealed class KerningJson
    {
        [JsonPropertyName("unicode1")]
        public int Unicode1 { get; set; }

        [JsonPropertyName("unicode2")]
        public int Unicode2 { get; set; }

        [JsonPropertyName("advance")]
        public float Advance { get; set; }
    }
}
