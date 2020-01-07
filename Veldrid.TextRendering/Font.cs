using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Typography.OpenFont;
using Typography.WebFont;

namespace Veldrid.TextRendering
{
    public class Font
    {
        private readonly Typeface typeface;

        public Font(string filePath)
        {
            SetupWoffDecompressorIfRequired();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var reader = new OpenFontReader();
                typeface = reader.Read(fs);
            }
        }

        public GlyphPointF[] GetCharacterGlyphPoints(char character)
        {
            var glyph = typeface.GetGlyphByName("e");
            return glyph.GlyphPoints;
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
