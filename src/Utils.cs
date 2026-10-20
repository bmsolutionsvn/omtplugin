using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace omtplugin
{
    internal class Utils
    {
        // [a * 256 + c] = c * 255 / a, rounded and capped: premultiplied colour back to straight.
        private static readonly byte[] divide = BuildDivide();

        private static byte[] BuildDivide()
        {
            byte[] t = new byte[256 * 256];
            for (int a = 1; a < 256; a++)
            {
                for (int c = 0; c < 256; c++)
                {
                    t[a * 256 + c] = (byte)Math.Min(255, (c * 255 + a / 2) / a);
                }
            }
            return t;
        }

        // OBS draws a BGRA frame as straight alpha; OMT and OMT-HX may hand it over premultiplied, which OBS would
        // multiply again and darken every soft edge. Divides the colour back out, in place.
        // ponytail: one CPU pass per frame (~1-2 ms at 1080p); a DrawAlphaDivide shader if 4K alpha gets heavy.
        public static void Unpremultiply(IntPtr bgra, int stride, int width, int height)
        {
            unsafe
            {
                fixed (byte* t = divide)
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* p = (byte*)bgra + (long)y * stride;
                        for (int x = 0; x < width; x++, p += 4)
                        {
                            int a = p[3];
                            if (a == 255 || a == 0) continue;
                            byte* row = t + a * 256;
                            p[0] = row[p[0]];
                            p[1] = row[p[1]];
                            p[2] = row[p[2]];
                        }
                    }
                }
            }
        }

        public static void P010ToP216(IntPtr srcY, int srcStrideY, IntPtr srcUV, int srcStrideUV, IntPtr dst, int dstStride, int width, int height)
        {
            unsafe
            {
                //Copy Y Plane
                IntPtr dstY = dst;
                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy((void*)srcY, (void*)dstY, width * 2, width * 2);
                    srcY += srcStrideY;
                    dstY += dstStride;
                }

                //Copy UV Plane, duplicating each line to convert from 4:2:0 to 4:2:2
                IntPtr dstUV = dst + (dstStride * height);
                int uvStride = width * 2;
                for (int y = 0; y < (height >> 1); y++)
                {
                    Buffer.MemoryCopy((void*)srcUV, (void*)dstUV, uvStride, uvStride);
                    dstUV += dstStride;
                    Buffer.MemoryCopy((void*)srcUV, (void*)dstUV, uvStride, uvStride);
                    dstUV += dstStride;
                    srcUV += srcStrideUV;
                }
            }
        }

        public static void P216ToP010(IntPtr src, int srcStride, IntPtr dstY, int dstStrideY, IntPtr dstUV, int dstStrideUV, int width, int height)
        {
            unsafe
            {
                //Copy Y Plane
                IntPtr srcY = src;
                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy((void*)srcY, (void*)dstY, width * 2, width * 2);
                    srcY += srcStride;
                    dstY += dstStrideY;
                }

                //Copy UV Plane, skipping every second line to convert from 4:2:2 to 4:2:0
                IntPtr srcUV = src + (srcStride * height);
                int uvStride = width * 2;
                for (int y = 0; y < (height >> 1); y++)
                {
                    Buffer.MemoryCopy((void*)srcUV, (void*)dstUV, uvStride, uvStride);
                    dstUV += dstStrideUV;
                    srcUV += srcStride;
                    srcUV += srcStride;
                }

            }
        }
    }
}
