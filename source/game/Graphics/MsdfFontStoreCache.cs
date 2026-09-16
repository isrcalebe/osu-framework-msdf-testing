using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;

namespace Msdf.Game.Graphics;

// Cached once at the Game level (see MsdfGameBase) and resolved by every MsdfSpriteText,
// so the same family/weight's atlas texture and parsed JSON are loaded once and shared,
// rather than every MsdfSpriteText instance re-uploading its own copy.
public sealed class MsdfFontStoreCache : IDisposable
{
    private readonly Dictionary<(string Family, string Weight), MsdfFontStore> stores = new Dictionary<(string, string), MsdfFontStore>();

    public MsdfFontStore GetOrCreate(IRenderer renderer, ShaderManager shaders, string family, string weight)
    {
        var key = (family, weight);

        if (!stores.TryGetValue(key, out var store))
            stores[key] = store = new MsdfFontStore(renderer, shaders, family, weight);

        return store;
    }

    public void Dispose()
    {
        foreach (var store in stores.Values)
            store.Dispose();

        stores.Clear();
    }
}
