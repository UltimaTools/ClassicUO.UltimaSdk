using System;

namespace Ultima
{
    [Flags]
    public enum TileFlag : ulong
    {
        None = 0x00000000,
        Background = 0x00000001,
        Weapon = 0x00000002,
        Transparent = 0x00000004,
        Translucent = 0x00000008,
        Wall = 0x00000010,
        Damaging = 0x00000020,
        Impassable = 0x00000040,
        Wet = 0x00000080,
        Unknown1 = 0x00000100,
        Surface = 0x00000200,
        Bridge = 0x00000400,
        Generic = 0x00000800,
        Window = 0x00001000,
        NoShoot = 0x00002000,
        Foliage = 0x00020000,
        PartialHue = 0x00040000,
        Internal = 0x00010000,
        Container = 0x00200000,
        Wearable = 0x00400000,
        LightSource = 0x00800000,
        Animation = 0x01000000,
        HoverOver = 0x02000000,
        Armor = 0x08000000,
        Roof = 0x10000000,
        Door = 0x20000000,
    }
}
