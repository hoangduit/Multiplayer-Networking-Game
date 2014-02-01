using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerNetworkingGame
{

        class UserControlledSprite : Sprite
        {
            public int score { get; set; }
            public bool isChasing { get; set; }
            //MouseState prevMouseState;
            public UserControlledSprite(Texture2D textureImage, Vector2 position, Point frameSize, int collisionOffset, Point currentFrame,
                Point sheetSize, Vector2 speed, bool chasing)
                : base(textureImage, position, frameSize, collisionOffset, currentFrame, sheetSize, speed, null, 0)
            {
                score = 0;
                isChasing = chasing;
            }

            public UserControlledSprite(Texture2D textureImage, Vector2 position, Point frameSize, int collisionOffset, Point currentFrame,
                Point sheetSize, Vector2 speed, int millisecondsPerFrame, bool chasing)
                : base(textureImage, position, frameSize, collisionOffset, currentFrame, sheetSize, speed, millisecondsPerFrame, null, 0)
            {
                score = 0;
                isChasing = chasing;
            }



            public override Vector2 direction
            {
                get
                {
                    Vector2 inputDirection = Vector2.Zero;

                    if (Keyboard.GetState().IsKeyDown(Keys.Left))
                        inputDirection.X -= 1;
                    if (Keyboard.GetState().IsKeyDown(Keys.Right))
                        inputDirection.X += 1;
                    if (Keyboard.GetState().IsKeyDown(Keys.Up))
                        inputDirection.Y -= 1;
                    if (Keyboard.GetState().IsKeyDown(Keys.Down))
                        inputDirection.Y += 1;

                    GamePadState gamepadState = GamePad.GetState(PlayerIndex.One);
                    if (gamepadState.ThumbSticks.Left.X != 0)
                        inputDirection.X += gamepadState.ThumbSticks.Left.X;
                    if (gamepadState.ThumbSticks.Left.Y != 0)
                        inputDirection.Y -= gamepadState.ThumbSticks.Left.Y;

                    return inputDirection * speed;
                }
            }

            public void Update(GameTime gameTime, Rectangle clientbounds, bool moving)
            {
                if (moving)
                {
                    position += direction;

                    //MouseState currMouseState = Mouse.GetState();
                    //if (currMouseState.X != prevMouseState.X ||
                    //currMouseState.Y != prevMouseState.Y)
                    //{
                    //position = new Vector2(currMouseState.X, currMouseState.Y);
                    //}
                    //prevMouseState = currMouseState;

                    if (position.X < 0)
                        position.X = 0;
                    if (position.Y < 0)
                        position.Y = 0;
                    if (position.X > clientbounds.Width - frameSize.X)
                        position.X = clientbounds.Width - frameSize.X;
                    if (position.Y > clientbounds.Height - frameSize.Y)
                        position.Y = clientbounds.Height - frameSize.Y;
                }

                base.Update(gameTime, clientbounds);
            }

        }
    
}
