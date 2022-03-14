using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharpText.Core
{
    /// <summary>
    /// Converted and contributed by KuroiRoy (Roy Zwart) from this Python implementation. Thanks!
    /// https://github.com/fonttools/fonttools/blob/8ab6af03c89726cf80ca3c4b755ae1bd0038b5da/Lib/fontTools/cu2qu/cu2qu.py
    /// </summary>
    public static class CubicToQuadraticConverter
    {
        private const int MaxN = 100;
        private const double TwoOverThree = 2d / 3d;
        private const double OneOverTwentySeven = 1 / 27;

        /// <summary>
        /// Approximate a cubic Bezier curve with a spline of n quadratics.
        /// </summary>
        /// <param name="curve">The four vectors representing the control points of the cubic Bezier curve</param>
        /// <param name="maxError">Permitted deviation from the original curve</param>
        /// <param name="spline">A list of 2D tuples, representing control points of the quadratic
        /// spline if it fits within the given tolerance, or null if no
        /// suitable spline could be calculated</param>
        /// <returns>True if the curve could be converted, else false</returns>
        internal static bool CurveToQuadratic(CubicCurve curve, float maxError, out Complex[] spline)
        {
            spline = null;

            for (var n = 1; n < MaxN + 1; n++)
            {
                if (CubicApproximateSpline(curve, n, maxError, out spline))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Approximate a cubic Bezier curve with a spline of n quadratics.
        /// </summary>
        /// <param name="curve">The four complex numbers representing control points of the cubic Bezier curve.</param>
        /// <param name="n">Number of quadratic Bezier curves in the spline.</param>
        /// <param name="maxError">Permitted deviation from the original curve.</param>
        /// <param name="spline">A list of `n+2` complex numbers, representing control points of the
        /// quadratic spline if it fits within the given tolerance, or null if
        /// no suitable spline could be calculated.</param>
        /// <returns>True if the curve could be converted, else false</returns>
        private static bool CubicApproximateSpline(CubicCurve curve, int n, float maxError, out Complex[] spline)
        {
            spline = null;

            if (n == 1)
            {
                if (CubicApproximateQuadratic(curve, maxError, out var quadraticCurve))
                {
                    spline = new Complex[] { quadraticCurve.Point0, quadraticCurve.Point1, quadraticCurve.Point2 };
                    return true;
                }
            }

            var cubics = SplitCubicIntoNIterations(curve.Point0, curve.Point1, curve.Point2, curve.Point3, n).ToArray();

            // Calculate the spline of quadratics and check errors at the same time
            var nextCurve = cubics[0];
            var nextQ1 = CubicApproximateControl(0, nextCurve.Point0, nextCurve.Point1, nextCurve.Point2, nextCurve.Point3);
            var q2 = curve.Point0;
            var d1 = Complex.Zero;

            var splineTemp = new List<Complex> { curve.Point0, nextQ1 };
            for (var i = 1; i < n + 1; i++)
            {
                // Current cubic to convert
                var (_, c1, c2, c3) = nextCurve;

                // Current quadratic approximation of current cubic
                var q0 = q2;
                var q1 = nextQ1;

                if (i < n)
                {
                    nextCurve = cubics[i];
                    nextQ1 = CubicApproximateControl(i / (n - 1), nextCurve.Point0, nextCurve.Point1, nextCurve.Point2, nextCurve.Point3);
                    splineTemp.Add(nextQ1);
                    q2 = (q1 + nextQ1) * 0.5f;
                }
                else
                {
                    q2 = c3;
                }

                // End-point deltas
                var d0 = d1;
                d1 = q2 - c3;

                var f1 = q0 + (q1 - q0) * TwoOverThree - c1;
                var f2 = q2 + (q1 - q2) * TwoOverThree - c2;

                if (Complex.Abs(d1) > maxError || !CubicFarthestFitsInside(d0, f1, f2, d1, maxError))
                {
                    return false;
                }
            }

            splineTemp.Add(curve.Point3);
            spline = splineTemp.ToArray();

            return true;
        }

        /// <summary>
        /// Approximate a cubic Bezier with a single quadratic within a given tolerance.
        /// </summary>
        /// <param name="controlPoints">Four complex numbers representing control points of the cubic Bezier curve.</param>
        /// <param name="maxError">Permitted deviation from the original curve.</param>
        /// <param name="quadraticCurve">Three complex numbers representing control points of the quadratic
        /// curve if it fits within the given tolerance, or null if no suitable
        /// curve could be calculated.</param>
        /// <returns>True if the curve could be converted, else false</returns>
        private static bool CubicApproximateQuadratic(CubicCurve controlPoints, float maxError, out QuadraticCurve quadraticCurve)
        {
            if (!TryCalculateIntersection(controlPoints.Point0, controlPoints.Point1, controlPoints.Point2, controlPoints.Point3, out var q1))
            {
                return false;
            }

            var c0 = controlPoints.Point0;
            var c3 = controlPoints.Point3;
            var c1 = c0 + (q1 - c0) * TwoOverThree;
            var c2 = c3 + (q1 - c3) * TwoOverThree;

            if (CubicFarthestFitsInside(0, c1 - controlPoints.Point1, c2 - controlPoints.Point2, 0, maxError))
            {
                return false;
            }

            quadraticCurve = new QuadraticCurve(c0, q1, c3);
            return true;
        }

        /// <summary>
        /// Split a cubic Bezier into n equal parts.
        /// Splits the curve into `n` equal parts by curve time.
        /// (t=0..1/n, t=1/n..2/n, ...)
        /// </summary>
        /// <param name="p0">p0 (complex): Start point of curve.</param>
        /// <param name="p1">p1 (complex): First handle of curve.</param>
        /// <param name="p2">p2 (complex): Second handle of curve.</param>
        /// <param name="p3">p3 (complex): End point of curve.</param>
        /// <param name="n"></param>
        /// <returns>An iterator yielding the control points (four complex values) of the subcurves.</returns>
        private static IEnumerable<CubicCurve> SplitCubicIntoNIterations(Complex p0, Complex p1, Complex p2, Complex p3, int n)
        {
            // Hand-coded special-cases
            if (n == 2)
            {
                return SplitCubicIntoTwo(p0, p1, p2, p3);
            }

            if (n == 3)
            {
                return SplitCubicIntoThree(p0, p1, p2, p3);
            }

            if (n == 4)
            {
                var ab = SplitCubicIntoTwo(p0, p1, p2, p3).ToArray();
                return SplitCubicIntoTwo(ab[0].Point0, ab[0].Point1, ab[0].Point2, ab[0].Point3).Concat(SplitCubicIntoTwo(ab[1].Point0, ab[1].Point1, ab[1].Point2, ab[1].Point3));
            }

            if (n == 6)
            {
                var ab = SplitCubicIntoTwo(p0, p1, p2, p3).ToArray();
                return SplitCubicIntoThree(ab[0].Point0, ab[0].Point1, ab[0].Point2, ab[0].Point3).Concat(SplitCubicIntoThree(ab[1].Point0, ab[1].Point1, ab[1].Point2, ab[1].Point3));
            }

            return SplitCubicIntoNIterator(p0, p1, p2, p3, n);
        }

        private static IEnumerable<CubicCurve> SplitCubicIntoNIterator(Complex p0, Complex p1, Complex p2, Complex p3, int n)
        {
            var (a, b, c, d) = CalculateCubicParameters(p0, p1, p2, p3);
            var dt = 1 / n;
            var delta2 = dt * dt;
            var delta3 = dt * delta2;

            for (int i = 0; i < n; i++)
            {
                var t1 = i * dt;
                var t1Pow2 = t1 * t1;

                var a1 = a * delta3;
                var b1 = (3 * a * t1 + b) * delta2;
                var c1 = (2 * b * t1 + c + 3 * a * t1Pow2) * dt;
                var d1 = a * t1 * t1Pow2 + b * t1Pow2 + c * t1 + d;

                yield return CalculateCubicCurve(a1, b1, c1, d1);
            }
        }

        private static IEnumerable<CubicCurve> SplitCubicIntoTwo(Complex p0, Complex p1, Complex p2, Complex p3)
        {
            var mid = (p0 + 3 * (p1 + p2) + p3) * 0.125;
            var deriv3 = (p3 + p2 - p1 - p0) * 0.125;

            yield return new CubicCurve(p0, (p0 + p1) * 0.5, mid - deriv3, mid);
            yield return new CubicCurve(mid, mid + deriv3, (p2 + p3) * 0.5, p3);
        }

        private static IEnumerable<CubicCurve> SplitCubicIntoThree(Complex p0, Complex p1, Complex p2, Complex p3)
        {
            var mid1 = (8 * p0 + 12 * p1 + 6 * p2 + p3) * OneOverTwentySeven;
            var deriv1 = (p3 + 3 * p2 - 4 * p0) * OneOverTwentySeven;
            var mid2 = (p0 + 6 * p1 + 12 * p2 + 8 * p3) * OneOverTwentySeven;
            var deriv2 = (4 * p3 - 3 * p1 - p0) * OneOverTwentySeven;

            yield return new CubicCurve(p0, (2 * p0 + p1) / 3, mid1 - deriv1, mid1);
            yield return new CubicCurve(mid1, mid1 + deriv1, mid2 - deriv2, mid2);
            yield return new CubicCurve(mid2, mid2 + deriv2, (p2 + 2 * p3) / 3, p3);
        }

        /// <summary>
        /// Approximate a control point.
        /// </summary>
        /// <param name="t">Position of control point.</param>
        /// <param name="p0">Start point of curve.</param>
        /// <param name="p1">First handle of curve.</param>
        /// <param name="p2">Second handle of curve</param>
        /// <param name="p3">End point of curve.</param>
        /// <returns>Location of candidate control point on quadratic curve.</returns>
        private static Complex CubicApproximateControl(double t, Complex p0, Complex p1, Complex p2, Complex p3)
        {
            var newP1 = p0 + (p1 - p0) * 1.5;
            var newP2 = p3 + (p2 - p3) * 1.5;

            return newP1 + (newP2 - newP1) * t;
        }

        /// <summary>
        /// Calculate the intersection of two lines.
        /// </summary>
        /// <param name="a">Start point of first line.</param>
        /// <param name="b">End point of first line.</param>
        /// <param name="c">Start point of second line.</param>
        /// <param name="d">End point of second line.</param>
        /// <param name="intersectionPoint">Location of intersection if one present, `Complex(NaN, NaN)`
        /// if no intersection was found.</param>
        /// <returns>True if the curve could be converted, else false</returns>
        private static bool TryCalculateIntersection(Complex a, Complex b, Complex c, Complex d, out Complex intersectionPoint)
        {
            var ab = b - a;
            var cd = d - c;
            var p = ab * Complex.ImaginaryOne;

            var dotPac = Dot(p, a - c);
            var dotPcd = Dot(p, cd);
            if (dotPcd == 0)
            {
                return false;
            }

            intersectionPoint = c + cd * (dotPac / dotPcd);
            return true;
        }

        /// <summary>
        /// Check if a cubic Bezier lies within a given distance of the origin.
        /// "Origin" means *the* origin (0,0), not the start of the curve. Note that no
        /// checks are made on the start and end positions of the curve; this function
        /// only checks the inside of the curve.
        /// </summary>
        /// <param name="p0">p0 (complex): Start point of curve.</param>
        /// <param name="p1">p1 (complex): First handle of curve.</param>
        /// <param name="p2">p2 (complex): Second handle of curve.</param>
        /// <param name="p3">p3 (complex): End point of curve.</param>
        /// <param name="maxError">tolerance (double): Distance from origin.</param>
        /// <returns>True if the cubic Bezier `p` entirely lies within a distance `tolerance` of the origin, false otherwise.</returns>
        private static bool CubicFarthestFitsInside(Complex p0, Complex p1, Complex p2, Complex p3, float maxError)
        {
            // First check p2 then p1, as p2 has higher error early on.
            if (Complex.Abs(p2) <= maxError && Complex.Abs(p1) <= maxError)
            {
                return true;
            }

            // Split
            var mid = (p0 + 3 * (p1 + p2) + p3) * 0.125;
            if (Complex.Abs(mid) > maxError)
            {
                return false;
            }

            var deriv3 = (p3 + p2 - p1 - p0) * 0.125;
            return CubicFarthestFitsInside(p0, (p0 + p1) * 0.5, mid - deriv3, mid, maxError)
                   && CubicFarthestFitsInside(mid, mid + deriv3, (p2 + p3) * 0.5, p3, maxError);
        }

        private static double Dot(Complex v1, Complex v2)
        {
            return (v1 * Complex.Conjugate(v2)).Real;
        }

        private static CubicCurve CalculateCubicCurve(Complex a, Complex b, Complex c, Complex d)
        {
            var p1 = (c / 3) + d;
            var p2 = (b + c) / 3 + p1;
            var p3 = a + d + c + b;

            return new CubicCurve(d, p1, p2, p3);
        }

        private static (Complex a, Complex b, Complex c, Complex d) CalculateCubicParameters(Complex p0, Complex p1, Complex p2, Complex p3)
        {
            var c = (p1 - p0) * 3;
            var b = (p2 - p1) * 3 - c;
            var d = p0;
            var a = p3 - d - c - b;

            return (a, b, c, d);
        }

        private struct QuadraticCurve
        {
            public Complex Point0;
            public Complex Point1;
            public Complex Point2;

            public QuadraticCurve(Complex p0, Complex p1, Complex p2)
            {
                Point0 = p0;
                Point1 = p1;
                Point2 = p2;
            }
        }
    }
}
