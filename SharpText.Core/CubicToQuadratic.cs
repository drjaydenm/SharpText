using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharpText.Core
{

public static class CubicToQuadratic
{

    private const int MaxN = 100;
    private const double _2_3 = 2d / 3d;
    private const double _1_27 = 1 / 27;

    private static double Dot (Complex v1, Complex v2)
    {
        return (v1 * Complex.Conjugate(v2)).Real;
    }

    private static CubicCurve CalculateCubicCurve (Complex a, Complex b, Complex c, Complex d)
    {
        var p1 = (c / 3) + d;
        var p2 = (b + c) / 3 + p1;
        var p3 = a + d + c + b;
        return new CubicCurve(d, p1, p2, p3);
    }

    /*

@cython.cfunc
@cython.inline
@cython.locals(a=cython.complex, b=cython.complex, c=cython.complex, d=cython.complex)
@cython.locals(_1=cython.complex, _2=cython.complex, _3=cython.complex, _4=cython.complex)
def calc_cubic_points(a, b, c, d):
    _1 = d
    _2 = (c / 3.0) + d
    _3 = (b + c) / 3.0 + _2
    _4 = a + d + c + b
    return _1, _2, _3, _4

*/

    private static (Complex a, Complex b, Complex c, Complex d) CalculateCubicParameters (Complex p0, Complex p1, Complex p2, Complex p3)
    {
        var c = (p1 - p0) * 3;
        var b = (p2 - p1) * 3 - c;
        var d = p0;
        var a = p3 - d - c - b;
        return (a, b, c, d);
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
    private static IEnumerable<CubicCurve> SplitCubicIntoNIterations (Complex p0, Complex p1, Complex p2, Complex p3, int n)
    {
        // Hand-coded special-cases
        if (n == 2) return SplitCubicIntoTwo(p0, p1, p2, p3);
        if (n == 3) return SplitCubicIntoThree(p0, p1, p2, p3);
        if (n == 4)
        {
            var ab = SplitCubicIntoTwo(p0, p1, p2, p3).ToArray();
            return SplitCubicIntoTwo(ab[0].p0, ab[0].p1, ab[0].p2, ab[0].p3).Concat(SplitCubicIntoTwo(ab[1].p0, ab[1].p1, ab[1].p2, ab[1].p3));
        }

        if (n == 6)
        {
            var ab = SplitCubicIntoTwo(p0, p1, p2, p3).ToArray();
            return SplitCubicIntoThree(ab[0].p0, ab[0].p1, ab[0].p2, ab[0].p3).Concat(SplitCubicIntoThree(ab[1].p0, ab[1].p1, ab[1].p2, ab[1].p3));
        }

        return SplitCubicIntoNIterator(p0, p1, p2, p3, n);
    }

    private static IEnumerable<CubicCurve> SplitCubicIntoNIterator (Complex p0, Complex p1, Complex p2, Complex p3, int n)
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

    private static IEnumerable<CubicCurve> SplitCubicIntoTwo (Complex p0, Complex p1, Complex p2, Complex p3)
    {
        var mid = (p0 + 3 * (p1 + p2) + p3) * 0.125;
        var deriv3 = (p3 + p2 - p1 - p0) * 0.125;

        yield return new CubicCurve(p0, (p0 + p1) * 0.5, mid - deriv3, mid);
        yield return new CubicCurve(mid, mid + deriv3, (p2 + p3) * 0.5, p3);
    }

    private static IEnumerable<CubicCurve> SplitCubicIntoThree (Complex p0, Complex p1, Complex p2, Complex p3)
    {
        var mid1 = (8 * p0 + 12 * p1 + 6 * p2 + p3) * _1_27;
        var deriv1 = (p3 + 3 * p2 - 4 * p0) * _1_27;
        var mid2 = (p0 + 6 * p1 + 12 * p2 + 8 * p3) * _1_27;
        var deriv2 = (4 * p3 - 3 * p1 - p0) * _1_27;

        yield return new CubicCurve(p0, (2 * p0 + p1) / 3, mid1 - deriv1, mid1);
        yield return new CubicCurve(mid1, mid1 + deriv1, mid2 - deriv2, mid2);
        yield return new CubicCurve(mid2, mid2 + deriv2, (p2 + 2 * p3) / 3, p3);
    }

    /// <summary>
    /// Approximate a control point.
    /// </summary>
    /// <param name="t"> t (double): Position of control point.</param>
    /// <param name="p0">p0 (complex): Start point of curve.</param>
    /// <param name="p1">p1 (complex): First handle of curve.</param>
    /// <param name="p2">p2 (complex): Second handle of curve</param>
    /// <param name="p3">p3 (complex): End point of curve.</param>
    /// <returns>complex: Location of candidate control point on quadratic curve.</returns>
    private static Complex CubicApproximateControl (double t, Complex p0, Complex p1, Complex p2, Complex p3)
    {
        var newP1 = p0 + (p1 - p0) * 1.5;
        var newP2 = p3 + (p2 - p3) * 1.5;
        return newP1 + (newP2 - newP1) * t;
    }

    /// <summary>
    /// Calculate the intersection of two lines.
    /// </summary>
    /// <param name="controlPoints">
    /// a (complex): Start point of first line.
    /// b (complex): End point of first line.
    /// c (complex): Start point of second line.
    /// d (complex): End point of second line.
    /// </param>
    /// <returns>complex: Location of intersection if one present, ``complex(NaN,NaN)``
    /// if no intersection was found.</returns>
    private static bool TryCalculateIntersection (Complex a, Complex b, Complex c, Complex d, out Complex intersectionPoint)
    {
        var ab = b - a;
        var cd = d - c;
        var p = ab * Complex.ImaginaryOne;

        var dotPac = Dot(p, a - c);
        var dotPcd = Dot(p, cd);
        if (dotPcd == 0) return false;

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
    /// <returns>True if the cubic Bezier ``p`` entirely lies within a distance ``tolerance`` of the origin, False otherwise.</returns>
    private static bool CubicFarthestFitsInside (Complex p0, Complex p1, Complex p2, Complex p3, float maxError)
    {
        // First check p2 then p1, as p2 has higher error early on.
        if (Complex.Abs(p2) <= maxError && Complex.Abs(p1) <= maxError) return true;

        // Split
        var mid = (p0 + 3 * (p1 + p2) + p3) * 0.125;
        if (Complex.Abs(mid) > maxError) return false;

        var deriv3 = (p3 + p2 - p1 - p0) * 0.125;
        return CubicFarthestFitsInside(p0, (p0 + p1) * 0.5, mid - deriv3, mid, maxError)
               && CubicFarthestFitsInside(mid, mid + deriv3, (p2 + p3) * 0.5, p3, maxError);
    }

    /// <summary>
    /// Approximate a cubic Bezier with a single quadratic within a given tolerance.
    /// </summary>
    /// <param name="controlPoints">Four complex numbers representing control points of the cubic Bezier curve.</param>
    /// <param name="maxError">Permitted deviation from the original curve.</param>
    /// <returns>Three complex numbers representing control points of the quadratic
    /// curve if it fits within the given tolerance, or ``None`` if no suitable
    /// curve could be calculated.</returns>
    private static bool CubicApproximateQuadratic (CubicCurve controlPoints, float maxError, out QuadraticCurve quadraticCurve)
    {
        // we define 2/3 as a keyword argument so that it will be evaluated only
        // once but still in the scope of this function

        if (!TryCalculateIntersection(controlPoints.p0, controlPoints.p1, controlPoints.p2, controlPoints.p3, out var q1)) return false;

        var c0 = controlPoints.p0;
        var c3 = controlPoints.p3;
        var c1 = c0 + (q1 - c0) * _2_3;
        var c2 = c3 + (q1 - c3) * _2_3;

        if (CubicFarthestFitsInside(0, c1 - controlPoints.p1, c2 - controlPoints.p2, 0, maxError)) return false;

        quadraticCurve = new QuadraticCurve(c0, q1, c3);
        return true;
    }

    /// <summary>
    /// Approximate a cubic Bezier curve with a spline of n quadratics.
    /// </summary>
    /// <param name="curve">Four complex numbers representing control points of the cubic Bezier curve.</param>
    /// <param name="n">Number of quadratic Bezier curves in the spline.</param>
    /// <param name="maxError">Permitted deviation from the original curve.</param>
    /// <returns>A list of ``n+2`` complex numbers, representing control points of the
    /// quadratic spline if it fits within the given tolerance, or ``None`` if
    /// no suitable spline could be calculated.</returns>
    private static bool CubicApproximateSpline (CubicCurve curve, int n, float maxError, out List<Complex> spline)
    {
        // we define 2/3 as a keyword argument so that it will be evaluated only
        // once but still in the scope of this function
        if (n == 1)
        {
            if (CubicApproximateQuadratic(curve, maxError, out var quadraticCurve))
            {
                spline = new List<Complex> { quadraticCurve.p0, quadraticCurve.p1, quadraticCurve.p2 };
                return true;
            }
        }

        var cubics = SplitCubicIntoNIterations(curve.p0, curve.p1, curve.p2, curve.p3, n).ToArray();

        // calculate the spline of quadratics and check errors at the same time.
        var nextCurve = cubics[0];
        var nextQ1 = CubicApproximateControl(0, nextCurve.p0, nextCurve.p1, nextCurve.p2, nextCurve.p3);
        var q2 = curve.p0;
        var d1 = Complex.Zero;

        spline = new List<Complex> { curve.p0, nextQ1 };
        for (int i = 1; i < n + 1; i++)
        {
            // Current cubic to convert
            var (_, c1, c2, c3) = nextCurve;

            // Current quadratic approximation of current cubic
            var q0 = q2;
            var q1 = nextQ1;
            if (i < n)
            {
                nextCurve = cubics[i];
                nextQ1 = CubicApproximateControl(i / (n - 1), nextCurve.p0, nextCurve.p1, nextCurve.p2, nextCurve.p3);
                spline.Add(nextQ1);
                q2 = (q1 + nextQ1) * 0.5f;
            }
            else
            {
                q2 = c3;
            }

            // End-point deltas
            var d0 = d1;
            d1 = q2 - c3;

            var f1 = q0 + (q1 - q0) * _2_3 - c1;
            var f2 = q2 + (q1 - q2) * _2_3 - c2;
            if (Complex.Abs(d1) > maxError || !CubicFarthestFitsInside(d0, f1, f2, d1, maxError)) return false;
        }

        spline.Add(curve.p3);
        return true;
    }

    /// <summary>
    /// Approximate a cubic Bezier curve with a spline of n quadratics.
    /// </summary>
    /// <param name="controlPoints">Four vectors representing the control points of the cubic Bezier curve</param>
    /// <param name="maxError">Permitted deviation from the original curve</param>
    /// <returns>A list of 2D tuples, representing control points of the quadratic
    /// spline if it fits within the given tolerance, or null if no
    /// suitable spline could be calculated.</returns>
    public static bool CurveToQuadratic (CubicCurve curve, float maxError, out List<Complex> spline)
    {
        for (int n = 1; n < MaxN + 1; n++)
        {
            if (CubicApproximateSpline(curve, n, maxError, out spline)) return true;
        }

        spline = new List<Complex>();
        return false;
    }
/*


@cython.locals(l=cython.int, last_i=cython.int, i=cython.int)
def curves_to_quadratic(curves, max_errors):
    """Return quadratic Bezier splines approximating the input cubic Beziers.
    Args:
        curves: A sequence of *n* curves, each curve being a sequence of four
            2D tuples.
        max_errors: A sequence of *n* floats representing the maximum permissible
            deviation from each of the cubic Bezier curves.
    Example::
        >>> curves_to_quadratic( [
        ...   [ (50,50), (100,100), (150,100), (200,50) ],
        ...   [ (75,50), (120,100), (150,75),  (200,60) ]
        ... ], [1,1] )
        [[(50.0, 50.0), (75.0, 75.0), (125.0, 91.66666666666666), (175.0, 75.0), (200.0, 50.0)], [(75.0, 50.0), (97.5, 75.0), (135.41666666666666, 82.08333333333333), (175.0, 67.5), (200.0, 60.0)]]
    The returned splines have "implied oncurve points" suitable for use in
    TrueType ``glif`` outlines - i.e. in the first spline returned above,
    the first quadratic segment runs from (50,50) to
    ( (75 + 125)/2 , (120 + 91.666..)/2 ) = (100, 83.333...).
    Returns:
        A list of splines, each spline being a list of 2D tuples.
    Raises:
        fontTools.cu2qu.Errors.ApproxNotFoundError: if no suitable approximation
        can be found for all curves with the given parameters.
    """

    curves = [[complex(*p) for p in curve] for curve in curves]
    assert len(max_errors) == len(curves)

    l = len(curves)
    splines = [None] * l
    last_i = i = 0
    n = 1
    while True:
        spline = cubic_approx_spline(curves[i], n, max_errors[i])
        if spline is None:
            if n == MAX_N:
                break
            n += 1
            last_i = i
            continue
        splines[i] = spline
        i = (i + 1) % l
        if i == last_i:
            # done. go home
            return [[(s.real, s.imag) for s in spline] for spline in splines]

    raise ApproxNotFoundError(curves)

     */

    private struct QuadraticCurve
    {

        public Complex p0;
        public Complex p1;
        public Complex p2;

        public QuadraticCurve (Complex p0, Complex p1, Complex p2)
        {
            this.p0 = p0;
            this.p1 = p1;
            this.p2 = p2;
        }

    }

    public struct CubicCurve
    {

        public Complex p0;
        public Complex p1;
        public Complex p2;
        public Complex p3;

        public CubicCurve (Complex p0, Complex p1, Complex p2, Complex p3)
        {
            this.p0 = p0;
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
        }

        public void Deconstruct (out Complex c0, out Complex c1, out Complex c2, out Complex c3)
        {
            c0 = p0;
            c1 = p1;
            c2 = p2;
            c3 = p3;
        }

    }

}

}
