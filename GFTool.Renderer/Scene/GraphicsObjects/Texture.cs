using BnTxx;
using System.Drawing;
using GFTool.Core.Utils;
using GFTool.Renderer.Core;
using OpenTK.Graphics.OpenGL4;
using System.IO;
using Trinity.Core.Flatbuffers.TR.Model;

namespace GFTool.Renderer.Scene.GraphicsObjects
{
    public class Texture : IDisposable
    {
        private class CachedTexture
        {
            public int TextureId;
            public int RefCount;
        }

        private static readonly Dictionary<string, CachedTexture> cache = new Dictionary<string, CachedTexture>(StringComparer.OrdinalIgnoreCase);
        private static readonly object cacheLock = new object();

        public string Name { get; private set; }
        public string SourceFile { get; private set; }
        public uint Slot { get; private set; }
        public int textureId { get; private set; }

        private readonly string cacheKey;
        private readonly string texturePath;
        private readonly string altTexturePath;
        private readonly string preferredName;

        public Texture(PathString modelPath, TRTexture img)
        {
            Name = img.Name;
            SourceFile = img.File;
            Slot = img.Slot;
            string texturePath;
            try
            {
                texturePath = Path.GetFullPath(modelPath.Combine(img.File));
            }
            catch
            {
                texturePath = modelPath.Combine(img.File);
            }
            var preferredName = Path.GetFileNameWithoutExtension(img.File);
            this.texturePath = texturePath;
            try
            {
                altTexturePath = Path.GetFullPath(modelPath.Combine(Path.GetFileName(img.File)));
            }
            catch
            {
                altTexturePath = modelPath.Combine(Path.GetFileName(img.File));
            }
            this.preferredName = preferredName;
            var keyPath = File.Exists(texturePath) ? texturePath : (File.Exists(altTexturePath) ? altTexturePath : texturePath);
            cacheKey = $"{keyPath}|{preferredName}";

            lock (cacheLock)
            {
                if (cache.TryGetValue(cacheKey, out var cached))
                {
                    cached.RefCount++;
                    textureId = cached.TextureId;
                    return;
                }
            }
        }

        public void EnsureLoaded()
        {
            if (textureId > 0)
            {
                return;
            }

            Bitmap? bitmap = null;
            try
            {
                if (TryGetResolvedSourcePath(out var source)) bitmap = BNTX.LoadFromFile(source, preferredName);
            }
            catch (Exception ex)
            {
                MessageHandler.Instance.AddMessage(MessageType.WARNING, $"Failed to decode texture {SourceFile}: {ex.Message}");
            }
            if (bitmap == null)
            {
                bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                MessageHandler.Instance.AddMessage(MessageType.WARNING, $"Failed to load texture: {SourceFile}");
            }

            if (bitmap.Width <= 0 || bitmap.Height <= 0)
                throw new InvalidDataException($"Invalid decoded pixels for {SourceFile}");

            int id = GL.GenTexture();
            try
            {
                using (bitmap)
                using (var upload = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(upload))
                        graphics.DrawImageUnscaled(bitmap, 0, 0);

                    var rect = new Rectangle(0, 0, upload.Width, upload.Height);
                    var bits = upload.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        GL.BindTexture(TextureTarget.Texture2D, id);
                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                            upload.Width, upload.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bits.Scan0);
                    }
                    finally
                    {
                        upload.UnlockBits(bits);
                    }
                }
            }
            catch
            {
                GL.DeleteTexture(id);
                throw;
            }

            // Set texture parameters
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);


            lock (cacheLock)
            {
                cache[cacheKey] = new CachedTexture { TextureId = id, RefCount = 1 };
            }

            textureId = id;
        }

        public bool TryGetResolvedSourcePath(out string path)
        {
            if (File.Exists(texturePath))
            {
                path = texturePath;
                return true;
            }

            if (File.Exists(altTexturePath))
            {
                path = altTexturePath;
                return true;
            }

            path = texturePath;
            return false;
        }

        public void Dispose()
        {
            if (textureId <= 0)
            {
                return;
            }

            lock (cacheLock)
            {
                if (!cache.TryGetValue(cacheKey, out var cached))
                {
                    GL.DeleteTexture(textureId);
                    textureId = 0;
                    return;
                }

                cached.RefCount--;
                if (cached.RefCount <= 0)
                {
                    GL.DeleteTexture(cached.TextureId);
                    cache.Remove(cacheKey);
                }
            }
            textureId = 0;
        }
    }
}
