using System.Numerics;

namespace SharpText.Core
{
    internal struct CubicCurve
    {
        public Complex Point0;
        public Complex Point1;
        public Complex Point2;
        public Complex Point3;

        public CubicCurve(Complex p0, Complex p1, Complex p2, Complex p3)
        {
            Point0 = p0;
            Point1 = p1;
            Point2 = p2;
            Point3 = p3;
        }

        public void Deconstruct(out Complex c0, out Complex c1, out Complex c2, out Complex c3)
        {
            c0 = Point0;
            c1 = Point1;
            c2 = Point2;
            c3 = Point3;
        }
    }
}
