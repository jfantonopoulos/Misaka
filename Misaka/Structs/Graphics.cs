using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Structs
{
    public struct RGBA
    {
        byte Red;
        byte Green;
        byte Blue;
        byte Alpha;

        public RGBA(byte r, byte g, byte b, byte a)
        {
            Red = r;
            Green = g;
            Blue = b;
            Alpha = a;
        }

        public RGBA(byte r, byte g, byte b) : this(r, g, b, 255) { }
    }
}
