using Sandbox;
using System;
using System.Threading.Tasks;
using Sandbox.Network;
using Sandbox.Physics;
using Sandbox.Rendering;
using SpringJoint = Sandbox.SpringJoint;

namespace Proximity;
public class PhysicsGrab : Component
{
	[Property] RangedFloat MinMaxDistance { get; set; } = new( 64, 128 );
	[Property] float GrabLinearDamping { get; set; } = 25;
	[Property] float GrabAngularDamping { get; set; } = 50;
	
	[RequireComponent] public PlayerController player { get; set; }
	
	public SceneTraceResult Tr { get; set; }
	public Sandbox.Physics.FixedJoint GrabJoint { get; set; }
	public PhysicsBody HeldBody { get; set; }
	public PhysicsBody GrabBody { get; set; }
	
	public Rotation InitialRotation { get; set; }
	public float InitialLinearDamping { get; set; }
	public float InitialAngularDamping { get; set; }
	
	public float GrabDistance { get; set; }
	public bool CanGrab { get; set; } = true;
	
	protected override void OnStart()
	{
		if ( IsProxy ) return;
		
		GrabBody = new PhysicsBody( Scene.PhysicsWorld );
	}
	
	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		GrabDistance += Input.MouseWheel.y * 5;
		GrabDistance = GrabDistance.Clamp( MinMaxDistance.Min, MinMaxDistance.Max );
		
		Tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), HeldBody.IsValid() ? GrabDistance : MinMaxDistance.Max )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();
		
		if ( Input.Down( "attack1" ) && !HeldBody.IsValid() ) Pickup();
		if ( !Input.Down( "attack1" ) && HeldBody.IsValid() ) Drop();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		if ( !HeldBody.IsValid() ) return;
		
		if ( player.GroundObject != null )
		{
			if ( player.GroundObject.Tags.Has( "held" ) )
			{
				if ( player.GroundObject?.Network.Owner == Network.Owner ) Drop();
				player.PreventGrounding( .1f );
				PreventGrabbing( 1 );
				return;
			}
		}
		
		GrabBody.Position = Tr.StartPosition + Tr.Direction * (HeldBody.IsValid() ? GrabDistance : MinMaxDistance.Max);
		GrabBody.Position += player.Velocity / 20;
		HeldBody.SmoothRotate( player.EyeAngles.ToRotation() * InitialRotation, (HeldBody.Mass / 10000).Clamp( 0.1f, 100 ), Time.Delta );
	}
	
	public void Pickup()
	{
		if ( !Tr.Hit || Tr.Body is null || Tr.Body.BodyType == PhysicsBodyType.Static || !CanGrab ) return;
		
		HeldBody = Tr.Body;
		HeldBody.AutoSleep = false;
		GrabDistance = (Tr.HitPosition - Scene.Camera.WorldPosition).Length;
		HeldBody.GetGameObject().Tags.Add( "held" );

		PlayerController comp;
		if ( !HeldBody.GetGameObject().Components.TryGet( out comp ) && HeldBody.GetGameObject().Tags.Has( "held" ) )
		{
			HeldBody.GetGameObject().Network.TakeOwnership();
		}

		var localOffset = HeldBody.Transform.PointToLocal( Tr.HitPosition );
		
		GrabJoint?.Remove();
		GrabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( GrabBody ), new PhysicsPoint( HeldBody ) );
		GrabJoint.Point1 = new PhysicsPoint( GrabBody );
		GrabJoint.Point2 = new PhysicsPoint( HeldBody, localOffset );
		
		var maxForce = 5 * Tr.Body.Mass * Scene.PhysicsWorld.Gravity.Length;
		GrabJoint.SpringLinear = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce );
		GrabJoint.SpringAngular = new PhysicsSpring( 0, 0, 0 );
		
		InitialRotation = player.EyeAngles.ToRotation().Inverse * HeldBody.Rotation;
		InitialAngularDamping = HeldBody.AngularDamping; //Keep track of angular damping value before pickup
		InitialLinearDamping = HeldBody.LinearDamping;
		HeldBody.AngularDamping = GrabAngularDamping;
		HeldBody.LinearDamping = GrabLinearDamping;
	}

	public void Drop()
	{
		HeldBody.GetGameObject().Tags.Remove( "held" );
		HeldBody.AutoSleep = true;
		HeldBody.AngularDamping = InitialAngularDamping; //Reset angular damping
		HeldBody.LinearDamping = InitialLinearDamping;
		
		GrabJoint?.Remove();
		GrabJoint = null;
		HeldBody = null;
	}
	
	public async void PreventGrabbing( float Seconds )
	{
		try
		{
			CanGrab = false;
			await Task.DelayRealtimeSeconds( Seconds );
			CanGrab = true;
		}
		catch (Exception e)
		{
			Log.Warning( "Couldn't prevent grabbing: " + e );
		}
	}
	
	protected override void OnPreRender()
	{
		if ( IsProxy ) return;
		
		base.OnPreRender();

		var hud = Scene.Camera.Hud;
		var center = new Vector2( Screen.Width / 2, Screen.Height / 2 );
		
		if ( Tr.Hit && HeldBody is null && Tr.Body.BodyType != PhysicsBodyType.Static )
		{
			hud.DrawLine( center + Vector2.Right * 5, center + Vector2.Left * 5, 2, Color.Yellow );
			hud.DrawLine( center + Vector2.Up * 5, center + Vector2.Down * 5, 2, Color.Yellow );
		}
	}
}
