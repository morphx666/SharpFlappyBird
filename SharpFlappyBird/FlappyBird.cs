using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Falling = 3,
            GameOver = 4
        }

        private readonly Image sprite;
        private int spriteIndex;
        private readonly int spriteWidth;
        private readonly int spriteW2;
        private readonly int spriteH2;
        private RectangleF spriteRect;
        private SpriteStates spriteState = SpriteStates.Waiting;
        private int spriteAngle = 0;
        private readonly Control surface;
        private int horizontalSpeed = 5;

        private readonly Image backgroundImage;
        private readonly Image groundImage;
        private readonly Image pipeImage;
        private readonly Image pipeInvertedImage;
        private readonly int bgImgHeight;

        private double birdOsc = 0;
        private int frameCount = 0;
        private List<(int FrameCount, double GapPosition)> pipes = new List<(int FrameCount, double GapPosition)>();

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
            spriteWidth = birdImage.Width / 3;
            spriteW2 = spriteWidth / 2;
            spriteH2 = sprite.Height / 2;
            spriteRect = new RectangleF(0, 0, spriteWidth, sprite.Height);

            SetSpriteRect();

            bgImgHeight = backgroundImage.Height;

            Velocity = new Vector(0, 0, 50, 0);
            Acceleration = new Vector(Velocity);

            InitGame();
            RunGameLogic();
        }

        public Vector Acceleration { get; set; }

        private void Up() {
            if(spriteState == SpriteStates.Waiting) {
                frameCount = 0;
                CreatePipes();
                spriteState = SpriteStates.Normal;
            }
            Acceleration = new Vector(8, 270, Origin);
            Velocity.Magnitude = 0;
        }

        private void InitGame() {
            surface.Paint += DrawScene;
            surface.KeyDown += (object s, KeyEventArgs e) => {
                switch(e.KeyCode) {
                    case Keys.Space:
                        Up();
                        break;
                }
            };
            surface.SizeChanged += (_, __) => base.TranslateAbs(backgroundImage.Width * 0.4, bgImgHeight * 0.50 - spriteH2);
        }

        private void RunGameLogic() {
            Task.Run(() => {
                Vector gUp = new Vector(1.1, 90, this.Origin);
                Vector gDn = new Vector(0.50, 90, this.Origin);
                int c = 0;
                int animateSprite = 0;

                while(true) {
                    Thread.Sleep(11);

                    if(animateSprite == 0) SetSpriteRect();
                    animateSprite = animateSprite >= 8 ? 0 : animateSprite + 1;

                    if(CheckCollision()) {
                        spriteState = SpriteStates.GameOver;
                        base.Y1 = bgImgHeight - spriteRect.Height; // FIXME: Calculate the actual height of the sprite based on its rotation
                    }

                    if(spriteState != SpriteStates.Waiting && spriteState != SpriteStates.GameOver) {
                        base.Move(Velocity);
                        Acceleration += gUp;
                        if(Acceleration.Angle == 270)
                            Acceleration += gUp;
                        else
                            Acceleration = gDn;
                        Velocity += Acceleration;

                        if(Velocity.Angle == 270) {
                            spriteState = SpriteStates.Up;
                            c = 0;
                        } else {
                            if(c++ >= 20)
                                spriteState = SpriteStates.Falling;
                            else // This looks nice, but it's not how the original game behaves
                                ;//spriteState = SpriteStates.Down;
                        }
                    }

                    surface.Invalidate();
                }
            });
        }

        private void SetSpriteRect() {
            if(spriteState != SpriteStates.GameOver) {
                spriteRect.X = spriteIndex * spriteWidth;
                spriteIndex = ++spriteIndex >= 3 ? 0 : spriteIndex;
            }
        }

        private void DrawScene(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;

            g.ScaleTransform(Scale, Scale);
            g.DrawImageUnscaled(backgroundImage, 0, 0);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            RenderPipes(g);
            RenderGround(g);
            RenderSprite(g);

            if(spriteState != SpriteStates.GameOver) frameCount += 1;
        }

        public void RenderSprite(Graphics g) {
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

            foreach((int FrameCount, double GapPosition) pipe in pipes) {
                xOffset = frameCount * horizontalSpeed;
                if(xOffset >= pipe.FrameCount) {
                    xOffset = backgroundImage.Width - (xOffset - pipe.FrameCount);

                    // Bottom Pipe
                    g.DrawImageUnscaled(pipeImage, xOffset,
                        (int)(bgImgHeight - h * pipe.GapPosition * gap));

                    // Top Pipe
                    g.DrawImageUnscaled(pipeInvertedImage, xOffset,
                        (int)(-h * pipe.GapPosition * gap - h * (1.0 - gap)));
                } else
                    break;
            }
        }

        private bool CheckCollision() {
            // Collision with floor
            if(base.Y1 + spriteWidth >= bgImgHeight) return true;

            // TODO: Implement collision detection against pipes
            return false;
        }

        private void CreatePipes() {
            pipes.Clear();

            var r = new Random();
            for(int i = 1; i <= 10; i++) {
                pipes.Add((600 * i, r.Next(2, 8) / 10.0));
            }
        }
    }
}