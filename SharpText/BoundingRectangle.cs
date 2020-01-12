using System;
using System.Numerics;

namespace SharpText
{
    public struct BoundingRectangle
    {
        public float StartX;
        public float StartY;
        public float EndX;
        public float EndY;
        public float Width => EndX - StartX;
        public float Height => EndY - StartY;

        public void Reset()
        {
            StartX = StartY = float.MaxValue;
            EndX = EndY = float.MinValue;
        }

        public void Include(float x, float y)
        {
            StartX = Math.Min(StartX, x);
            StartY = Math.Min(StartY, y);
            EndX = Math.Max(EndX, x);
            EndY = Math.Max(EndY, y);
        }

        public Vector4 ToVector4()
        {
            return new Vector4(StartX, StartY, EndX, EndY);
        }
    }
}
