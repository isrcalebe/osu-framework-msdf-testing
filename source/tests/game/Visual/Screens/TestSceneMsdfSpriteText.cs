using Msdf.Game.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK.Graphics;

namespace Msdf.Game.Tests.Visual.Screens;

// Compares the framework's own SpriteText (BMFont-based bitmap rendering, using the same
// embedded Inter font as everywhere else in this project) against MsdfSpriteText (the MSDF
// technique from TestSceneMsdfGlyph, extended to real multi-glyph text) -- same string,
// "sem MSDF" vs "com MSDF". The zoom slider (same pattern as TestSceneMsdfComparison) is
// the actual point: bitmap SpriteText blurs/pixelates once scaled past its baked
// resolution, while MsdfSpriteText's edges stay crisp at any scale.
public partial class TestSceneMsdfSpriteText : MsdfTestScene
{
    private const float top_bar_height = 48;
    private const float demo_font_size = 28.0f;
    private const string demo_text = "MSDF Sample Aa Oo 123";

    private readonly BindableNumber<float> zoom = new BindableNumber<float>(1f)
    {
        MinValue = 0.5f,
        MaxValue = 8f,
        Precision = 0.01f,
    };

    private SpriteText zoomValueText = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.MidnightBlue,
            },
            new ComparisonColumn(
                "SpriteText (sem MSDF, bitmap Inter)",
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = demo_text,
                    Font = MsdfFont.Inter.With(size: demo_font_size),
                },
                zoom)
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                Width = 0.5f,
                Padding = new MarginPadding { Top = top_bar_height + 8 },
            },
            new ComparisonColumn(
                "MsdfSpriteText (com MSDF)",
                new MsdfSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = demo_text,
                    FontSize = demo_font_size,
                },
                zoom)
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

    private partial class ComparisonColumn : Container
    {
        private readonly string label;
        private readonly Drawable sample;
        private readonly Bindable<float> zoom;

        private Container zoomArea = null!;

        public ComparisonColumn(string label, Drawable sample, Bindable<float> zoom)
        {
            this.label = label;
            this.sample = sample;
            this.zoom = zoom.GetBoundCopy();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = label,
                    Font = MsdfFont.Roboto.With(size: 20.0f),
                },
                // Masking lives on a container that is never scaled, so the clip bounds
                // stay pinned to the column's own bounds. Scaling this same container
                // (as an earlier version did) grows the clip region right along with the
                // zoomed content, defeating the clip entirely -- hence the extra nesting.
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = zoomArea = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Child = sample,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            zoom.BindValueChanged(e => zoomArea.Scale = new Vector2(e.NewValue), true);
        }
    }
}
