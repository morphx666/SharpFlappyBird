using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.IO;
using Eto;

namespace SharpFlappyBird {
    public class MainForm : Form {
        protected Drawable Canvas;
        private readonly FlappyBird bird;

        public MainForm() {
            JsonReader.Load(this);

            FontFamily ff = new FontFamily("04b_19");

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

                if(ff.LocalizedName != "04b_19") {
                    MessageBox.Show(@"Please install the font 'flappy.ttf' under the folder 'Assets\font' before running the game", MessageBoxType.Error);
                    Application.Instance.Quit();
                } else {
                    MessageBox.Show("The Eto.Forms version does not currently support the following features:" +
                                    "\n" +
                                    "\n - Text Rendering outlining (missing support for 'GraphicsPath.AddString')" +
                                    "\n - Audio under non-Windows platforms (due to an apparent bug in the 'MannagedBass' library)",
                                    "Eto.Forms framework limitations",
                                    MessageBoxType.Information);
                }
                };

            Canvas.Paint += (object s, PaintEventArgs e) => bird.DrawScene(e);
        }

        private static string GetAsset(string subFolder, string assetFileName) {
            if(Eto.Platform.Detect.IsMac) { // FIXME: Need to figure out a better a way to manage the assets
                string path = EtoEnvironment.GetFolderPath(EtoSpecialFolder.ApplicationResources);
                return Path.GetFullPath(Path.Combine(path, "../MacOS", assetFileName));
            }
            return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }
    }
}