﻿using Alex.API.Utils;
using Alex.API.World;
using Alex.Worlds.Abstraction;
using Alex.Worlds.Chunks;
using Microsoft.Xna.Framework;

namespace Alex.Worlds.Singleplayer.Generators
{
	public class EmptyWorldGenerator : IWorldGenerator
	{
		private ChunkColumn _sharedChunk;
		public EmptyWorldGenerator()
		{
			_sharedChunk = new ChunkColumn();
			//_sharedChunk.IsAllAir = true;
			for (int x = 0; x < ChunkColumn.ChunkWidth; x++)
			{
				for (int z = 0; z < ChunkColumn.ChunkDepth; z++)
				{
					for (int y = 0; y < ChunkColumn.ChunkHeight; y++)
					{
					//	_sharedChunk.SetBlockState(x, y, z, new Air());
						_sharedChunk.SetSkyLight(x, y, z, 15);
					}

					_sharedChunk.SetHeight(x, z, 0);
				}
			}
		}

		public ChunkColumn GenerateChunkColumn(ChunkCoordinates chunkCoordinates)
		{
			return _sharedChunk;
		}

		public Vector3 GetSpawnPoint()
		{
			return Vector3.Zero;
		}

		public void Initialize()
		{

		}
	}
}
