using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    /// <summary>The parameters in this class have only effect on the following Texture loads.</summary>
    public class TextureLoaderParameters
    {
        /// <summary>(Debug Aid, should be set to false) If set to false only Errors will be printed. If set to true, debug information (Warnings and Queries) will be printed in addition to Errors.</summary>
        public bool Verbose { get; set; } = false;

        /// <summary>Always-valid fallback parameter for GL.BindTexture (Default: 0). This number will be returned if loading the Texture failed. You can set this to a checkerboard texture or similar, which you have already loaded.</summary>
        public uint OpenGLDefaultTexture { get; set; } = 0;

        /// <summary>Compressed formats must have a border of 0, so this is constant.</summary>
        public int Border { get; set; } = 0;

        /// <summary>false==DirectX TexCoords, true==OpenGL TexCoords (Default: true)</summary>
        public bool FlipImages { get; set; } = true;

        /// <summary>When enabled, will use Glu to create MipMaps for images loaded with GDI+ (Default: false)</summary>
        public bool BuildMipmapsForUncompressed { get; set; } = false;

        /// <summary>Selects the Magnification filter for following Textures to be loaded. (Default: Nearest)</summary>
        public TextureMagFilter MagnificationFilter { get; set; } = TextureMagFilter.Nearest;

        /// <summary>Selects the Minification filter for following Textures to be loaded. (Default: Nearest)</summary>
        public TextureMinFilter MinificationFilter { get; set; } = TextureMinFilter.Nearest;

        /// <summary>Selects the S Wrapping for following Textures to be loaded. (Default: Repeat)</summary>
        public TextureWrapMode WrapModeS { get; set; } = TextureWrapMode.Repeat;

        /// <summary>Selects the T Wrapping for following Textures to be loaded. (Default: Repeat)</summary>
        public TextureWrapMode WrapModeT { get; set; } = TextureWrapMode.Repeat;

        /// <summary>Selects the Texture Environment Mode for the following Textures to be loaded. Default: Modulate)</summary>
        public TextureEnvMode EnvMode { get; set; } = TextureEnvMode.Modulate;
    }
}
