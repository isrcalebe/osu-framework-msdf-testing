using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osuTK.Graphics;

namespace Msdf.Game.Graphics;

// Renders text via the same layout/glyph engine osu.Framework's own SpriteText uses
// (TextBuilder/MultilineTextBuilder/TruncatingTextBuilder), but sourcing glyphs from an
// MsdfFontStore atlas instead of a bitmap FontStore -- see spec-msdf-spritetext-parity.md for
// why this can't simply inherit SpriteText (its glyph store is a private, non-overridable
// [Resolved] field) and has to reimplement the same public surface via composition instead.
//
// A Drawable, not a CompositeDrawable: like SpriteText, glyphs are drawn directly by a single
// DrawNode (see the nested MsdfSpriteTextDrawNode below) rather than as one child Drawable per
// glyph -- the same reason SpriteText avoids per-character Drawables.
public partial class MsdfSpriteText : Drawable, IHasLineBaseHeight, ITexturedShaderDrawable, IHasCurrentValue<string>
{
    private static readonly char[] default_never_fixed_width_characters = { '.', ',', ':', ' ', ' ', ' ' };

    [Resolved]
    private MsdfFontStoreCache fontStoreCache { get; set; } = null!;

    [Resolved]
    private LocalisationManager localisation { get; set; } = null!;

    private ILocalisedBindableString localisedText = null!;

    public IShader? TextureShader { get; private set; }

    private IRenderer? renderer;
    private ShaderManager? shaders;
    private MsdfGlyphLookupStore? glyphLookupStore;
    private float distanceRange;

    public MsdfSpriteText()
    {
        current.BindValueChanged(text =>
        {
            // importantly, to avoid a feedback loop which will overwrite a localised text object, check equality of the resulting text before propagating a basic string to Text.
            if (localisedText == null || text.NewValue != localisedText.Value)
                Text = text.NewValue;
        });

        AddLayout(charactersCache);
        AddLayout(shadowOffsetCache);
        AddLayout(textBuilderCache);
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        this.renderer = renderer;
        this.shaders = shaders;

        glyphLookupStore = new MsdfGlyphLookupStore(fontStoreCache, renderer, shaders);
        TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MsdfGlyph");

        localisedText = localisation.GetLocalisedBindableString(Text);

        updateFontStore();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        localisedText.BindValueChanged(str =>
        {
            current.Value = localisedText.Value;

            if (string.IsNullOrEmpty(str.NewValue))
            {
                if (requiresAutoSizedWidth)
                    base.Width = Padding.TotalHorizontal;
                if (requiresAutoSizedHeight)
                    base.Height = Padding.TotalVertical;
            }

            invalidate(true);
        }, true);
    }

    public LocalisableString Text
    {
        get;
        set
        {
            if (field.Equals(value))
                return;

            field = value;
            localisedText?.Text = value;
        }
    } = string.Empty;

    private readonly BindableWithCurrent<string> current = new BindableWithCurrent<string>();

    public Bindable<string> Current
    {
        get => current.Current;
        set => current.Current = value;
    }

    private string displayedText => localisedText?.Value ?? Text.ToString();

    public FontUsage Font
    {
        get;
        set
        {
            field = value;

            invalidate(true, true);
            shadowOffsetCache.Invalidate();

            if (renderer != null)
                updateFontStore();
        }
    } = MsdfFont.GetFont();

    public bool AllowMultiline
    {
        get;
        set
        {
            if (field == value)
                return;

            if (value)
                Truncate = false;

            field = value;
            invalidate(true, true);
        }
    } = true;

    public bool Shadow
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Invalidate(Invalidation.DrawNode);
        }
    }

    public Color4 ShadowColour
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Invalidate(Invalidation.DrawNode);
        }
    } = new Color4(0, 0, 0, 0.2f);

    public Vector2 ShadowOffset
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            invalidate(true);
            shadowOffsetCache.Invalidate();
        }
    } = new Vector2(0, 0.06f);

    public bool UseFullGlyphHeight
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            invalidate(true, true);
        }
    } = true;

    public bool Truncate
    {
        get;
        set
        {
            if (field == value)
                return;

            if (value)
                AllowMultiline = false;

            field = value;
            invalidate(true, true);
        }
    }

    public string EllipsisString
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            invalidate(true, true);
        }
    } = "…";

    public bool IsTruncated { get; private set; }

    private bool requiresAutoSizedWidth => explicitWidth == null && (RelativeSizeAxes & Axes.X) == 0;

    private bool requiresAutoSizedHeight => explicitHeight == null && (RelativeSizeAxes & Axes.Y) == 0;

    private float? explicitWidth;

    public override float Width
    {
        get
        {
            if (requiresAutoSizedWidth)
                computeCharacters();
            return base.Width;
        }
        set
        {
            if (explicitWidth == value)
                return;

            base.Width = value;
            explicitWidth = value;

            invalidate(true, true);
        }
    }

    public float MaxWidth
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            invalidate(true, true);
        }
    } = float.PositiveInfinity;

    private float? explicitHeight;

    public override float Height
    {
        get
        {
            if (requiresAutoSizedHeight)
                computeCharacters();
            return base.Height;
        }
        set
        {
            if (explicitHeight == value)
                return;

            base.Height = value;
            explicitHeight = value;

            invalidate(true, true);
        }
    }

    public override Vector2 Size
    {
        get
        {
            if (requiresAutoSizedWidth || requiresAutoSizedHeight)
                computeCharacters();
            return base.Size;
        }
        set
        {
            Width = value.X;
            Height = value.Y;
        }
    }

    public Vector2 Spacing
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            invalidate(true, true);
        }
    }

    public MarginPadding Padding
    {
        get;
        set
        {
            if (field.Equals(value))
                return;

            if (!Validation.IsFinite(value))
                throw new ArgumentException($@"{nameof(Padding)} must be finite, but is {value}.");

            field = value;
            invalidate(true, true);
        }
    }

    public override bool IsPresent => base.IsPresent && (AlwaysPresent || !string.IsNullOrEmpty(displayedText));

    private readonly LayoutValue charactersCache = new LayoutValue(Invalidation.DrawSize | Invalidation.Presence, InvalidationSource.Parent);

    private readonly List<TextBuilderGlyph> charactersBacking = new List<TextBuilderGlyph>();

    private List<TextBuilderGlyph> characters
    {
        get
        {
            computeCharacters();
            return charactersBacking;
        }
    }

    private void computeCharacters()
    {
        if (glyphLookupStore == null)
            return;

        if (charactersCache.IsValid)
            return;

        IsTruncated = false;

        charactersBacking.Clear();

        Vector2 textBounds = Vector2.Zero;

        try
        {
            if (string.IsNullOrEmpty(displayedText))
                return;

            TextBuilder textBuilder = getTextBuilder();

            textBuilder.Reset();
            textBuilder.AddText(displayedText);
            textBounds = textBuilder.Bounds;

            if (textBuilder is TruncatingTextBuilder truncatingTextBuilder)
                IsTruncated = truncatingTextBuilder.IsTruncated;
        }
        finally
        {
            if (requiresAutoSizedWidth)
                base.Width = textBounds.X + Padding.Right;
            if (requiresAutoSizedHeight)
                base.Height = textBounds.Y + Padding.Bottom;

            base.Width = Math.Min(base.Width, MaxWidth);

            charactersCache.Validate();
        }
    }

    private readonly LayoutValue<Vector2> shadowOffsetCache = new LayoutValue<Vector2>(Invalidation.DrawInfo, InvalidationSource.Parent);

    private Vector2 premultipliedShadowOffset =>
        shadowOffsetCache.IsValid ? shadowOffsetCache.Value : shadowOffsetCache.Value = ToScreenSpace(ShadowOffset * Font.Size) - ToScreenSpace(Vector2.Zero);

    private void invalidate(bool characters = false, bool textBuilder = false)
    {
        if (characters)
            charactersCache.Invalidate();

        if (textBuilder)
            InvalidateTextBuilder();

        Invalidate(Invalidation.RequiredParentSizeToFit);
    }

    protected override DrawNode CreateDrawNode() => new MsdfSpriteTextDrawNode(this);

    private readonly LayoutValue<TextBuilder> textBuilderCache = new LayoutValue<TextBuilder>(Invalidation.DrawSize, InvalidationSource.Parent);

    protected void InvalidateTextBuilder() => textBuilderCache.Invalidate();

    protected virtual TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store)
    {
        float builderMaxWidth = requiresAutoSizedWidth
            ? MaxWidth
            : ApplyRelativeAxes(RelativeSizeAxes, new Vector2(Math.Min(MaxWidth, base.Width), base.Height), FillMode).X - Padding.Right;

        if (AllowMultiline)
        {
            return new MultilineTextBuilder(store, Font, builderMaxWidth, UseFullGlyphHeight, new Vector2(Padding.Left, Padding.Top), Spacing, charactersBacking,
                default_never_fixed_width_characters);
        }

        if (Truncate)
        {
            return new TruncatingTextBuilder(store, Font, builderMaxWidth, EllipsisString, UseFullGlyphHeight, new Vector2(Padding.Left, Padding.Top), Spacing, charactersBacking,
                default_never_fixed_width_characters);
        }

        return new TextBuilder(store, Font, builderMaxWidth, UseFullGlyphHeight, new Vector2(Padding.Left, Padding.Top), Spacing, charactersBacking,
            default_never_fixed_width_characters);
    }

    private TextBuilder getTextBuilder()
    {
        if (!textBuilderCache.IsValid)
            textBuilderCache.Value = CreateTextBuilder(glyphLookupStore!);

        return textBuilderCache.Value;
    }

    // DistanceRange is resolved eagerly whenever Font changes rather than read lazily from
    // MsdfFontStoreCache inside the DrawNode -- MsdfSpriteTextDrawNode.ApplyState() runs off the
    // update thread's captured state only, it cannot touch the cache itself.
    private void updateFontStore()
        => distanceRange = fontStoreCache.GetOrCreate(renderer!, shaders!, Font.Family ?? string.Empty, Font.Weight ?? string.Empty).DistanceRange;

    public override string ToString() => $@"""{displayedText}"" " + base.ToString();

    public float LineBaseHeight
    {
        get
        {
            computeCharacters();
            return textBuilderCache.Value.LineBaseHeight;
        }
    }

    // Mirrors SpriteTextDrawNode (osu.Framework.Graphics.Sprites, internal -- can't be reused
    // directly, see spec-msdf-spritetext-parity.md section 0): same per-glyph DrawQuad technique,
    // plus binding the active family/weight's DistanceRange uniform that sh_MsdfGlyph.fs needs
    // (see MsdfGlyphSprite for the same uniform bound the other way, per raw glyph instance).
    //
    // One MsdfSpriteText uses one family/weight at a time (see spec section 4), so DistanceRange
    // is a single value for the whole DrawNode, not per-glyph.
    private class MsdfSpriteTextDrawNode : TexturedShaderDrawNode
    {
        protected new MsdfSpriteText Source => (MsdfSpriteText)base.Source;

        private bool shadow;
        private ColourInfo shadowColour;
        private Vector2 shadowOffset;
        private float distanceRange;

        private List<ScreenSpaceGlyphPart>? parts;
        private IUniformBuffer<MsdfParameters>? parametersBuffer;

        public MsdfSpriteTextDrawNode(MsdfSpriteText source)
            : base(source)
        {
        }

        public override void ApplyState()
        {
            base.ApplyState();

            updateScreenSpaceCharacters();
            distanceRange = Source.distanceRange;
            shadow = Source.Shadow;

            if (shadow)
            {
                shadowColour = Source.ShadowColour;
                shadowOffset = Source.premultipliedShadowOffset;
            }
        }

        protected override void Draw(IRenderer renderer)
        {
            Debug.Assert(parts != null);

            base.Draw(renderer);

            BindTextureShader(renderer);

            var avgColour = (Color4)DrawColourInfo.Colour.AverageColour;
            float shadowAlpha = MathF.Pow(Math.Max(Math.Max(avgColour.R, avgColour.G), avgColour.B), 2);

            var finalShadowColour = DrawColourInfo.Colour;
            finalShadowColour.ApplyChild(shadowColour.MultiplyAlpha(shadowAlpha));

            for (int i = 0; i < parts.Count; i++)
            {
                if (shadow)
                {
                    var shadowQuad = parts[i].DrawQuad;

                    renderer.DrawQuad(parts[i].Texture,
                        new Quad(
                            shadowQuad.TopLeft + shadowOffset,
                            shadowQuad.TopRight + shadowOffset,
                            shadowQuad.BottomLeft + shadowOffset,
                            shadowQuad.BottomRight + shadowOffset),
                        finalShadowColour, inflationPercentage: parts[i].InflationPercentage);
                }

                renderer.DrawQuad(parts[i].Texture, parts[i].DrawQuad, DrawColourInfo.Colour, inflationPercentage: parts[i].InflationPercentage);
            }

            UnbindTextureShader(renderer);
        }

        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            base.BindUniformResources(shader, renderer);

            parametersBuffer ??= renderer.CreateUniformBuffer<MsdfParameters>();
            parametersBuffer.Data = new MsdfParameters { DistanceRange = new UniformFloat { Value = distanceRange } };

            shader.BindUniformBlock("m_MsdfParameters", parametersBuffer);
        }

        private void updateScreenSpaceCharacters()
        {
            int partCount = Source.characters.Count;

            if (parts == null)
                parts = new List<ScreenSpaceGlyphPart>(partCount);
            else
            {
                parts.Clear();
                parts.EnsureCapacity(partCount);
            }

            Vector2 inflationAmount = DrawInfo.MatrixInverse.ExtractScale().Xy;

            foreach (var character in Source.characters)
            {
                parts.Add(new ScreenSpaceGlyphPart
                {
                    DrawQuad = Source.ToScreenSpace(character.DrawRectangle.Inflate(inflationAmount)),
                    InflationPercentage = new Vector2(
                        character.DrawRectangle.Size.X == 0 ? 0 : inflationAmount.X / character.DrawRectangle.Size.X,
                        character.DrawRectangle.Size.Y == 0 ? 0 : inflationAmount.Y / character.DrawRectangle.Size.Y),
                    Texture = character.Texture,
                });
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct MsdfParameters
        {
            public UniformFloat DistanceRange;
            private readonly UniformPadding12 padding;
        }

        private struct ScreenSpaceGlyphPart
        {
            public Quad DrawQuad;
            public Vector2 InflationPercentage;
            public Texture Texture;
        }
    }
}
