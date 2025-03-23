using Sandbox;

public sealed class NetworkHeldObject : Component
{
	public Dictionary<Connection, GameObject> Owners { get; set; } = new();
}
