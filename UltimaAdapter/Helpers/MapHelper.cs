namespace Ultima.Helpers
{
    public sealed class MapHelper
    {
        public static void CheckForNewMapSize()
        {
            if (Files.GetFilePath("map1.mul") != null || Files.GetFilePath("map1legacymul.uop") != null)
            {
                Map.Trammel = Map.Trammel.Width == 7168
                    ? new Map(1, 1, 7168, 4096)
                    : new Map(1, 1, 6144, 4096);
            }
            else
            {
                Map.Trammel = Map.Trammel.Width == 7168
                    ? new Map(0, 1, 7168, 4096)
                    : new Map(0, 1, 6144, 4096);
            }
        }
    }
}
