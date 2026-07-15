using System.Collections.Generic;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public sealed class SkillInfo
    {
        public int Index { get; }
        public string Name { get; }
        public bool IsAction { get; }

        internal SkillInfo(int index, string name, bool isAction)
        {
            Index = index;
            Name = name;
            IsAction = isAction;
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
    }
}
