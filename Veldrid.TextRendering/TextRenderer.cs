using System;
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
        public RgbaFloat Color;
        public float LetterSpacing;
        public DrawableRect Rectangle;
    }

    public struct DrawableGlyph
    {
        public VertexPosition3Coord2[] Vertices;
        public float AdvanceX;
    }

    public struct DrawableRect
    {
        public float StartX;
        public float StartY;
        public float EndX;
        public float EndY;
        public float Width => EndX - StartX;
        public float Height => EndY - StartY;

        public void Reset()
        {
            StartX = StartY = float.MaxValue;
            EndX = EndY = float.MinValue;
        }

        public void Include(float x, float y)
        {
            StartX = Math.Min(StartX, x);
            StartY = Math.Min(StartY, y);
            EndX = Math.Max(EndX, x);
            EndY = Math.Max(EndY, y);
        }

        public Vector4 ToVector4()
        {
            return new Vector4(StartX, StartY, EndX, EndY);
        }
    }

    public class TextRenderer
    {
        public Font Font { get; private set; }

        private readonly GraphicsDevice graphicsDevice;
        private DeviceBuffer glyphVertexBuffer;
        private DeviceBuffer quadVertexBuffer;
        private DeviceBuffer textVertexPropertiesBuffer;
        private DeviceBuffer textFragmentPropertiesBuffer;
        private Pipeline outputPipeline;
        private Pipeline outputColorPipeline;
        private Pipeline glyphPipeline;
        private VertexPosition2[] quadVertices;
        private TextVertexProperties textVertexProperties;
        private TextFragmentProperties textFragmentProperties;
        private ResourceSet textPropertiesSet;
        private ResourceSet textTextureSet;
        private ResourceSet dummyTextureSet;
        private Texture glyphTexture;
        private TextureView glyphTextureView;
        private Framebuffer glyphTextureFramebuffer;
        private Texture dummyTexture;
        private TextureView dummyTextureView;
        private Shader[] glyphShaders;
        private Shader[] textShaders;
        private Vector2[] jitterPattern = {
            new Vector2(-1 / 12f, -5 / 12f),
            new Vector2( 1 / 12f,  1 / 12f),
            new Vector2( 3 / 12f, -1 / 12f),
            new Vector2( 5 / 12f,  5 / 12f),
            new Vector2( 7 / 12f, -3 / 12f),
            new Vector2( 9 / 12f,  3 / 12f)
        };
        private List<DrawableText> textToDraw;
        private float aspectWidth;
        private float aspectHeight;

        public TextRenderer(GraphicsDevice graphicsDevice, Font font)
        {
            this.graphicsDevice = graphicsDevice;
            Font = font;
            
            textToDraw = new List<DrawableText>();

            Initialize();
        }

        public void UpdateFont(Font font)
        {
            Font = font;
        }

        public void DrawText(string text, Vector2 coordsInPixels, RgbaFloat color, float letterSpacing = 1f)
        {
            var drawable = new DrawableText
            {
                Text = text,
                Glyphs = new DrawableGlyph[text.Length],
                Position = coordsInPixels,
                Color = color,
                LetterSpacing = letterSpacing
            };

            var measurementInfo = Font.GetMeasurementInfoForString(text);
            var accumulatedAdvanceWidths = 0f;
            drawable.Rectangle.Reset();

            for (var i = 0; i < text.Length; i++)
            {
                var glyph = Font.GetGlyphByCharacter(text[i]);
                var vertices = Font.GlyphToVertices(glyph);

                // Extend the text rectangle to contain this letter
                for (var j = 0; j < vertices.Length; j++)
                {
                    vertices[j].Position.Y += measurementInfo.Descender;
                    drawable.Rectangle.Include(vertices[j].Position.X + accumulatedAdvanceWidths, vertices[j].Position.Y);
                }

                drawable.Glyphs[i] = new DrawableGlyph
                {
                    Vertices = vertices,
                    AdvanceX = measurementInfo.AdvanceWidths[i]
                };
                accumulatedAdvanceWidths += measurementInfo.AdvanceWidths[i];
            }

            textToDraw.Add(drawable);
        }

        public void Draw(CommandList commandList)
        {
            var textGroupedByColor = textToDraw.GroupBy(t => t.Color);

            // Render text sets by color
            foreach (var colorGroup in textGroupedByColor)
            {
                commandList.SetPipeline(glyphPipeline);
                commandList.SetGraphicsResourceSet(0, textPropertiesSet);
                commandList.SetFramebuffer(glyphTextureFramebuffer);

                commandList.ClearColorTarget(0, new RgbaFloat(0, 0, 0, 0));

                var updateRect = new DrawableRect();
                updateRect.Reset();
                foreach (var drawable in colorGroup)
                {
                    var advanceX = 0f;
                    for (var i = 0; i < drawable.Glyphs.Length; i++)
                    {
                        var glyphAdvanceX = drawable.Glyphs[i].AdvanceX;
                        // Transform letter spacing from 1 based to 0 based
                        glyphAdvanceX += (drawable.LetterSpacing - 1) * Font.FontSizeInPixels;

                        var glyphCoordsInPixels = new Vector2(advanceX, 0) + drawable.Position;
                        DrawGlyph(commandList, drawable.Glyphs[i].Vertices, glyphCoordsInPixels);
                        advanceX += glyphAdvanceX;
                    }

                    updateRect.Include(drawable.Position.X * aspectWidth / 2f,
                        drawable.Position.Y * aspectHeight / 2f);
                    updateRect.Include((drawable.Position.X + advanceX) * aspectWidth / 2f,
                        (drawable.Position.Y + Font.FontSizeInPixels) * aspectHeight / 2f);
                }

                const float paddingPixels = 1;
                updateRect.Include(updateRect.StartX - (paddingPixels * aspectWidth / 2f),
                    updateRect.StartY - (paddingPixels * aspectHeight / 2f));
                updateRect.Include(updateRect.EndX + (paddingPixels * aspectWidth / 2f),
                    updateRect.EndY + (paddingPixels * aspectHeight / 2f));

                // 2nd pass, render everything to the framebuffer
                DrawOutput(commandList, new RgbaFloat(0, 0, 0, 0), false, updateRect.ToVector4());

                // We need to do a second pass if we would like a color other than black text
                if (colorGroup.Key != new RgbaFloat(0, 0, 0, 1))
                {
                    DrawOutput(commandList, colorGroup.Key, true, updateRect.ToVector4());
                }
            }

            textToDraw.Clear();
        }

        private void DrawGlyph(CommandList commandList, VertexPosition3Coord2[] glyphVertices, Vector2 coordsInPixels)
        {
            // Resize the vertex buffer if required
            var requiredBufferSize = VertexPosition3Coord2.SizeInBytes * (uint)glyphVertices.Length;
            if (glyphVertexBuffer.SizeInBytes < requiredBufferSize)
            {
                glyphVertexBuffer.Dispose();
                glyphVertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(
                    new BufferDescription(requiredBufferSize, BufferUsage.VertexBuffer));
            }

            commandList.UpdateBuffer(glyphVertexBuffer, 0, glyphVertices);
            commandList.SetVertexBuffer(0, glyphVertexBuffer);

            var coordsInScreenSpace = coordsInPixels * new Vector2(aspectWidth, aspectHeight);
            var textTransformMatrix = Matrix4x4.CreateScale(aspectWidth, -aspectHeight, 1)
                * Matrix4x4.CreateTranslation(-1, 0, 0)
                * Matrix4x4.CreateTranslation(coordsInScreenSpace.X, 1f - coordsInScreenSpace.Y, 0);

            for (var i = 0; i < jitterPattern.Length; i++)
            {
                var jitter = jitterPattern[i];

                var glyphTransformMatrix = Matrix4x4.CreateTranslation(new Vector3(jitter, 0)) * textTransformMatrix;
                textVertexProperties.Transform = glyphTransformMatrix;
                commandList.UpdateBuffer(textVertexPropertiesBuffer, 0, textVertexProperties);

                if (i % 2 == 0)
                {
                    textFragmentProperties.GlyphColor = new RgbaFloat(i == 0 ? 1 : 0, i == 2 ? 1 : 0, i == 4 ? 1 : 0, 0);
                    commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);
                }

                commandList.Draw((uint)glyphVertices.Length);
            }
        }

        private void DrawOutput(CommandList commandList, RgbaFloat color, bool secondPass, Vector4 rect)
        {
            textVertexProperties.Rectangle = rect;
            commandList.UpdateBuffer(textVertexPropertiesBuffer, 0, textVertexProperties);

            textFragmentProperties.GlyphColor = color;
            commandList.UpdateBuffer(textFragmentPropertiesBuffer, 0, textFragmentProperties);

            commandList.SetPipeline(secondPass ? outputColorPipeline : outputPipeline);
            commandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
            commandList.SetGraphicsResourceSet(0, textPropertiesSet);
            // HACK workaround issue with texture view caching for shader resources
            commandList.SetGraphicsResourceSet(1, dummyTextureSet);
            commandList.SetGraphicsResourceSet(1, textTextureSet);
            commandList.SetVertexBuffer(0, quadVertexBuffer);
            commandList.Draw((uint)quadVertices.Length);
        }

        private void Initialize()
        {
            var factory = graphicsDevice.ResourceFactory;
            aspectWidth = 2f / graphicsDevice.SwapchainFramebuffer.Width;
            aspectHeight = 2f / graphicsDevice.SwapchainFramebuffer.Height;

            glyphVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition3Coord2.SizeInBytes, BufferUsage.VertexBuffer));
            quadVertexBuffer = factory.CreateBuffer(new BufferDescription(VertexPosition2.SizeInBytes * 4, BufferUsage.VertexBuffer));
            quadVertices = new VertexPosition2[]
            {
                new VertexPosition2(new Vector2(-1, 1)),
                new VertexPosition2(new Vector2(-1, -1)),
                new VertexPosition2(new Vector2(1, 1)),
                new VertexPosition2(new Vector2(1, -1)),
            };
            graphicsDevice.UpdateBuffer(quadVertexBuffer, 0, quadVertices);

            textVertexPropertiesBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<TextVertexProperties>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            textFragmentPropertiesBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<TextFragmentProperties>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            textVertexProperties = new TextVertexProperties
            {
                Transform = new Matrix4x4(),
                Rectangle = new Vector4(0, 0, 1, 1)
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
            // HACK workaround issue with texture view caching for shader resources
            dummyTexture = factory.CreateTexture(TextureDescription.Texture2D(1, 1, 1, 1, colorFormat, TextureUsage.Sampled));
            dummyTextureView = factory.CreateTextureView(dummyTexture);

            var shaderOptions = GetCompileOptions();
            CompileShaders(factory, shaderOptions);

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
            // HACK workaround issue with texture view caching for shader resources
            dummyTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                textTextureLayout,
                dummyTextureView,
                graphicsDevice.LinearSampler));

            var additiveBlendState = new BlendStateDescription(RgbaFloat.White,
                new BlendAttachmentDescription(
                    blendEnabled: true,
                    sourceColorFactor: BlendFactor.One,
                    destinationColorFactor: BlendFactor.One,
                    colorFunction: BlendFunction.Add,
                    sourceAlphaFactor: BlendFactor.One,
                    destinationAlphaFactor: BlendFactor.One,
                    alphaFunction: BlendFunction.Add));

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
                    shaders: textShaders,
                    specializations: shaderOptions.Specializations),
                resourceLayouts: new ResourceLayout[] { textPropertiesLayout, textTextureLayout },
                outputs: graphicsDevice.SwapchainFramebuffer.OutputDescription
            );
            outputPipeline = factory.CreateGraphicsPipeline(pipelineDescription);

            pipelineDescription.BlendState = additiveBlendState;
            outputColorPipeline = factory.CreateGraphicsPipeline(pipelineDescription);

            pipelineDescription.Outputs = new OutputDescription(null, new OutputAttachmentDescription(colorFormat));
            pipelineDescription.BlendState = additiveBlendState;
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
                shaders: glyphShaders,
                specializations: shaderOptions.Specializations);
            glyphPipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }

        private void CompileShaders(ResourceFactory factory, CrossCompileOptions options)
        {
            var glyphVertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.GlyphTextVertex), "main");
            var glyphFragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.GlyphTextFragment), "main");

            glyphShaders = factory.CreateFromSpirv(glyphVertexShaderDesc, glyphFragmentShaderDesc, options);

            var textVertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex, Encoding.UTF8.GetBytes(Shaders.TextVertex), "main");
            var textFragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment, Encoding.UTF8.GetBytes(Shaders.TextFragment), "main");

            textShaders = factory.CreateFromSpirv(textVertexShaderDesc, textFragmentShaderDesc, options);
        }

        private CrossCompileOptions GetCompileOptions()
        {
            var isOpenGl = graphicsDevice.BackendType == GraphicsBackend.OpenGL
                || graphicsDevice.BackendType == GraphicsBackend.OpenGLES;

            var specializations = new[] { new SpecializationConstant(0, isOpenGl) }; //FlipSamplerUVs
            return new CrossCompileOptions(isOpenGl && !graphicsDevice.IsDepthRangeZeroToOne, false, specializations);
        }
    }
}
