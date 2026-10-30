using BnTxx;
using System.Drawing;
using GFTool.Renderer.Scene.GraphicsObjects;

namespace GFTool.RenderControl_WinForms;

public static class TexturePreviewExtensions
{
    public static Bitmap? LoadPreviewBitmap(this Texture texture)
    {
        try
        {
            if (!texture.TryGetResolvedSourcePath(out var path)) return null;
            return BNTX.LoadFromFile(path, Path.GetFileNameWithoutExtension(texture.SourceFile));
        }
        catch { return null; }
    }
}
