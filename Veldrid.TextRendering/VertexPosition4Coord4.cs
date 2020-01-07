using System.Numerics;

namespace Veldrid.TextRendering
{
    public struct VertexPosition4Coord4
    {
        public Vector4 Position;
        public Vector4 Coord;

        public VertexPosition4Coord4(Vector4 position, Vector4 coord)
        {
            Position = position;
            Coord = coord;
        }

        public const uint SizeInBytes = 32;

        public static VertexLayoutDescription LayoutDescription = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float4),
            new VertexElementDescription("Coord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
        );
    }
}
