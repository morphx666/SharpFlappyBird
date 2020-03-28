using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedBass;
using RayCasting;

namespace SharpFlappyBird {
    public class FlappyBird : Vector {
        public Vector Velocity;
        public Vector Acceeration;
        public float Scale = 0.65f;

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

        private readonly Font gameFontLarge;
        private readonly Font gameFontSmall;
        private readonly StringFormat gameFontFormat;

        private readonly int sndHndJump;
        private readonly int sndHndScore;
        private readonly int sndHndGameOver;

        private double birdOsc = 0;
        private int frameCount = 0;
        private readonly List<Pipe> pipes = new List<Pipe>();
        private readonly ConcurrentBag<Rectangle> collisionRects = new ConcurrentBag<Rectangle>();

        private readonly bool isMonoRT = Type.GetType("System.MonoType") != null;
        private readonly MethodInvoker paintSurface;

        public FlappyBird(Control surface,
                          Image birdImage,
                          Image backgroundImage,
                          Image groundImage,
                          Image pipeImage,
                          FontFamily gameFontFamily,
                          string jumpSound,
                          string scoreSound,
                          string gameOverSound) : base(0, 0, 50, 0) {
            this.surface = surface;
            paintSurface = new MethodInvoker(() => surface.Invalidate());

            this.backgroundImage = backgroundImage;
            this.groundImage = groundImage;
            this.pipeImage = pipeImage;
            this.pipeInvertedImage = (Image)pipeImage.Clone();
            pipeInvertedImage.RotateFlip(RotateFlipType.RotateNoneFlipY);

            this.gameFontLarge = new Font(gameFontFamily, 30, FontStyle.Regular);
            this.gameFontSmall = new Font(gameFontFamily, 18, FontStyle.Regular);
            gameFontFormat = new StringFormat { Alignment = StringAlignment.Center };

            sprite = birdImage;
            spriteRect = new RectangleF(0, 0, sprite.Width / 3, sprite.Height);
            spriteW2 = (int)(spriteRect.Width / 2);
            spriteH2 = (int)(spriteRect.Height / 2);

            bgImgHeight = backgroundImage.Height;

            ResetGame();
            InitGame();
            RunGameLogic();

            SetupBASS();

            sndHndJump = Bass.CreateStream(jumpSound);
            sndHndScore = Bass.CreateStream(scoreSound);
            sndHndGameOver = Bass.CreateStream(gameOverSound);
        }

        private void SetupBASS() {
            string platform = Runtime.Platform.ToString().ToLower();
            string architecture = Environment.Is64BitProcess || Runtime.Platform == Runtime.Platforms.Mac ? "x64" : "x86";

            if(platform.StartsWith("arm")) {
                architecture = platform.EndsWith("hard") ? "hardfp" : "softfp";
                platform = "arm";
            }

            string path = Path.GetFullPath(Path.Combine("Bass", platform, architecture));
            FileInfo lib = new DirectoryInfo(path).GetFiles()[0];
            File.Copy(lib.FullName, Path.GetFullPath(Path.Combine(lib.Name)), true);

            Bass.Init();
        }

        public Vector Acceleration { get; set; }

        private void Up() {
            if(gameState != GameStates.Normal) return;
            if(spriteState == SpriteStates.Waiting) {
                frameCount = 0;
                CreatePipes();
                spriteState = SpriteStates.Normal;
            }
            Acceleration = new Vector(8, 270, Origin);
            Velocity.Magnitude = 0;

            Bass.ChannelPlay(sndHndJump, true);
        }

        private void InitGame() {
            surface.KeyDown += (object s, KeyEventArgs e) => {
                switch(e.KeyCode) {
                    case Keys.Space:
                        Up();
                        break;
                    case Keys.Enter:
                        if(gameState == GameStates.GameOver) ResetGame();
                        break;
                }
            };
        }

        private void ResetGame() {
            Velocity = new Vector(0, 0, 50, 0);
            Acceleration = new Vector(Velocity);
            frameCount = 0;
            spriteIndex = 1;
            score = 0;
            SetSpriteRect(true);
            pipes.Clear();
            while(!collisionRects.IsEmpty) collisionRects.TryTake(out _);
            spriteAngle = 0;
            base.TranslateAbs(backgroundImage.Width * 0.4, bgImgHeight * 0.60 - spriteH2);

            spriteState = SpriteStates.Waiting;
            gameState = GameStates.Normal;
        }

        private void RunGameLogic() {
            Task.Run(() => {
                Vector gUp = new Vector(1.0, 90, this.Origin);
                Vector gDn = new Vector(0.7, 90, this.Origin);
                int fallDelay = 0;
                int animateSprite = 0;

                while(true) {
                    Thread.Sleep(11);

                    if(animateSprite == 0) SetSpriteRect(false);
                    animateSprite = animateSprite >= 8 ? 0 : animateSprite + 1;

                    if(spriteState != SpriteStates.Waiting && gameState != GameStates.GameOver) {
                        if(CheckCollision()) {
                            if(gameState == GameStates.GameOver)
                                base.Y1 = bgImgHeight - spriteRect.Height; // FIXME: Calculate the actual height of the sprite based on its rotation
                        }

                        base.Move(Velocity);
                        Acceleration += gUp;
                        if(Acceleration.Angle == 270)
                            Acceleration += gUp;
                        else
                            Acceleration = gDn;
                        Velocity += Acceleration;

                        if(Velocity.Angle == 270) {
                            spriteState = SpriteStates.Up;
                            fallDelay = 0;
                        } else {
                            if(fallDelay++ >= 20)
                                spriteState = SpriteStates.Falling;
                            else // This looks nice, but it's not how the original game behaves
                                ;//spriteState = SpriteStates.Down;
                        }
                    }

                    if(surface.IsHandleCreated) surface.BeginInvoke(paintSurface);
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

            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            g.ScaleTransform(Scale, Scale);

            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(backgroundImage, 0, 0);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceOver;

            RenderPipes(g);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceCopy;
            RenderGround(g);
            if(!isMonoRT) g.CompositingMode = CompositingMode.SourceOver;
            RenderSprite(g);

            if(gameState == GameStates.Normal) frameCount += 1;

            RenderText(g, score.ToString(), 1, Brushes.White, gameFontLarge);
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
            using(GraphicsPath p = new GraphicsPath()) {
                p.AddString(text,
                            font.FontFamily, (int)FontStyle.Regular, g.DpiY * font.Size / 72.0f,
                            new Rectangle(0, gameFontLarge.Height * line, (int)(backgroundImage.Width * Scale), font.Height),
                            gameFontFormat);
                g.DrawPath(new Pen(Brushes.Black, 6), p);
                g.FillPath(color, p);
            }
        }

        private void RenderSprite(Graphics g) {
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

            g.DrawImage(sprite,
                            -spriteW2, -(spriteH2 + yOffset),
                            spriteRect,
                            GraphicsUnit.Pixel);

            g.ResetTransform();
        }

        private void RenderGround(Graphics g) {
            int w = groundImage.Width - 1;
            int groundOffset = (frameCount * horizontalSpeed) % w;
            for(int x = -groundOffset; x < backgroundImage.Width; x += w)
                g.DrawImageUnscaled(groundImage, x, bgImgHeight);
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

                    // Bottom Pipe
                    g.DrawImageUnscaled(pipeImage, xOffset,
                        (int)(bgImgHeight - hole));
                    collisionRects.Add(new Rectangle(xOffset, (int)(bgImgHeight - hole), pipeImage.Width, pipeImage.Height));

                    // Top Pipe
                    g.DrawImageUnscaled(pipeInvertedImage, xOffset,
                        (int)(-hole - topOffset));
                    collisionRects.Add(new Rectangle(xOffset, (int)(-hole - topOffset), pipeImage.Width, pipeImage.Height));

                    if(!pipe.Passed && base.X1 >= xOffset) {
                        pipe.Passed = true;
                        score += 1;
                        Bass.ChannelPlay(sndHndScore, true);
                    }
                } else
                    break;
            }
        }

        private bool CheckCollision() {
            // Collision with floor
            if(base.Y1 + spriteRect.Width >= bgImgHeight) {
                gameState = GameStates.GameOver;
                Bass.ChannelPlay(sndHndGameOver, true);
                return true;
            }

            Rectangle sr = new Rectangle((int)base.X1,
                                         (int)base.Y1,
                                         (int)spriteRect.Width,
                                         (int)spriteRect.Height);
            while(collisionRects.TryTake(out Rectangle cr)) {
                if(cr.IntersectsWith(sr)) {
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
    }
}