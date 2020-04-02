using CoreGraphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UIKit;
using PIXELFMT = SixLabors.ImageSharp.PixelFormats.Bgra32;

[assembly: AssemblyTitle("Demo.iOSLib")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace Demo.iOSLib
{
    public class Class1
    {
        public UIImage Load(string name, Stream stream, IImageTransform transform)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Image<PIXELFMT> sharpImg = Image.Load<PIXELFMT>(stream);
            var argb = MemoryMarshal.Cast<PIXELFMT, int>(sharpImg.GetPixelSpan()).ToArray();
            var width = sharpImg.Width;
            var height = sharpImg.Height;

            // This is why I can't just use CoreGraphics to load the image directly.
            if (transform != null) argb = transform.Transform(argb, width, height);

            // Coerce into an iOS image. Only premultiplied variants are supported.
            var flags = CGBitmapFlags.ByteOrder32Little | CGBitmapFlags.PremultipliedFirst;
            for (int i = 0; i < argb.Length; i++)
            {
                int aarrggbb = argb[i];
                int aa = (aarrggbb >> 24) & 0xff;
                int rr00bb = aarrggbb & 0xff00ff;
                int gg00 = aarrggbb & 0xff00;
                int rr00bb00 = (rr00bb * aa) & ~0xff00ff;
                int gg0000 = (gg00 * aa) & 0xff0000;
                argb[i] = (aarrggbb & ~0x00ffffff) | (((rr00bb00 | gg0000) >> 8) & 0xffffff);
            }

            // The "safe" alternative of GCHandle.Alloc didn't work for me.
            unsafe
            {
                fixed (int* pBuf = argb)
                {
                    var ctxt = new CGBitmapContext(new IntPtr((void*)pBuf), width, height, 8, 4 * width, CGColorSpace.CreateGenericRgb(), flags);
                    var img = new UIImage(ctxt.ToImage());
                    return img;
                }
            }
        }
    }

    public interface IImageTransform
    {
        /// <summary>KISS: implementers may assume that buf.Length == w * h and that transformation in place is permitted</summary>
        /// <param name="buf">Raster of ARGB values.</param>
        int[] Transform(int[] buf, int w, int h);
    }
}
