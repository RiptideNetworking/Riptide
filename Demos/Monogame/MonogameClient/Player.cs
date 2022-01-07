// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RiptideNetworking;

namespace Monogame.TestClient
{
    public class Player
    {
        public Vector2 Position { get; set; }

        public Player()
        {
            Random rng = new Random();
            Position = new Vector2(rng.Next(200, 600), rng.Next(200, 400));
        }

        public void Update(float deltaTime)
        {
            KeyboardState keyboard = Keyboard.GetState();
            int h = (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left) ? -1 : 0) + (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right) ? +1 : 0);
            int v = (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up) ? -1 : 0) + (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down) ? +1 : 0);

            Vector2 pos = new Vector2(h, v);
            if (h != 0 && v != 0)
            {
                pos = Vector2.Normalize(pos);
            }
            Position += pos * deltaTime * 96;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(ExampleGame.Pixel, Position, null, Color.White, 0, Vector2.Zero, 16, SpriteEffects.None, 0);
        }
    }
}
