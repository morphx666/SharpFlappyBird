using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.IO;
using Eto;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Eto.Threading;

namespace SharpFlappyBird {
    public class MainForm : Form {
        protected Drawable Canvas;
        private readonly FlappyBird bird;
        private static Dictionary<FontData, Bitmap> cache = new Dictionary<FontData, Bitmap>();

        public MainForm() {
            JsonReader.Load(this);

            FontFamily ff = Fonts.Monospace(12).Family;
            try {
                ff = new FontFamily("04b_19");
            } catch { }

            Image bImg = new Bitmap(GetAsset("images", "background.png"));
            Image gImg = new Bitmap(GetAsset("images", "ground.png"));

            bird = new FlappyBird(Canvas,
                                  new Bitmap(GetAsset("images", "bird.png")),
                                  bImg,
                                  gImg,
                                  new Bitmap(GetAsset("images", "pipe.png")),
                                  ff,
                                  GetAsset("sounds", "jump.ogg"),
                                  GetAsset("sounds", "score.ogg"),
                                  GetAsset("sounds", "gameover.ogg"),
                                  GetAsset("sounds", "song.ogg"));

            bird.Exit += () => Application.Instance.Quit();

            this.Shown += (_, __) => {
                RectangleF sb = Screen.FromPoint(PointF.Empty).WorkingArea;
                while((int)((bImg.Height + gImg.Height) * bird.Scale) > sb.Height - 16) bird.Scale -= 0.05f;

                // Resize client area
                this.ClientSize = new Size((int)(bImg.Width * bird.Scale),
                                           (int)((bImg.Height + gImg.Height) * bird.Scale));

                // Center screen
                Task.Run(() => {
                    if(Platform.Detect.IsGtk) System.Threading.Thread.Sleep(250); // Because... reasons...
                    Application.Instance.Invoke(() => this.Location = new Point((int)((sb.Width - this.Width) / 2),
                                              (int)(sb.Height - this.Height) / 2));
                });

                if(ff?.LocalizedName != "04b_19") {
                    MessageBox.Show(@"Please install the font 'flappy.ttf' under the folder 'Assets\font' before running the game", MessageBoxType.Error);
                    Application.Instance.Quit();
                }
            };

            Canvas.Paint += (object s, PaintEventArgs e) => bird.DrawScene(e);
        }

        private static string GetAsset(string subFolder, string assetFileName) {
            if(Platform.Detect.IsMac) {
                string path = EtoEnvironment.GetFolderPath(EtoSpecialFolder.ApplicationResources);
                return Path.GetFullPath(Path.Combine(path, "../MonoBundle", assetFileName));
            } else
                return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }

        #region Simulate Path.AddString under NetStandard 2.0
        public static int CachedTextCount { get => cache.Count; }

        private struct FontData {
            private readonly string text;
            private readonly Font font;
            private readonly int hash;

            public FontData(string text, Font font) {
                this.text = text;
                this.font = font;

                hash = text.GetHashCode() | font.GetHashCode();
            }

            public static bool operator ==(FontData fd1, FontData fd2) {
                return fd1.text == fd2.text &&
                       fd1.font == fd2.font;
            }

            public static bool operator !=(FontData fd1, FontData fd2) {
                return !(fd1 == fd2);
            }

            public override bool Equals(object obj) {
                return this == (FontData)obj;
            }

            public override int GetHashCode() {
                return hash;
                //return base.GetHashCode(); // This crashes under macOS
            }
        }

        private static GraphicsPath FromString(Graphics pg, string s, Font f, Rectangle layoutRect, int boderSize) {
            GraphicsPath p = new GraphicsPath();

            using(Bitmap bmp = new Bitmap(layoutRect.Width, layoutRect.Height, pg)) {
                using(Graphics g = new Graphics(bmp)) {
                    g.Clear(Colors.Black);
                    g.DrawText(f, Brushes.White, PointF.Empty, s);
                }

                int b = boderSize / 2;
                int x;
                int lx = 0;
                using(BitmapData bd = bmp.Lock()) {
                    for(int y = 0; y < layoutRect.Height; y++) { // Flood fill algorythm (see VB6's FormShaper Control)
                        x = 0;
                        while(x < layoutRect.Width) {
                            while(x < layoutRect.Width && bd.GetPixel(x, y) == Colors.Black) x++;
                            if(x < layoutRect.Width) {
                                lx = x;
                                while(x < layoutRect.Width && bd.GetPixel(x, y) != Colors.Black) x++;
                                if(x > layoutRect.Width) x = layoutRect.Width;
                                p.AddRectangle(Rectangle.FromSides(lx + b, y, x + b, y + 1));
                            }
                        }
                    }
                }
            }
            return p;
        }

        public static Bitmap CreateString(Graphics pg, string text, Brush color, Font font, Rectangle layoutRect, int borderSie = 6) {
            FontData fd = new FontData(text, font);
            if(cache.ContainsKey(fd)) return cache[fd];

            Bitmap bmp = new Bitmap(layoutRect.Width, layoutRect.Height, PixelFormat.Format32bppRgba);
            using(Graphics g = new Graphics(bmp)) {
                using(GraphicsPath p = FromString(pg, text, font, layoutRect, borderSie)) {
                    g.DrawPath(new Pen(Brushes.Black, borderSie), p);
                    g.FillPath(color, p);
                }
            }

            cache.Add(fd, bmp);
            return bmp;
        }
        #endregion
    }
}