﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Alex.API.Data.Options;
using Alex.API.Resources;
using Alex.API.Services;
using Alex.API.Utils;
using Alex.Blocks;
using Alex.Blocks.Minecraft;
using Alex.Blocks.State;
using Alex.Entities;
using Alex.Gamestates;
using Alex.Graphics;
using Alex.Graphics.Effect;
using Alex.Gui;
using Alex.Items;
using Alex.Networking.Java;
using Alex.ResourcePackLib;
using Alex.ResourcePackLib.Generic;
using Alex.ResourcePackLib.Json.Models.Blocks;
using Alex.Utils;
using Alex.Utils.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using NLog;
using DateTime = System.DateTime;
using Task = System.Threading.Tasks.Task;

namespace Alex
{
	public class ResourceManager
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(ResourceManager));

		private LinkedList<McResourcePack> ActiveResourcePacks { get; } = new LinkedList<McResourcePack>();
		public McResourcePack ResourcePack => ActiveResourcePacks.First?.Value;
		public BedrockResourcePack BedrockResourcePack { get; private set; }
		public Registries Registries { get; private set; }
		public AtlasGenerator Atlas { get; private set; }

		private IStorageSystem Storage { get; }
		private IOptionsProvider Options { get; }
		private IRegistryManager RegistryManager { get; }
		private Alex Alex { get; }
		private MCJavaAssetsUtil AssetsUtil { get; }
		private MCBedrockAssetUtils BedrockAssetUtil { get; }

		private ContentManager ContentManager { get; }
		
		public static Effect EntityEffect { get; set; }
		public static Effect BlockEffect { get; set; }
		public static Effect LightingEffect { get; set; }
		
		public List<MCPack> Packs { get; } = new List<MCPack>();
		
		public ResourceManager(IServiceProvider serviceProvider)
		{
			Atlas = new AtlasGenerator();
			Storage = serviceProvider.GetService<IStorageSystem>();

			Options = serviceProvider.GetService<IOptionsProvider>();
			RegistryManager = serviceProvider.GetService<IRegistryManager>();
			Alex = serviceProvider.GetService<Alex>();
			ContentManager = serviceProvider.GetService<ContentManager>();
			
			AssetsUtil = new MCJavaAssetsUtil(Storage); //ContentManager.Load<byte[]>();
			BedrockAssetUtil = new MCBedrockAssetUtils(Storage);
		}

		private void ResourcePacksChanged(string[] oldvalue, string[] newvalue)
		{
			Log.Info($"Resource packs changed.");
			
			SplashScreen splashScreen = new SplashScreen();
			Alex.GameStateManager.SetActiveState(splashScreen);

			Task.Run(() =>
			{
				LoadResourcePacks(Alex.GraphicsDevice, splashScreen, newvalue);
				Alex.GameStateManager.Back();
			});
		}

		public bool TryLoadResourcePackInfo(string file, out ResourcePackManifest manifest)
		{
			manifest = default;
			if (!File.Exists(file))
			{
				return false;
			}

			ResourcePackLib.ResourcePack info;
			using(FileStream stream = File.OpenRead(file))
			using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
				info = ResourcePackLib.ResourcePack.LoadFromArchive(archive);

			if (info == null || info == default)
			{
				return false;
			}

			manifest = info.Info;
			return true;
		}

		private McResourcePack LoadResourcePack(IProgressReceiver progressReceiver, Stream stream, bool useModelResolver = false, McResourcePack.McResourcePackPreloadCallback preloadCallback = null)
		{
			Stopwatch sw = Stopwatch.StartNew();
			
			Log.Info($"Loading resource pack...");
			
			McResourcePack resourcePack = null;

			using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
			{
				resourcePack = new McResourcePack(archive, preloadCallback);
			}

			sw.Stop();
			
			Log.Info($"Loaded {resourcePack.BlockModels.Count} block models from resourcepack");
			Log.Info($"Loaded {resourcePack.ItemModels.Count} item models from resourcepack");
			Log.Info($"Resource pack loading took: {sw.ElapsedMilliseconds}ms");

			var language = resourcePack.Languages.Values.FirstOrDefault(x => x.Namespace.Equals("minecraft"));
			if (language != null)
			{
				foreach (var translation in language)
				{
					ChatParser.TranslationRules[translation.Key] = translation.Value;
				}
			}

			return resourcePack;
		}

		private void LoadModels(IProgressReceiver progressReceiver, McResourcePack resourcePack, bool replaceModels,
            bool reportMissingModels)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int imported = BlockFactory.LoadResources(RegistryManager, this, resourcePack, replaceModels, reportMissingModels, progressReceiver);
            sw.Stop();

            Log.Info($"Imported {imported} blockstate variants from resourcepack in {sw.ElapsedMilliseconds}ms!");
        }

        private void LoadTextures(GraphicsDevice device, IProgressReceiver progressReceiver,
            McResourcePack resourcePack, bool isFirst)
        {
	        progressReceiver.UpdateProgress(0, $"Loading textures: {resourcePack.Manifest?.Name ?? "Unknown"}");
	        
            if (!isFirst)
            {
	            Atlas.LoadResourcePackOnTop(device,
		            ActiveResourcePacks.First(),
		            resourcePack,
		            progressReceiver);
            }
            else
            {
                Atlas.LoadResourcePack(device, resourcePack, progressReceiver);
            }

          //  if (!isFirst)
            {
	            progressReceiver.UpdateProgress(0, $"Loading UI textures: {resourcePack.Manifest?.Name ?? "Unknown"}");
	            Alex.GuiRenderer.LoadResourcePackTextures(resourcePack, progressReceiver);
            }
        }

        private bool CheckBedrockAssets(IProgressReceiver progressReceiver, out byte[] bedrockResources)
        {
	        bedrockResources = null;
	        
			try
			{
				string bedrockPath = BedrockAssetUtil.CheckAndDownloadResources(progressReceiver).Result;
				if (string.IsNullOrWhiteSpace(bedrockPath) || !Storage.TryReadBytes(bedrockPath, out bedrockResources))
				{
					Log.Error("Could not load any of the required Bedrock assets! Are you connected to the internet?");
					Log.Info($"A manual fix is available, see: https://github.com/kennyvv/Alex/wiki/Bedrock-Assets");
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Could not check for latests assets! Are you connected to the internet?");
				return false;
			}

			return true;
		}

        private bool CheckJavaAssets(IProgressReceiver progressReceiver, out byte[] javaResources)
        {
	        try
	        {
		        string path = AssetsUtil.EnsureTargetReleaseAsync(JavaProtocol.VersionId, progressReceiver).Result;

		        if (!Storage.TryReadBytes(path, out javaResources))
		        {
			        Log.Error($"Could not load any assets! Are you connected to the internet?");

			        javaResources = null;

			        //bedrockResources = null;
			        return false;
		        }
	        }
	        catch (Exception ex)
	        {
		        Log.Error(ex, $"Could not check for latests assets! Are you connected to the internet?");
		        javaResources = null;
		        //bedrockResources = null;
		        return false;
	        }

	        return true;
        }

        public string DeviceID { get; private set; } = null;
        private void LoadHWID()
        {
	        string hwid = "";
	        string path = "hwid.txt";

	        if (Storage.TryReadString(path, out hwid))
	        {
		        DeviceID = hwid;

		        return;
	        }

	        hwid = Guid.NewGuid().ToString();
	        Storage.TryWriteString(path, hwid);
        }
        
        public DirectoryInfo SkinPackDirectory { get; private set; } = null;
        public DirectoryInfo ResourcePackDirectory { get; private set; } = null;
        private  McResourcePack.McResourcePackPreloadCallback PreloadCallback { get; set; }
        public bool CheckResources(GraphicsDevice device, IProgressReceiver progressReceiver, McResourcePack.McResourcePackPreloadCallback preloadCallback)
        {
	        LoadHWID();
	        
	        PreloadCallback = preloadCallback;
	        
	        Log.Info($"Loading registries...");
	        progressReceiver?.UpdateProgress(0, "Loading registries...");
	        Registries = JsonConvert.DeserializeObject<Registries>(ReadStringResource("Alex.Resources.registries.json"));
	        progressReceiver?.UpdateProgress(100, "Loading registries...");
	        
			byte[] defaultResources;
			byte[] defaultBedrock;

			if (!CheckJavaAssets(progressReceiver, out defaultResources))
			{
				return false;
			}
			
			Log.Info($"Loading vanilla resources...");
			using (MemoryStream stream = new MemoryStream(defaultResources))
			{
				var vanilla = LoadResourcePack(progressReceiver, stream, false, preloadCallback);
				vanilla.Manifest.Name = "Vanilla";
				
				ActiveResourcePacks.AddFirst(vanilla);
			}

			if (!CheckBedrockAssets(progressReceiver, out defaultBedrock))
			{
				return false;
			}

			var bedrockPath = Path.Combine("assets", "bedrock");

            DirectoryInfo directory;
            if (!Storage.TryGetDirectory(bedrockPath, out directory))
            {
	            Log.Warn($"The bedrock resources required to play this game are not set-up correctly!");
	            Console.ReadLine();
	            Environment.Exit(1);
	            return false;
            }

            /*if (!directories.Any(x => x.Name.Equals("models")))
            {
				Log.Warn($"Please make sure to extract the MC:Bedrock resource pack into \"{directory.FullName}\"");
                Console.ReadLine();
                Environment.Exit(1);
				return false;
            }*/

            if (directory.GetFileSystemInfos().Length == 0)
            {
	            Log.Info($"Extracting required resources...");
	            progressReceiver?.UpdateProgress(50, "Extracting resources...");

	            byte[] zipped = defaultBedrock;//ReadResource("Alex.Resources.resources.zip");
	            using (MemoryStream ms = new MemoryStream(zipped))
	            {
		            using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read))
		            {
			            archive.ExtractToDirectory(directory.FullName);
		            }
	            }
            }
            
            var directories = directory.GetDirectories();

          /*  if (!directories.Any(x => x.Name.Equals("definitions")))
            {
				Log.Warn($"The required definition files are not found. Any questions can be asked on Discord.");
                Console.ReadLine();
				Environment.Exit(1);
                return false;
            }*/
			//report(ResourcePack.AsciiFont);

			Log.Info($"Loading bedrock resources...");
			BedrockResourcePack = new BedrockResourcePack(directory);

			EntityFactory.LoadModels(this, device, true, progressReceiver);


			Log.Info($"Loading known entity data...");
			EntityFactory.Load(this, progressReceiver);

            Storage.TryGetDirectory(Path.Combine("assets", "resourcepacks"), out DirectoryInfo root);
            ResourcePackDirectory = root;

            LoadRegistries(progressReceiver);

            LoadResourcePacks(device, progressReceiver, Options.AlexOptions.ResourceOptions.LoadedResourcesPacks.Value);

            ItemFactory.Init(RegistryManager, this, ResourcePack, progressReceiver);

            if (Storage.TryGetDirectory(Path.Combine("assets", "bedrockpacks"), out DirectoryInfo info))
            {
	            SkinPackDirectory = info;
	            LoadBedrockPacks(progressReceiver, info);
            }
            else
            {
	            if (Storage.TryCreateDirectory(Path.Combine("assets", "bedrockpacks")))
	            {
		            if (Storage.TryGetDirectory(Path.Combine("assets", "bedrockpacks"), out var dirInfo))
		            {
			            SkinPackDirectory = dirInfo;
		            }
	            }
            }
            
            Options.AlexOptions.ResourceOptions.LoadedResourcesPacks.Bind(ResourcePacksChanged);
            _hasInit = true;
            
            return true;
		}

        public void LoadBedrockPacks(IProgressReceiver progressReceiver, DirectoryInfo directoryInfo)
        {
	        progressReceiver?.UpdateProgress(0, "Loading bedrock .MCPack files...");

	        var files = directoryInfo.EnumerateFiles("*.mcpack").ToArray();

	        for (var index = 0; index < files.Length; index++)
	        {
		        var file = files[index];
		        progressReceiver?.UpdateProgress(index * (files.Length / 100), "Loading bedrock .MCPack files...", file.Name);

		        try
		        {
			        using (var archive = new ZipArchive(file.Open(FileMode.Open, FileAccess.Read), ZipArchiveMode.Read))
			        {
				        MCPack pack = new MCPack(archive);
				        Packs.Add(pack);
			        }
		        }
		        catch (Exception ex)
		        {
			        Log.Warn(ex, $"Failed to load bedrock .MCPack file: {file.Name}: {ex}");
		        }
	        }
        }

        private bool _hasInit = false;
        private void LoadResourcePacks(GraphicsDevice device, IProgressReceiver progress, string[] resourcePacks)
        {
	        var countBefore = ActiveResourcePacks.Count;
	        
	        var first = ActiveResourcePacks.First.Value;
	        ActiveResourcePacks.Clear();

	        ActiveResourcePacks.AddFirst(first);

	        if (_hasInit)
	        {
		        PreloadCallback?.Invoke(first.FontBitmap, McResourcePack.BitmapFontCharacters.ToList());
		        Atlas.Reset();
	        }
	        
	        foreach (string file in resourcePacks)
	        {
		        try
		        {
			        string resourcePackPath = Path.Combine(ResourcePackDirectory.FullName, file);
			        if (File.Exists(resourcePackPath))
			        {
				        Log.Info($"Loading resourcepack {file}...");

				        using (FileStream stream = new FileStream(resourcePackPath, FileMode.Open))
				        {
					        var pack = LoadResourcePack(progress, stream, true, null);
					        if (pack.Manifest != null && string.IsNullOrWhiteSpace(pack.Manifest.Name))
					        {
						        pack.Manifest.Name = Path.GetFileNameWithoutExtension(file);
					        }
					        ActiveResourcePacks.AddLast(pack);
				        }
			        }
		        }
		        catch (Exception e)
		        {
			        Log.Warn(e, $"Could not load resourcepack {file}: {e.ToString()}");
		        }
	        }
	        
	        bool isFirst = true;

	        isFirst = true;
	        foreach (var resourcePack in ActiveResourcePacks)
	        {
		        LoadModels(progress, resourcePack, !isFirst, isFirst);
		        if (isFirst)
		        { //Only load models for vanilla until above we fix blockstate replacement issues.
			        isFirst = false;
			        break;
		        }
	        }

	        isFirst = true;
	        foreach (var resourcePack in ActiveResourcePacks)
	        {
		        Alex.GuiRenderer.LoadLanguages(resourcePack, progress);
		        
		        LoadTextures(device, progress, resourcePack, isFirst);

		        if (isFirst)
			        isFirst = false;
	        }
	        
	        Alex.GuiRenderer.SetLanguage(Options.AlexOptions.MiscelaneousOptions.Language.Value);

	        var f = ActiveResourcePacks.LastOrDefault(x => x.FontBitmap != null);
	        if (f != null)
	        {
		        PreloadCallback?.Invoke(f.FontBitmap, McResourcePack.BitmapFontCharacters.ToList());
	        }
        }
        
        private void LoadRegistries(IProgressReceiver progress)
        {
	        progress.UpdateProgress(0, "Loading block model registry...");
	        RegistryManager.AddRegistry<BlockModelEntry, Graphics.Models.Blocks.BlockModel>(new BlockModelRegistry());
	        
	        progress.UpdateProgress(0, "Loading blockstate registry...");
	        RegistryManager.AddRegistry(new RegistryBase<BlockState>("blockstate"));
	        
	        progress.UpdateProgress(50, "Loading block registry...");
	        RegistryManager.AddRegistry(new BlockRegistry());
        }

        public static string ReadStringResource(string resource)
		{
			return Encoding.UTF8.GetString(ReadResource(resource));
		}
		
		public static byte[] ReadResource(string resource)
		{
			var fontStream = Assembly.GetEntryAssembly().GetManifestResourceStream(resource);
			return fontStream.ReadAllBytes();
		}
	}

	public class Registries
	{
        [JsonProperty("minecraft:fluid")]
        public RegistryBase Fluids { get; set; }

        [JsonProperty("minecraft:block")]
		public RegistryBase Blocks { get; set; }

		[JsonProperty("minecraft:item")]
		public RegistryBase Items { get; set; }

		[JsonProperty("minecraft:entity_type")]
		public RegistryBase Entities { get; set; }

        public class RegistryBase
		{
			[JsonProperty("default")]
			public string Default { get; set; } = null;

			[JsonProperty("protocol_id")]
			public int ProtocolId { get; set; }

			[JsonProperty("entries")]
			public IReadOnlyDictionary<string, RegistryEntry> Entries { get; set; }
		}

		public class RegistryEntry
		{
			[JsonProperty("protocol_id")]
			public int ProtocolId { get; set; }
		}
	}
}
