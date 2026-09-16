using osu.Framework.Graphics.Sprites;
using Msdf.Game.Graphics;

namespace Msdf.Game.Extensions.FontExtensions;

public static class FontExtensions
{
    public static FontUsage With(this FontUsage usage, FontTypeface? typeface = null, float? size = null, FontWeight? weight = null, bool? italics = null, bool? fixedWidth = null)
    {
        var familyStr = typeface != null ? MsdfFont.GetFamilyString(typeface.Value) : usage.Family;
        var weightStr = weight != null ? MsdfFont.GetWeightString(familyStr, weight.Value) : usage.Weight;

        return usage.With(familyStr, size, weightStr, italics, fixedWidth);
    }
}
