// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monogame.Common;
using RiptideNetworking;
using RiptideNetworking.Utils;

namespace Monogame.TestClient
{
    public class ExampleGame : Game
    {
        public static Texture2D Pixel { get; set; }

        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Message playerMessage;
        private Player localPlayer;

        public ExampleGame()
        {
            RiptideLogger.Initialize(Console.WriteLine, false);

            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 800,
                PreferredBackBufferHeight = 600
            };
            graphics.ApplyChanges();

            IsMouseVisible = true;

            Exiting += (s, e) => Network.Client.Disconnect();

            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        }

        protected override void Initialize()
        {
            Network.Init();

            localPlayer = new Player();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            localPlayer.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            playerMessage = Message.Create(MessageSendMode.unreliable, MessageId.PlayerPosition);
            _ = playerMessage.AddVector2(localPlayer.Position);

            Network.Client.Send(playerMessage);

            Network.Client.Tick();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();

            localPlayer.Draw(spriteBatch);

            foreach (Player p in Network.RemoteClients.Values)
            {
                p.Draw(spriteBatch);
            }

            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
