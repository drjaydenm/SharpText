using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpText.Core;
using SharpText.Veldrid;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace SharpText.DemoApp
{
    public static class Program
    {
        private static InputSnapshot inputSnapshot;
        private static Sdl2Window window;
        private static GraphicsDevice graphicsDevice;
        private static ResourceFactory factory;
        private static CommandList commandList;
        private static Font infoFont;
        private static Font demoFont;
        private static ITextRenderer infoTextRenderer;
        private static ITextRenderer demoTextRenderer;
        private static float letterSpacing = 1;
        private static float fontSize = 20;
        private static int currentFontIndex = 0;
        private static string[] fonts =
        {
            "Fonts/OpenSans-Regular.woff",
            "Fonts/AmaticSC-Regular.ttf",
            "Fonts/LeArchitect.ttf",
            "Fonts/Sacramento-Regular.ttf",
            "Fonts/neon2.ttf"
        };
        private static int currentColorIndex = 0;
        private static Color[] colors =
        {
            RgbaFloat.Black.ToSharpTextColor(),
            RgbaFloat.White.ToSharpTextColor(),
            RgbaFloat.Blue.ToSharpTextColor()
        };

        public static void Main(string[] args)
        {
            window = CreateWindow();
            window.Resized += () => graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);

            Initialize();

            while (window.Exists)
            {
                inputSnapshot = window.PumpEvents();

                Update();
                Draw();
            }

            Dispose();
        }

        private static void Initialize()
        {
            graphicsDevice = CreateGraphicsDevice();
            factory = graphicsDevice.ResourceFactory;
            commandList = factory.CreateCommandList();

            infoFont = new Font(fonts[0], 13);
            infoTextRenderer = new VeldridTextRenderer(graphicsDevice, commandList, infoFont);
            window.Resized += infoTextRenderer.ResizeToSwapchain;

            demoFont = new Font(fonts[currentFontIndex], fontSize);
            demoTextRenderer = new VeldridTextRenderer(graphicsDevice, commandList, demoFont);
            window.Resized += demoTextRenderer.ResizeToSwapchain;
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
                UpdateFont();
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Down && ke.Down))
            {
                fontSize -= 0.5f;
                UpdateFont();
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Left && ke.Down))
            {
                letterSpacing -= 0.01f;
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Right && ke.Down))
            {
                letterSpacing += 0.01f;
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Enter && ke.Down))
            {
                currentFontIndex = (currentFontIndex + 1) % fonts.Length;
                UpdateFont();
            }
            if (inputSnapshot.KeyEvents.Any(ke => ke.Key == Key.Space && ke.Down))
            {
                currentColorIndex = (currentColorIndex + 1) % colors.Length;
            }

            infoTextRenderer.Update();
            demoTextRenderer.Update();

            var xAccumulated = 0f;
            const float xInset = 10;
            const float lineSpacing = 5;
            infoTextRenderer.DrawText("Debug Controls:", new Vector2(xInset, xAccumulated += xInset), colors[0]);
            infoTextRenderer.DrawText("Up/Down = Increase/Decrease Font Size", new Vector2(xInset, xAccumulated += lineSpacing + infoFont.FontSizeInPixels), colors[0]);
            infoTextRenderer.DrawText("Right/Left = Increase/Decrease Letter Spacing", new Vector2(xInset, xAccumulated += lineSpacing + infoFont.FontSizeInPixels), colors[0]);
            infoTextRenderer.DrawText("Enter = Change Font", new Vector2(xInset, xAccumulated += lineSpacing + infoFont.FontSizeInPixels), colors[0]);
            infoTextRenderer.DrawText("Space = Change Colorm", new Vector2(xInset, xAccumulated += lineSpacing + infoFont.FontSizeInPixels), colors[0]);
            infoTextRenderer.DrawText($"Current Font: {demoFont.Name}", new Vector2(xInset, xAccumulated += lineSpacing + infoFont.FontSizeInPixels), colors[0]);

            demoTextRenderer.DrawText("Sixty zippers were quickly picked from the woven jute bag.", new Vector2(xInset, xAccumulated += (lineSpacing * 5) + infoFont.FontSizeInPixels), colors[currentColorIndex], letterSpacing);
            demoTextRenderer.DrawText("The quick brown fox jumps over the lazy dog", new Vector2(xInset, xAccumulated += lineSpacing + demoFont.FontSizeInPixels), colors[(currentColorIndex + 1) % colors.Length], letterSpacing);
            demoTextRenderer.DrawText("How vexingly quick daft zebras jump!", new Vector2(xInset, xAccumulated += lineSpacing + demoFont.FontSizeInPixels), colors[(currentColorIndex + 2) % colors.Length], letterSpacing);
        }

        private static void Draw()
        {
            commandList.Begin();
            commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);

            commandList.ClearColorTarget(0, RgbaFloat.CornflowerBlue);
            commandList.ClearDepthStencil(1f);

            infoTextRenderer.Draw();
            demoTextRenderer.Draw();

            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            graphicsDevice.WaitForIdle();

            graphicsDevice.SwapBuffers();
        }

        private static void Dispose()
        {
            graphicsDevice.WaitForIdle();

            infoTextRenderer.Dispose();
            demoTextRenderer.Dispose();

            commandList.Dispose();
        }

        private static Sdl2Window CreateWindow()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowUtils.SetProcessDpiAwareness();
            }

            var windowCI = new WindowCreateInfo
            {
                X = 100,
                Y = 100,
                WindowWidth = 800,
                WindowHeight = 600,
                WindowTitle = "SharpText"
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
                swapchainSrgbFormat: false
            ));
        }

        private static void UpdateFont()
        {
            demoFont = new Font(fonts[currentFontIndex], fontSize);
            demoTextRenderer.UpdateFont(demoFont);
        }
    }
}
