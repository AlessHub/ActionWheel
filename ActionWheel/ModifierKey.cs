using System;

namespace ActionWheel;

[Flags]
public enum ModifierKey
{
    None  = 0,
    Ctrl  = 1,
    Shift = 2,
    Alt   = 4,
}
