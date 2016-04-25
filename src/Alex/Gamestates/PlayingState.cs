﻿using System;
using System.Collections.Generic;
using System.Linq;
using Alex.Blocks;
using Alex.Properties;
using Alex.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Alex.Gamestates
{
	public class PlayingState : Gamestate
	{
        private List<string> ChatMessages { get; set; }
		private FrameCounter FpsCounter { get; set; }
		private Texture2D CrosshairTexture { get; set; }
		private bool RenderDebug { get; set; } = false;
	    private bool RenderChatInput { get; set; } = false;
		public override void Init(RenderArgs args)
		{
			OldKeyboardState = Keyboard.GetState();
			FpsCounter = new FrameCounter();
			CrosshairTexture = ResManager.ImageToTexture2D(Resources.crosshair);
			SelectedBlock = Vector3.Zero;
		    ChatMessages = new List<string>()
		    {
		        "<Alex> there",
                "<Alex> This is a test message."
		    };
            Alex.Instance.OnCharacterInput += OnCharacterInput;
			base.Init(args);
		}

	    private void OnCharacterInput(object sender, char c)
	    {
	        if (RenderChatInput)
	        {
	            Input += c;
	        }
	    }

	    public override void Stop()
		{
			base.Stop();
		}

		public override void Render2D(RenderArgs args)
		{
			FpsCounter.Update((float) args.GameTime.ElapsedGameTime.TotalSeconds);

			args.SpriteBatch.Begin();
			args.SpriteBatch.Draw(CrosshairTexture,
				new Vector2(CenterScreen.X - CrosshairTexture.Width/2f, CenterScreen.Y - CrosshairTexture.Height/2f));

		    if (RenderChatInput)
		    {
		        var heightCalc = Alex.Font.MeasureString("!");
		        if (Input.Length > 0)
		        {
		            heightCalc = Alex.Font.MeasureString(Input);
		        }

                int extra = 0;
                if (heightCalc.X > args.GraphicsDevice.Viewport.Width / 2f)
                {
                    extra = (int)(heightCalc.X - args.GraphicsDevice.Viewport.Width / 2f);
                }

		        args.SpriteBatch.FillRectangle(
		            new Rectangle(0, (int) (args.GraphicsDevice.Viewport.Height - (heightCalc.Y + 5)),
		                (args.GraphicsDevice.Viewport.Width/2) + extra, (int) heightCalc.Y),
		            new Color(Color.Black, 64));

                args.SpriteBatch.DrawString(Alex.Font, Input,
                    new Vector2(5, (int)(args.GraphicsDevice.Viewport.Height - (heightCalc.Y + 5))), Color.White);
            }

		    var count = 2;
		    foreach (var msg in ChatMessages.TakeLast(5).Reverse())
		    {
		        var heightCalc = Alex.Font.MeasureString(msg);

		        int extra = 0;
		        if (heightCalc.X > args.GraphicsDevice.Viewport.Width/2f)
		        {
		            extra = (int) (heightCalc.X - args.GraphicsDevice.Viewport.Width/2f);
		        }

		        args.SpriteBatch.FillRectangle(
		            new Rectangle(0, (int) (args.GraphicsDevice.Viewport.Height - ((heightCalc.Y*count) + 10)),
                        (args.GraphicsDevice.Viewport.Width / 2) + extra, (int) heightCalc.Y),
		            new Color(Color.Black, 64));
		        args.SpriteBatch.DrawString(Alex.Font, msg,
		            new Vector2(5, (int) (args.GraphicsDevice.Viewport.Height - ((heightCalc.Y*count) + 10))), Color.White);
		        count++;
		    }

		    if (RenderDebug)
			{
				var fpsString = string.Format("Alex {0} ({1} FPS)", Alex.Version, Math.Round(FpsCounter.AverageFramesPerSecond));
				var meisured = Alex.Font.MeasureString(fpsString);

				args.SpriteBatch.FillRectangle(new Rectangle(0, 0, (int)meisured.X, (int)meisured.Y), new Color(Color.Black, 64));
				args.SpriteBatch.DrawString(Alex.Font,
					fpsString, new Vector2(0, 0),
					Color.White);

				var y = (int) meisured.Y;
				var positionString = "Position: " + Game.MainCamera.Position;
				meisured = Alex.Font.MeasureString(positionString);

				args.SpriteBatch.FillRectangle(new Rectangle(0, y, (int)meisured.X, (int)meisured.Y), new Color(Color.Black, 64));
				args.SpriteBatch.DrawString(Alex.Font, positionString, new Vector2(0,y), Color.White);

				y += (int)meisured.Y;

				positionString = "Looking at: " + SelectedBlock;
				meisured = Alex.Font.MeasureString(positionString);

				args.SpriteBatch.FillRectangle(new Rectangle(0, y, (int)meisured.X, (int)meisured.Y), new Color(Color.Black, 64));
				args.SpriteBatch.DrawString(Alex.Font, positionString, new Vector2(0, y), Color.White);

                y += (int)meisured.Y;

                positionString = "Vertices: " + Alex.Instance.World.Vertices;
                meisured = Alex.Font.MeasureString(positionString);

                args.SpriteBatch.FillRectangle(new Rectangle(0, y, (int)meisured.X, (int)meisured.Y), new Color(Color.Black, 64));
                args.SpriteBatch.DrawString(Alex.Font, positionString, new Vector2(0, y), Color.White);

                y += (int)meisured.Y;

                positionString = "Chunks: " + Alex.Instance.World.ChunkCount;
                meisured = Alex.Font.MeasureString(positionString);

                args.SpriteBatch.FillRectangle(new Rectangle(0, y, (int)meisured.X, (int)meisured.Y), new Color(Color.Black, 64));
                args.SpriteBatch.DrawString(Alex.Font, positionString, new Vector2(0, y), Color.White);
            }
			args.SpriteBatch.End();

			if (SelectedBlock.Y > 0 && SelectedBlock.Y < 256)
			{
				var selBlock = Alex.Instance.World.GetBlock(SelectedBlock.X, SelectedBlock.Y, SelectedBlock.Z);
				var boundingBox = new BoundingBox(SelectedBlock + selBlock.BlockModel.Offset,
					SelectedBlock + selBlock.BlockModel.Offset + selBlock.BlockModel.Size);

				args.SpriteBatch.RenderBoundingBox(
					boundingBox,
					Game.MainCamera.ViewMatrix, Game.MainCamera.ProjectionMatrix, Color.LightGray);
			}

			base.Render2D(args);
		}

		public override void Render3D(RenderArgs args)
		{
			Alex.Instance.World.Render();
			base.Render3D(args);
		}

	    private string Input = "";
		private Vector3 SelectedBlock;
		private KeyboardState OldKeyboardState;
	    private MouseState OldMouseState;
		public override void OnUpdate(GameTime gameTime)
		{
			SelectedBlock = RayTracer.Raytrace();
			
			if (Alex.Instance.IsActive)
			{
			    if (!RenderChatInput)
			    {
                    Alex.Instance.UpdateCamera(gameTime);
                }

				Alex.Instance.HandleInput();

			    MouseState currentMouseState = Mouse.GetState();
			    if (currentMouseState != OldMouseState)
			    {
			        if (currentMouseState.LeftButton == ButtonState.Pressed)
			        {
			            if (SelectedBlock.Y > 0 && SelectedBlock.Y < 256)
			            {
			                Alex.Instance.World.SetBlock(SelectedBlock.X, SelectedBlock.Y, SelectedBlock.Z, new Air());
			            }
			        }

			        if (currentMouseState.RightButton == ButtonState.Pressed)
			        {
			            if (SelectedBlock.Y > 0 && SelectedBlock.Y < 256)
			            {
			                Alex.Instance.World.SetBlock(SelectedBlock.X, SelectedBlock.Y + 1, SelectedBlock.Z, new Stone());
			            }
			        }
			    }
			    OldMouseState = currentMouseState;

				KeyboardState currentKeyboardState = Keyboard.GetState();
				if (currentKeyboardState != OldKeyboardState)
				{
					if (currentKeyboardState.IsKeyDown(KeyBinds.Menu))
					{
					    if (RenderChatInput)
					    {
					        RenderChatInput = false;
					    }
					}

					if (currentKeyboardState.IsKeyDown(KeyBinds.DebugInfo))
					{
						RenderDebug = !RenderDebug;
					}

				    if (RenderChatInput) //Handle Input
				    {
				        if (currentKeyboardState.IsKeyDown(Keys.Back))
				        {
				            if (Input.Length > 0) Input = Input.Remove(Input.Length - 1, 1);
				        }

				        if (currentKeyboardState.IsKeyDown(Keys.Enter))
				        {
				            //Submit message
				            if (Input.Length > 0)
				            {
                                //For testing:
                                ChatMessages.Add("<Username> " + Input);
				            }
                            Input = string.Empty;
                            RenderChatInput = false;
                        }
                    }
				    else
				    {
				        if (currentKeyboardState.IsKeyDown(KeyBinds.Chat))
				        {
				            RenderChatInput = !RenderChatInput;
				            Input = string.Empty;
				        }
				    }
				}
				OldKeyboardState = currentKeyboardState;
			}

			base.OnUpdate(gameTime);
		}
	}
}