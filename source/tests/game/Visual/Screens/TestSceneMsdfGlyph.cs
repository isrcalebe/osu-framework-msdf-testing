using System;
using Msdf.Game.Graphics;
using Msdf.Game.Resources;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
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
// pxrange is read back from the generated json's atlas.distanceRange and bound to the shader
// as a uniform (see MsdfGlyphSprite), so it doesn't need to match a shader constant.
public partial class TestSceneMsdfGlyph : MsdfTestScene
{
    private TextureStore? msdfTextures;

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        var resources = new DllResourceStore(MsdfResourceAssemblyProvider.Assembly);

        // Dedicated, non-atlased texture store: mipmaps would blur the distance channels,
        // and bilinear (not nearest) filtering is required for correct edge reconstruction.
        msdfTextures = new TextureStore(
            renderer,
            new TextureLoaderStore(resources),
            useAtlas: false,
            filteringMode: TextureFilteringMode.Linear,
            manualMipmaps: true);

        var atlas = msdfTextures.Get(@"Textures/Msdf/msdf-test-atlas.png");
        var shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");

        byte[] json = resources.Get(@"Textures/Msdf/msdf-test-atlas.json") ?? throw new InvalidOperationException("Could not find embedded resource 'Textures/Msdf/msdf-test-atlas.json'.");
        float distanceRange = MsdfFontStore.ReadDistanceRange(json);

        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.MidnightBlue,
            },
            new MsdfGlyphSprite(atlas, shader, distanceRange)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(-150, 0),
                Size = new Vector2(64),
            },
            new MsdfGlyphSprite(atlas, shader, distanceRange)
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
}
