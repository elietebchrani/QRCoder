#if NETFRAMEWORK || NETSTANDARD2_0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using static QRCoder.QRCodeGenerator;

namespace QRCoder
{
    public class QRCode : AbstractQRCode, IDisposable
    {
        /// <summary>
        /// Constructor without params to be used in COM Objects connections
        /// </summary>
        public QRCode() { }

        public QRCode(QRCodeData data) : base(data) { }

        public Bitmap GetGraphic(int pixelsPerModule)
        {
            return this.GetGraphic(pixelsPerModule, Color.Black, Color.White, true);
        }

        public Bitmap GetGraphic(int pixelsPerModule, string darkColorHtmlHex, string lightColorHtmlHex, bool drawQuietZones = true)
        {
            return this.GetGraphic(pixelsPerModule, ColorTranslator.FromHtml(darkColorHtmlHex), ColorTranslator.FromHtml(lightColorHtmlHex), drawQuietZones);
        }

        public Bitmap GetGraphic(int pixelsPerModule, Color darkColor, Color lightColor, bool drawQuietZones = true)
        {
            var size = (this.QrCodeData.ModuleMatrix.Count - (drawQuietZones ? 0 : 8)) * pixelsPerModule;
            var offset = drawQuietZones ? 0 : 4 * pixelsPerModule;

            var bmp = new Bitmap(size, size);
            using (var gfx = Graphics.FromImage(bmp))
            using (var lightBrush = new SolidBrush(lightColor))
            using (var darkBrush = new SolidBrush(darkColor))
            {
                for (var x = 0; x < size + offset; x = x + pixelsPerModule)
                {
                    for (var y = 0; y < size + offset; y = y + pixelsPerModule)
                    {
                        var module = this.QrCodeData.ModuleMatrix[(y + pixelsPerModule) / pixelsPerModule - 1][(x + pixelsPerModule) / pixelsPerModule - 1];

                        if (module)
                        {
                            gfx.FillRectangle(darkBrush, new Rectangle(x - offset, y - offset, pixelsPerModule, pixelsPerModule));
                        }
                        else
                        {
                            gfx.FillRectangle(lightBrush, new Rectangle(x - offset, y - offset, pixelsPerModule, pixelsPerModule));
                        }
                    }
                }

                gfx.Save();
            }

            return bmp;
        }

        private bool isFrame(int x, int y, int size, int pixelsPerModule)
        {
            x /= pixelsPerModule;
            y /= pixelsPerModule;
            size /= pixelsPerModule;

            // Top Left Frame
            if (x < 11 && y < 11)
                return true;

            if (size - 11 <= x && y < 11)
                return true;

            if (x < 11 && size - 11 <= y)
                return true;

            return false;
        }

        private bool isLeftStartBar(int x, int y, int pixelsPerModule)
        {
            x /= pixelsPerModule;
            y /= pixelsPerModule;

            if (x <= 4)
                return true;

            if (!_regionsArray[x - 1, y])
                return true;

            return false;
        }

        private bool isRightEndBar(int x, int y, int pixelsPerModule)
        {
            x /= pixelsPerModule;
            y /= pixelsPerModule;

            if (_regionsArray.GetLength(0) - 5 <= x)
                return true;

            if (!_regionsArray[x + 1, y])
                return true;

            return false;
        }

        private bool isAloneBar(int x, int y, int pixelsPerModule)
        {
            if (isLeftStartBar(x, y, pixelsPerModule) && isRightEndBar(x, y, pixelsPerModule))
                return true;

            return false;
        }

        private bool[,] _regionsArray;

        private void FillInRegionsArray(int size, int offset, int pixelsPerModule)
        {
            _regionsArray = new bool[size / pixelsPerModule, size / pixelsPerModule];

            for (int x = 0; x < size + offset; x += pixelsPerModule)
                for (int y = 0; y < size + offset; y += pixelsPerModule)
                {
                    bool module = this.QrCodeData.ModuleMatrix[(y + pixelsPerModule) / pixelsPerModule - 1][(x + pixelsPerModule) / pixelsPerModule - 1];

                    if (module)
                        _regionsArray[x / pixelsPerModule, y / pixelsPerModule] = true;
                }
        }

        public Bitmap GetModernGraphic(int pixelsPerModule, Color darkColor, Color lightColor, Bitmap icon = null, int iconSizePercent = 15, int iconBorderWidth = 6, bool drawQuietZones = true)
        {
            var size = (this.QrCodeData.ModuleMatrix.Count - (drawQuietZones ? 0 : 8)) * pixelsPerModule;
            var offset = drawQuietZones ? 0 : 4 * pixelsPerModule;

            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var gfx = Graphics.FromImage(bmp))
            using (var lightBrush = new SolidBrush(lightColor))
            using (var darkBrush = new SolidBrush(darkColor))
            {
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gfx.CompositingQuality = CompositingQuality.HighQuality;
                gfx.Clear(lightColor);

                var drawIconFlag = icon != null && iconSizePercent > 0 && iconSizePercent <= 100;

                GraphicsPath iconPath = null;
                float iconDestWidth = 0, iconDestHeight = 0, iconX = 0, iconY = 0;

                if (drawIconFlag)
                {
                    iconDestWidth = iconSizePercent * bmp.Width / 100f;
                    iconDestHeight = drawIconFlag ? iconDestWidth * icon.Height / icon.Width : 0;
                    iconX = (bmp.Width - iconDestWidth) / 2;
                    iconY = (bmp.Height - iconDestHeight) / 2;

                    var centerDest = new RectangleF(iconX - iconBorderWidth, iconY - iconBorderWidth, iconDestWidth + iconBorderWidth * 2, iconDestHeight + iconBorderWidth * 2);
                    iconPath = this.CreateRoundedRectanglePath(centerDest, iconBorderWidth * 2);
                }

                FillInRegionsArray(size, offset, pixelsPerModule);

                for (var x = 0; x < size + offset; x += pixelsPerModule)
                    for (var y = 0; y < size + offset; y += pixelsPerModule)
                    {
                        var module = this.QrCodeData.ModuleMatrix[(y + pixelsPerModule) / pixelsPerModule - 1][(x + pixelsPerModule) / pixelsPerModule - 1];

                        int rounded = Convert.ToInt32(pixelsPerModule / 2.5);

                        if (isFrame(x, y, size, pixelsPerModule))
                            rounded = 0;

                        if (module)
                        {
                            Rectangle rectangle = new Rectangle(x - offset, y - offset, pixelsPerModule, pixelsPerModule);
                            GraphicsPath spot = RoundedRect(rectangle, pixelsPerModule, rounded);

                            if (drawIconFlag)
                            {
                                Region region = new Region(spot);

                                region.Exclude(iconPath);
                                gfx.FillRegion(darkBrush, region);
                            }
                            else
                                gfx.FillPath(darkBrush, spot);
                        }
                        else
                            gfx.FillRectangle(lightBrush, new Rectangle(x - offset, y - offset, pixelsPerModule, pixelsPerModule));
                    }

                if (drawIconFlag)
                {
                    var iconDestRect = new RectangleF(iconX, iconY, iconDestWidth, iconDestHeight);
                    gfx.DrawImage(icon, iconDestRect, new RectangleF(0, 0, icon.Width, icon.Height), GraphicsUnit.Pixel);
                }

                gfx.Save();
            }

            return bmp;
        }

        public GraphicsPath RoundedRect(Rectangle bounds, int pixelsPerModule, int radius)
        {
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int spacing = 2;
            bounds.Y += spacing / 2;
            bounds.Height -= spacing * 2;

            int diameter = radius * 2;

            if (isAloneBar(bounds.X, bounds.Y, pixelsPerModule))
                return FullLeftCircle(bounds, diameter);

            if (isLeftStartBar(bounds.X, bounds.Y, pixelsPerModule))
                return SemiLeftCircle(bounds, diameter);

            if (isRightEndBar(bounds.X, bounds.Y, pixelsPerModule))
                return SemiRightCircle(bounds, diameter);

            path.AddRectangle(bounds);
            return path;
        }

        private GraphicsPath FullLeftCircle(Rectangle bounds, int diameter)
        {
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            GraphicsPath path = new GraphicsPath();

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private GraphicsPath SemiLeftCircle(Rectangle bounds, int diameter)
        {
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            GraphicsPath path = new GraphicsPath();

            Rectangle r = new Rectangle(bounds.X + (diameter / 2), bounds.Y, bounds.Width, bounds.Height);
            path.AddRectangle(r);

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private GraphicsPath SemiRightCircle(Rectangle bounds, int diameter)
        {
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            GraphicsPath path = new GraphicsPath();

            Rectangle r = new Rectangle(bounds.X, bounds.Y, bounds.Width - (diameter / 2), bounds.Height);
            path.AddRectangle(r);

            // top left arc  

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;

            path.CloseFigure();
            return path;
        }

        internal GraphicsPath CreateRoundedRectanglePath(RectangleF rect, int cornerRadius)
        {
            var roundedRect = new GraphicsPath();
            roundedRect.AddArc(rect.X, rect.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
            roundedRect.AddLine(rect.X + cornerRadius, rect.Y, rect.Right - cornerRadius * 2, rect.Y);
            roundedRect.AddArc(rect.X + rect.Width - cornerRadius * 2, rect.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
            roundedRect.AddLine(rect.Right, rect.Y + cornerRadius * 2, rect.Right, rect.Y + rect.Height - cornerRadius * 2);
            roundedRect.AddArc(rect.X + rect.Width - cornerRadius * 2, rect.Y + rect.Height - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 0, 90);
            roundedRect.AddLine(rect.Right - cornerRadius * 2, rect.Bottom, rect.X + cornerRadius * 2, rect.Bottom);
            roundedRect.AddArc(rect.X, rect.Bottom - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
            roundedRect.AddLine(rect.X, rect.Bottom - cornerRadius * 2, rect.X, rect.Y + cornerRadius * 2);
            roundedRect.CloseFigure();
            return roundedRect;
        }
    }

    public static class QRCodeHelper
    {
        public static Bitmap GetQRCode(string plainText, int pixelsPerModule, Color darkColor, Color lightColor, ECCLevel eccLevel, bool forceUtf8 = false, bool utf8BOM = false, EciMode eciMode = EciMode.Default, int requestedVersion = -1, Bitmap icon = null, int iconSizePercent = 15, int iconBorderWidth = 6, bool drawQuietZones = true)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(plainText, eccLevel, forceUtf8, utf8BOM, eciMode, requestedVersion))
            using (var qrCode = new QRCode(qrCodeData))
                return qrCode.GetModernGraphic(pixelsPerModule, darkColor, lightColor, icon, iconSizePercent, iconBorderWidth, drawQuietZones);
        }


        public static byte[] BitmapToBytes(Bitmap bitmap, int? size)
        {
            if (size.HasValue)
                bitmap = ResizeImage(bitmap, size.Value);

            return ToBytes(bitmap);
        }

        private static byte[] ToBytes(this Image image, ImageFormat format)
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream, format);
                stream.Position = 0;

                return ToBytes(stream);
            }
        }

        private static byte[] ToBytes(this Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Bmp);
                stream.Position = 0;

                return ToBytes(stream);
            }
        }

        private static byte[] ToBytes(Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        private static Bitmap ResizeImage(Bitmap image, int size)
        {
            var destRect = new Rectangle(0, 0, size, size);
            var destImage = new Bitmap(size, size);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}

#endif