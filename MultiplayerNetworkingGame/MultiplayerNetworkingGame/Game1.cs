using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace MultiplayerNetworkingGame
{
    public enum GameState { SignIn, FindSession, CreateSession, Start, InGame, GameOver };

    public enum MessageType { StartGame, EndGame, RestartGame, RejoinLobby, UpdatePlayerPos, AddBomb, HitBomb };
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Texture2D backgroundTexture;

        SpriteFont scoreFont;
        int currentScore = 0;

        GameState currentGameState = GameState.SignIn;
        

        //when sending and receiving packets, message type(end game, start game, etc) must be sent first
        //if its to update player position, then you need to first send message type UpdatePlayerPos, then send Vector2
        

        Vector2 chasingSpeed = new Vector2(4, 4);
        Vector2 chasedSpeed = new Vector2(6, 6);

        NetworkSession networkSession; //self explanatory i hope :P

        //packet reader and writers for sending data over network
        PacketReader reader = new PacketReader();
        PacketWriter writer = new PacketWriter();

        //creates a list of bombs, when people add to list, more bombs are created/dropped
        List<UserControlledSprite> bombList = new List<UserControlledSprite>();
        //when chaser is hit, there is an effect. this is how long the effect is still going to last
        int bombEffectCoolDown = 0;
        //this is how long til next bomb is able to be dropped
        int bombCoolDown = 0;
        
        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            

        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            
            //adds a game component of type GamerServicesComponent that enables all networking and gamer services functionality :O this is BIG
            Components.Add(new GamerServicesComponent(this));
            

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            //audioEngine = new AudioEngine(@"Content/Audio/GameAudio.xgs");
            //waveBank = new WaveBank(audioEngine, @"Content/Audio/Wave Bank.xwb");
            //soundBank = new SoundBank(audioEngine, @"Content/Audio/Sound Bank.xsb");

            //trackCue = soundBank.GetCue("track");
            //trackCue.Play();

            //soundBank.PlayCue("start");



            graphics.PreferredBackBufferHeight = 768;
            graphics.PreferredBackBufferWidth = 1024;

            scoreFont = Content.Load<SpriteFont>(@"fonts\arial");

            backgroundTexture = Content.Load<Texture2D>(@"Images\background (2)");
            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            //you dont want the game running when gamer windows are open :|
            if (this.IsActive)
            {
                switch (currentGameState)
                {
                    case GameState.SignIn:
                        Update_SignIn();
                        break;
                    case GameState.FindSession:
                        Update_FindSession();
                        break;
                    case GameState.CreateSession:
                        Update_CreateSession();
                        break;
                    case GameState.Start:
                        Update_Start(gameTime);
                        /*
                        if (Keyboard.GetState().GetPressedKeys().Length > 0)
                        {
                            currentGameState = GameState.InGame;
                            spriteManager.Enabled = true;
                            spriteManager.Visible = true;
                        } */
                        break;
                    case GameState.InGame:
                        Update_InGame(gameTime);
                        break;
                    case GameState.GameOver:
                        Update_GameOver(gameTime);
                        /*if (Keyboard.GetState().IsKeyDown(Keys.Enter))
                            Exit();*/

                        break;
                }
            }

            if (networkSession != null)
            {
                networkSession.Update();
            }

            //audioEngine.Update();

            base.Update(gameTime);
        }

        protected void Update_SignIn()
        {
            //if No local gamers signed in, then show the sign-in screen(UI that signs into XBOX Live)
            if (Gamer.SignedInGamers.Count < 1)
            {
                Guide.ShowSignIn(1, false);
            }
            else
            {
                currentGameState = GameState.FindSession;
            }
        }

        protected void Update_FindSession()
        {
            //Sessions is a list of AvailableNetworkSessions(I think lol), basically storing 
            AvailableNetworkSessionCollection sessions = NetworkSession.Find(NetworkSessionType.SystemLink, 1, null);
            if (sessions.Count == 0)
            {
                currentGameState = GameState.CreateSession;
            }
            else
            {
                //basically, NetworkSession.Join method takes a AvailableNetworkSession
                networkSession = NetworkSession.Join(sessions[0]);
                WireUpEvents();
                currentGameState = GameState.Start;
            }
        }

        protected void WireUpEvents()
        {
            //basically, network session has a event called GamerJoined that occurs when a new player joins the multiplayer session
            networkSession.GamerJoined += GamerJoined;
            networkSession.GamerLeft += GamerLeft;
        }

        void GamerJoined(object sender, GamerJoinedEventArgs e)
        {
            if (e.Gamer.IsHost)
            {
                e.Gamer.Tag = CreateChasingSprite();
            }
            else
            {
                e.Gamer.Tag = CreateChasedSprite();
            }
        }

        private UserControlledSprite CreateChasingSprite()
        {
            return new UserControlledSprite(Content.Load<Texture2D>(@"Images/bolt"), new Vector2(((Window.ClientBounds.X / 2) + 150), ((Window.ClientBounds.Y / 2) + 150)), new Point(75, 75), 10, new Point(0, 0), new Point(6, 8), chasingSpeed, true);

        }

        private UserControlledSprite CreateChasedSprite()
        {
            return new UserControlledSprite(Content.Load<Texture2D>(@"Images/fourblades"), new Vector2(((Window.ClientBounds.X / 2) - 150), ((Window.ClientBounds.Y / 2) - 150)), new Point(75, 75), 10, new Point(0, 0), new Point(6, 8), chasedSpeed, false);
        }

        void GamerLeft(object sender, GamerLeftEventArgs e)
        {
            //dispose of network session
            networkSession.Dispose();
            networkSession = null;

            currentGameState = GameState.FindSession;
        }

        protected void Update_CreateSession()
        {
            networkSession = NetworkSession.Create(NetworkSessionType.SystemLink, 1, 2);
            networkSession.AllowHostMigration = true;
            networkSession.AllowJoinInProgress = false;

            WireUpEvents();
            currentGameState = GameState.Start;
        }

        protected void Update_Start(GameTime gameTime)
        {
            //local gamers is a list of gamers
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];

            if (networkSession.AllGamers.Count == 2)
            {
                if (Keyboard.GetState().IsKeyDown(Keys.Space))
                {
                    writer.Write((int)MessageType.StartGame);
                    localGamer.SendData(writer, SendDataOptions.Reliable);

                    StartGame();
                }

            }
            ProcessIncomingData(gameTime);
        }

        protected void StartGame()
        {
            currentGameState = GameState.InGame;
        }

        protected void ProcessIncomingData(GameTime gameTime)
        {
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];

            while (localGamer.IsDataAvailable)
            {
                NetworkGamer sender;
                localGamer.ReceiveData(reader, out sender);

                if (!sender.IsLocal)
                {
                    MessageType type = (MessageType)reader.ReadInt32();
                    switch (type)
                    {
                        case MessageType.StartGame:
                            StartGame();
                            break;
                        case MessageType.EndGame:
                            EndGame();
                            break;
                        case MessageType.RejoinLobby:
                            RejoinLobby();
                            break;
                        case MessageType.UpdatePlayerPos:
                            UpdateOtherPlayer(gameTime);
                            break;
                        case MessageType.RestartGame:
                            RestartGame();
                            break;
                        case MessageType.AddBomb:
                            //packet reader will, after reading message type, receive a vector2 of position of bomb
                            AddBomb(reader.ReadVector2());
                            break;
                        case MessageType.HitBomb:
                            //packet reader will, after reading message type, receive a int of index of bomb in bombList
                            HitBomb(reader.ReadInt32());
                            break;
                    }
                }
            }
        }

        protected void EndGame()
        {
            currentGameState = GameState.GameOver;
        }

        protected void RejoinLobby()
        {
            SwitchPlayersandReset(false);
            currentGameState = GameState.Start;
        }

        protected void RestartGame()
        {
            SwitchPlayersandReset(true);
            StartGame();
        }

        protected void AddBomb(Vector2 position)
        {
            UserControlledSprite newBomb = new UserControlledSprite(Content.Load<Texture2D>(@"Images/bomb"), position, new Point(75, 75), 10, new Point(0, 0), new Point(6, 8), Vector2.Zero, false);
            bombList.Add(newBomb);
            

            bombCoolDown = 5000;
            
        }

        protected void HitBomb(int index)
        {

            bombList.Remove(bombList[index]);

            bombEffectCoolDown = 5000;

            NetworkGamer chaser = GetChaser();

            UserControlledSprite realchaser = (UserControlledSprite)chaser.Tag;

            realchaser.speed *= .5f;
        }

        //will switch sprites and chaser vs chased
        //however, boolean depends on whether its actually switching host or chaser vs chased
        private void SwitchPlayersandReset(bool switchPlayers)
        {
            if (networkSession.AllGamers.Count == 2)
            {
                //if switchPlayers is true, then the player wants to actually switch players(chaser vs chased)
                if (switchPlayers)
                {

                    if (((UserControlledSprite)networkSession.AllGamers[0].Tag).isChasing)
                    {
                        //by creating a new sprite(chasing, chased), positions and stuff will reset
                        networkSession.AllGamers[0].Tag = CreateChasedSprite();
                        networkSession.AllGamers[1].Tag = CreateChasingSprite();
                    }
                    else
                    {
                        networkSession.AllGamers[0].Tag = CreateChasingSprite();
                        networkSession.AllGamers[1].Tag = CreateChasedSprite();
                    }
                }

                //else if switchPlayers = false, then player wants to just set host as chaser
                else
                {
                    if (networkSession.AllGamers[0].IsHost)
                    {
                        networkSession.AllGamers[0].Tag = CreateChasingSprite();
                        networkSession.AllGamers[1].Tag = CreateChasedSprite();
                    }
                    else
                    {
                        networkSession.AllGamers[0].Tag = CreateChasedSprite();
                        networkSession.AllGamers[1].Tag = CreateChasingSprite();
                    }
                }
            }
        }

        protected void UpdateOtherPlayer(GameTime gameTime)
        {
            NetworkGamer otherPlayer = GetOtherPlayer();

            UserControlledSprite otherSprite = (UserControlledSprite)otherPlayer.Tag;

            //you will always need the packet reader to read a vector or int to update a remote player from another comp
            Vector2 otherPosition = reader.ReadVector2();

            otherSprite.Position = otherPosition;

            if (!otherSprite.isChasing)
            {
                int score = reader.ReadInt32();
                otherSprite.score = score;
            }

            //set moving to false because you dont want to move it, you already set position, only animate frame
            otherSprite.Update(gameTime, Window.ClientBounds, false);
        }

        protected NetworkGamer GetOtherPlayer()
        {
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                if (!gamer.IsLocal)
                {
                    return gamer;
                }
            }
            return null;
        }

        protected void Update_InGame(GameTime gameTime)
        {
            //will update your own player, must also send updates to other player too
            UpdateLocalPlayer(gameTime);

            //will update the other player
            ProcessIncomingData(gameTime);

            //you only want the host checking for collisions
            if (networkSession.IsHost)
            {
                //why check for collisions when you dont have >1 gamer :O
                if (networkSession.AllGamers.Count == 2)
                {
                    UserControlledSprite sprite1 = (UserControlledSprite)networkSession.AllGamers[0].Tag;
                    UserControlledSprite sprite2 = (UserControlledSprite)networkSession.AllGamers[1].Tag;

                    //collision
                    if (sprite1.collisionRect.Intersects(sprite2.collisionRect))
                    {
                        //dont set enum GameState, just EndGame() since the method already does the enum stuff
                        //you want to make sure the other player knows its game over though
                        writer.Write((int)MessageType.EndGame);
                        //reliable because you need it to go through
                        networkSession.LocalGamers[0].SendData(writer, SendDataOptions.Reliable);

                        EndGame();
                    }

                    NetworkGamer chaser = GetChaser();
                    UserControlledSprite realchaser = (UserControlledSprite)chaser.Tag;
                    for (int i = 0; i < bombList.Count; i++)
                    {
                        UserControlledSprite bomb = bombList[i];
                        if (realchaser.collisionRect.Intersects(bomb.collisionRect))
                        {
                            HitBomb(i);

                            writer.Write((int)MessageType.HitBomb);
                            writer.Write(i);
                            networkSession.LocalGamers[0].SendData(writer, SendDataOptions.InOrder);
                        }
                    }
                }
                
            }
            EffectExpired(gameTime);
        }

        protected void EffectExpired(GameTime gameTime)
        {
            bombEffectCoolDown -= gameTime.ElapsedGameTime.Milliseconds;
            if (bombEffectCoolDown <= 0)
            {
                NetworkGamer chaser = GetChaser();
                UserControlledSprite realchaser = (UserControlledSprite)chaser.Tag;
                realchaser.speed = realchaser.originalSpeed;
            }
        }

        protected NetworkGamer GetChaser()
        {
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                if (((UserControlledSprite)gamer.Tag).isChasing)
                {
                    return gamer;
                }
            }
            return null;
        }

        protected void UpdateLocalPlayer(GameTime gameTime)
        {
            //you know this will def be the local gamer as there is only 1 allowed
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];

            //gotta get the sprite to modify it
            UserControlledSprite localSprite = (UserControlledSprite)localGamer.Tag;

            //now actually update sprite regualrly
            //moving is true because this is just you controlling it
            localSprite.Update(gameTime, Window.ClientBounds, true);

            if (!localSprite.isChasing)
            {
                localSprite.score += gameTime.ElapsedGameTime.Milliseconds;
            }

            writer.Write((int)MessageType.UpdatePlayerPos);
            writer.Write(localSprite.Position);
            if (!localSprite.isChasing)
            {
                writer.Write(localSprite.score);
            }
            //simple sprites may not send data, you must use the localnetworkgamer object
            //the options is InOrder because its ok if the packets dont get there, but they at least must be in order
            localGamer.SendData(writer, SendDataOptions.ReliableInOrder);

            //if your local sprite is a chaser
            if (localSprite.isChasing)
            {
                //if you're able to drop a bomb
                if (bombCoolDown <= 0)
                {
                    //and if you press space
                    if (Keyboard.GetState().IsKeyDown(Keys.Space))
                    {
                        AddBomb(localSprite.Position);

                        writer.Write((int)MessageType.AddBomb);
                        writer.Write(localSprite.Position);
                        localGamer.SendData(writer, SendDataOptions.ReliableInOrder);
                    }
                }
            }

            
        }

        private void Update_GameOver(GameTime gameTime)
        {
            KeyboardState boardState = Keyboard.GetState();

            //restart game key, enter
            if (boardState.IsKeyDown(Keys.Enter))
            {
                writer.Write((int)MessageType.RestartGame);
                networkSession.LocalGamers[0].SendData(writer, SendDataOptions.Reliable);
                RestartGame();
            }
            //rejoin lobby key, escape
            if (boardState.IsKeyDown(Keys.Escape))
            {
                writer.Write((int)MessageType.RejoinLobby);
                networkSession.LocalGamers[0].SendData(writer, SendDataOptions.Reliable);
                RejoinLobby();
            }

            //if this didnt happen for local player, see if other player pressed these
            ProcessIncomingData(gameTime);
        }
        /// <summary>
        /// This is called when the game should draw itself.                            
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            //dont want to draw when gamer services windows open
            if (this.IsActive)
            {
                switch (currentGameState)
                {
                    //dont do anything for signin, findsession, and createsession because gamer services takes care of that
                    case GameState.SignIn:
                    case GameState.FindSession:
                    case GameState.CreateSession:
                        GraphicsDevice.Clear(Color.DarkBlue);
                        break;
                    case GameState.Start:

                        DrawStartScreen();

                        break;
                    case GameState.InGame:
                        DrawInGameScreen(gameTime);

                        break;

                    case GameState.GameOver:
                        DrawGameOverScreen();

                        break;
                }
            }

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }

        private void DrawStartScreen()
        {
            GraphicsDevice.Clear(Color.AliceBlue);

            spriteBatch.Begin();
            string text = "Welcome, to the CHASING GAME!!!!(bolt chases skullball)\n";
           
            text += "Press Arrow Keys to move, chased press space to drop bombs\n";
            //notice property Gamertag..., not Tag!
            text += networkSession.Host.Gamertag + " is THE HOST AND WILL PLAY AS BOLT FIRST!";
            spriteBatch.DrawString(scoreFont, text, new Vector2((Window.ClientBounds.Width / 2)
            - (scoreFont.MeasureString(text).X / 2),
            (Window.ClientBounds.Height / 2)
            - (scoreFont.MeasureString(text).Y / 2)),
            Color.SaddleBrown);

            //tell players to press space to begin when theres 2 players
            if (networkSession.AllGamers.Count == 2)
            {
                text = "(Press SPACE to get READY TO ROLL!!!)";
                spriteBatch.DrawString(scoreFont, text, new Vector2((Window.ClientBounds.Width / 2)
                - (scoreFont.MeasureString(text).X / 2),
                (Window.ClientBounds.Height / 2)
                - (scoreFont.MeasureString(text).Y / 2) + 60),
                Color.SaddleBrown);
            }
            else
            {
                text = "(Waiting for players :()";
                spriteBatch.DrawString(scoreFont, text, new Vector2((Window.ClientBounds.Width / 2)
                - (scoreFont.MeasureString(text).X / 2),
                (Window.ClientBounds.Height / 2)
                - (scoreFont.MeasureString(text).Y / 2) + 60),
                Color.SaddleBrown);
            }

            text = "\n\nCurrent Players:";
            foreach (Gamer gamer in networkSession.AllGamers)
            {
                text += "\n" + gamer.Gamertag;
            }
            spriteBatch.DrawString(scoreFont, text, new Vector2((Window.ClientBounds.Width / 2)
            - (scoreFont.MeasureString(text).X / 2),
            (Window.ClientBounds.Height / 2)
            - (scoreFont.MeasureString(text).Y / 2) + 90),
            Color.SaddleBrown);
            spriteBatch.End();
        }

        private void DrawInGameScreen(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);

            spriteBatch.Begin();

            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                UserControlledSprite sprite = (UserControlledSprite)gamer.Tag;
                sprite.Draw(gameTime, spriteBatch);
                if (!sprite.isChasing)
                {
                    string text = "Score: " + sprite.score.ToString();
                    spriteBatch.DrawString(scoreFont, text, new Vector2(10, 10), Color.SaddleBrown);
                }
            }

            foreach (UserControlledSprite bomb in bombList)
            {
                bomb.Draw(gameTime, spriteBatch);

            }

            spriteBatch.End();
        }

        private void DrawGameOverScreen()
        {
            GraphicsDevice.Clear(Color.Navy);

            spriteBatch.Begin();
            string gameover = "Game over!\n";
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                UserControlledSprite sprite = (UserControlledSprite)gamer.Tag;
                if (!sprite.isChasing)
                {
                    gameover += "Score: " + sprite.score.ToString();
                }
            }
            gameover += "\nPress Enter to Switch players and Play Again";
            gameover += "\nPress Escape to exit to Game Lobby";

            spriteBatch.DrawString(scoreFont, gameover, new Vector2((Window.ClientBounds.Width / 2)
            - (scoreFont.MeasureString(gameover).X / 2),
            (Window.ClientBounds.Height / 2)
            - (scoreFont.MeasureString(gameover).Y / 2)),
            Color.WhiteSmoke);

            spriteBatch.End();
        }

    }
}
