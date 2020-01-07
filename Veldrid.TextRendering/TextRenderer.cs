using System.Numerics;
using System.Text;
using Veldrid.SPIRV;

namespace Veldrid.TextRendering
{
    public class TextRenderer
    {
        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private Pipeline pipeline;

        public void Initialize(GraphicsDevice graphicsDevice)
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

        public void Draw(CommandList commandList)
        {
            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            commandList.SetPipeline(pipeline);
            commandList.DrawIndexed(
                indexCount: 4,
                instanceCount: 1,
                indexStart: 0,
                vertexOffset: 0,
                instanceStart: 0);
        }
    }
}
