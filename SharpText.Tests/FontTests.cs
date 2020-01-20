using System.IO;
using System.Linq;
using NUnit.Framework;
using SharpText.Core;
using Shouldly;

namespace SharpText.Tests
{
    [TestFixture]
    public class FontTests
    {
        private const string FontPath = "Fonts/OpenSans-Regular.woff";

        [Test]
        public void Constructor_WithFilePath()
        {
            var font = new Font(FontPath, 10);

            font.Name.ShouldBe("Open Sans");
        }

        [Test]
        public void Constructor_WithStream()
        {
            Font font = null;
            using (var fs = new FileStream(FontPath, FileMode.Open, FileAccess.Read))
            {
                font = new Font(fs, 10);
            }

            font.Name.ShouldBe("Open Sans");
        }

        [Test]
        public void GetVerticesForCharacter_ReturnsCorrectNumber()
        {
            var font = new Font(FontPath, 10);

            var aVertices = font.GetVerticesForCharacter('a');
            aVertices.Length.ShouldBe(126);
            var bVertices = font.GetVerticesForCharacter('B');
            bVertices.Length.ShouldBe(102);
        }

        [Test]
        public void GetVerticesForString_ReturnsCorrectValues()
        {
            var text = "testing123";
            var font = new Font(FontPath, 10);

            var vertices = font.GetVerticesForString(text);

            vertices.Vertices.Length.ShouldBe(text.Length);
            vertices.Bounds.StartX.ShouldBe(0, 0.5);
            vertices.Bounds.StartY.ShouldBe(0, 0.5);
            vertices.Bounds.EndX.ShouldBe(5, 0.5);
            vertices.Bounds.EndY.ShouldBe(8, 0.5);
        }

        [Test]
        public void GetMeasurementInfoForString_ReturnsCorrectValues()
        {
            var text = "testing123";
            var font = new Font(FontPath, 10);

            var measurementInfo = font.GetMeasurementInfoForString(text);

            measurementInfo.LineHeight.ShouldBe(font.FontSizeInPixels, font.FontSizeInPixels * 0.1f);
            measurementInfo.AdvanceWidths.Aggregate((a, b) => a + b).ShouldBe(40, 2);
        }
    }
}
