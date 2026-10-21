using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort.Game
{
    /// <summary>
    /// Loads art from Assets/Resources/Art by file name. Missing art returns null so the
    /// game falls back to plain colored boxes; art can be added one file at a time.
    /// Textures are turned into sprites at runtime, so import settings do not matter.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite ParcelBase { get { return Get("parcel_base"); } }
        public static Sprite ParcelHidden { get { return Get("parcel_hidden"); } }
        public static Sprite TruckCab { get { return Get("truck_cab"); } }
        public static Sprite TruckCargo { get { return Get("truck_cargo"); } }

        // Where the cargo box sits on truck_cab.png, as fractions of the cab image:
        // left edge, width, and the y of the bed's top surface measured from the top.
        public const float CargoLeft = 0.044f;
        public const float CargoWidth = 0.635f;
        public const float BedTop = 0.553f;

        public static Sprite Get(string name)
        {
            Sprite sprite;
            if (cache.TryGetValue(name, out sprite)) return sprite;

            var tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            cache[name] = sprite;
            return sprite;
        }

        public static float Aspect(Sprite sprite)
        {
            return sprite.rect.width / sprite.rect.height;
        }
    }
}
