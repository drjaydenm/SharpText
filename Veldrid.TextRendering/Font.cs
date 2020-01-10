using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Typography.OpenFont;
using Typography.TextLayout;
using Typography.WebFont;

namespace Veldrid.TextRendering
{
    /// <summary>
    /// Contains measurement information for a piece of text
    /// </summary>
    public struct MeasurementInfo
    {
        public float Ascender;
        public float Descender;
        public float[] AdvanceWidths;
    }

    /// <summary>
    /// Represents a font file and its associated glyphs and allows access to glyph vertices
    /// </summary>
    public class Font
    {
        public ushort UnitsPerEm => typeface.UnitsPerEm;
        public float FontSizeInPoints { get; private set; }
        public float FontSizeInPixels => FontSizeInPoints * POINTS_TO_PIXELS;

        private const float POINTS_TO_PIXELS = 4f / 3f;
        private const float PIXELS_TO_POINTS = 3f / 4f;

        private readonly Typeface typeface;
        private readonly Dictionary<char, Glyph> loadedGlyphs;
        private readonly GlyphPathBuilder pathBuilder;
        private readonly GlyphTranslatorToVertices pathTranslator;

        /// <summary>
        /// Create a new font instance
        /// </summary>
        /// <param name="filePath">Path to the font file</param>
        /// <param name="fontSizeInPixels">The desired font size in pixels</param>
        public Font(string filePath, float fontSizeInPixels)
        {
            SetupWoffDecompressorIfRequired();

            FontSizeInPoints = fontSizeInPixels * PIXELS_TO_POINTS;
            loadedGlyphs = new Dictionary<char, Glyph>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var reader = new OpenFontReader();
                typeface = reader.Read(fs);
            }

            pathBuilder = new GlyphPathBuilder(typeface);
            pathTranslator = new GlyphTranslatorToVertices();
        }

        /// <summary>
        /// Get a glyph from the font by character
        /// </summary>
        /// <param name="character">The character</param>
        /// <returns>The glyph for the character</returns>
        public Glyph GetGlyphByCharacter(char character)
        {
            if (loadedGlyphs.ContainsKey(character))
                return loadedGlyphs[character];
            
            var glyphIndex = typeface.CmapTable.LookupIndex(character);
            var glyph = typeface.GetGlyphByIndex(glyphIndex);
            
            loadedGlyphs.Add(character, glyph);

            return glyph;
        }

        /// <summary>
        /// Returns vertices for the specified glyph in pixel units
        /// </summary>
        /// <param name="glyph">The glyph</param>
        /// <returns>Vertices in pixel units</returns>
        public VertexPosition3Coord2[] GlyphToVertices(Glyph glyph)
        {
            pathBuilder.BuildFromGlyph(glyph, FontSizeInPoints);

            pathBuilder.ReadShapes(pathTranslator);
            var vertices = pathTranslator.ResultingVertices;

            // Reorient the vertices so 0,0 is the top left corner
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i].Position.Y = FontSizeInPixels - vertices[i].Position.Y;
            }

            return vertices;
        }

        /// <summary>
        /// Return glyph advance distances along the X axis to layout text
        /// </summary>
        /// <param name="text">Text to layout</param>
        /// <returns>Advance distances for each glyph in pixel units</returns>
        public MeasurementInfo GetMeasurementInfoForString(string text)
        {
            var layout = new GlyphLayout();
            layout.Typeface = typeface;

            var measure = layout.LayoutAndMeasureString(text.ToCharArray(), 0, text.Length, FontSizeInPoints);

            var glyphPositions = layout.ResultUnscaledGlyphPositions;
            var advanceWidths = new float[glyphPositions.Count];
            for (var i = 0; i < glyphPositions.Count; i++)
            {
                glyphPositions.GetGlyph(i, out var advanceW);
                advanceWidths[i] = advanceW * (FontSizeInPixels / UnitsPerEm);
            }

            return new MeasurementInfo
            {
                Ascender = measure.AscendingInPx,
                Descender = measure.DescendingInPx,
                AdvanceWidths = advanceWidths
            };
        }

        /// <summary>
        /// The initial WOFF decompressor is null and throws an exception
        /// So we use SharpZipLib to inflate the file
        /// </summary>
        private static void SetupWoffDecompressorIfRequired()
        {
            if (WoffDefaultZlibDecompressFunc.DecompressHandler != null)
                return;

            WoffDefaultZlibDecompressFunc.DecompressHandler = (byte[] compressedBytes, byte[] decompressedResult) =>
            {
                try
                {
                    var inflater = new Inflater();
                    inflater.SetInput(compressedBytes);
                    inflater.Inflate(decompressedResult);

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    return false;
                }
            };
        }
    }
}
