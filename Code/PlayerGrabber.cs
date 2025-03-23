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
	[RequireComponent] public LineRenderer line { get; set; }
	
	[Sync, Change] NetList<Vector3> LinePointsSync { get; set; } = [Vector3.Zero, Vector3.Zero, Vector3.Zero];
	[Sync] bool LineEnabled { get; set; } = false;
	[Sync] Vector3 GrabPosSync { get; set; }
	[Sync] Rotation GrabRotSync { get; set; }
	
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
		GrabBody = new PhysicsBody( Scene.PhysicsWorld );
	}
	
	protected override void OnUpdate()
	{
		line.VectorPoints[0] = LinePointsSync[0];
		line.VectorPoints[1] = LinePointsSync[1];
		line.VectorPoints[2] = LinePointsSync[2];

		GrabDistance += Input.MouseWheel.y * MinMaxDistance.Max / 10;
		GrabDistance = GrabDistance.Clamp( MinMaxDistance.Min, MinMaxDistance.Max );
		
		Tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), HeldBody.IsValid() ? GrabDistance : MinMaxDistance.Max )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();
		
		if ( IsProxy ) return;
		
		if ( Input.Down( "attack1" ) && !HeldBody.IsValid() ) Pickup();
		if ( !Input.Down( "attack1" ) && HeldBody.IsValid() ) Drop();
		
		if ( HeldBody.IsValid() && line.Enabled )
		{
			LinePointsSync[0] = Scene.Camera.WorldPosition + Vector3.Down * 35 + Scene.Camera.WorldRotation.Right * 20;
			var pos2 = GrabBody.Position + Vector3.Direction( LinePointsSync[2], LinePointsSync[0] ) * 15;
			LinePointsSync[1] = LinePointsSync[1].LerpTo( pos2, 0.2f );
			LinePointsSync[2] = LinePointsSync[2].LerpTo( GrabJoint.Point2.Transform.Position, 0.2f );
		}
	}

	protected override void OnFixedUpdate()
	{
		line.Enabled = LineEnabled;
		GrabBody.Position = GrabPosSync;
		GrabBody.Rotation = GrabRotSync;
		
		if ( IsProxy ) return;

		if ( !HeldBody.IsValid() ) return;
		
		if ( player.GroundObject != null )
		{
			if ( player.GroundObject.Tags.Has( "held" ) )
			{
				if ( player.GroundObject.Network.IsOwner ) Drop();
				player.PreventGrounding( .1f );
				PreventGrabbing( 1 );
				return;
			}
		}
		
		GrabPosSync = Tr.StartPosition + Tr.Direction * (HeldBody.IsValid() ? GrabDistance : MinMaxDistance.Max);
		GrabRotSync = player.EyeAngles.ToRotation() * InitialRotation;
	}
	
	[Rpc.Broadcast]
	public void Pickup()
	{
		if ( !Tr.Hit || Tr.Body is null || Tr.Body.BodyType == PhysicsBodyType.Static || !CanGrab ) return;
		
		GrabJoint?.Remove();
		GrabJoint = null;
		HeldBody = Tr.Body;

		var obj = HeldBody.GetGameObject();
		if ( !obj.Components.Get<PlayerController>().IsValid() && 
		     !obj.Components.Get<NetworkHeldObject>().IsValid() )
		{
			Log.Info("take ownership");
			obj.Network.TakeOwnership();
			var net = obj.AddComponent<NetworkHeldObject>();
			net.Owners.TryAdd( Network.Owner, GameObject );
		}
		
		HeldBody.AutoSleep = false;
		GrabDistance = (Tr.HitPosition - Scene.Camera.WorldPosition).Length;

		var localOffset = HeldBody.Transform.PointToLocal( Tr.HitPosition );
		GrabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( GrabBody ), new PhysicsPoint( HeldBody ) );
		GrabJoint.Point1 = new PhysicsPoint( GrabBody );
		GrabJoint.Point2 = new PhysicsPoint( HeldBody, localOffset );
		
		var maxForce = 5 * Tr.Body.Mass.Clamp( 0, 800 ) * Scene.PhysicsWorld.Gravity.Length;
		GrabJoint.SpringLinear = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce );
		GrabJoint.SpringAngular = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce * 5 );
		
		line.VectorPoints[0] = Scene.Camera.WorldPosition + Vector3.Down * 35 + Scene.Camera.WorldRotation.Right * 20;
		line.VectorPoints[1] = GrabBody.Position + Vector3.Direction( line.VectorPoints[2], line.VectorPoints[0] ) * 15;
		line.VectorPoints[2] = GrabJoint.Point2.Transform.Position;
		LineEnabled = true;
		InitialRotation = player.EyeAngles.ToRotation().Inverse * HeldBody.Rotation;
		InitialAngularDamping = HeldBody.AngularDamping; //Keep track of angular damping value before pickup
		InitialLinearDamping = HeldBody.LinearDamping;
		
		HeldBody.AngularDamping = GrabAngularDamping;
		HeldBody.LinearDamping = GrabLinearDamping;
	}

	[Rpc.Broadcast]
	public void Drop()
	{
		if ( !HeldBody.IsValid() ) return;
		
		var net = HeldBody.GetGameObject().GetComponent<NetworkHeldObject>();
		if ( net.IsValid() )
		{
			net.Owners.Remove( Network.Owner );
			if ( net.Owners.Count == 0 ) net.Destroy();
		}
		
		LineEnabled = false;
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
		base.OnPreRender();

		var hud = Scene.Camera.Hud;
		var center = new Vector2( Screen.Width / 2, Screen.Height / 2 );

		if ( GrabBody.IsValid() && HeldBody.IsValid() )
		{
			Gizmo.Draw.SolidSphere( GrabBody.Position, 2 );
			Gizmo.Draw.SolidSphere( GrabJoint.Point2.Transform.Position, 2 );
			Gizmo.Draw.Line( GrabBody.Position, GrabJoint.Point2.Transform.Position );
		}
		
		if ( IsProxy ) return;
		
		if ( Tr.Hit && HeldBody is null && Tr.Body.BodyType != PhysicsBodyType.Static )
		{
			hud.DrawCircle( center, 2, Color.Yellow );
		}
	}
}
