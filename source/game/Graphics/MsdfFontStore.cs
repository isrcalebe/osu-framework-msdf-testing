using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Msdf.Game.Resources;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace Msdf.Game.Graphics;

// Glyph metrics for a single character, converted from msdf-atlas-gen's JSON into
// framework-native rectangles. Null bounds mean the glyph has no ink (e.g. space) --
// only the advance applies.
public readonly record struct MsdfGlyph(float Advance, RectangleF? PlaneBounds, RectangleF? AtlasBounds);

// Caches the MSDF atlas texture, shader, and per-glyph metrics for MsdfSpriteText.
// Cached once at the Game level (see MsdfGameBase) since the atlas is shared by every
// MsdfSpriteText instance -- there is no reason to reload the texture/JSON per drawable.
//
// Atlas reproduction command (tools/Inter_24pt-Regular.ttf, no -charset/-chars means the
// default full printable ASCII set):
//   msdf-atlas-gen.exe -font tools/Inter_24pt-Regular.ttf -type msdf -format png -size 48
//     -pxrange 4 -yorigin top -imageout inter-msdf-atlas.png -json inter-msdf-atlas.json
// -yorigin top makes both atlasBounds and planeBounds Y-down, matching screen/texture space
// directly (see planeBounds/atlasBounds usage in MsdfSpriteText). pxrange (4) must match
// MSDF_PX_RANGE in sh_MsdfGlyph.fs -- it already does, so the shader is reused unmodified.
public sealed class MsdfFontStore : IDisposable
{
    private const string atlas_texture_name = @"Textures/Msdf/inter-msdf-atlas.png";
    private const string atlas_json_name = @"Textures/Msdf/inter-msdf-atlas.json";

    public Texture Atlas { get; }

    public IShader Shader { get; }

    public float Ascender { get; }

    public float Descender { get; }

    public IReadOnlyDictionary<char, MsdfGlyph> Glyphs { get; }

    private readonly TextureStore textureStore;

    public MsdfFontStore(IRenderer renderer, ShaderManager shaders)
    {
        var resources = new DllResourceStore(MsdfResourceAssemblyProvider.Assembly);

        textureStore = new TextureStore(
            renderer,
            new TextureLoaderStore(resources),
            useAtlas: false,
            filteringMode: TextureFilteringMode.Linear,
            manualMipmaps: true);

        Atlas = textureStore.Get(atlas_texture_name);
        Shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");

        string json = Encoding.UTF8.GetString(resources.Get(atlas_json_name) ?? throw new InvalidOperationException($"Could not find embedded resource '{atlas_json_name}'."));
        var atlasJson = System.Text.Json.JsonSerializer.Deserialize<AtlasJson>(json) ?? throw new InvalidOperationException("Failed to parse MSDF atlas JSON.");

        Ascender = atlasJson.Metrics.Ascender;
        Descender = atlasJson.Metrics.Descender;

        var glyphs = new Dictionary<char, MsdfGlyph>();

        foreach (var glyph in atlasJson.Glyphs)
        {
            glyphs[(char)glyph.Unicode] = new MsdfGlyph(
                glyph.Advance,
                toRectangle(glyph.PlaneBounds),
                toRectangle(glyph.AtlasBounds));
        }

        Glyphs = glyphs;
    }

    private static RectangleF? toRectangle(BoundsJson? bounds)
        => bounds == null ? null : new RectangleF(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);

    public void Dispose() => textureStore.Dispose();

    private sealed class AtlasJson
    {
        [JsonPropertyName("metrics")]
        public MetricsJson Metrics { get; set; } = new MetricsJson();

        [JsonPropertyName("glyphs")]
        public List<GlyphJson> Glyphs { get; set; } = new List<GlyphJson>();
    }

    private sealed class MetricsJson
    {
        [JsonPropertyName("ascender")]
        public float Ascender { get; set; }

        [JsonPropertyName("descender")]
        public float Descender { get; set; }
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
}
