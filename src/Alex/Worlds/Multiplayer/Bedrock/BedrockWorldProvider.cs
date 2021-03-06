﻿using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Alex.API.Events;
using Alex.API.Services;
using Alex.API.World;
using Alex.Entities;
using Alex.Gui.Forms;
using Alex.Net;
using Alex.Net.Bedrock.Raknet;
using Alex.Utils.Inventories;
using Alex.Worlds.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using MiNET.Net;
using MiNET.Utils;
using NLog;
using ChunkCoordinates = Alex.API.Utils.ChunkCoordinates;
using MathF = System.MathF;
using PlayerLocation = Alex.API.Utils.PlayerLocation;

namespace Alex.Worlds.Multiplayer.Bedrock
{
	public class BedrockWorldProvider : WorldProvider
	{
		private static Logger Log = LogManager.GetCurrentClassLogger();
		
		public Alex Alex { get; }
		protected BedrockClient Client { get; }

		private HighPrecisionTimer _gameTickTimer;
		private IEventDispatcher EventDispatcher { get; }
		public BedrockFormManager FormManager { get; }
		
		public BedrockWorldProvider(Alex alex, IPEndPoint endPoint, PlayerProfile profile, DedicatedThreadPool threadPool,
			out NetworkProvider networkProvider)
		{
			Alex = alex;
			var eventDispatcher = alex.Services.GetRequiredService<IEventDispatcher>();
			EventDispatcher = eventDispatcher;
			
			//Client = new ExperimentalBedrockClient(alex, alex.Services, this, endPoint);
			Client = new BedrockClient(alex, eventDispatcher, endPoint, profile, threadPool, this);
			networkProvider = Client;
			
			EventDispatcher.RegisterEvents(this);

			var guiManager = Alex.GuiManager;
			FormManager = new BedrockFormManager(networkProvider, guiManager, alex.InputManager);
		}

		public override Vector3 GetSpawnPoint()
		{
			return new Vector3(Client.SpawnPoint.X, Client.SpawnPoint.Y, Client.SpawnPoint.Z);
		}

		private uint GetAdventureFlags()
		{
			uint flags = 0;

			if (_flying) flags |= 0x200;

			return flags;
		}
		
		private bool _initiated = false;
		private bool _flying = false;
		private PlayerLocation _lastLocation = new PlayerLocation();
        private PlayerLocation _lastSentLocation = new PlayerLocation();
        
        private long _tickTime = 0;
        private long _lastPrioritization = 0;
        private void GameTick(object state)
		{
			if (World == null) return;

			if (_initiated)
			{
				_tickTime++;

				if (World.Player != null && Client.HasSpawned)
				{
					//	player.IsSpawned = Spawned;

					if (World.Player.IsFlying != _flying)
					{
						_flying = World.Player.IsFlying;

						McpeAdventureSettings settings = McpeAdventureSettings.CreateObject();
						settings.flags = GetAdventureFlags();
						Client.SendPacket(settings);
						//SendPlayerAbilities(player);
					}

					var pos = (PlayerLocation) World.Player.KnownPosition.Clone();

					if (pos.DistanceTo(_lastSentLocation) > 0.0f
					    || MathF.Abs(pos.HeadYaw - _lastSentLocation.HeadYaw) > 0.0f
					    || MathF.Abs(pos.Pitch - _lastSentLocation.Pitch) > 0.0f)
					{
						SendLocation(pos);
						_lastSentLocation = pos;
					}

					if ((pos.DistanceTo(_lastLocation) > 16f || MathF.Abs(pos.HeadYaw - _lastLocation.HeadYaw) >= 5.0f)
					    && (_tickTime - _lastPrioritization >= 10))
					{
						World.ChunkManager.FlagPrioritization();

						SendLocation(pos);

						_lastLocation = pos;
						UnloadChunks(new ChunkCoordinates(pos), Client.ChunkRadius + 3);

						_lastPrioritization = _tickTime;
					}
				}

				if (_tickTime % 20 == 0 && CustomConnectedPong.CanPing)
				{
					Client.SendPing();
				}
				
				World.Player.OnTick();
				World.EntityManager.Tick();
				World.PhysicsEngine.Tick();
			}
		}

		private void SendLocation(PlayerLocation location)
		{
			Client.SendMcpeMovePlayer(new MiNET.Utils.PlayerLocation(location.X,
				location.Y + Player.EyeLevel, location.Z, location.HeadYaw,
				location.Yaw, location.Pitch), location.OnGround);
		}

		//private ThreadSafeList<ChunkCoordinates> _loadedChunks = new ThreadSafeList<ChunkCoordinates>();
		private void UnloadChunks(ChunkCoordinates center, double maxViewDistance)
		{
			var chunkPublisher = Client.LastChunkPublish;
			
			//Client.ChunkRadius
			foreach (var chunk in World.ChunkManager.GetAllChunks())
			{
				var distance = chunk.Key.DistanceTo(center);
				
				if (chunkPublisher != null)
				{
					if (chunk.Key.DistanceTo(new ChunkCoordinates(new Vector3(chunkPublisher.coordinates.X,
						chunkPublisher.coordinates.Y, chunkPublisher.coordinates.Z))) < (chunkPublisher.radius / 16f))
						continue;
				}
				
				if (distance > maxViewDistance)
				{
					//_chunkCache.TryRemove(chunkColumn.Key, out var waste);
					UnloadChunk(chunk.Key);
				}
			}
			//Parallel.ForEach(_loadedChunks.ToArray(), (chunkColumn) =>
			//{
				/*if (chunkPublisher != null)
				{
					if (chunkColumn.DistanceTo(new ChunkCoordinates(new Vector3(chunkPublisher.coordinates.X,
						    chunkPublisher.coordinates.Y, chunkPublisher.coordinates.Z))) < chunkPublisher.radius)
						return;
				}*/
				
				
		//	});
		}

		public void UnloadChunk(ChunkCoordinates coordinates)
		{
			World.UnloadChunk(coordinates);
		}

		protected override void Initiate()
		{
			_initiated = true;
			Client.World = World;
			World.Player.SetInventory(new BedrockInventory(46));

			CustomConnectedPong.CanPing = true;
			_gameTickTimer = new HighPrecisionTimer(50, GameTick);// new System.Threading.Timer(GameTick, null, 50, 50);
		}

		private bool VerifyConnection()
		{
			return Client.IsConnected;
		}

		public override Task Load(ProgressReport progressReport)
		{
			Client.GameStarted = false;
			
			return Task.Run(
				() =>
				{
					Stopwatch timer = Stopwatch.StartNew();
					progressReport(LoadingState.ConnectingToServer, 25);

					var resetEvent = new ManualResetEventSlim(false);

					Client.Start(resetEvent);
					progressReport(LoadingState.ConnectingToServer, 50);

					//	Client.HaveServer = true;

					//Client.SendOpenConnectionRequest1();
					if (!resetEvent.Wait(TimeSpan.FromSeconds(5)))
					{
						Client.ShowDisconnect("Could not connect to server!");

						return;
					}

					progressReport(LoadingState.ConnectingToServer, 98);

					//progressReport(LoadingState.LoadingChunks, 0);

					var  percentage         = 0;
					var  statusChanged      = false;
					var  done               = false;
					int  previousPercentage = 0;
					bool hasSpawnChunk      = false;

					while (true)
					{
						double radiusSquared = Math.Pow(Client.ChunkRadius, 2);
						var    target        = radiusSquared;

						percentage = (int) ((100 / target) * World.ChunkManager.ChunkCount);

						if (Client.GameStarted && percentage != previousPercentage)
						{
							progressReport(LoadingState.LoadingChunks, percentage);
							previousPercentage = percentage;

							//Log.Info($"Progress: {percentage} ({ChunksReceived} of {target})");
						}

						if (!statusChanged)
						{
							if (Client.PlayerStatus == 3 || Client.PlayerStatusChanged.WaitOne(50) || Client.HasSpawned
							    || Client.ChangeDimensionResetEvent.WaitOne(5))
							{
								statusChanged = true;

								//Client.SendMcpeMovePlayer();


								//Client.IsEmulator = false;
							}
						}

						if (!hasSpawnChunk)
						{
							if (World.ChunkManager.TryGetChunk(
								new ChunkCoordinates(
									new PlayerLocation(Client.SpawnPoint.X, Client.SpawnPoint.Y, Client.SpawnPoint.Z)),
								out _))
							{
								hasSpawnChunk = true;
							}
						}

						if (((percentage >= 100 || hasSpawnChunk)))
						{
							if (statusChanged)
							{
								break;
							}
						}

						if (!VerifyConnection())
						{
							Client.ShowDisconnect("Connection lost.");

							timer.Stop();

							return;
						}
					}

					var packet = McpeSetLocalPlayerAsInitialized.CreateObject();
					packet.runtimeEntityId = Client.EntityId;

					Client.SendPacket(packet);

					var p = World.Player.KnownPosition;

					Client.SendMcpeMovePlayer(
						new MiNET.Utils.PlayerLocation(p.X, p.Y, p.Z, p.HeadYaw, p.Yaw, p.Pitch),
						World.Player.KnownPosition.OnGround);

					//SkyLightCalculations.Calculate(WorldReceiver as World);

					//Client.IsEmulator = false;
					progressReport(LoadingState.Spawning, 99);
					timer.Stop();

					//TODO: Check if spawn position is safe.
				});
		}
		public override void Dispose()
		{
			base.Dispose();
			Client.Dispose();
			
			EventDispatcher?.UnregisterEvents(this);
		}
	}
}
