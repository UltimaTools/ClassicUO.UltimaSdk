using System;
using System.Collections.Generic;
using System.IO;

namespace Ultima
{
    public static class BodyConverter
    {
        public static int[] Table1 { get; private set; }
        public static int[] Table2 { get; private set; }
        public static int[] Table3 { get; private set; }
        public static int[] Table4 { get; private set; }

        static BodyConverter()
        {
            Initialize();
        }

        public static void Initialize()
        {
            string path = Files.GetFilePath("bodyconv.def");
            if (path == null)
                return;

            List<int> list1 = new List<int>();
            List<int> list2 = new List<int>();
            List<int> list3 = new List<int>();
            List<int> list4 = new List<int>();

            int max1 = 0, max2 = 0, max3 = 0, max4 = 0;

            using (var ip = new StreamReader(path))
            {
                while (ip.ReadLine() is { } line)
                {
                    line = line.Trim();

                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("\""))
                        continue;

                    try
                    {
                        string[] split = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (!int.TryParse(split[0], out int original))
                            continue;

                        if (!int.TryParse(split[1], out int anim2)) anim2 = -1;
                        if (!int.TryParse(split[2], out int anim3)) anim3 = -1;
                        if (!int.TryParse(split[3], out int anim4)) anim4 = -1;
                        if (!int.TryParse(split[4], out int anim5)) anim5 = -1;

                        if (anim2 != -1)
                        {
                            if (anim2 == 68) anim2 = 122;
                            if (original > max1) max1 = original;
                            list1.Add(original);
                            list1.Add(anim2);
                        }

                        if (anim3 != -1)
                        {
                            if (original > max2) max2 = original;
                            list2.Add(original);
                            list2.Add(anim3);
                        }

                        if (anim4 != -1)
                        {
                            if (original > max3) max3 = original;
                            list3.Add(original);
                            list3.Add(anim4);
                        }

                        if (anim5 != -1)
                        {
                            if (original > max4) max4 = original;
                            list4.Add(original);
                            list4.Add(anim5);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            Table1 = CreateTable(max1, list1);
            Table2 = CreateTable(max2, list2);
            Table3 = CreateTable(max3, list3);
            Table4 = CreateTable(max4, list4);
        }

        private static int[] CreateTable(int max, List<int> list)
        {
            var table = new int[max + 1];
            for (int i = 0; i < table.Length; ++i)
                table[i] = -1;

            for (int i = 0; i < list.Count; i += 2)
                table[list[i]] = list[i + 1];

            return table;
        }

        public static bool Contains(int body)
        {
            if (Table1 != null && body >= 0 && body < Table1.Length && Table1[body] != -1) return true;
            if (Table2 != null && body >= 0 && body < Table2.Length && Table2[body] != -1) return true;
            if (Table3 != null && body >= 0 && body < Table3.Length && Table3[body] != -1) return true;
            if (Table4 != null && body >= 0 && body < Table4.Length && Table4[body] != -1) return true;
            return false;
        }

        public static int Convert(ref int body)
        {
            if (Table1 != null && body >= 0 && body < Table1.Length)
            {
                int val = Table1[body];
                if (val != -1) { body = val; return 2; }
            }

            if (Table2 != null && body >= 0 && body < Table2.Length)
            {
                int val = Table2[body];
                if (val != -1) { body = val; return 3; }
            }

            if (Table3 != null && body >= 0 && body < Table3.Length)
            {
                int val = Table3[body];
                if (val != -1) { body = val; return 4; }
            }

            if (Table4 != null && body >= 0 && body < Table4.Length)
            {
                int val = Table4[body];
                if (val == -1) return 1;
                body = val;
                return 5;
            }

            return 1;
        }

        public static int GetTrueBody(int fileType, int index)
        {
            switch (fileType)
            {
                default:
                case 1: return index;
                case 2:
                    if (Table1 != null && index >= 0)
                        for (int i = 0; i < Table1.Length; ++i)
                            if (Table1[i] == index) return i;
                    break;
                case 3:
                    if (Table2 != null && index >= 0)
                        for (int i = 0; i < Table2.Length; ++i)
                            if (Table2[i] == index) return i;
                    break;
                case 4:
                    if (Table3 != null && index >= 0)
                        for (int i = 0; i < Table3.Length; ++i)
                            if (Table3[i] == index) return i;
                    break;
                case 5:
                    if (Table4 != null && index >= 0)
                        for (int i = 0; i < Table4.Length; ++i)
                            if (Table4[i] == index) return i;
                    break;
            }
            return -1;
        }
    }
}