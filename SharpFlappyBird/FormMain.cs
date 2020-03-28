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

            Image bImg = Image.FromFile(GetAsset("background.png"));
            Image gImg = Image.FromFile(GetAsset("ground.png"));

            fc.AddFontFile(GetAsset("flappy.ttf", "font"));

            bird = new FlappyBird(this,
                                  Image.FromFile(GetAsset("bird.png")),
                                  bImg,
                                  gImg,
                                  Image.FromFile(GetAsset("pipe.png")),
                                  fc.Families[0]);

            // Resize client area
            this.ClientSize = new Size((int)(bImg.Width * bird.Scale),
                                       (int)((bImg.Height + gImg.Height) * bird.Scale));

            // Center screen
            Rectangle sb = Screen.FromControl(this).Bounds;
            this.Location = new Point((sb.Width - this.Width) / 2,
                                      (sb.Height - (this.Height + SystemInformation.CaptionHeight)) / 2);
        }

        private static string GetAsset(string assetFileName, string subFolder = "") {
            return Path.GetFullPath(Path.Combine("Assets", subFolder, assetFileName));
        }

        protected override void OnPaint(PaintEventArgs e) {
            bird.DrawScene(e);
        }
    }
}