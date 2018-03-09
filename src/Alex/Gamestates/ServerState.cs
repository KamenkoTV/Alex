﻿using System.Net;
using Alex.Gamestates.Playing;
using Alex.Rendering.UI;
using Alex.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Alex.Gamestates
{
	public class ServerState : GameState
	{
		public ServerState(Alex alex) : base(alex)
		{

		}

		private Texture2D BackGround { get; set; }

		protected override void OnLoad(RenderArgs args)
		{
			BackGround = TextureUtils.ImageToTexture2D(args.GraphicsDevice, Resources.mcbg);

			//Alex.ShowMouse();
			Alex.IsMouseVisible = true;

			Controls.Add("server-ip", new InputField()
			{
				Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y - 30),
				PlaceHolder = "Server address",
				Text = "127.0.0.1"
			});

			Controls.Add("server-port", new InputField()
			{
				Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 20),
				PlaceHolder = "19132",
				Text = "19132"
			});

			Button opton = new Button("Connect to server")
			{
				Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 70),
			};
			opton.OnButtonClick += Opton_OnButtonClick;

			Controls.Add("optbtn", opton);

			Button backbton = new Button("Back")
			{
				Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 120),
			};
			backbton.OnButtonClick += backBton_OnButtonClick;

			Controls.Add("backbtn", backbton);

			Controls.Add("logo", new Logo());
			Controls.Add("info", new Info());
		}

		private void Opton_OnButtonClick()
		{
			InputField ip = (InputField)Controls["server-ip"];
			InputField port = (InputField)Controls["server-port"];

			if (ip.Text == string.Empty)
			{
				ErrorText = "Enter a server address";
				return;
			}

			if (port.Text == string.Empty)
			{
				ErrorText = "Enter a server port";
				return;
			}

			ErrorText = "Servers are not currently supported.";
			
		}

		private static IPAddress ResolveAddress(string address)
		{
			IPAddress outAddress;
			if (IPAddress.TryParse(address, out outAddress))
			{
				return outAddress;
			}
			return Dns.GetHostEntry(address).AddressList[0];
		}

		private void backBton_OnButtonClick()
		{
			Alex.GameStateManager.SetActiveState("menu");
			Alex.GameStateManager.RemoveState("serverMenu");
		}

		private string ErrorText = string.Empty;
		protected override void OnDraw2D(RenderArgs args)
		{
			args.SpriteBatch.Begin();

			//Start draw background
			var retval = new Rectangle(
				args.SpriteBatch.GraphicsDevice.Viewport.X,
				args.SpriteBatch.GraphicsDevice.Viewport.Y,
				args.SpriteBatch.GraphicsDevice.Viewport.Width,
				args.SpriteBatch.GraphicsDevice.Viewport.Height);
			args.SpriteBatch.Draw(BackGround, retval, Color.White);
			//End draw backgroun

			if (ErrorText != string.Empty)
			{
				var meisure = Alex.Font.MeasureString(ErrorText);
				args.SpriteBatch.DrawString(Alex.Font, ErrorText,
					new Vector2((int)(CenterScreen.X - (meisure.X / 2)), (int)CenterScreen.Y - (30 + meisure.Y + 5)), Color.Red);
			}

			args.SpriteBatch.End();
		}

		protected override void OnUpdate(GameTime gameTime)
		{
			Controls["server-ip"].Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y - 30);
			Controls["server-port"].Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 20);
			Controls["optbtn"].Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 70);
			Controls["backbtn"].Location = new Vector2((int)(CenterScreen.X - 200), (int)CenterScreen.Y + 120);
		}
	}
}
