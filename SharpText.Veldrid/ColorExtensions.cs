using SharpText.Core;
using Veldrid;

namespace SharpText.Veldrid
{
    public static class ColorExtensions
    {
        public static RgbaFloat ToVeldridColor(this Color color)
        {
            return new RgbaFloat(color.R, color.G, color.B, color.A);
        }

        public static Color ToSharpTextColor(this RgbaFloat color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }
}
