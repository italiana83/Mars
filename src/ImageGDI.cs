using OpenTK.Graphics.OpenGL;
using SkiaSharp;
using System.Diagnostics;


namespace Mars
{
    public class ImageGDI
    {

        public static void LoadFromDisk(string filename, TextureLoaderParameters parameters, out uint texturehandle,
            out TextureTarget dimension, out int Width, out int Height)
        {
            dimension = (OpenTK.Graphics.OpenGL.TextureTarget)0;
            texturehandle = parameters.OpenGLDefaultTexture;
            ErrorCode GLError = ErrorCode.NoError;

            SKBitmap currentBitmap = null;

            try // Exceptions will be thrown if any Problem occurs while working on the file. 
            {
                using (var stream = new SKFileStream(filename))
                using (var codec = SKCodec.Create(stream))
                {
                    var info = codec.Info;
                    Width = info.Width;
                    Height = info.Height;

                    // Decode the image
                    currentBitmap = SKBitmap.Decode(filename);
                    if (currentBitmap == null)
                        throw new ArgumentException("Failed to decode image: " + filename);
                }

                if (parameters.FlipImages)
                {
                    using (var surface = SKSurface.Create(new SKImageInfo(currentBitmap.Width, currentBitmap.Height)))
                    {
                        var canvas = surface.Canvas;
                        canvas.Scale(1, -1, 0, currentBitmap.Height / 2.0f); // Flip vertically
                        canvas.DrawBitmap(currentBitmap, 0, 0);

                        // Replace the original bitmap with the flipped version
                        var flippedBitmap = SKBitmap.FromImage(surface.Snapshot());
                        currentBitmap.Dispose();
                        currentBitmap = flippedBitmap;
                    }
                }

                if (currentBitmap.Height > 1)
                    dimension = OpenTK.Graphics.OpenGL.TextureTarget.Texture2D;
                else
                    dimension = OpenTK.Graphics.OpenGL.TextureTarget.Texture1D;

                GL.GenTextures(1, out texturehandle);
                GL.BindTexture(dimension, texturehandle);

                #region Load Texture
                OpenTK.Graphics.OpenGL.PixelInternalFormat pif;
                OpenTK.Graphics.OpenGL.PixelFormat pf;
                OpenTK.Graphics.OpenGL.PixelType pt;

                if (parameters.Verbose)
                    Trace.WriteLine("File: " + filename + " Format: " + currentBitmap.ColorType);

                // Convert SKColorType to OpenGL pixel format
                switch (currentBitmap.ColorType)
                {
                    case SKColorType.Gray8:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.R8;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Red;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Rgb565:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgb;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgb;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedShort565;
                        break;
                    case SKColorType.Rgb888x:
                    //case SKColorType.Rgb:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgb8;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgb;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Bgra8888:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Bgra;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Rgba8888:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgba;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    default:
                        // Convert to a supported format (RGBA)
                        var convertedBitmap = new SKBitmap(currentBitmap.Width, currentBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                        using (var canvas = new SKCanvas(convertedBitmap))
                        {
                            canvas.DrawBitmap(currentBitmap, 0, 0);
                        }
                        currentBitmap.Dispose();
                        currentBitmap = convertedBitmap;

                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgba;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                }

                // Get the pixel data
                var pixelData = currentBitmap.GetPixels();
                IntPtr scan0 = pixelData;

                if (currentBitmap.Height > 1)
                { // image is 2D
                    if (parameters.BuildMipmapsForUncompressed)
                    {
                        throw new Exception("Cannot build mipmaps, Glu is deprecated.");
                        // GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                    }
                    else
                        GL.TexImage2D(dimension, 0, pif, currentBitmap.Width, currentBitmap.Height, parameters.Border, pf, pt, scan0);
                }
                else
                { // image is 1D
                    if (parameters.BuildMipmapsForUncompressed)
                    {
                        throw new Exception("Cannot build mipmaps, Glu is deprecated.");
                        // GL.GenerateMipmap(GenerateMipmapTarget.Texture1D);
                    }
                    else
                        GL.TexImage1D(dimension, 0, pif, currentBitmap.Width, parameters.Border, pf, pt, scan0);
                }

                GL.Finish();
                GLError = GL.GetError();
                if (GLError != ErrorCode.NoError)
                {
                    throw new ArgumentException("Error building TexImage. GL Error: " + GLError);
                }
                #endregion Load Texture

                #region Set Texture Parameters
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);

                GLError = GL.GetError();
                if (GLError != ErrorCode.NoError)
                {
                    throw new ArgumentException("Error setting Texture Parameters. GL Error: " + GLError);
                }
                #endregion Set Texture Parameters

                return; // success
            }
            catch (Exception e)
            {
                dimension = (TextureTarget)0;
                texturehandle = parameters.OpenGLDefaultTexture;
                throw new ArgumentException("Texture Loading Error: Failed to read file " + filename + ".\n" + e);
            }
            finally
            {
                currentBitmap?.Dispose();
            }
        }

        public static void LoadFromDisk(string filename, TextureLoaderParameters parameters, out uint texturehandle, out TextureTarget dimension)
        {
            dimension = (OpenTK.Graphics.OpenGL.TextureTarget)0;
            texturehandle = parameters.OpenGLDefaultTexture;
            ErrorCode GLError = ErrorCode.NoError;

            SKBitmap currentBitmap = null;

            try // Exceptions will be thrown if any Problem occurs while working on the file. 
            {
                // Load bitmap using SkiaSharp
                currentBitmap = SKBitmap.Decode(filename);
                if (currentBitmap == null)
                    throw new ArgumentException("Failed to decode image: " + filename);

                // Flip image if required
                if (parameters.FlipImages)
                {
                    using (var surface = SKSurface.Create(new SKImageInfo(currentBitmap.Width, currentBitmap.Height)))
                    {
                        var canvas = surface.Canvas;
                        canvas.Translate(0, currentBitmap.Height);
                        canvas.Scale(1, -1); // Flip vertically
                        canvas.DrawBitmap(currentBitmap, 0, 0);

                        // Replace the original bitmap with the flipped version
                        var flippedBitmap = SKBitmap.FromImage(surface.Snapshot());
                        currentBitmap.Dispose();
                        currentBitmap = flippedBitmap;
                    }
                }

                if (currentBitmap.Height > 1)
                    dimension = OpenTK.Graphics.OpenGL.TextureTarget.Texture2D;
                else
                    dimension = OpenTK.Graphics.OpenGL.TextureTarget.Texture1D;

                GL.GenTextures(1, out texturehandle);
                GL.BindTexture(dimension, texturehandle);

                #region Load Texture
                OpenTK.Graphics.OpenGL.PixelInternalFormat pif;
                OpenTK.Graphics.OpenGL.PixelFormat pf;
                OpenTK.Graphics.OpenGL.PixelType pt;

                if (parameters.Verbose)
                    Trace.WriteLine("File: " + filename + " Format: " + currentBitmap.ColorType);

                // Convert SKColorType to OpenGL pixel format
                switch (currentBitmap.ColorType)
                {
                    case SKColorType.Gray8:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.R8;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Red;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Rgb565:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgb;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgb;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedShort565;
                        break;
                    case SKColorType.Rgb888x:
                    //case SKColorType.Rgb:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgb8;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgb;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Bgra8888:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Bgra;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    case SKColorType.Rgba8888:
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgba;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                    default:
                        // Convert unsupported formats to RGBA
                        using (var convertedBitmap = new SKBitmap(currentBitmap.Width, currentBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
                        using (var canvas = new SKCanvas(convertedBitmap))
                        {
                            canvas.DrawBitmap(currentBitmap, 0, 0);
                            currentBitmap.Dispose();
                            currentBitmap = convertedBitmap.Copy();
                        }
                        pif = OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba;
                        pf = OpenTK.Graphics.OpenGL.PixelFormat.Rgba;
                        pt = OpenTK.Graphics.OpenGL.PixelType.UnsignedByte;
                        break;
                }

                // Get the pixel data
                var pixelData = currentBitmap.GetPixels();
                IntPtr scan0 = pixelData;

                if (currentBitmap.Height > 1)
                { // image is 2D
                    if (parameters.BuildMipmapsForUncompressed)
                    {
                        throw new Exception("Cannot build mipmaps, Glu is deprecated.");
                        // GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                    }
                    else
                        GL.TexImage2D(dimension, 0, pif, currentBitmap.Width, currentBitmap.Height, parameters.Border, pf, pt, scan0);
                }
                else
                { // image is 1D
                    if (parameters.BuildMipmapsForUncompressed)
                    {
                        throw new Exception("Cannot build mipmaps, Glu is deprecated.");
                        // GL.GenerateMipmap(GenerateMipmapTarget.Texture1D);
                    }
                    else
                        GL.TexImage1D(dimension, 0, pif, currentBitmap.Width, parameters.Border, pf, pt, scan0);
                }

                GL.Finish();
                GLError = GL.GetError();
                if (GLError != ErrorCode.NoError)
                {
                    throw new ArgumentException("Error building TexImage. GL Error: " + GLError);
                }
                #endregion Load Texture

                #region Set Texture Parameters
                GL.TexParameter(dimension, TextureParameterName.TextureMinFilter, (int)parameters.MinificationFilter);
                GL.TexParameter(dimension, TextureParameterName.TextureMagFilter, (int)parameters.MagnificationFilter);

                GL.TexParameter(dimension, TextureParameterName.TextureWrapS, (int)parameters.WrapModeS);
                GL.TexParameter(dimension, TextureParameterName.TextureWrapT, (int)parameters.WrapModeT);

                //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureLoaderParameters.EnvMode);

                GLError = GL.GetError();
                if (GLError != ErrorCode.NoError)
                {
                    throw new ArgumentException("Error setting Texture Parameters. GL Error: " + GLError);
                }
                #endregion Set Texture Parameters

                return; // success
            }
            catch (Exception e)
            {
                dimension = (TextureTarget)0;
                texturehandle = parameters.OpenGLDefaultTexture;
                throw new ArgumentException("Texture Loading Error: Failed to read file " + filename + ".\n" + e);
                // return; // failure
            }
            finally
            {
                currentBitmap?.Dispose();
            }
        }
    }
}
