using Sandbox;

public sealed class ProximityPlayer : Component
{
	[RequireComponent] PlayerController player { get; set; }
	protected override void OnStart()
	{
		
	}
	
	protected override void OnFixedUpdate()
	{
		var newFOV = float.Lerp( Scene.Camera.FieldOfView, Preferences.FieldOfView * (1 + player.Velocity.Length / 2000), Time.Delta * 10 );
		Scene.Camera.FieldOfView = newFOV;
	}
}
