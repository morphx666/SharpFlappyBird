using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.IO;
using Eto;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace SharpFlappyBird {
    public class MainForm : Form {
        protected Drawable Canvas;
        private readonly FlappyBird bird;

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
                this.Location = new Point((int)((sb.Width - this.Width) / 2),
                                          (int)(sb.Height - this.Height) / 2);

                if(ff?.LocalizedName != "04b_19") {
                    MessageBox.Show(@"Please install the font 'flappy.ttf' under the folder 'Assets\font' before running the game", MessageBoxType.Error);
                    Application.Instance.Quit();
                } else {
                    //MessageBox.Show("The Eto.Forms version does not currently support the following features:" +
                    //                "\n" +
                    //                "\n - Text Rendering outlining (missing support for 'GraphicsPath.AddString')",
                    //                "Eto.Forms framework limitations",
                    //                MessageBoxType.Information);
                }
            };

            Canvas.Paint += (object s, PaintEventArgs e) => bird.DrawScene(e);
        }

        private static string GetAsset(string subFolder, string assetFileName) {
            if(Platform.Detect.IsMac) { // FIXME: Need to figure out a better a way to manage the assets
                string path = EtoEnvironment.GetFolderPath(EtoSpecialFolder.ApplicationResources);
                return Path.GetFullPath(Path.Combine(path, "../MacOS", assetFileName));
            }
            return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }

        private struct FontData {
            private readonly string text;
            private readonly FontFamily family;
            private readonly FontStyle style;
            private readonly float emSize;

            public FontData(string text, FontFamily family, FontStyle style, float emSize) {
                this.text = text;
                this.family = family;
                this.style = style;
                this.emSize = emSize;
            }

            public static bool operator ==(FontData fd1, FontData fd2) {
                return fd1.text == fd2.text &&
                       fd1.family == fd2.family &&
                       fd1.style == fd2.style &&
                       fd1.emSize == fd2.emSize;
            }

            public static bool operator !=(FontData fd1, FontData fd2) {
                return !(fd1 == fd2);
            }

            public override bool Equals(object obj) {
                return this == (FontData)obj;
            }

            public override int GetHashCode() {
                return base.GetHashCode();
            }
        }

        private static Dictionary<FontData, GraphicsPath> cache = new Dictionary<FontData, GraphicsPath>();
        public static GraphicsPath FromString(Graphics pg, string s, FontFamily family, FontStyle style, float emSize, Rectangle layoutRect) {
            FontData fd = new FontData(s, family, style, emSize);
            if(cache.ContainsKey(fd)) return cache[fd];

            GraphicsPath p = new GraphicsPath();

            using(Bitmap bmp = new Bitmap(layoutRect.Width, layoutRect.Height, pg)) {
                using(Graphics g = new Graphics(bmp)) {
                    g.Clear(Colors.Black);
                    using(Font f = new Font(family, emSize)) {
                        g.DrawText(f, Brushes.White, PointF.Empty, s);
                    }
                }

                int x;
                int lx = 0;
                for(int y = 0; y < layoutRect.Height; y++) {
                    x = 0;

                    while(x < layoutRect.Width) {
                        while(x < layoutRect.Width && bmp.GetPixel(x, y) == Colors.Black) x++;

                        if(x < layoutRect.Width) {
                            lx = x;
                            while(x < layoutRect.Width && bmp.GetPixel(x, y) != Colors.Black) x++;

                            if(x > layoutRect.Width) x = layoutRect.Width;

                            Rectangle r = Rectangle.FromSides(lx, y, x, y + 1);
                            r.Offset(layoutRect.X, layoutRect.Y);
                            p.AddRectangle(r);
                        }
                    }
                }

                cache.Add(fd, p);
                return p;
            }
        }
    }
}