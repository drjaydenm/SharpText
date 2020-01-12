using System.Numerics;
using Veldrid;

namespace SharpText
{
    public struct VertexPosition2
    {
        public Vector2 Position;

        public VertexPosition2(Vector2 position)
        {
            Position = position;
        }

        public const uint SizeInBytes = 8;

        public override string ToString()
        {
            return $"<{Position.X}, {Position.Y}>";
        }

        public static VertexLayoutDescription LayoutDescription = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)
        );
    }
}
