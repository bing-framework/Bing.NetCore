using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

// http://www.codeproject.com/aspnet/CaptchaImage.asp
// Jeff Atwood
// http://www.codinghorror.com/</remarks>

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 验证码.
    /// </summary>
    public class CaptchaImage
    {
        /// <summary>
        /// The _rand
        /// </summary>
        private readonly Random _rand;

        /// <summary>
        /// The _font family name
        /// </summary>
        private string _fontFamilyName;

        /// <summary>
        /// The _height
        /// </summary>
        private int _height;

        /// <summary>
        /// The _random text chars
        /// </summary>
        private string _randomTextChars;

        /// <summary>
        /// The _random text length
        /// </summary>
        private int _randomTextLength;

        /// <summary>
        /// The _width
        /// </summary>
        private int _width;

        #region "Public Enums"

        /// <summary>
        /// Amount of background noise to add to rendered image
        /// </summary>
        public enum BackgroundNoiseLevel
        {
            /// <summary>
            /// The none
            /// </summary>
            None,

            /// <summary>
            /// The low
            /// </summary>
            Low,

            /// <summary>
            /// The medium
            /// </summary>
            Medium,

            /// <summary>
            /// The high
            /// </summary>
            High,

            /// <summary>
            /// The extreme
            /// </summary>
            Extreme
        }

        /// <summary>
        /// Amount of random font warping to apply to rendered text
        /// </summary>
        public enum FontWarpFactor
        {
            /// <summary>
            /// The none
            /// </summary>
            None,

            /// <summary>
            /// The low
            /// </summary>
            Low,

            /// <summary>
            /// The medium
            /// </summary>
            Medium,

            /// <summary>
            /// The high
            /// </summary>
            High,

            /// <summary>
            /// The extreme
            /// </summary>
            Extreme
        }

        /// <summary>
        /// Amount of curved line noise to add to rendered image
        /// </summary>
        public enum LineNoiseLevel
        {
            /// <summary>
            /// The none
            /// </summary>
            None,

            /// <summary>
            /// The low
            /// </summary>
            Low,

            /// <summary>
            /// The medium
            /// </summary>
            Medium,

            /// <summary>
            /// The high
            /// </summary>
            High,

            /// <summary>
            /// The extreme
            /// </summary>
            Extreme
        }

        #endregion

        #region "Public Properties"

        /// <summary>
        /// Returns a GUID that uniquely identifies this Captcha
        /// </summary>
        /// <value>The unique identifier.</value>
        public string UniqueId { get; private set; }

        /// <summary>
        /// Returns the date and time this image was last rendered
        /// </summary>
        /// <value>The rendered at.</value>
        public DateTime RenderedAt { get; private set; }

        /// <summary>
        /// Font family to use when drawing the Captcha text. If no font is provided, a random font will be chosen from the font whitelist for each character.
        /// </summary>
        /// <value>The font.</value>
        public string Font
        {
            get { return _fontFamilyName; }
            set
            {
                Font font1 = null;
                try
                {
                    font1 = new Font(value, 9f);
                    _fontFamilyName = value;
                }
                catch (Exception ex)
                {
                    _fontFamilyName = FontFamily.GenericSerif.Name;
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    if (font1 != null) font1.Dispose();
                }
            }
        }

        /// <summary>
        /// Amount of random warping to apply to the Captcha text.
        /// </summary>
        /// <value>The font warp.</value>
        public FontWarpFactor FontWarp { get; set; }

        /// <summary>
        /// Amount of background noise to apply to the Captcha image.
        /// </summary>
        /// <value>The background noise.</value>
        public BackgroundNoiseLevel BackgroundNoise { get; set; }

        /// <summary>
        /// Gets or sets the line noise.
        /// </summary>
        /// <value>The line noise.</value>
        public LineNoiseLevel LineNoise { get; set; }

        /// <summary>
        /// A string of valid characters to use in the Captcha text.
        /// A random character will be selected from this string for each character.
        /// </summary>
        /// <value>The text chars.</value>
        public string TextChars
        {
            get { return _randomTextChars; }
            set
            {
                _randomTextChars = value;
                Text = GenerateRandomText();
            }
        }

        /// <summary>
        /// Number of characters to use in the Captcha text.
        /// </summary>
        /// <value>The length of the text.</value>
        public int TextLength
        {
            get { return _randomTextLength; }
            set
            {
                _randomTextLength = value;
                Text = GenerateRandomText();
            }
        }

        /// <summary>
        /// Returns the randomly generated Captcha text.
        /// </summary>
        /// <value>The text.</value>
        public string Text { get; private set; }

        /// <summary>
        /// Width of Captcha image to generate, in pixels
        /// </summary>
        /// <value>The width.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">width;width must be greater than 60.</exception>
        public int Width
        {
            get { return _width; }
            set
            {
                if ((value < 60))
                {
                    throw new ArgumentOutOfRangeException("width", value, "width must be greater than 60.");
                }
                _width = value;
            }
        }

        /// <summary>
        /// Height of Captcha image to generate, in pixels
        /// </summary>
        /// <value>The height.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">height;height must be greater than 20.</exception>
        public int Height
        {
            get { return _height; }
            set
            {
                if (value < 20)
                {
                    throw new ArgumentOutOfRangeException("height", value, "height must be greater than 20.");
                }
                _height = value;
            }
        }

        /// <summary>
        /// A semicolon-delimited list of valid fonts to use when no font is provided.
        /// </summary>
        /// <value>The font whitelist.</value>
        public string FontWhitelist { get; set; }

        /// <summary>
        /// Background color for the captcha image
        /// </summary>
        /// <value>The color of the back.</value>
        public Color BackColor { get; set; }

        /// <summary>
        /// Color of captcha text
        /// </summary>
        /// <value>The color of the font.</value>
        public Color FontColor { get; set; }

        /// <summary>
        /// Color for dots in the background noise
        /// </summary>
        /// <value>The color of the noise.</value>
        public Color NoiseColor { get; set; }

        /// <summary>
        /// Color for the background lines of the captcha image
        /// </summary>
        /// <value>The color of the line.</value>
        public Color LineColor { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="CaptchaImage"/> class.
        /// </summary>
        public CaptchaImage()
        {
            LineColor = Color.Black;
            NoiseColor = Color.Black;
            FontColor = Color.Black;
            BackColor = Color.White;
            _rand = new Random();
            FontWarp = FontWarpFactor.Low;
            BackgroundNoise = BackgroundNoiseLevel.Low;
            LineNoise = LineNoiseLevel.None;
            _width = 180;
            _height = 50;
            _randomTextLength = 5;
            _randomTextChars = "ACDEFGHJKLNPQRTUVXYZ2346789";
            _fontFamilyName = "";
            // -- a list of known good fonts in on both Windows XP and Windows Server 2003
            FontWhitelist = "arial;arial black;comic sans ms;courier new;estrangelo edessa;franklin gothic medium;" +
                            "georgia;lucida console;lucida sans unicode;mangal;microsoft sans serif;palatino linotype;" +
                            "sylfaen;tahoma;times new roman;trebuchet ms;verdana";
            Text = GenerateRandomText();
            RenderedAt = DateTime.Now;
            UniqueId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Forces a new Captcha image to be generated using current property value settings.
        /// </summary>
        /// <returns>Bitmap.</returns>
        public Bitmap RenderImage()
        {
            return GenerateImagePrivate();
        }

        /// <summary>
        /// Returns a random font family from the font whitelist
        /// </summary>
        /// <returns>System.String.</returns>
        private string RandomFontFamily()
        {
            string[] ff = FontWhitelist.Split(';');
            //-- small optimization so we don't have to split for each char
            return ff[_rand.Next(0, ff.Length)];
        }

        /// <summary>
        /// generate random text for the CAPTCHA
        /// </summary>
        /// <returns>System.String.</returns>
        private string GenerateRandomText()
        {
            var sb = new StringBuilder(_randomTextLength);
            int maxLength = _randomTextChars.Length;
            for (int n = 0; n <= _randomTextLength - 1; n++)
            {
                sb.Append(_randomTextChars.Substring(_rand.Next(maxLength), 1));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a random point within the specified x and y ranges
        /// </summary>
        /// <param name="xmin">The xmin.</param>
        /// <param name="xmax">The xmax.</param>
        /// <param name="ymin">The ymin.</param>
        /// <param name="ymax">The ymax.</param>
        /// <returns>PointF.</returns>
        private PointF RandomPoint(int xmin, int xmax, int ymin, int ymax)
        {
            return new PointF(_rand.Next(xmin, xmax), _rand.Next(ymin, ymax));
        }

        /// <summary>
        /// Returns a random point within the specified rectangle
        /// </summary>
        /// <param name="rect">The rect.</param>
        /// <returns>PointF.</returns>
        private PointF RandomPoint(Rectangle rect)
        {
            return RandomPoint(rect.Left, rect.Width, rect.Top, rect.Bottom);
        }

        /// <summary>
        /// Returns a GraphicsPath containing the specified string and font
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="f">The f.</param>
        /// <param name="r">The r.</param>
        /// <returns>GraphicsPath.</returns>
        private GraphicsPath TextPath(string s, Font f, Rectangle r)
        {
            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Near;
            sf.LineAlignment = StringAlignment.Near;
            var gp = new GraphicsPath();
            gp.AddString(s, f.FontFamily, (int)f.Style, f.Size, r, sf);
            return gp;
        }

        /// <summary>
        /// Returns the CAPTCHA font in an appropriate size
        /// </summary>
        /// <returns>Font.</returns>
        private Font GetFont()
        {
            float fsize = 0.0f;
            string fname = _fontFamilyName;
            if (fname == "")
            {
                fname = RandomFontFamily();
            }
            switch (FontWarp)
            {
                case FontWarpFactor.None:
                    fsize = Convert.ToInt32(_height * 0.7);
                    break;

                case FontWarpFactor.Low:
                    fsize = Convert.ToInt32(_height * 0.8);
                    break;

                case FontWarpFactor.Medium:
                    fsize = Convert.ToInt32(_height * 0.85);
                    break;

                case FontWarpFactor.High:
                    fsize = Convert.ToInt32(_height * 0.9);
                    break;

                case FontWarpFactor.Extreme:
                    fsize = Convert.ToInt32(_height * 0.95);
                    break;
            }
            return new Font(fname, fsize, FontStyle.Bold);
        }

        /// <summary>
        /// Renders the CAPTCHA image
        /// </summary>
        /// <returns>Bitmap.</returns>
        private Bitmap GenerateImagePrivate()
        {
            Font fnt;
            Rectangle rect;
            Brush br;
            var bmp = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
            using (Graphics gr = Graphics.FromImage(bmp))
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                //-- fill an empty white rectangle
                rect = new Rectangle(0, 0, _width, _height);
                using (br = new SolidBrush(BackColor)) // was Color.White
                {
                    gr.FillRectangle(br, rect);
                }

                int charOffset = 0;
                double charWidth = _width / _randomTextLength;
                Rectangle rectChar;
                using (br = new SolidBrush(FontColor)) // was Color.Black
                {
                    foreach (char c in Text)
                    {
                        //-- establish font and draw area
                        using (fnt = GetFont())
                        {
                            rectChar = new Rectangle(Convert.ToInt32(charOffset * charWidth), 0,
                                                     Convert.ToInt32(charWidth), _height);
                            //-- warp the character
                            using (GraphicsPath gp = TextPath(c.ToString(), fnt, rectChar))
                            {
                                WarpText(gp, rectChar);
                                //-- draw the character
                                gr.FillPath(br, gp);
                            }
                        }
                        charOffset += 1;
                    }
                }
                AddNoise(gr, rect);
                AddLine(gr, rect);
                //-- clean up unmanaged resources
            }
            return bmp;
        }

        /// <summary>
        /// Warp the provided text GraphicsPath by a variable amount
        /// </summary>
        /// <param name="textPath">The text path.</param>
        /// <param name="rect">The rect.</param>
        private void WarpText(GraphicsPath textPath, Rectangle rect)
        {
            float WarpDivisor = 1.0f;
            float RangeModifier = 1.0f;
            switch (FontWarp)
            {
                case FontWarpFactor.None:
                    return;

                case FontWarpFactor.Low:
                    WarpDivisor = 6;
                    RangeModifier = 1;
                    break;

                case FontWarpFactor.Medium:
                    WarpDivisor = 5;
                    RangeModifier = 1.3f;
                    break;

                case FontWarpFactor.High:
                    WarpDivisor = 4.5f;
                    RangeModifier = 1.4f;
                    break;

                case FontWarpFactor.Extreme:
                    WarpDivisor = 4;
                    RangeModifier = 1.5f;
                    break;
            }
            var rectF = new RectangleF(Convert.ToSingle(rect.Left), 0, Convert.ToSingle(rect.Width), rect.Height);
            int hrange = Convert.ToInt32(rect.Height / WarpDivisor);
            int wrange = Convert.ToInt32(rect.Width / WarpDivisor);
            int left = rect.Left - Convert.ToInt32(wrange * RangeModifier);
            int top = rect.Top - Convert.ToInt32(hrange * RangeModifier);
            int width = rect.Left + rect.Width + Convert.ToInt32(wrange * RangeModifier);
            int height = rect.Top + rect.Height + Convert.ToInt32(hrange * RangeModifier);
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (width > Width) width = Width;
            if (height > Height) height = Height;
            PointF leftTop = RandomPoint(left, left + wrange, top, top + hrange);
            PointF rightTop = RandomPoint(width - wrange, width, top, top + hrange);
            PointF leftBottom = RandomPoint(left, left + wrange, height - hrange, height);
            PointF rightBottom = RandomPoint(width - wrange, width, height - hrange, height);
            var points = new[] { leftTop, rightTop, leftBottom, rightBottom };
            var m = new Matrix();
            m.Translate(0, 0);
            textPath.Warp(points, rectF, m, WarpMode.Perspective, 0);
        }

        /// <summary>
        /// Add a variable level of graphic noise to the image
        /// </summary>
        /// <param name="graphics1">The graphics1.</param>
        /// <param name="rect">The rect.</param>
        private void AddNoise(Graphics graphics1, Rectangle rect)
        {
            int density = 0;
            int size = 0;
            switch (BackgroundNoise)
            {
                case BackgroundNoiseLevel.None:
                    return;

                case BackgroundNoiseLevel.Low:
                    density = 30;
                    size = 40;
                    break;

                case BackgroundNoiseLevel.Medium:
                    density = 18;
                    size = 40;
                    break;

                case BackgroundNoiseLevel.High:
                    density = 16;
                    size = 39;
                    break;

                case BackgroundNoiseLevel.Extreme:
                    density = 12;
                    size = 38;
                    break;
            }
            using (var br = new SolidBrush(NoiseColor))
            {
                int max = Convert.ToInt32(Math.Max(rect.Width, rect.Height) / size);
                for (int i = 0; i <= Convert.ToInt32((rect.Width * rect.Height) / density); i++)
                {
                    graphics1.FillEllipse(br, _rand.Next(rect.Width), _rand.Next(rect.Height), _rand.Next(max),
                                          _rand.Next(max));
                }
            }
        }

        /// <summary>
        /// Add variable level of curved lines to the image
        /// </summary>
        /// <param name="graphics1">The graphics1.</param>
        /// <param name="rect">The rect.</param>
        private void AddLine(Graphics graphics1, Rectangle rect)
        {
            int length = 0;
            float width = 1.0f;
            int linecount = 0;
            switch (LineNoise)
            {
                case LineNoiseLevel.None:
                    return;

                case LineNoiseLevel.Low:
                    length = 4;
                    width = Convert.ToSingle(_height / 31.25);
                    // 1.6
                    linecount = 1;
                    break;

                case LineNoiseLevel.Medium:
                    length = 5;
                    width = Convert.ToSingle(_height / 27.7777);
                    // 1.8
                    linecount = 1;
                    break;

                case LineNoiseLevel.High:
                    length = 3;
                    width = Convert.ToSingle(_height / 25);
                    // 2.0
                    linecount = 2;
                    break;

                case LineNoiseLevel.Extreme:
                    length = 3;
                    width = Convert.ToSingle(_height / 22.7272);
                    // 2.2
                    linecount = 3;
                    break;
            }
            var pf = new PointF[length + 1];
            using (var p = new Pen(LineColor, width)) // was Color.Black
            {
                for (int l = 1; l <= linecount; l++)
                {
                    for (int i = 0; i <= length; i++)
                    {
                        pf[i] = RandomPoint(rect);
                    }
                    graphics1.DrawCurve(p, pf, 1.75f);
                }
            }
        }
    }
}
