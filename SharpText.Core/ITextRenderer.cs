using System.Numerics;

namespace SharpText.Core
{
	/// <summary>
	/// Common interface for text renderers
	/// </summary>
	public interface ITextRenderer
	{
		/// <summary>
		/// Draw a string of text in the specified location and color
		/// </summary>
		/// <param name="text">The text to draw</param>
		/// <param name="coordsInPixels">The X,Y coords for the text, with the 0,0 origin in the top left</param>
		/// <param name="color">The color of the text</param>
		/// <param name="letterSpacing">Letter spacing to use</param>
		void DrawText(string text, Vector2 coordsInPixels, Color color, float letterSpacing = 1f);

		/// <summary>
		/// Draw all accumulated strings of text
		/// </summary>
		void Draw();

		/// <summary>
		/// Update the font in use by the renderer. WARNING, this will clear out any caches and could be an expensive call
		/// </summary>
		/// <param name="font">The new font</param>
		void UpdateFont(Font font);
	}
}
