using System.Numerics;
using System.Text;
using Veldrid.SPIRV;

namespace Veldrid.TextRendering
{
    public class TextRenderer
    {
        private readonly GraphicsDevice graphicsDevice;
        private readonly Font font;

        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private Pipeline pipeline;
        private VertexPositionColor[] glyphVertices;

        public TextRenderer(GraphicsDevice graphicsDevice, Font font)
        {
            this.graphicsDevice = graphicsDevice;
            this.font = font;

            Initialize();
        }

        public void DrawText(string text, Vector2 coords)
        {
            foreach (var letter in text)
            {
                var fontUnitsPerEm = font.UnitsPerEm;
                var glyph = font.GetGlyphByCharacter(letter);
                var scale = 1f / font.FontSize;

                glyphVertices = font.GlyphToVertices(glyph);
                for (var i = 0; i < glyphVertices.Length; i++)
                {
                    glyphVertices[i].Position *= scale;
                }

                // Only the first letter for now
                break;
            }
        }

        public void Draw(CommandList commandList)
        {
            if (vertexBuffer.SizeInBytes < VertexPositionColor.SizeInBytes * glyphVertices.Length)
            {
                vertexBuffer.Dispose();
                vertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(
                    new BufferDescription((uint)glyphVertices.Length * VertexPositionColor.SizeInBytes, BufferUsage.VertexBuffer));
            }

            commandList.UpdateBuffer(vertexBuffer, 0, glyphVertices);

            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            commandList.SetPipeline(pipeline);
            commandList.Draw((uint)glyphVertices.Length);
        }

        private void Initialize()
        {
            var factory = graphicsDevice.ResourceFactory;

            VertexPositionColor[] quadVertices =
            {
                new VertexPositionColor(new Vector2(-.75f, .75f), RgbaFloat.Red),
                new VertexPositionColor(new Vector2(.75f, .75f), RgbaFloat.Green),
                new VertexPositionColor(new Vector2(-.75f, -.75f), RgbaFloat.Blue),
                new VertexPositionColor(new Vector2(.75f, -.75f), RgbaFloat.Yellow)
            };
            ushort[] quadIndices = { 0, 1, 2, 3 };

            vertexBuffer = factory.CreateBuffer(new BufferDescription(4 * VertexPositionColor.SizeInBytes, BufferUsage.VertexBuffer));
            indexBuffer = factory.CreateBuffer(new BufferDescription(4 * sizeof(ushort), BufferUsage.IndexBuffer));

            graphicsDevice.UpdateBuffer(vertexBuffer, 0, quadVertices);
            graphicsDevice.UpdateBuffer(indexBuffer, 0, quadIndices);

            ShaderDescription vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.VertexShader), "main");
            ShaderDescription fragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.FragmentShader), "main");

            var shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

            var pipelineDescription = new GraphicsPipelineDescription(
                blendState: BlendStateDescription.SingleOverrideBlend,
                depthStencilStateDescription: new DepthStencilStateDescription(
                    depthTestEnabled: true,
                    depthWriteEnabled: true,
                    comparisonKind: ComparisonKind.LessEqual),
                rasterizerState: new RasterizerStateDescription(
                    cullMode: FaceCullMode.Back,
                    fillMode: PolygonFillMode.Solid,
                    frontFace: FrontFace.Clockwise,
                    depthClipEnabled: true,
                    scissorTestEnabled: false),
                primitiveTopology: PrimitiveTopology.TriangleStrip,
                shaderSet: new ShaderSetDescription(
                    vertexLayouts: new[] { VertexPositionColor.LayoutDescription },
                    shaders: shaders),
                resourceLayouts: System.Array.Empty<ResourceLayout>(),
                outputs: graphicsDevice.SwapchainFramebuffer.OutputDescription
            );
            pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }
    }
}
