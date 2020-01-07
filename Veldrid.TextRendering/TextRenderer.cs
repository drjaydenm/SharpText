using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid.SPIRV;

namespace Veldrid.TextRendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TextProperties
    {
        public float ThicknessAndMode;
        private float _padding1;
        private float _padding2;
        private float _padding3;
        public Matrix4x4 Transform;
        public Vector4 GlyphColor;
        public Vector4 Rectangle;
    }

    public class TextRenderer
    {
        private readonly GraphicsDevice graphicsDevice;
        private readonly Font font;

        private DeviceBuffer vertexBuffer;
        private DeviceBuffer textPropertiesBuffer;
        private Pipeline pipeline;
        private VertexPosition4Coord4[] glyphVertices;
        private TextProperties textProperties;
        private ResourceSet textPropertiesSet;

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

                // Only the first letter for now
                break;
            }
        }

        public void Draw(CommandList commandList)
        {
            var requiredBufferSize = VertexPosition4Coord4.SizeInBytes * (uint)glyphVertices.Length;
            if (vertexBuffer.SizeInBytes < requiredBufferSize)
            {
                vertexBuffer.Dispose();
                vertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(
                    new BufferDescription(requiredBufferSize, BufferUsage.VertexBuffer));
            }

            commandList.UpdateBuffer(vertexBuffer, 0, glyphVertices);
            commandList.SetVertexBuffer(0, vertexBuffer);

            var matrixA = Matrix4x4.CreateScale(2f / graphicsDevice.SwapchainFramebuffer.Width, 2f / graphicsDevice.SwapchainFramebuffer.Height, 1f)
                * Matrix4x4.CreateTranslation(-1, 1, 0);
            var matrixB = Matrix4x4.CreateScale(1f / font.FontSize) * matrixA;
            textProperties.Transform = matrixB;
            commandList.UpdateBuffer(textPropertiesBuffer, 0, textProperties);

            commandList.SetPipeline(pipeline);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);

            commandList.Draw((uint)glyphVertices.Length);
        }

        private void Initialize()
        {
            var factory = graphicsDevice.ResourceFactory;

            vertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition4Coord4.SizeInBytes, BufferUsage.VertexBuffer));

            textPropertiesBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<TextProperties>(), BufferUsage.UniformBuffer));
            textProperties = new TextProperties
            {
                ThicknessAndMode = 0, // TODO support other modes
                Transform = new Matrix4x4(),
                GlyphColor = new Vector4(0, 0.5f, 1, 1),
                Rectangle = new Vector4(-1, -1, 1, 1)
            };
            graphicsDevice.UpdateBuffer(textPropertiesBuffer, 0, textProperties);

            ShaderDescription vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.GlyphTextVertex), "main");
            ShaderDescription fragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.GlyphTextFragment), "main");

            var shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

            var textPropertiesLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("TextPropertiesBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)));

            textPropertiesSet = factory.CreateResourceSet(new ResourceSetDescription(
                textPropertiesLayout,
                textPropertiesBuffer));

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
                    vertexLayouts: new[] { VertexPosition4Coord4.LayoutDescription },
                    shaders: shaders),
                resourceLayouts: new ResourceLayout[] { textPropertiesLayout },
                outputs: graphicsDevice.SwapchainFramebuffer.OutputDescription
            );
            pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }
    }
}
