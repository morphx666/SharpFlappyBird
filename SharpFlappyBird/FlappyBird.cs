using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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

        private readonly Image sprite;
        private int spriteIndex;
        private readonly int spriteW2;
        private readonly int spriteH2;
        private RectangleF spriteRect;
        private SpriteStates spriteState = SpriteStates.Waiting;
        private int spriteAngle = 0;
        private readonly Control surface;
        private readonly int horizontalSpeed = 5;

        private GameStates gameState = GameStates.Normal;

        private readonly Image backgroundImage;
        private readonly Image groundImage;
        private readonly Image pipeImage;
        private readonly Image pipeInvertedImage;
        private readonly int bgImgHeight;

        private double birdOsc = 0;
        private int frameCount = 0;
        private readonly List<(int FrameCount, double GapPosition)> pipes = new List<(int FrameCount, double GapPosition)>();
        private readonly ConcurrentBag<Rectangle> collisionRects = new ConcurrentBag<Rectangle>();

        public FlappyBird(Control surface,
                          Image birdImage,
                          Image backgroundImage,
                          Image groundImage,
                          Image pipeImage) : base(0, 0, 50, 0) {
            this.surface = surface;
            this.backgroundImage = backgroundImage;
            this.groundImage = groundImage;
            this.pipeImage = pipeImage;
            this.pipeInvertedImage = (Image)pipeImage.Clone();
            pipeInvertedImage.RotateFlip(RotateFlipType.RotateNoneFlipY);

            sprite = birdImage;
            spriteRect = new RectangleF(0, 0, sprite.Width / 3, sprite.Height);
            spriteW2 = (int)(spriteRect.Width / 2);
            spriteH2 = (int)(spriteRect.Height / 2);

            bgImgHeight = backgroundImage.Height;

            ResetGame();
            InitGame();
            RunGameLogic();
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
            SetSpriteRect(true);
            pipes.Clear();
            while(!collisionRects.IsEmpty) collisionRects.TryTake(out _);
            spriteAngle = 0;
            base.TranslateAbs(backgroundImage.Width * 0.4, bgImgHeight * 0.50 - spriteH2);

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

                    surface.Invalidate();
                }
            });
        }

        private void SetSpriteRect(bool force) {
            if(force || (gameState == GameStates.Normal && spriteState != SpriteStates.Waiting)) {
                spriteRect.X = spriteIndex * spriteRect.Width;
                spriteIndex = ++spriteIndex >= 3 ? 0 : spriteIndex;
            }
        }

        public void DrawScene(PaintEventArgs e) {
            Graphics g = e.Graphics;

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            g.ScaleTransform(Scale, Scale);

            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.DrawImageUnscaled(backgroundImage, 0, 0);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

            RenderPipes(g);
            RenderGround(g);
            RenderSprite(g);

            if(gameState == GameStates.Normal) frameCount += 1;
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
            int h = pipeImage.Height - groundImage.Height;
            double factor = h * gap;
            double topOffset = h * (1.0 - gap);

            foreach((int FrameCount, double GapPosition) pipe in pipes) {
                xOffset = frameCount * horizontalSpeed;
                if(xOffset >= pipe.FrameCount) {
                    xOffset = backgroundImage.Width - (xOffset - pipe.FrameCount);

                    // Bottom Pipe
                    g.DrawImageUnscaled(pipeImage, xOffset,
                        (int)(bgImgHeight - pipe.GapPosition * factor));
                    collisionRects.Add(new Rectangle(xOffset, (int)(bgImgHeight - pipe.GapPosition * factor), pipeImage.Width, pipeImage.Height));

                    // Top Pipe
                    g.DrawImageUnscaled(pipeInvertedImage, xOffset,
                        (int)(-factor * pipe.GapPosition - topOffset));
                    collisionRects.Add(new Rectangle(xOffset, (int)(-factor * pipe.GapPosition - topOffset), pipeImage.Width, pipeImage.Height));
                } else
                    break;
            }
        }

        private bool CheckCollision() {
            // Collision with floor
            if(base.Y1 + spriteRect.Width >= bgImgHeight) {
                gameState = GameStates.GameOver;
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
                pipes.Add((FrameCount: 600 * i,
                           GapPosition: r.Next(2, 8) / 10.0));
            }
        }
    }
}