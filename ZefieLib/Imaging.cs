using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ZefieLib
{
    public class Imaging
    {
        /// <summary>
        /// Inverts the colors of the bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns>Inverted Bitmap</returns>
        public static Bitmap InvertColors(Bitmap bitmap)
        {
            var bitmapRead = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var bitmapLength = bitmapRead.Stride * bitmapRead.Height;
            var bitmapBGRA = new byte[bitmapLength];
            Marshal.Copy(bitmapRead.Scan0, bitmapBGRA, 0, bitmapLength);
            bitmap.UnlockBits(bitmapRead);

            for (int i = 0; i < bitmapLength; i += 4)
            {
                bitmapBGRA[i] = (byte)(255 - bitmapBGRA[i]);
                bitmapBGRA[i + 1] = (byte)(255 - bitmapBGRA[i + 1]);
                bitmapBGRA[i + 2] = (byte)(255 - bitmapBGRA[i + 2]);
                //        [i + 3] = ALPHA.
            }
            Bitmap outBMP = new Bitmap(bitmap);
            var bitmapWrite = outBMP.LockBits(new Rectangle(0, 0, outBMP.Width, outBMP.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            Marshal.Copy(bitmapBGRA, 0, bitmapWrite.Scan0, bitmapLength);
            outBMP.UnlockBits(bitmapWrite);
            return outBMP;
        }
        /// <summary>
        /// Inverts the colors of the image
        /// </summary>
        /// <param name="image"></param>
        /// <returns>Inverted Bitmap</returns>
        public static Image InvertColors(Image image)
        {
            Bitmap bitmap = new Bitmap(image);
            bitmap = InvertColors(bitmap);
            return (Image)bitmap;
        }
        /// <summary>
        /// Scales the image to the specified size
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns>Scaled image</returns>
        public static Image Scale(Image image, int width, int height)
        {
            //a holder for the result
            Bitmap result = new Bitmap(width, height);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //use a graphics object to draw the resized image into the bitmap
            using (Graphics graphics = Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                //draw the image into the target bitmap
                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            //return the resulting bitmap
            return (Image)result;
        }
        /// <summary>
        /// Scales the image to the specified percent of the original size
        /// </summary>
        /// <param name="image"></param>
        /// <param name="percent"></param>
        /// <returns>Scaled image</returns>
        public static Image Scale(Image image, double percent)
        {
            int width = (int)Math.CalcPercentOf((double)image.Width, percent);
            int height = (int)Math.CalcPercentOf((double)image.Height, percent);
            return Scale(image, width, height);
        }
        /// <summary>
        /// Scales the image to the specified width, while retaining aspect ratio
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <returns>Scaled image</returns>
        public static Image Scale(Image image, int width)
        {
            double percent = Math.CalcPercent(width, image.Width);
            int height = (int)Math.CalcPercentOf((double)image.Height, percent);
            return Scale(image, width, height);
        }
        /// <summary>
        /// Scales the bitmap the specified size
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns>Scaled bitmap</returns>
        public static Bitmap Scale(Bitmap bitmap, int width, int height)
        {
            Image image = (Image)bitmap;
            return new Bitmap(Scale(image, width, height));
        }
        /// <summary>
        /// Scales the bitmap to the specified percent of the original size
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="percent"></param>
        /// <returns>Scaled bitmap</returns>
        public static Bitmap Scale(Bitmap bitmap, double percent)
        {
            Image image = (Image)bitmap;
            int width = (int)Math.CalcPercentOf((double)image.Width, percent);
            int height = (int)Math.CalcPercentOf((double)image.Height, percent);
            return new Bitmap(Scale(image, width, height));
        }
        /// <summary>
        /// Scales the bitmap to the specified width, while retaining aspect ratio
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="width"></param>
        /// <returns>Scaled bitmap</returns>
        public static Bitmap Scale(Bitmap bitmap, int width)
        {
            Image image = (Image)bitmap;
            double percent = Math.CalcPercent(width, image.Width);
            int height = (int)Math.CalcPercentOf((double)image.Height, percent);
            return new Bitmap(Scale(image, width, height));
        }
    }
}
