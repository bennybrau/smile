using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Kinect;

namespace Smile
{
    public enum FileFormat
    {
        Jpeg,
        Png,
        Bmp,
        Tiff,
        Gif
    }

    /// <summary>
    /// Creates the bitmap representation of a Kinect color frame.
    /// </summary>
    public class ColorBitmapGenerator
    {
        #region Properties

        /// <summary>
        /// Returns the RGB pixel values.
        /// </summary>
        byte[] _pixels;

        /// <summary>
        /// Returns the width of the bitmap.
        /// </summary>
        int _width;

        /// <summary>
        /// Returns the height of the bitmap.
        /// </summary>
        int _height;

        /// <summary>
        /// Returns the stream of the bitmap.
        /// </summary>
        Stream _stream;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the actual bitmap.
        /// </summary>
        public WriteableBitmap Bitmap { get; protected set; }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the bitmap with new frame data.
        /// </summary>
        /// <param name="frame">The specified Kinect color frame.</param>
        /// <param name="format">The specified color format.</param>
        public void Update(ColorFrame frame, ColorImageFormat format)
        {
            if (Bitmap == null)
            {
                _width = frame.FrameDescription.Width;
                _height = frame.FrameDescription.Height;
                _pixels = new byte[_width * _height * 4];
                Bitmap = new WriteableBitmap(_width, _height);
                _stream = Bitmap.PixelBuffer.AsStream();
            }

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(_pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(_pixels, format);
            }

            _stream.Seek(0, SeekOrigin.Begin);
            _stream.Write(_pixels, 0, _pixels.Length);

            Bitmap.Invalidate();
        }

        /// <summary>
        /// Updates the bitmap with new frame data.
        /// </summary>
        /// <param name="frame">The specified Kinect color frame.</param>
        public void Update(ColorFrame frame)
        {
            Update(frame, ColorImageFormat.Bgra);
        }

        public async Task<StorageFile> SaveBitmapToFile(WriteableBitmap bitmap, string fileName, FileFormat format)
        {
            BitmapEncoder encoder;
            Guid encoderGuid = BitmapEncoder.JpegEncoderId;

            switch(format)
            {
                case FileFormat.Jpeg:
                    fileName += ".jpg";
                    encoderGuid = BitmapEncoder.JpegEncoderId;
                    break;
                case FileFormat.Png:
                    fileName += ".png";
                    encoderGuid = BitmapEncoder.PngEncoderId;
                    break;
                case FileFormat.Bmp:
                    fileName += ".bmp";
                    encoderGuid = BitmapEncoder.BmpEncoderId;
                    break;
                case FileFormat.Tiff:
                    fileName += ".tiff";
                    encoderGuid = BitmapEncoder.TiffEncoderId;
                    break;
                case FileFormat.Gif:
                    fileName += ".gif";
                    encoderGuid = BitmapEncoder.GifEncoderId;
                    break;
            }

            StorageFile file = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                encoder = await BitmapEncoder.CreateAsync(encoderGuid, stream);
                Stream pixelStream = bitmap.PixelBuffer.AsStream();
                byte[] pixels = new byte[pixelStream.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 
                                    (uint)bitmap.PixelWidth, 
                                    (uint)bitmap.PixelHeight, 
                                    96.0, 
                                    96.0, 
                                    pixels);
                await encoder.FlushAsync();
            }
            return file;
        }

        #endregion
    }

    /// <summary>
    /// Provides some common functionality for manipulating color frames.
    /// </summary>
    public static class BitmapExtensions
    {
        #region Members

        /// <summary>
        /// The color bitmap creator.
        /// </summary>
        static ColorBitmapGenerator _colorBitmapGenerator = new ColorBitmapGenerator();

        #endregion

        #region Public methods

        /// <summary>
        /// Converts the specified color frame to a bitmap image.
        /// </summary>
        /// <param name="frame">The specified color frame.</param>
        /// <returns>The bitmap representation of the color frame.</returns>
        public static WriteableBitmap ToBitmap(this ColorFrame frame)
        {
            _colorBitmapGenerator.Update(frame);

            return _colorBitmapGenerator.Bitmap;
        }

        #endregion
    }
}
