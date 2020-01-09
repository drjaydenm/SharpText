using System.Collections.Generic;
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

    public struct DrawableText
    {
        public string Text;
        public DrawableGlyph[] Glyphs;
        public Vector2 Position;
    }

    public struct DrawableGlyph
    {
        public VertexPosition3Coord2[] Vertices;
        public float AdvanceX;
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
        private Vector2[] jitterPattern = new[]
        {
            new Vector2(-1 / 12f, -5 / 12f),
            new Vector2( 1 / 12f,  1 / 12f),
            new Vector2( 3 / 12f, -1 / 12f),
            new Vector2( 5 / 12f,  5 / 12f),
            new Vector2( 7 / 12f, -3 / 12f),
            new Vector2( 9 / 12f,  3 / 12f)
        };
        private List<DrawableText> textToDraw;

        public TextRenderer(GraphicsDevice graphicsDevice, Font font)
        {
            this.graphicsDevice = graphicsDevice;
            this.font = font;
            
            textToDraw = new List<DrawableText>();

            Initialize();
        }

        public void DrawText(string text, Vector2 coords)
        {
            var drawable = new DrawableText
            {
                Text = text,
                Glyphs = new DrawableGlyph[text.Length],
                Position = coords
            };

            for (var i = 0; i < text.Length; i++)
            {
                var glyph = font.GetGlyphByCharacter(text[i]);

                drawable.Glyphs[i] = new DrawableGlyph
                {
                    Vertices = font.GlyphToVertices(glyph),
                    // TODO calculate this properly
                    AdvanceX = (glyph.Bounds.XMax - glyph.Bounds.XMin) * (1f / font.FontSizeInPixels * 0.55f)
                };
            }

            textToDraw.Add(drawable);
        }

        public void Draw(CommandList commandList)
        {
            commandList.SetPipeline(glyphPipeline);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);
            commandList.SetFramebuffer(glyphTextureFramebuffer);

            commandList.ClearColorTarget(0, new RgbaFloat(0, 0, 0, 0));

            var advanceX = 0f;
            foreach (var drawable in textToDraw)
            {
                for (var i = 0; i < drawable.Glyphs.Length; i++)
                {
                    // TODO fix negative height
                    DrawGlyph(commandList, drawable.Glyphs[i].Vertices, new Vector2(advanceX, -(2f / graphicsDevice.SwapchainFramebuffer.Height) * 20));
                    advanceX += drawable.Glyphs[i].AdvanceX * (1f / graphicsDevice.SwapchainFramebuffer.Width);
                }
            }

            // 2nd pass
            textFragmentProperties.GlyphColor = new RgbaFloat(0, 0, 0, 0);
            commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);

            commandList.SetPipeline(outputPipeline);
            commandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);
            commandList.SetGraphicsResourceSet(1, textTextureSet);
            commandList.SetVertexBuffer(0, quadVertexBuffer);
            commandList.Draw((uint)quadVertices.Length);

            textToDraw.Clear();
        }

        private void DrawGlyph(CommandList commandList, VertexPosition3Coord2[] glyphVertices, Vector2 coord)
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

            var matrixA = Matrix4x4.CreateScale(2f / graphicsDevice.SwapchainFramebuffer.Width, 2f / graphicsDevice.SwapchainFramebuffer.Height, 1)
                * Matrix4x4.CreateTranslation(-1, 1, 0)
                * Matrix4x4.CreateTranslation(coord.X, coord.Y, 0);

            for (var i = 0; i < jitterPattern.Length; i++)
            {
                var jitter = jitterPattern[i];

                var matrixB = Matrix4x4.CreateTranslation(new Vector3(jitter, 0)) * matrixA;
                textVertexProperties.Transform = matrixB;
                commandList.UpdateBuffer(textVertexPropertiesBuffer, 0, textVertexProperties);

                if (i % 2 == 0)
                {
                    textFragmentProperties.GlyphColor = new RgbaFloat(i == 0 ? 1 : 0, i == 2 ? 1 : 0, i == 4 ? 1 : 0, 0);
                    commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);
                }

                commandList.Draw((uint)glyphVertices.Length);
            }
        }

        private void Initialize()
        {
            var factory = graphicsDevice.ResourceFactory;

            glyphVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition3Coord2.SizeInBytes, BufferUsage.VertexBuffer));
            quadVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition2.SizeInBytes * 4, BufferUsage.VertexBuffer));
            quadVertices = new VertexPosition2[]
            {
                new VertexPosition2(new Vector2(0, 0)),
                new VertexPosition2(new Vector2(0, 1)),
                new VertexPosition2(new Vector2(1, 0)),
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

            var colorFormat = PixelFormat.B8_G8_R8_A8_UNorm;
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
                        sourceColorFactor: BlendFactor.SourceAlpha,
                        destinationColorFactor: BlendFactor.Zero,
                        colorFunction: BlendFunction.Add,
                        sourceAlphaFactor: BlendFactor.SourceAlpha,
                        destinationAlphaFactor: BlendFactor.Zero,
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
            pipelineDescription.BlendState = new BlendStateDescription(RgbaFloat.White,
                new BlendAttachmentDescription(
                    blendEnabled: true,
                    sourceColorFactor: BlendFactor.One,
                    destinationColorFactor: BlendFactor.One,
                    colorFunction: BlendFunction.Add,
                    sourceAlphaFactor: BlendFactor.One,
                    destinationAlphaFactor: BlendFactor.One,
                    alphaFunction: BlendFunction.Add));
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { textPropertiesLayout };
            pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleList;
            pipelineDescription.RasterizerState = new RasterizerStateDescription(
                cullMode: FaceCullMode.None,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.Clockwise,
                depthClipEnabled: true,
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
