using System.Runtime.InteropServices;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace Msdf.Game.Graphics;

// Draws a single MSDF-sampled quad with sh_MsdfGlyph.fs, binding the atlas's own
// distanceRange as a uniform (see MsdfFontStore.DistanceRange) so the shader stays
// correct regardless of which -pxrange the source atlas was generated with.
// Shared by MsdfSpriteText (one instance per glyph) and the raw-shader test scenes
// (TestSceneMsdfGlyph/TestSceneMsdfComparison) so every consumer of sh_MsdfGlyph.fs
// binds this uniform -- a plain Sprite would leave it unbound and break antialiasing.
public partial class MsdfGlyphSprite : Sprite
{
    private readonly Texture glyphTexture;
    private readonly IShader glyphShader;
    private readonly float distanceRange;

    public MsdfGlyphSprite(Texture glyphTexture, IShader glyphShader, float distanceRange)
    {
        this.glyphTexture = glyphTexture;
        this.glyphShader = glyphShader;
        this.distanceRange = distanceRange;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Texture = glyphTexture;
        TextureShader = glyphShader;
    }

    protected override DrawNode CreateDrawNode() => new MsdfGlyphSpriteDrawNode(this);

    private class MsdfGlyphSpriteDrawNode : SpriteDrawNode
    {
        protected new MsdfGlyphSprite Source => (MsdfGlyphSprite)base.Source;

        private float distanceRange;
        private IUniformBuffer<MsdfParameters>? parametersBuffer;

        public MsdfGlyphSpriteDrawNode(MsdfGlyphSprite source)
            : base(source)
        {
        }

        public override void ApplyState()
        {
            base.ApplyState();

            distanceRange = Source.distanceRange;
        }

        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            base.BindUniformResources(shader, renderer);

            parametersBuffer ??= renderer.CreateUniformBuffer<MsdfParameters>();
            parametersBuffer.Data = new MsdfParameters { DistanceRange = new UniformFloat { Value = distanceRange } };

            shader.BindUniformBlock("m_MsdfParameters", parametersBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct MsdfParameters
        {
            public UniformFloat DistanceRange;
            private readonly UniformPadding12 padding;
        }
    }
}
