using System.Numerics;

namespace Veldrid.TextRendering
{
    public struct VertexPosition2
    {
        public Vector2 Position;

        public VertexPosition2(Vector2 position)
        {
            Position = position;
        }

        public const uint SizeInBytes = 8;

        public static VertexLayoutDescription LayoutDescription = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2)
        );
    }
}
