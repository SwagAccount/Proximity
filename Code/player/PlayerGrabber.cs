using System;

namespace Proximity;
public partial class PhysicsGrab : Component
{
	[Property] RangedFloat MinMaxDistance { get; set; } = new( 64, 128 );
	[Property] float GrabLinearDamping { get; set; } = 25;
	[Property] float GrabAngularDamping { get; set; } = 50;
	
	[RequireComponent] public PlayerController player { get; set; }
	[RequireComponent] public LineRenderer line { get; set; }

	[ConVar( "pr.debug_grab", ConVarFlags.Saved )] public static bool GrabDebug { get; set; } = false;
	
	[Sync, Change] NetList<Vector3> LinePointsSync { get; set; } = [Vector3.Zero, Vector3.Zero, Vector3.Zero];
	[Sync] bool LineEnabled { get; set; } = false;
	[Sync] Vector3 GrabPosSync { get; set; }
	[Sync] Rotation GrabRotSync { get; set; }
	[Sync] public GameObject HeldObject { get; set; }
	[Sync] Vector3 LocalOffset { get; set; }
	
	public SceneTraceResult Tr { get; set; }
	public Rotation InitialRotation { get; set; }
	[Property] public float InitialLinearDamping { get; set; }
	[Property] public float InitialAngularDamping { get; set; }
	
	public float GrabDistance { get; set; }
	public bool CanGrab { get; set; } = true;
	Color PlayerColor { get; set; }
	
	protected override void OnStart()
	{
		GrabBody = new PhysicsBody( Scene.PhysicsWorld );
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
		
		if ( Input.Down( "attack1" ) && !HeldObject.IsValid() ) Pickup();
		if ( !Input.Down( "attack1" ) && HeldObject.IsValid() ) Drop();
		
		if ( HeldObject.IsValid() && line.Enabled )
		{
			LinePointsSync[0] = Scene.Camera.WorldPosition + Vector3.Down * 35 + Scene.Camera.WorldRotation.Right * 20;
			var pos2 = GrabPosSync + Vector3.Direction( LinePointsSync[2], LinePointsSync[0] ) * 15;
			LinePointsSync[1] = LinePointsSync[1].LerpTo( pos2, 0.2f );
			LinePointsSync[2] = LinePointsSync[2].LerpTo( HeldObject.WorldTransform.PointToWorld( LocalOffset ), 0.2f );
		}
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

		if ( player.GroundObject != null && player.GroundObject.Network.IsOwner )
		{
			if ( player.GroundObject.Network.IsOwner ) Drop();
			player.PreventGrounding( .1f );
			PreventGrabbing( 1 );
			return;
		}
		
		GrabPosSync = Tr.StartPosition + Tr.Direction * (HeldObject.IsValid() ? GrabDistance : MinMaxDistance.Max);
		GrabRotSync = player.EyeAngles.ToRotation() * InitialRotation;
	}
	
	public void Pickup()
	{
		if ( !Tr.Hit || Tr.Body is null || Tr.Body.BodyType == PhysicsBodyType.Static || !CanGrab ) return;
		
		HeldObject = Tr.GameObject;
		HeldBody.AutoSleep = false;
		GrabDistance = (Tr.HitPosition - Scene.Camera.WorldPosition).Length;
		LocalOffset = HeldBody.Transform.PointToLocal( Tr.HitPosition );
		InitialRotation = player.EyeAngles.ToRotation().Inverse * HeldBody.Rotation;

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
		
		GrabJoint?.Remove();
		HeldObject = null;

		lastGrabbed = null;
		LastBody = null;
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

		if ( GrabBody.IsValid() && HeldObject.IsValid() && GrabDebug )
		{
			Gizmo.Draw.SolidSphere( GrabBody.Position, 2 );
			Gizmo.Draw.SolidSphere( GrabJoint.Point2.Transform.Position, 2 );
			Gizmo.Draw.Line( GrabBody.Position, GrabJoint.Point2.Transform.Position );
		}
		
		if ( IsProxy ) return;
		
		if ( Tr.Hit && HeldObject is null && Tr.Body.BodyType != PhysicsBodyType.Static )
		{
			hud.DrawCircle( center, 2, Color.Yellow );
		}
	}
}
