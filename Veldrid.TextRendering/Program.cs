using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;

namespace Veldrid.TextRendering
{
    public static class Program
    {
        private static InputSnapshot inputSnapshot;
        private static Sdl2Window window;
        private static GraphicsDevice graphicsDevice;
        private static ResourceFactory factory;
        private static CommandList commandList;
        private static DeviceBuffer vertexBuffer;
        private static DeviceBuffer indexBuffer;
        private static Pipeline pipeline;

        public static void Main(string[] args)
        {
            window = CreateWindow();

            Initialize();

            while (window.Exists)
            {
                inputSnapshot = window.PumpEvents();

                Update();
                Draw();
            }
        }

        private static void Initialize()
        {
            graphicsDevice = CreateGraphicsDevice();
            factory = graphicsDevice.ResourceFactory;
            commandList = factory.CreateCommandList();

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

        private static void Update()
        {
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Escape && ke.Down))
            {
                window.Close();
            }
        }

        private static void Draw()
        {
            commandList.Begin();
            commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);

            commandList.ClearColorTarget(0, RgbaFloat.White);
            commandList.ClearDepthStencil(1f);

            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            commandList.SetPipeline(pipeline);
            commandList.DrawIndexed(
                indexCount: 4,
                instanceCount: 1,
                indexStart: 0,
                vertexOffset: 0,
                instanceStart: 0);

            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            graphicsDevice.WaitForIdle();

            graphicsDevice.SwapBuffers();
        }

        private static Sdl2Window CreateWindow()
        {
            var windowCI = new WindowCreateInfo
            {
                X = 100,
                Y = 100,
                WindowWidth = 800,
                WindowHeight = 600,
                WindowTitle = "Veldrid.TextRendering"
            };
            return VeldridStartup.CreateWindow(ref windowCI);
        }

        private static GraphicsDevice CreateGraphicsDevice()
        {
            return VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions(
                debug: false,
                swapchainDepthFormat: PixelFormat.R16_UNorm,
                syncToVerticalBlank: false,
                resourceBindingModel: ResourceBindingModel.Improved,
                preferDepthRangeZeroToOne: true,
                preferStandardClipSpaceYDirection: true
            ));
        }
    }
}
