using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.Fonts;
using SixLaborsFont = SixLabors.Fonts.Font;

namespace SharpText.Core
{
    /// <summary>
    /// Contains measurement information for a piece of text
    /// </summary>
    public struct MeasurementInfo
    {
        public float Ascender;
        public float Descender;
        public float LineHeight;
        public float[] AdvanceWidths;
    }

    /// <summary>
    /// Contains vertices and bounds information for a piece of text
    /// </summary>
    public struct StringVertices
    {
        public VertexPosition3Coord2[][] Vertices;
        public BoundingRectangle Bounds;
    }

    /// <summary>
    /// Represents a font file and its associated glyphs and allows access to glyph vertices
    /// </summary>
    public class Font
    {
        public float FontSizeInPoints { get; private set; }
        public float FontSizeInPixels => FontSizeInPoints * POINTS_TO_PIXELS;
        public string Name => font.Name;

        private const float POINTS_TO_PIXELS = 4f / 3f;
        private const float PIXELS_TO_POINTS = 3f / 4f;

        private readonly FontDescription typeface;
        private readonly SixLaborsFont font;
        private readonly RendererOptions options;
        private readonly Dictionary<char, Glyph> loadedGlyphs;
        private readonly GlyphTranslatorToVertices pathTranslator;
        private readonly TextRenderer renderer;

        private float TotalHeight => 100;// (typeface.Bounds.YMax - typeface.Bounds.YMin) * (FontSizeInPixels / typeface.UnitsPerEm);

        /// <summary>
        /// Create a new font instance
        /// </summary>
        /// <param name="filePath">Path to the font file</param>
        /// <param name="fontSizeInPixels">The desired font size in pixels</param>
        public Font(string filePath, float fontSizeInPixels)
        {
            FontSizeInPoints = fontSizeInPixels * PIXELS_TO_POINTS;
            loadedGlyphs = new Dictionary<char, Glyph>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var collection = new FontCollection();
                collection.Install(fs, out typeface);
                font = collection.Families.First().CreateFont(fontSizeInPixels);
            }

            options = new RendererOptions(font);
            pathTranslator = new GlyphTranslatorToVertices();
            renderer = new TextRenderer(pathTranslator);
        }

        /// <summary>
        /// Create a new font instance
        /// </summary>
        /// <param name="fontStream">Stream to the font data</param>
        /// <param name="fontSizeInPixels">The desired font size in pixels</param>
        public Font(Stream fontStream, float fontSizeInPixels)
        {
            FontSizeInPoints = fontSizeInPixels * PIXELS_TO_POINTS;
            loadedGlyphs = new Dictionary<char, Glyph>();

            var collection = new FontCollection();
            collection.Install(fontStream, out typeface);
            font = collection.Families.First().CreateFont(fontSizeInPixels);

            options = new RendererOptions(font);
            pathTranslator = new GlyphTranslatorToVertices();
            renderer = new TextRenderer(pathTranslator);
        }

        /// <summary>
        /// Returns vertices for the specified character in pixel units
        /// </summary>
        /// <param name="character">The character</param>
        /// <returns>Vertices in pixel units</returns>
        public VertexPosition3Coord2[] GetVerticesForCharacter(char character)
        {
            pathTranslator.Render(new char[] { character }, options);

            var vertices = pathTranslator.ResultingVertices;

            return vertices;
        }

        /// <summary>
        /// Returns vertices and bounds info for the given text
        /// </summary>
        /// <param name="text">The text</param>
        /// <returns>Vertices and bounds info</returns>
        public StringVertices GetVerticesForString(string text)
        {
            var stringData = new StringVertices();
            stringData.Vertices = new VertexPosition3Coord2[text.Length][];

            for (var i = 0; i < text.Length; i++)
            {
                var vertices = GetVerticesForCharacter(text[i]);
                for (var j = 0; j < vertices.Length; j++)
                {
                    stringData.Bounds.Include(vertices[j].Position.X, vertices[j].Position.Y);
                }

                stringData.Vertices[i] = vertices;
            }

            var measure = TextMeasurer.Measure(text, options);
            stringData.Bounds = new BoundingRectangle
            {
                StartX = measure.Left,
                EndX = measure.Right,
                StartY = measure.Top,
                EndY = measure.Bottom
            };

            return stringData;
        }

        /// <summary>
        /// Return glyph advance distances along the X axis to layout text
        /// </summary>
        /// <param name="text">Text to layout</param>
        /// <returns>Advance distances for each glyph in pixel units</returns>
        public MeasurementInfo GetMeasurementInfoForString(string text)
        {
            var measure = TextMeasurer.Measure(text, options);

            //var glyphPositions = layout.ResultUnscaledGlyphPositions;
            var advanceWidths = new float[text.Length];
            var scale = FontSizeInPixels / TotalHeight;
            for (var i = 0; i < advanceWidths.Length; i++)
            {
                advanceWidths[i] = i * scale;
                //glyphPositions.GetGlyph(i, out var advanceW);
                //advanceWidths[i] = advanceW * (FontSizeInPixels / typeface.UnitsPerEm) * scale;
            }

            return new MeasurementInfo
            {
                Ascender = font.Ascender,
                Descender = font.Descender,
                AdvanceWidths = advanceWidths,
                LineHeight = font.LineHeight
            };
        }

        /// <summary>
        /// Get a glyph from the font by character
        /// </summary>
        /// <param name="character">The character</param>
        /// <returns>The glyph for the character</returns>
        private Glyph GetGlyphByCharacter(char character)
        {
            if (loadedGlyphs.ContainsKey(character))
                return loadedGlyphs[character];

            var glyph = font.GetGlyph(character);

            loadedGlyphs.Add(character, glyph);

            return glyph;
        }
    }
}
