using System.Numerics;

namespace Veldrid.TextRendering
{
    public struct VertexPosition4
    {
        public Vector4 Position;

        public VertexPosition4(Vector4 position)
        {
            Position = position;
        }

        public const uint SizeInBytes = 16;

        public static VertexLayoutDescription LayoutDescription = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
        );
    }
}
