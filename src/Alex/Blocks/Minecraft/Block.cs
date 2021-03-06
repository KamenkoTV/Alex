﻿using System;
using Alex.API.Blocks;
using Alex.API.Items;
using Alex.API.Resources;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Properties;
using Alex.Blocks.State;
using Alex.Entities;
using Alex.Items;
using Alex.ResourcePackLib.Json;
using Alex.Utils;
using Alex.Worlds;
using Alex.Worlds.Abstraction;
using Alex.Worlds.Chunks;
using Microsoft.Xna.Framework;
using MiNET.Blocks;
using NLog;
using ItemBlock = Alex.Items.ItemBlock;
using ItemMaterial = Alex.API.Utils.ItemMaterial;
using ItemType = Alex.API.Utils.ItemType;

namespace Alex.Blocks.Minecraft
{
	public class Block : IRegistryEntry<Block>
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(Block));

		protected static PropertyBool Lit = new PropertyBool("lit", "true", "false");
		
		public bool Solid { get; set; }
		public bool Transparent { get; set; }
		public bool Animated { get; set; } = false;
		public bool Renderable { get; set; }
		public bool HasHitbox { get; set; }
		public bool IsFullCube { get; set; } = true;
		public bool IsFullBlock { get; set; } = true;

		public bool RandomTicked { get; set; } = false;
		public bool IsReplacible { get; set; } = false;
		public bool RequiresUpdate { get; set; } = false;
		public bool CanInteract { get; set; } = false;

		public string Name
		{
			get { return Location.ToString(); }
			set { Location = value; }
		}
		
	    public virtual byte LightValue { get; set; } = 0;
	    public int LightOpacity { get; set; } = 1;
	    
		public BlockState BlockState { get; set; }
		public bool IsWater { get; set; } = false;
		public bool IsSourceBlock { get; set; } = false;
		
		private float _hardness = -1f;

		public float Hardness
		{
			get
			{
				if (_hardness >= 0f)
					return _hardness;

				return BlockMaterial.Hardness;
			}
			set
			{
				_hardness = value;
			}
		}

        private IMaterial _material = new Material(MapColor.STONE);

		public virtual IMaterial BlockMaterial
		{
			get { return _material; }
			set
			{
				IMaterial newValue = value;

				_material = newValue;
			}
		}

		public BlockCoordinates Coordinates { get; set; }
		protected Block(int blockId, byte metadata)
	    {
		    Solid = true;
		    Transparent = false;
		    Renderable = true;
		    HasHitbox = true;
	    }

	    public Block(uint blockStateId)
	    {
		    Solid = true;
		    Transparent = false;
		    Renderable = true;
		    HasHitbox = true;
	    }

		protected Block(string blockName)
		{
			Solid = true;
			Transparent = false;
			Renderable = true;
			HasHitbox = true;
		}

		protected Block()
		{
			Solid = true;
			Transparent = false;
			Renderable = true;
			HasHitbox = true;
		}

		public virtual bool CanClimb(BlockFace face)
		{
			return false;
		}

		public virtual BlockState BlockPlaced(IBlockAccess world, BlockState state, BlockCoordinates position)
		{
			return state;
			/*if (BlockState is BlockState s)
			{
				if (s.IsMultiPart)
				{
					BlockStateResource blockStateResource;

					if (Alex.Instance.Resources.ResourcePack.BlockStates.TryGetValue(s.Name, out blockStateResource))
					{
						BlockState.Model = new CachedResourcePackModel(Alex.Instance.Resources,
							MultiPartModels.GetBlockStateModels(world, position, s.VariantMapper.GetDefaultState(), blockStateResource));
						world.SetBlockState(position.X, position.Y, position.Z, BlockState);
					}
				}
			}*/
		}
		
		public virtual void Interact(World world, BlockCoordinates position, BlockFace face, Entity sourceEntity)
		{

		}

		public virtual void BlockUpdate(World world, BlockCoordinates position, BlockCoordinates updatedBlock)
		{
			
		}

		public virtual Item[] GetDrops(Item tool)
		{
			if (BlockMaterial.IsToolRequired && !BlockMaterial.CanUseTool(tool.ItemType, tool.Material))
			{
				return new Item[0];
			}
			
			return new Item[] { new ItemBlock(BlockState) { Count = 1 } };
		}

        public double GetBreakTime(Item miningTool)
		{
			double secondsForBreak = Hardness;
			bool isHarvestable = GetDrops(miningTool)?.Length > 0;
			
			if (BlockMaterial.IsToolRequired)
			{
				isHarvestable = BlockMaterial.CanUseTool(miningTool.ItemType, miningTool.Material);
			}
			
			if (isHarvestable)
			{
				secondsForBreak *= 1.5;
			}
			else
			{
				secondsForBreak *= 5;
			}
			if (secondsForBreak == 0D)
			{
				secondsForBreak = 0.05;
			}

			int tierMultiplier = 1;
			if (BlockMaterial.CanUseTool(miningTool.ItemType, miningTool.Material))
			{
				switch (miningTool.Material)
				{
					case ItemMaterial.Wood:
						tierMultiplier = 2;
						break;
					case ItemMaterial.Stone:
						tierMultiplier = 4;
						break;
					case ItemMaterial.Gold:
						tierMultiplier = 12;
						break;
					case ItemMaterial.Iron:
						tierMultiplier = 6;
						break;
					case ItemMaterial.Diamond:
						tierMultiplier = 8;
						break;
				}
			}

			if (isHarvestable)
			{
				switch (miningTool.ItemType)
				{
					case ItemType.Shears:
						if (this is Wool)
						{
							return secondsForBreak / 5;
						}
						else if (this is Leaves || this is AcaciaLeaves || this is Cobweb)
						{
							return secondsForBreak / 15;
						}
						break;
					case ItemType.Sword:
						if (this is Cobweb)
						{
							return secondsForBreak / 15;
						}
						return secondsForBreak / 1.5;
					case ItemType.Shovel:
					case ItemType.Axe:
					case ItemType.PickAxe:
					case ItemType.Hoe:
						return secondsForBreak / tierMultiplier;
				}
			}

			return secondsForBreak;
		}

        public virtual bool CanCollide()
        {
	        return true;
        }

        public virtual bool ShouldRenderFace(BlockFace face, Block neighbor)
        {
	        if (Transparent)
	        {
		        if (Solid)
		        {
			        //	if (IsFullCube && Name.Equals(block.Name)) return false;
			        if (neighbor.Solid && (neighbor.Transparent || !neighbor.IsFullCube))
			        {
				        //var block = world.GetBlock(pos.X, pos.Y, pos.Z);
				        if (!BlockMaterial.IsOpaque && !neighbor.BlockMaterial.IsOpaque)
					        return false;

				        if (!IsFullBlock || !neighbor.IsFullBlock) return true;
			        }
			        
			        //If neighbor is solid & not transparent. Hmmm?
			        if (neighbor.Solid && !(neighbor.Transparent || !neighbor.IsFullCube)) return true;
		        }
		        else
		        {
			      //  if (neighbor.Solid && neighbor.Transparent && neighbor.IsFullCube)
				   //     return true;
			        
			        if (neighbor.Solid && !(neighbor.Transparent || neighbor.IsFullCube)) return false;
		        }
	        }


	        if (Solid && (neighbor.Transparent || !neighbor.IsFullCube)) return true;
	        //   if (me.Transparent && block.Transparent && !block.Solid) return false;
	        if (Transparent) return true;
	        if (!Transparent && (neighbor.Transparent || !neighbor.IsFullCube)) return true;
	        if (neighbor.Solid && !(neighbor.Transparent || !neighbor.IsFullCube)) return false;
	        if (Solid && neighbor.Solid && neighbor.IsFullCube) return false;
			
	        return true;
        }

        public virtual bool CanAttach(BlockFace face, Block block)
        {
	        return block.Solid && (block.IsFullCube || (block.BlockState.Name.Equals(
		        BlockState.Name, StringComparison.InvariantCultureIgnoreCase)));
        }

        public string DisplayName { get; set; } = null;
	    public override string ToString()
	    {
		    return DisplayName ?? GetType().Name;
	    }

		public virtual BlockState GetDefaultState()
		{
			BlockState r = null;
			if (BlockState is BlockState s)
			{
				r = s.VariantMapper.GetDefaultState();
			}

			if (r == null)
				return new BlockState()
				{

				};

			return r;
		}

		public ResourceLocation Location { get; private set; }
		public IRegistryEntry<Block> WithLocation(ResourceLocation location)
		{
			Location = location;

			return this;
		}

		public Block Value => this;
	}
}