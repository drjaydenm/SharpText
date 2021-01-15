using System;

namespace SharpText.Core
{
    public struct Color : IEquatable<Color>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public Color(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override bool Equals(object obj)
        {
            return Equals((Color)obj);
        }

        public bool Equals(Color other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() + G.GetHashCode() + B.GetHashCode() + A.GetHashCode();
        }

        public static bool operator ==(Color val1, Color val2)
        {
            if ((object)val1 == null)
                return (object)val2 == null;

            return val1.Equals(val2);
        }

        public static bool operator !=(Color val1, Color val2)
        {
            return !(val1 == val2);
        }
    }
}
