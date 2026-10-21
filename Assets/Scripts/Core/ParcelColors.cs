namespace ParcelSort.Core
{
    /// <summary>
    /// Parcel colors are stored as small integers. In level files each color is one letter,
    /// and an upper-case letter in a stack means the parcel starts face down (hidden).
    /// </summary>
    public static class ParcelColors
    {
        public const string Letters = "rgbypocw";
        public const int Count = 8;
        public const int None = -1;

        public static readonly string[] Names = { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Cyan", "White" };

        public static int FromLetter(char c)
        {
            return Letters.IndexOf(char.ToLowerInvariant(c));
        }

        public static char ToLetter(int color, bool hidden = false)
        {
            char c = Letters[color];
            return hidden ? char.ToUpperInvariant(c) : c;
        }
    }
}
