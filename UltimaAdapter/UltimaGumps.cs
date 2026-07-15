namespace Ultima
{
    public static class Gumps
    {
        public static bool IsValidIndex(int index)
        {
            var file = Files.Manager?.Gumps?.File;
            if (file == null) return false;
            if (index < 0 || index >= file.Entries.Length) return false;
            ref var entry = ref file.GetValidRefEntry(index);
            return !entry.Equals(ClassicUO.IO.UOFileIndex.Invalid);
        }
    }
}
