using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#if WINFORMS
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing;
#else
using Eto.Drawing;
using Eto.Forms;
#endif
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RayCasting;
using ManagedBass;

namespace SharpFlappyBird {
    public class FlappyBird : Vector {
        private float mScale = 1.0f;
        public bool CanRun = true;
        private bool isClosing = false;

        private enum SpriteStates {
            Waiting = -1,
            Up = 0,
            Normal = 1,
            Down = 2,
            Falling = 3
        }

        private enum GameStates {
            Normal = 0,
            Crashed = 1,
            GameOver
        }

        private class Pipe {
            public int FrameCount;
            public double GapPosition;
            public bool Passed;

            public Pipe(int frameCount, double gapPosition, bool passed) {
                FrameCount = frameCount;
                GapPosition = gapPosition;
                Passed = passed;
            }
        }

        private Vector velocity;
        private Vector acceleration;

        private readonly Image sprite;
        private int spriteIndex;
        private readonly int spriteW2;
        private readonly int spriteH2;
        private RectangleF spriteRect;
        private SpriteStates spriteState = SpriteStates.Waiting;
        private int spriteAngle = 0;
        private readonly Control surface;
        private readonly int horizontalSpeed = 5;
        private int score;

        private GameStates gameState = GameStates.Normal;

        private readonly Image backgroundImage;
        private readonly Image groundImage;
        private readonly Image pipeImage;
        private readonly Image pipeInvertedImage;
        private readonly int bgImgHeight;

        private FontFamily gameFontFamily;
        private Font gameFontLarge;
        private Font gameFontSmall;
#if WINFORMS
        private readonly StringFormat gameFontFormat;
#endif

        private readonly int sndHndJump;
        private readonly int sndHndScore;
        private readonly int sndHndGameOver;
        private readonly int sndHndBackgroundMusic;

        private double birdOsc = 0;
        private int frameCount = 0;
        private readonly List<Pipe> pipes = new List<Pipe>();
        private readonly ConcurrentBag<Rectangle> pipesRects = new ConcurrentBag<Rectangle>();

        private readonly bool isMonoRT = false;// Type.GetType("System.MonoType") != null;
#if WINFORMS
        private readonly MethodInvoker paintSurface;
#endif

        public delegate void OnExit();
        public event OnExit Exit;

        public FlappyBird(Control surface,
                          Image birdImage,
                          Image backgroundImage,
                          Image groundImage,
                          Image pipeImage,
                          FontFamily gameFontFamily,
                          string jumpSound,
                          string scoreSound,
                          string gameOverSound,
                          string backgroundMusic) : base(0, 0, 50, 0) {
            this.surface = surface;
            this.surface.Focus();
#if WINFORMS
            paintSurface = new MethodInvoker(() => surface.Invalidate());
#endif

            this.backgroundImage = backgroundImage;
            this.groundImage = groundImage;
            this.pipeImage = pipeImage;
#if WINFORMS
            this.pipeInvertedImage = (Image)pipeImage.Clone();
            pipeInvertedImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
#else
            using(Bitmap bmp = new Bitmap(pipeImage.Size, PixelFormat.Format32bppRgba)) { // Flip Y
                using(Graphics g = new Graphics(bmp)) {
                    g.TranslateTransform(bmp.Width / 2, bmp.Height / 2);
                    g.RotateTransform(180);
                    g.DrawImage(pipeImage, -bmp.Width / 2, -bmp.Height / 2);
                }
                this.pipeInvertedImage = (Image)bmp.Clone();
            }
#endif

            this.gameFontFamily = gameFontFamily;
#if WINFORMS
            gameFontFormat = new StringFormat { Alignment = StringAlignment.Center };
#else
            ((Window)surface.FindParent(typeof(Form))).Closing += (_, __) => isClosing = true;
#endif
            Scale = mScale;

            sprite = birdImage;
            spriteRect = new RectangleF(0, 0, sprite.Width / 3, sprite.Height);
            spriteW2 = (int)(spriteRect.Width / 2);
            spriteH2 = (int)(spriteRect.Height / 2);

            bgImgHeight = backgroundImage.Height;

            SetupEventHandlers();
            ResetGame();
            RunGameLogic();

            if(!isMonoRT) {
                if(CanRun = SetupBASS()) {
                    sndHndJump = Bass.CreateStream(jumpSound);
                    sndHndScore = Bass.CreateStream(scoreSound);
                    sndHndGameOver = Bass.CreateStream(gameOverSound);

                    sndHndBackgroundMusic = Bass.SampleLoad(backgroundMusic, 0, 0, 1, BassFlags.Loop);
                    int h = Bass.SampleGetChannel(sndHndBackgroundMusic);
                    Bass.ChannelPlay(h);
                }
            }
        }

        public float Scale {
            get => mScale;
            set {
                mScale = value;

#if WINFORMS
                gameFontLarge?.Dispose();
                gameFontLarge = new Font(gameFontFamily, 50 * mScale, FontStyle.Regular);
                gameFontSmall?.Dispose();
                gameFontSmall = new Font(gameFontFamily, 30 * mScale, FontStyle.Regular);
#else
                // FIXME: Why do we need the 1.6 factor?
                gameFontLarge?.Dispose();
                gameFontLarge = new Font(gameFontFamily.Name, 1.6f * 50 * mScale);
                gameFontSmall?.Dispose();
                gameFontSmall = new Font(gameFontFamily.Name, 1.6f * 30 * mScale);
#endif
            }
        }

        private void Up() {
            if(gameState != GameStates.Normal) return;
            if(spriteState == SpriteStates.Waiting) {
                frameCount = 0;
                CreatePipes();
                spriteState = SpriteStates.Normal;
            }
            acceleration = new Vector(8, PI270, Origin);
            velocity.Magnitude = 0;

            if(!isMonoRT) Bass.ChannelPlay(sndHndJump, true);
        }

        private void SetupEventHandlers() {
            surface.KeyDown += (object s, KeyEventArgs e) => {
#if WINFORMS
                switch(e.KeyCode) {
#else
                switch(e.Key) {
#endif
                    case Keys.Space:
                        Up();
                        break;
                    case Keys.Enter:
                        if(gameState == GameStates.GameOver) ResetGame();
                        break;
                    case Keys.Escape:
                        Exit?.Invoke();
                        break;
                }
            };

#if WINFORMS
            surface.MouseDown += (object s, MouseEventArgs e) => {
                switch(e.Button) {
                    case MouseButtons.Left:
                        Up();
                        break;
                    case MouseButtons.Right:
                        if(gameState == GameStates.GameOver) ResetGame();
                        break;
                }
            };
#endif
        }

        private void ResetGame() {
            velocity = new Vector(0, 0, 50, 0);
            acceleration = new Vector(velocity);
            frameCount = 0;
            spriteIndex = 1;
            score = 0;
            SetSpriteRect(true);
            pipes.Clear();
            while(!pipesRects.IsEmpty) pipesRects.TryTake(out _);
            spriteAngle = 0;
            base.TranslateAbs(backgroundImage.Width * 0.4, bgImgHeight * 0.54 - spriteH2 * (1 - mScale));

            spriteState = SpriteStates.Waiting;
            gameState = GameStates.Normal;
        }

        private void RunGameLogic() {
            Task.Run(() => {
                Vector gUp = new Vector(1.0, PI90, this.Origin);
                Vector gDn = new Vector(0.7, PI90, this.Origin);
                int fallDelay = 0;
                int animateSprite = 0;

                while(!isClosing) {
                    Thread.Sleep(11);

                    if(animateSprite == 0) SetSpriteRect(false);
                    animateSprite = animateSprite >= 8 ? 0 : animateSprite + 1;

                    if(spriteState != SpriteStates.Waiting && gameState != GameStates.GameOver) {
                        if(CheckCollision()) {
                            if(gameState == GameStates.GameOver)
                                base.Y1 = bgImgHeight - spriteRect.Height; // FIXME: Calculate the actual height of the sprite based on its rotation
                        }

                        base.Move(velocity);
                        acceleration += gUp;
                        if(acceleration.Angle == PI270)
                            acceleration += gUp;
                        else
                            acceleration = gDn;
                        velocity += acceleration;

                        if(velocity.Angle == PI270) {
                            spriteState = SpriteStates.Up;
                            fallDelay = 0;
                        } else {
                            if(fallDelay++ >= 20)
                                spriteState = SpriteStates.Falling;
                            /* else // This looks nice, but it's not how the original game behaves
                                ;//spriteState = SpriteStates.Down;
                            */
                        }
                    }

#if WINFORMS
                    if(surface.IsHandleCreated) surface.BeginInvoke(paintSurface);
#else
                    Application.Instance.Invoke(() => surface.Invalidate());
#endif
                }
            });
        }

        private void SetSpriteRect(bool force) {
            if(force || (gameState == GameStates.Normal)) {
                spriteRect.X = spriteIndex * spriteRect.Width;
                spriteIndex = ++spriteIndex >= 3 ? 0 : spriteIndex;
            }
        }

        public void DrawScene(PaintEventArgs e) {
            Graphics g = e.Graphics;

#if WINFORMS
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
#else
            g.ImageInterpolation = ImageInterpolation.Low;
#endif

            g.ScaleTransform(mScale, mScale);

#if WINFORMS
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(backgroundImage, 0, 0);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceOver;

            RenderPipes(g);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceCopy;
            RenderGround(g);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceOver;
            RenderSprite(g);

            if(gameState == GameStates.Normal) frameCount += 1;
#else
            g.DrawImage(backgroundImage, 0, 0);
            RenderPipes(g);
            RenderGround(g);
            RenderSprite(g);

            if(gameState == GameStates.Normal) frameCount += 1;
#endif

            RenderText(g, score.ToString(), 1, Brushes.WhiteSmoke, gameFontLarge);
            if(gameState == GameStates.Normal) {
                if(spriteState == SpriteStates.Waiting) {
                    RenderText(g, "Sharp Flappy Bird", 3, Brushes.Goldenrod, gameFontLarge);
                    RenderText(g, "C# implementation by", 9, Brushes.Gainsboro, gameFontSmall);
                    RenderText(g, "Xavier Flix", 10, Brushes.Gainsboro, gameFontSmall);

                    RenderText(g, "Press SPACEBAR to Start", 6, Brushes.YellowGreen, gameFontSmall);
                }
            } else {
                RenderText(g, "Game Over", 4, Brushes.OrangeRed, gameFontLarge);
                RenderText(g, "Press ENTER to Restart", 6, Brushes.YellowGreen, gameFontSmall);
            }
        }

        private void RenderText(Graphics g, string text, int line, Brush color, Font font) {
#if WINFORMS
            using(GraphicsPath p = new GraphicsPath()) {
                p.AddString(text,
                            font.FontFamily, (int)FontStyle.Regular, g.DpiY * font.Size / 72.0f,
                            new Rectangle(0, gameFontLarge.Height * line, (int)(backgroundImage.Width * mScale), font.Height),
                            gameFontFormat);
                g.DrawPath(new Pen(Brushes.Black, 6), p);
                g.FillPath(color, p);
            }
#else
            // FIXME: Since Eto doesn't support GraphicsPath.AddString,
            // add a semi-transparent background to give the text some contrast 
            SizeF s = g.MeasureString(font, text);
            float lh = 1.3f * gameFontLarge.LineHeight * line;
            g.FillRectangle(Color.FromArgb(32, 32, 32, 128), new RectangleF(
                            (backgroundImage.Width - s.Width) / 2.0f,
                            lh,
                            s.Width,
                            s.Height));

            // FIXME: Questions about the DrawText RectangleF:
            // 1) Why the width doesn't need to be scaled?
            // 2) Why the height needs to be so... large?
            g.DrawText(font, color,
                        new RectangleF(0, lh,
                                       backgroundImage.Width, backgroundImage.Height),
                        text,
                        FormattedTextWrapMode.None,
                        FormattedTextAlignment.Center,
                        FormattedTextTrimming.None);
#endif
        }

        private void RenderSprite(Graphics g) {
#if !WINFORMS
            g.SaveTransform();
#endif
            g.TranslateTransform((float)(base.X1 + spriteW2),
                                 (float)(base.Y1 + spriteH2));

            switch(spriteState) {
                case SpriteStates.Up:
                    if(spriteAngle > -30) spriteAngle -= 10;
                    break;
                case SpriteStates.Down:
                    if(spriteAngle < 30) spriteAngle += 10;
                    break;
                case SpriteStates.Falling:
                    if(spriteAngle < 90) spriteAngle += 10;
                    break;
            }
            g.RotateTransform(spriteAngle);

            float yOffset = 0;
            if(spriteState == SpriteStates.Waiting) {
                yOffset = (float)(10.0 * Math.Sin(birdOsc));
                birdOsc = birdOsc >= 360 ? 0 : birdOsc + 0.15;
            }

#if WINFORMS
            g.DrawImage(sprite,
                            -spriteW2, -(spriteH2 + yOffset),
                            spriteRect,
                            GraphicsUnit.Pixel);

            g.ResetTransform();
#else
            g.DrawImage(sprite,
                            spriteRect,
                            new RectangleF(-spriteW2, -(spriteH2 + yOffset),
                                           spriteRect.Width, spriteRect.Height));

            g.RestoreTransform();
#endif
        }

        private void RenderGround(Graphics g) {
            int w = groundImage.Width - 1;
            int groundOffset = (frameCount * horizontalSpeed) % w;
            for(int x = -groundOffset; x < backgroundImage.Width; x += w)
#if WINFORMS
                g.DrawImageUnscaled(groundImage, x, bgImgHeight);
#else
                g.DrawImage(groundImage, x, bgImgHeight);
#endif
        }

        private void RenderPipes(Graphics g) {
            double gap = 1.0 - 0.25; // 25% gap
            int xOffset;
            double hole;
            int h = pipeImage.Height - groundImage.Height;
            double factor = h * gap;
            double topOffset = h * (1.0 - gap);

            foreach(Pipe pipe in pipes) {
                xOffset = frameCount * horizontalSpeed;
                if(xOffset >= pipe.FrameCount) {
                    xOffset = backgroundImage.Width - (xOffset - pipe.FrameCount);
                    hole = pipe.GapPosition * factor;

#if WINFORMS
                    // Bottom Pipe
                    g.DrawImageUnscaled(pipeImage, xOffset,
                        (int)(bgImgHeight - hole));
                    pipesRects.Add(new Rectangle(xOffset, (int)(bgImgHeight - hole), pipeImage.Width, pipeImage.Height));

                    // Top Pipe
                    g.DrawImageUnscaled(pipeInvertedImage, xOffset,
                        (int)(-hole - topOffset));
                    pipesRects.Add(new Rectangle(xOffset, (int)(-hole - topOffset), pipeImage.Width, pipeImage.Height));
#else
                    // Bottom Pipe
                    g.DrawImage(pipeImage, xOffset,
                        (int)(bgImgHeight - hole));
                    pipesRects.Add(new Rectangle(xOffset, (int)(bgImgHeight - hole), pipeImage.Width, pipeImage.Height));

                    // Top Pipe
                    g.DrawImage(pipeInvertedImage, xOffset,
                        (int)(-hole - topOffset));
                    pipesRects.Add(new Rectangle(xOffset, (int)(-hole - topOffset), pipeImage.Width, pipeImage.Height));
#endif

                    if(!pipe.Passed && base.X1 >= xOffset) {
                        pipe.Passed = true;
                        score += 1;
                        if(!isMonoRT) Bass.ChannelPlay(sndHndScore, true);
                    }
                } else
                    break;
            }
        }

        private bool CheckCollision() {
            if(base.Y1 + spriteRect.Width >= bgImgHeight) { // Collision with floor
                gameState = GameStates.GameOver;
                if(!isMonoRT) Bass.ChannelPlay(sndHndGameOver, true);
                return true;
            }

            Rectangle sr = new Rectangle((int)base.X1,
                                         (int)base.Y1,
                                         (int)spriteRect.Width,
                                         (int)spriteRect.Height);
            while(pipesRects.TryTake(out Rectangle cr)) {
#if WINFORMS
                if(cr.IntersectsWith(sr)) { // Collision with pipe
#else
                if(cr.Intersects(sr)) { // Collision with pipe
#endif
                    gameState = GameStates.Crashed;
                    return true;
                }
            }
            return false;
        }

        private void CreatePipes() {
            pipes.Clear();

            var r = new Random();
            for(int i = 1; i <= 10; i++) {
                pipes.Add(new Pipe(600 * i, r.Next(2, 8) / 10.0, false));
            }
        }

        private bool SetupBASS() {
            string platform = Runtime.Platform.ToString().ToLower();
            string architecture = Environment.Is64BitProcess || Runtime.Platform == Runtime.Platforms.MacOSX ? "x64" : "x86";

            if(platform.StartsWith("arm")) {
                architecture = platform.EndsWith("hard") ? "hardfp" : "softfp";
                platform = "arm";
            }

            string path = Path.GetFullPath(Path.Combine("Bass", platform, architecture));
            FileInfo lib = new DirectoryInfo(path).GetFiles()[0];
            File.Copy(lib.FullName, Path.GetFullPath(Path.Combine(lib.Name)), true);

#if DEBUG
            Console.WriteLine($"Bass Library: {lib.Name} ({platform} {architecture})");
#endif

            bool result;
            try {
                result = Bass.Init();
            } catch(DllNotFoundException) {
#if WINFORMS
                Application.Restart();
#else
                Application.Instance.Restart();
#endif
                result = false;
            } finally {
                result = true;
            }

            return result;
        }
    }
}