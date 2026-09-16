using System;
using Msdf.Game.Graphics;
using Msdf.Game.Resources;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.IO.Stores;
using osuTK.Graphics;

namespace Msdf.Game.Tests.Visual.Screens;

// Side-by-side visual comparison for the central claim of the MSDF proof of concept
// (see TestSceneMsdfGlyph / sh_MsdfGlyph.fs): reconstructing the distance field is only
// half the story -- converting it to coverage with a fixed threshold (sh_MsdfGlyphHardThreshold.fs)
// still aliases at any scale, while the fwidth-based ramp (sh_MsdfGlyph.fs) stays crisp.
// The zoom slider re-scales both columns identically so the two techniques can be inspected
// at any magnification, not just the two fixed sizes baked into TestSceneMsdfGlyph.
// Same atlas as TestSceneMsdfGlyph is reused here; see that file for the exact
// msdf-atlas-gen reproduction command and the pxrange value baked into both shaders.
public partial class TestSceneMsdfComparison : MsdfTestScene
{
    private const float top_bar_height = 48;

    private readonly BindableNumber<float> zoom = new BindableNumber<float>(1f)
    {
        MinValue = 0.5f,
        MaxValue = 8f,
        Precision = 0.01f,
    };

    private TextureStore? msdfTextures;
    private SpriteText zoomValueText = null!;

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        var resources = new DllResourceStore(MsdfResourceAssemblyProvider.Assembly);

        msdfTextures = new TextureStore(
            renderer,
            new TextureLoaderStore(resources),
            useAtlas: false,
            filteringMode: TextureFilteringMode.Linear,
            manualMipmaps: true);

        var atlas = msdfTextures.Get(@"Textures/Msdf/msdf-test-atlas.png");

        byte[] json = resources.Get(@"Textures/Msdf/msdf-test-atlas.json") ?? throw new InvalidOperationException("Could not find embedded resource 'Textures/Msdf/msdf-test-atlas.json'.");
        float distanceRange = MsdfFontStore.ReadDistanceRange(json);

        var smoothShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");
        var hardThresholdShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyphHardThreshold");

        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.MidnightBlue,
            },
            new ComparisonColumn("MSDF (fwidth antialiasing)", atlas, smoothShader, distanceRange, zoom)
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                Width = 0.5f,
                Padding = new MarginPadding { Top = top_bar_height + 8 },
            },
            new ComparisonColumn("Alpha-test bruto (threshold fixo, sem suavização)", atlas, hardThresholdShader, distanceRange, zoom)
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                Width = 0.5f,
                X = 0.5f,
                Padding = new MarginPadding { Top = top_bar_height + 8 },
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                RelativePositionAxes = Axes.X,
                Width = 2,
                X = 0.5f,
                Origin = Anchor.TopCentre,
                Colour = Color4.White,
                Alpha = 0.25f,
            },
            new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = top_bar_height,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.6f,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Text = "Zoom",
                        Font = MsdfFont.Roboto.With(size: 18.0f),
                    },
                    new BasicSliderBar<float>
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 90,
                        Width = 280,
                        Height = 20,
                        BackgroundColour = Color4.DarkSlateGray,
                        SelectionColour = Color4.SkyBlue,
                        Current = zoom,
                    },
                    zoomValueText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 90 + 280 + 16,
                        Font = MsdfFont.Roboto.With(size: 18.0f),
                    },
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        zoom.BindValueChanged(e => zoomValueText.Text = $"{e.NewValue:0.00}x", true);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        msdfTextures?.Dispose();
    }

    private partial class ComparisonColumn : Container
    {
        private readonly string label;
        private readonly Texture atlas;
        private readonly IShader shader;
        private readonly float distanceRange;
        private readonly Bindable<float> zoom;

        private Container glyphArea = null!;

        public ComparisonColumn(string label, Texture atlas, IShader shader, float distanceRange, Bindable<float> zoom)
        {
            this.label = label;
            this.atlas = atlas;
            this.shader = shader;
            this.distanceRange = distanceRange;
            this.zoom = zoom.GetBoundCopy();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = label,
                    Font = MsdfFont.Roboto.With(size: 20.0f),
                },
                glyphArea = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        new MsdfGlyphSprite(atlas, shader, distanceRange)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Position = new Vector2(0, -80),
                            Size = new Vector2(48),
                        },
                        new MsdfGlyphSprite(atlas, shader, distanceRange)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Position = new Vector2(0, 100),
                            Size = new Vector2(280),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            zoom.BindValueChanged(e => glyphArea.Scale = new Vector2(e.NewValue), true);
        }
    }
}
