using System;
using Sandbox;
using Sandbox.Audio;

public sealed class Footsteps : Component, PlayerController.IEvents
{
	[RequireComponent] PlayerController player { get; set; }
	[Property] SoundEvent Concrete { get; set; }
	TimeSince TimeSinceStep { get; set; }
	protected override void OnStart()
	{
		TimeSinceStep = 0;
	}
	
	protected override void OnFixedUpdate()
	{
		player.Renderer.OnFootstepEvent += PlayFootstepSound;
	}
	
	public void PlayFootstepSound( SceneModel.FootstepEvent step )
	{
		if ( TimeSinceStep < 0.2f || !player.IsOnGround || player.Velocity.IsNearZeroLength ) return;
		TimeSinceStep = 0;
		
		var snd = Sound.Play( Concrete );
		snd.TargetMixer = Mixer.Master;
		snd.Position = step.Transform.Position;
	}
}
