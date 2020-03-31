using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace SharpFlappyBird {
    public partial class FormMain : Form {
        private readonly FlappyBird bird;
        private readonly PrivateFontCollection fc = new PrivateFontCollection();

        public FormMain() {
            InitializeComponent();

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer,
                          true);

            Image bImg = Image.FromFile(GetAsset("images", "background.png"));
            Image gImg = Image.FromFile(GetAsset("images", "ground.png"));

            fc.AddFontFile(GetAsset("font", "flappy.ttf"));

            bird = new FlappyBird(this,
                                  Image.FromFile(GetAsset("images", "bird.png")),
                                  bImg,
                                  gImg,
                                  Image.FromFile(GetAsset("images", "pipe.png")),
                                  fc.Families[0],
                                  GetAsset("sounds", "jump.ogg"),
                                  GetAsset("sounds", "score.ogg"),
                                  GetAsset("sounds", "gameover.ogg"),
                                  GetAsset("sounds", "song.ogg"));

            this.Shown += (_, __) => {
                // Resize client area
                this.ClientSize = new Size((int)(bImg.Width * bird.Scale),
                                           (int)((bImg.Height + gImg.Height) * bird.Scale));

                // Center screen
                Rectangle sb = Screen.FromControl(this).WorkingArea;
                this.Location = new Point((sb.Width - this.Width) / 2,
                                          (sb.Height - (this.Height + SystemInformation.CaptionHeight)) / 2);
            };
        }

        private static string GetAsset(string subFolder, string assetFileName) {
            return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }

        protected override void OnPaint(PaintEventArgs e) {
            bird.DrawScene(e);
        }
    }
}