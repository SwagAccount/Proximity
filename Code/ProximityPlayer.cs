using Proximity;
using Sandbox;

public sealed class ProximityPlayer : Component
{
	[RequireComponent] PlayerController player { get; set; }
	[RequireComponent] PhysicsGrab grab { get; set; }
	protected override void OnStart()
	{
		player.Renderer.RenderType = ModelRenderer.ShadowRenderType.On;
		
		if ( IsProxy ) return;
		
		player.Renderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
	}
	
	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var fov = Preferences.FieldOfView * (1 + player.Velocity.WithZ( 0 ).Length / 2000);
		var newFOV = float.Lerp( Scene.Camera.FieldOfView, fov, Time.Delta * 10 );
		Scene.Camera.FieldOfView = newFOV;
	}
}
