using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace SharpFlappyBird {
    public partial class FormMain : Form {
        private readonly FlappyBird fbird;
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

            fbird = new FlappyBird(this,
                                  Image.FromFile(GetAsset("images", "bird.png")),
                                  bImg,
                                  gImg,
                                  Image.FromFile(GetAsset("images", "pipe.png")),
                                  fc.Families[0],
                                  GetAsset("sounds", "jump.ogg"),
                                  GetAsset("sounds", "score.ogg"),
                                  GetAsset("sounds", "gameover.ogg"),
                                  GetAsset("sounds", "song.ogg"));

            fbird.Exit += () => this.Close();

            this.Shown += (_, __) => {
                Rectangle sb = Screen.FromControl(this).WorkingArea;
                while((int)((bImg.Height + gImg.Height) * fbird.Scale) > sb.Height - SystemInformation.CaptionHeight) fbird.Scale -= 0.05f;

                // Resize client area
                this.ClientSize = new Size((int)(bImg.Width * fbird.Scale),
                                           (int)((bImg.Height + gImg.Height) * fbird.Scale));

                // Center screen
                this.Location = new Point((sb.Width - this.Width) / 2,
                                          (sb.Height - this.Height) / 2);
            };

            if(!fbird.CanRun) Application.Exit();
        }

        private static string GetAsset(string subFolder, string assetFileName) {
            return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }

        protected override void OnPaint(PaintEventArgs e) {
            fbird.DrawScene(e);
        }
    }
}