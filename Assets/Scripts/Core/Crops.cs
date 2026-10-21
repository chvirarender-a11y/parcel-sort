namespace ParcelSort.Core
{
    /// <summary>
    /// Crop colors are small integers. In level files each crop (and each harvester) is one letter.
    /// </summary>
    public static class Crops
    {
        public const string Letters = "rgbypowk";
        public const int Count = 8;
        public const int None = -1;
        public const char Empty = '.';

        public static readonly string[] Names = { "Tomato", "Cabbage", "Blueberry", "Corn", "Eggplant", "Pumpkin", "Cauliflower", "Radish" };

        public static int FromLetter(char c)
        {
            return Letters.IndexOf(char.ToLowerInvariant(c));
        }

        public static char ToLetter(int crop)
        {
            return crop < 0 ? Empty : Letters[crop];
        }
    }
}
