using System.Linq;
using System.Numerics;
using Veldrid.Sdl2;
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
        private static TextRenderer textRenderer;
        private static float letterSpacing = 1;
        private static float fontSize = 20;

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

            var font = new Font("Fonts/OpenSans-Regular.woff", fontSize);
            textRenderer = new TextRenderer(graphicsDevice, font);
        }

        private static void Update()
        {
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Escape && ke.Down))
            {
                window.Close();
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Up && ke.Down))
            {
                fontSize += 0.5f;
                textRenderer.UpdateFont(new Font("Fonts/OpenSans-Regular.woff", fontSize));
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Down && ke.Down))
            {
                fontSize -= 0.5f;
                textRenderer.UpdateFont(new Font("Fonts/OpenSans-Regular.woff", fontSize));
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Left && ke.Down))
            {
                letterSpacing -= 0.01f;
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Right && ke.Down))
            {
                letterSpacing += 0.01f;
            }

            textRenderer.DrawText("Sixty zippers were quickly picked from the woven jute bag.", Vector2.Zero, RgbaFloat.DarkRed, letterSpacing);
        }

        private static void Draw()
        {
            commandList.Begin();
            commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);

            commandList.ClearColorTarget(0, RgbaFloat.White);
            commandList.ClearDepthStencil(1f);

            textRenderer.Draw(commandList);

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
                preferStandardClipSpaceYDirection: true,
                swapchainSrgbFormat: true
            ));
        }
    }
}
