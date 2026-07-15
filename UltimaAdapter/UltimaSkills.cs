using System.Collections.Generic;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public sealed class SkillInfo
    {
        public int Index { get; set; }
        public string Name { get; }
        public bool IsAction { get; }
        public int Extra { get; }

        internal SkillInfo(int index, string name, bool isAction)
        {
            Index = index;
            Name = name;
            IsAction = isAction;
            Extra = 0;
        }

        public SkillInfo(int nr, string name, bool action, int extra)
        {
            Index = nr;
            Name = name;
            IsAction = action;
            Extra = extra;
        }
    }

    public static class Skills
    {
        public static SkillInfo GetSkill(int index)
        {
            var skills = Files.Manager?.Skills?.Skills;
            if (skills != null && index >= 0 && index < skills.Count)
            {
                var s = skills[index];
                return new SkillInfo(s.Index, s.Name, s.HasAction);
            }
            return null;
        }

        public static List<SkillInfo> SkillEntries
        {
            get
            {
                var result = new List<SkillInfo>();
                var skills = Files.Manager?.Skills?.Skills;
                if (skills != null)
                {
                    foreach (var s in skills)
                        result.Add(new SkillInfo(s.Index, s.Name, s.HasAction));
                }
                return result;
            }
        }

        public static void Reload() { }

        public static void Save(string path)
        {
            // Stub - skills save not implemented
        }
    }
}
