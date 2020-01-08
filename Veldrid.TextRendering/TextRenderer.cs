using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid.SPIRV;

namespace Veldrid.TextRendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TextVertexProperties
    {
        public Matrix4x4 Transform;
        public Vector4 Rectangle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TextFragmentProperties
    {
        public float ThicknessAndMode;
        private float _padding1;
        private float _padding2;
        private float _padding3;
        public RgbaFloat GlyphColor;
    }

    public class TextRenderer
    {
        private readonly GraphicsDevice graphicsDevice;
        private readonly Font font;

        private DeviceBuffer glyphVertexBuffer;
        private DeviceBuffer quadVertexBuffer;
        private DeviceBuffer textVertexPropertiesBuffer;
        private DeviceBuffer textFragmentPropertiesBuffer;
        private Pipeline outputPipeline;
        private Pipeline glyphPipeline;
        private VertexPosition3Coord2[] glyphVertices = new VertexPosition3Coord2[0];
        private VertexPosition2[] quadVertices;
        private TextVertexProperties textVertexProperties;
        private TextFragmentProperties textFragmentProperties;
        private ResourceSet textPropertiesSet;
        private ResourceSet textTextureSet;
        private Texture glyphTexture;
        private TextureView glyphTextureView;
        private Framebuffer glyphTextureFramebuffer;
        private Shader[] glyphShaders;
        private Shader[] textShaders;

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

                //glyphVertices = font.GlyphToVertices(glyph);
                glyphVertices = KnownGlyphVertices.OpenSansLowerT.ToArray();
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
            var requiredBufferSize = VertexPosition3Coord2.SizeInBytes * (uint)glyphVertices.Length;
            if (glyphVertexBuffer.SizeInBytes < requiredBufferSize)
            {
                glyphVertexBuffer.Dispose();
                glyphVertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(
                    new BufferDescription(requiredBufferSize, BufferUsage.VertexBuffer));
            }

            commandList.UpdateBuffer(glyphVertexBuffer, 0, glyphVertices);
            commandList.SetVertexBuffer(0, glyphVertexBuffer);

            var matrixA = Matrix4x4.CreateTranslation(-1, 0, 0);
            textVertexProperties.Transform = matrixA;
            commandList.UpdateBuffer(textVertexPropertiesBuffer, 0, textVertexProperties);

            textFragmentProperties.GlyphColor = new RgbaFloat(0, 0.5f, 1, 1);
            commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);

            commandList.SetPipeline(glyphPipeline);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);
            commandList.SetFramebuffer(glyphTextureFramebuffer);

            commandList.ClearColorTarget(0, new RgbaFloat(0, 0, 0, 0));
            commandList.Draw((uint)glyphVertices.Length);

            // 2nd pass
            textFragmentProperties.GlyphColor = new RgbaFloat(0, 0, 0, 0);
            commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);

            commandList.SetPipeline(outputPipeline);
            commandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);
            commandList.SetGraphicsResourceSet(1, textTextureSet);
            commandList.SetVertexBuffer(0, quadVertexBuffer);
            commandList.Draw((uint)quadVertices.Length);
        }

        private void Initialize()
        {
            var factory = graphicsDevice.ResourceFactory;

            glyphVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition3Coord2.SizeInBytes, BufferUsage.VertexBuffer));
            quadVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition2.SizeInBytes * 4, BufferUsage.VertexBuffer));
            quadVertices = new VertexPosition2[]
            {
                new VertexPosition2(new Vector2(-1, -1)),
                new VertexPosition2(new Vector2(-1, 1)),
                new VertexPosition2(new Vector2(1, -1)),
                new VertexPosition2(new Vector2(1, 1))
            };
            graphicsDevice.UpdateBuffer(quadVertexBuffer, 0, quadVertices);

            textVertexPropertiesBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<TextVertexProperties>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            textFragmentPropertiesBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<TextFragmentProperties>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            textVertexProperties = new TextVertexProperties
            {
                Transform = new Matrix4x4(),
                Rectangle = new Vector4(-1, -1, 1, 1)
            };
            textFragmentProperties = new TextFragmentProperties
            {
                ThicknessAndMode = 0, // TODO support other modes
                GlyphColor = new RgbaFloat(0, 0.5f, 1, 1)
            };
            graphicsDevice.UpdateBuffer(textVertexPropertiesBuffer, 0, textVertexProperties);
            graphicsDevice.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);

            var colorFormat = graphicsDevice.SwapchainFramebuffer.OutputDescription.ColorAttachments[0].Format;
            glyphTexture = factory.CreateTexture(TextureDescription.Texture2D(graphicsDevice.SwapchainFramebuffer.Width, graphicsDevice.SwapchainFramebuffer.Height, 1, 1, colorFormat, TextureUsage.RenderTarget | TextureUsage.Sampled));
            glyphTextureView = factory.CreateTextureView(glyphTexture);
            glyphTextureFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(null, glyphTexture));

            CompileShaders(factory);

            var textPropertiesLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("TextVertexPropertiesBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("TextFragmentPropertiesBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

            var textTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("GlyphTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("GlyphTextureSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            textPropertiesSet = factory.CreateResourceSet(new ResourceSetDescription(
                textPropertiesLayout,
                textVertexPropertiesBuffer,
                textFragmentPropertiesBuffer));

            textTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                textTextureLayout,
                glyphTextureView,
                graphicsDevice.LinearSampler));

            var pipelineDescription = new GraphicsPipelineDescription(
                blendState: new BlendStateDescription(RgbaFloat.White,
                    new BlendAttachmentDescription(
                        blendEnabled: true,
                        sourceColorFactor: BlendFactor.Zero,
                        destinationColorFactor: BlendFactor.SourceColor,
                        colorFunction: BlendFunction.Add,
                        sourceAlphaFactor: BlendFactor.Zero,
                        destinationAlphaFactor: BlendFactor.SourceAlpha,
                        alphaFunction: BlendFunction.Add)),
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
                    vertexLayouts: new[] { VertexPosition2.LayoutDescription },
                    shaders: textShaders),
                resourceLayouts: new ResourceLayout[] { textPropertiesLayout, textTextureLayout },
                outputs: graphicsDevice.SwapchainFramebuffer.OutputDescription
            );
            outputPipeline = factory.CreateGraphicsPipeline(pipelineDescription);

            pipelineDescription.Outputs = new OutputDescription(null, new OutputAttachmentDescription(colorFormat));
            pipelineDescription.BlendState = BlendStateDescription.SingleAdditiveBlend;
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { textPropertiesLayout };
            pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleList;
            pipelineDescription.RasterizerState = new RasterizerStateDescription(
                cullMode: FaceCullMode.None,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.CounterClockwise,
                depthClipEnabled: false,
                scissorTestEnabled: false);
            pipelineDescription.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { VertexPosition3Coord2.LayoutDescription },
                shaders: glyphShaders);
            glyphPipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }

        private void CompileShaders(ResourceFactory factory)
        {
            var glyphVertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.GlyphTextVertex), "main");
            var glyphFragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.GlyphTextFragment), "main");

            glyphShaders = factory.CreateFromSpirv(glyphVertexShaderDesc, glyphFragmentShaderDesc);

            var textVertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.TextVertex), "main");
            var textFragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.TextFragment), "main");

            textShaders = factory.CreateFromSpirv(textVertexShaderDesc, textFragmentShaderDesc);
        }
    }
}
