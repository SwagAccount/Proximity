using Sandbox;

public sealed class NetworkHeldObject : Component
{
	[Sync, Change] public NetDictionary<Connection, GameObject> Owners { get; set; } = new();
	[Property] Dictionary<Connection, GameObject> OwnersDebug { get; set; }

	protected override void OnFixedUpdate()
	{
		OwnersDebug = Owners.ToDictionary();
	}
}
