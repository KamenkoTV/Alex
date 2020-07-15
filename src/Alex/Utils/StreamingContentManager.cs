﻿using System;
using System.IO;
using Microsoft.Xna.Framework.Content;

namespace Alex.Utils
{
	internal static class ContentManagerExtensions
	{
		/*public static T Load<T>(this ContentManager cont, byte[] data)
		{
			using (StreamingContentManager c = new StreamingContentManager(data, cont.ServiceProvider, cont.RootDirectory))
			{
				return c.Load<T>("Memory");
			}
		}*/
	}
	
	public class StreamingContentManager : ContentManager
	{
		//private byte[] _data;

		internal StreamingContentManager(IServiceProvider serviceProvider, string rootDirectory) : base(serviceProvider, rootDirectory)
		{
			//_data = data;
		}

		protected override Stream OpenStream(string assetName)
		{
			return new MemoryStream(ResourceManager.ReadResource(assetName), false);
		}
	}
}
