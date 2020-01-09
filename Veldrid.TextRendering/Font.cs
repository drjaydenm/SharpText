using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Typography.OpenFont;
using Typography.WebFont;

namespace Veldrid.TextRendering
{
    public class Font
    {
        public ushort UnitsPerEm => typeface.UnitsPerEm;
        public int FontSizeInPoints { get; private set; }
        public int FontSizeInPixels => (int)(FontSizeInPoints * 1.333333f);

        private readonly Typeface typeface;
        private readonly Dictionary<char, Glyph> loadedGlyphs;
        private readonly GlyphPathBuilder pathBuilder;
        private readonly GlyphTranslatorToVertices pathTranslator;

        public Font(string filePath, int fontSizeInPoints)
        {
            SetupWoffDecompressorIfRequired();

            FontSizeInPoints = fontSizeInPoints;
            loadedGlyphs = new Dictionary<char, Glyph>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var reader = new OpenFontReader();
                typeface = reader.Read(fs);
            }

            pathBuilder = new GlyphPathBuilder(typeface);
            pathTranslator = new GlyphTranslatorToVertices();
        }

        public Glyph GetGlyphByCharacter(char character)
        {
            if (loadedGlyphs.ContainsKey(character))
                return loadedGlyphs[character];
            
            var glyphIndex = typeface.CmapTable.LookupIndex(character);
            var glyph = typeface.GetGlyphByIndex(glyphIndex);
            
            loadedGlyphs.Add(character, glyph);

            return glyph;
        }

        public VertexPosition3Coord2[] GlyphToVertices(Glyph glyph)
        {
            pathBuilder.BuildFromGlyph(glyph, FontSizeInPoints);

            pathBuilder.ReadShapes(pathTranslator);
            var vertices = pathTranslator.ResultingVertices;

            return vertices;
        }

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
