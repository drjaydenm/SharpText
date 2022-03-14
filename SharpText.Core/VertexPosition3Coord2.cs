using System.Numerics;

namespace SharpText.Core
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

        public override string ToString()
        {
            return $"<{Position.X}, {Position.Y}, {Position.Z}> <{Coord.X}, {Coord.Y}>";
        }
    }
}
