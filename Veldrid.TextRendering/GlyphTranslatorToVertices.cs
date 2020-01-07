using System.Collections.Generic;
using System.Numerics;
using Typography.OpenFont;

namespace Veldrid.TextRendering
{
    public enum TriangleKind
    {
        Solid,
        QuadraticCurve
    }

    public class GlyphTranslatorToVertices : IGlyphTranslator
    {
        public VertexPositionColor[] ResultingVertices => vertices.ToArray();

        private float lastMoveX;
        private float lastMoveY;
        private float lastX;
        private float lastY;
        private short contourCount;
        private List<VertexPositionColor> vertices;

        public void BeginRead(int countourCount)
        {
            Reset();
        }

        public void EndRead()
        {
        }

        public void MoveTo(float x, float y)
        {
            lastX = lastMoveX = x;
            lastY = lastMoveY = y;
            contourCount = 0;
        }

        public void CloseContour()
        {
        }

        public void Curve3(float x1, float y1, float x2, float y2)
        {
            if (++contourCount >= 2)
            {
                AppendTriangle(lastMoveX, lastMoveY, lastX, lastY, x2, y2, TriangleKind.Solid);
            }

            AppendTriangle(lastX, lastY, x1, y1, x2, y2, TriangleKind.QuadraticCurve);
            lastX = x2;
            lastY = y2;
        }

        public void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            if (++contourCount >= 2)
            {
                AppendTriangle(lastMoveX, lastMoveY, lastX, lastY, x2, y2, TriangleKind.Solid);
            }

            AppendTriangle(lastX, lastY, x1, y1, x2, y2, TriangleKind.QuadraticCurve);
            AppendTriangle(lastX, lastY, x2, y2, x3, y3, TriangleKind.QuadraticCurve);
            lastX = x3;
            lastY = y3;
        }

        public void LineTo(float x, float y)
        {
            if (++contourCount >= 2)
            {
                AppendTriangle(lastMoveX, lastMoveY, lastX, lastY, x, y, TriangleKind.Solid);
            }

            lastX = x;
            lastY = y;
        }

        public void Reset()
        {
            vertices = new List<VertexPositionColor>();
            lastMoveX = lastMoveY = lastX = lastY = 0;
            contourCount = 0;
        }

        private void AppendTriangle(float x1, float y1, float x2, float y2, float x3, float y3, TriangleKind kind)
        {
            AppendVertex(x1, y1);
            AppendVertex(x2, y2);
            AppendVertex(x3, y3);
        }

        private void AppendVertex(float x, float y)
        {
            vertices.Add(new VertexPositionColor(new Vector2(x, y), RgbaFloat.Black));
        }
    }
}
