using Alex.Worlds;

namespace Alex.Entities.Hostile
{
	public class Ghast : Flying
	{
		public Ghast(World level) : base((EntityType)41, level)
		{
			JavaEntityId = 56;
			Height = 4;
			Width = 4;
		}
	}
}
