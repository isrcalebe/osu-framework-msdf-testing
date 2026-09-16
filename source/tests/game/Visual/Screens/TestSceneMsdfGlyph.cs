using Msdf.Game.Resources;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osuTK.Graphics;

namespace Msdf.Game.Tests.Visual.Screens;

// Isolated proof of concept for MSDF (multi-channel signed distance field) font rendering:
// a hand-written fragment shader samples an msdf-atlas-gen atlas directly, without touching
// GlyphStore/BitmapFont/SpriteText. See sh_MsdfGlyph.fs for the reconstruction/antialiasing math.
//
// Atlas reproduction command (Windows' own arial.ttf used as a throwaway source font):
//   msdf-atlas-gen.exe -font "C:\Windows\Fonts\arial.ttf" -chars "'A', 'O'" -type msdf -format png
//     -size 64 -pxrange 4 -imageout msdf-test-atlas.png -json msdf-test-atlas.json
// pxrange used above (4) must match MSDF_PX_RANGE in sh_MsdfGlyph.fs.
public partial class TestSceneMsdfGlyph : MsdfTestScene
{
    private TextureStore? msdfTextures;

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        // Dedicated, non-atlased texture store: mipmaps would blur the distance channels,
        // and bilinear (not nearest) filtering is required for correct edge reconstruction.
        msdfTextures = new TextureStore(
            renderer,
            new TextureLoaderStore(new DllResourceStore(MsdfResourceAssemblyProvider.Assembly)),
            useAtlas: false,
            filteringMode: TextureFilteringMode.Linear,
            manualMipmaps: true);

        var atlas = msdfTextures.Get(@"Textures/Msdf/msdf-test-atlas.png");
        var shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");

        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.MidnightBlue,
            },
            new MsdfSprite(atlas, shader)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(-150, 0),
                Size = new Vector2(64),
            },
            new MsdfSprite(atlas, shader)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(150, 0),
                Size = new Vector2(320),
            },
        };
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        msdfTextures?.Dispose();
    }

    private partial class MsdfSprite : Sprite
    {
        private readonly Texture msdfTexture;
        private readonly IShader msdfShader;

        public MsdfSprite(Texture msdfTexture, IShader msdfShader)
        {
            this.msdfTexture = msdfTexture;
            this.msdfShader = msdfShader;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Texture = msdfTexture;
            TextureShader = msdfShader;
        }
    }
}
