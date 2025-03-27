using System;
using System.Numerics;
using Sandbox.Physics;

namespace Proximity;
public partial class PhysicsGrab : Component
{
	[Property] RangedFloat MinMaxDistance { get; set; } = new( 64, 128 );
	[Property] float GrabLinearDamping { get; set; } = 25;
	[Property] float GrabAngularDamping { get; set; } = 50;
	[Property, Range(0, 1, 0.05f)] float RotateSpeed { get; set; } 
	
	[RequireComponent] public PlayerController player { get; set; }
	[RequireComponent] public LineRenderer line { get; set; }

	[ConVar( "pr.debug_grab", ConVarFlags.Saved )] public static bool GrabDebug { get; set; } = false;
	
	[Sync, Change] NetList<Vector3> LinePointsSync { get; set; } = [Vector3.Zero, Vector3.Zero, Vector3.Zero];
	[Sync] bool LineEnabled { get; set; } = false;
	[Sync] bool IsRotating { get; set; }
	[Sync] Vector3 GrabPosSync { get; set; }
	[Sync] Rotation GrabRotSync { get; set; } = Rotation.Identity;
	[Sync] public GameObject HeldObject { get; set; }
	[Sync] Vector3 LocalOffset { get; set; }
	
	public SceneTraceResult Tr { get; set; }
	public Rotation InitialRotation { get; set; }
	public Rotation RotationOffset { get; set; }
	[Property] public float InitialLinearDamping { get; set; }
	[Property] public float InitialAngularDamping { get; set; }
	
	public float GrabDistance { get; set; }
	public bool CanGrab { get; set; } = true;
	Color PlayerColor { get; set; }
	
	protected override void OnStart()
	{
		MoveBody = new PhysicsBody( Scene.PhysicsWorld );
		RotateBody = new PhysicsBody( Scene.PhysicsWorld );
		PlayerColor = Color.Random.WithAlpha( 1 );
	}
	
	protected override void OnUpdate()
	{
		line.VectorPoints[0] = LinePointsSync[0];
		line.VectorPoints[1] = LinePointsSync[1];
		line.VectorPoints[2] = LinePointsSync[2];

		GrabDistance += Input.MouseWheel.y * MinMaxDistance.Max / 10;
		GrabDistance = GrabDistance.Clamp( MinMaxDistance.Min, MinMaxDistance.Max );
		
		Tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), HeldObject.IsValid() ? GrabDistance : MinMaxDistance.Max )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();
		
		if ( IsProxy ) return;

		HandleInput();
		
		GrabPosSync = Tr.StartPosition + Tr.Direction * ( HeldObject.IsValid() ? GrabDistance : MinMaxDistance.Max );
		GrabRotSync = player.EyeAngles.ToRotation() * InitialRotation;
		
		if ( HeldObject.IsValid() && line.Enabled )
		{
			LinePointsSync[0] = Scene.Camera.WorldPosition + Vector3.Down * 35 + Scene.Camera.WorldRotation.Right * 20;
			var pos2 = GrabPosSync + Vector3.Direction( LinePointsSync[2], LinePointsSync[0] ) * 15;
			LinePointsSync[1] = LinePointsSync[1].LerpTo( pos2, 0.2f );
			LinePointsSync[2] = LinePointsSync[2].LerpTo( HeldObject.WorldTransform.PointToWorld( LocalOffset ), 0.2f );
		}
	}

	public void HandleInput()
	{
		if ( Input.Down( "attack1" ) && !HeldObject.IsValid() ) Pickup();
		if ( !Input.Down( "attack1" ) && HeldObject.IsValid() ) Drop();
		
		if ( Input.Pressed( "attack2" ) && HeldObject.IsValid() )
		{
			IsRotating = true;
			player.UseInputControls = false;
			player.WishVelocity = 0; // hacky bullshit TODO: let the player move while rotating
		}
		if ( Input.Released( "attack2" ) )
		{
			IsRotating = false;
			player.UseInputControls = true;
		}
		
		if ( Input.Down( "attack2" ) && HeldObject.IsValid() )
		{
			Rotate( new Angles( 0.0f, player.EyeTransform.Rotation.Yaw(), 0.0f ), Input.MouseDelta * RotateSpeed );
		}
	}
	
	public void Rotate( Rotation eye, Vector3 input )
	{
		var localRot = eye;
		localRot *= Rotation.FromAxis( Vector3.Up, input.x * RotateSpeed );
		localRot *= Rotation.FromAxis( Vector3.Right, input.y * RotateSpeed );
		localRot = eye.Inverse * localRot;

		InitialRotation = localRot * InitialRotation;
	}

	protected override void OnFixedUpdate()
	{
		line.Enabled = LineEnabled;

		MoveObject();
		
		if ( IsProxy ) return;

		if ( !HeldObject.IsValid() ) return;

		if ( !HeldObject.Tags.Has( "held" ) )
		{
			AddHeldTag( HeldObject );
			HeldObject.Network.TakeOwnership();
		}

		if ( player.GroundObject != null && player.GroundObject.Tags.Has( "held" ) )
		{
			if ( player.GroundObject.Network.IsOwner ) Drop();
			player.PreventGrounding( .1f );
			PreventGrabbing( .5f );
		}
	}
	
	public void Pickup()
	{
		if ( !Tr.Hit || Tr.Body is null || Tr.Body.BodyType == PhysicsBodyType.Static || !CanGrab ) return;
		
		HeldObject = Tr.GameObject;
		HeldBody.AutoSleep = false;
		GrabDistance = (Tr.HitPosition - Scene.Camera.WorldPosition).Length;
		LocalOffset = HeldBody.Transform.PointToLocal( Tr.HitPosition );
		InitialRotation = player.EyeAngles.ToRotation().Inverse * HeldBody.Rotation;
		RotationOffset = Rotation.FromPitch( 0 );

		if ( !HeldObject.Tags.Has( "held" ) )
		{
			AddHeldTag( HeldObject );
			HeldObject.Network.TakeOwnership();
		}
		
		line.VectorPoints[0] = Scene.Camera.WorldPosition + Vector3.Down * 35 + Scene.Camera.WorldRotation.Right * 20;
		line.VectorPoints[1] = GrabPosSync + Vector3.Direction( line.VectorPoints[2], line.VectorPoints[0] ) * 15;
		line.VectorPoints[2] = HeldObject.WorldTransform.PointToWorld( LocalOffset );
		LineEnabled = true;
		
		if ( GrabDebug && HeldObject.Network.IsOwner )
		{
			HeldObject.Components.Get<HighlightOutline>()?.Destroy();
			HeldObject.Components.Create<HighlightOutline>().Color = PlayerColor;
		}
	}

	[Rpc.Broadcast]
	public void AddHeldTag( GameObject obj )
	{
		if ( obj.Tags.Has( "held" ) ) return;
		obj.Tags.Add( "held" );
	}

	[Rpc.Broadcast]
	public void RemoveHeldTag( GameObject obj )
	{
		if ( !obj.Tags.Has( "held" ) ) return;
		obj.Tags.Remove( "held" );
	}

	[Rpc.Broadcast]
	public void Drop()
	{
		if ( !HeldObject.IsValid() ) return;

		HeldBody.LinearDamping = InitialLinearDamping;
		HeldBody.AngularDamping = InitialAngularDamping;

		LineEnabled = false;
		HeldObject.Components.Get<HighlightOutline>()?.Destroy();
		if ( !IsProxy && HeldObject.Network.IsOwner ) RemoveHeldTag( HeldObject );

		HeldBody.AutoSleep = true;
		MoveJoint?.Remove();
		RotateJoint?.Remove();
		HeldObject = null;
		lastGrabbed = null;
		LastBody = null;
		player.UseInputControls = true;
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

		if ( MoveJoint.IsValid() && MoveBody.IsValid() && HeldObject.IsValid() && GrabDebug )
		{
			Gizmo.Draw.SolidSphere( MoveBody.Position, 2 );
			Gizmo.Draw.SolidSphere( MoveJoint.Point2.Transform.Position, 2 );
			Gizmo.Draw.Line( MoveBody.Position, MoveJoint.Point2.Transform.Position );
			Gizmo.Draw.Color = Color.Blue;
			Gizmo.Draw.Arrow( GrabPosSync, GrabPosSync + InitialRotation.Forward * 20 );
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.Arrow( GrabPosSync, GrabPosSync + GrabRotSync.Forward * 20 );
		}
		
		if ( IsProxy ) return;
		
		if ( Tr.Hit && HeldObject is null && Tr.Body.BodyType != PhysicsBodyType.Static )
		{
			hud.DrawCircle( center, 2, Color.Yellow );
		}
	}
}
