using System.Numerics;

namespace Veldrid.TextRendering
{
    public struct VertexPosition3Coord2
    {
        public Vector3 Position;
        public Vector2 Coord;

        public VertexPosition3Coord2(Vector3 position, Vector2 coord)
        {
            Position = position;
            Coord = coord;
        }

        public const uint SizeInBytes = 24;

        public static VertexLayoutDescription LayoutDescription = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("Coord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)
        );
    }
}
