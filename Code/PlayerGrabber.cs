using Sandbox;
using System;
using Sandbox.Physics;
using SpringJoint = Sandbox.SpringJoint;

namespace Proximity;
public class PhysicsGrab : Component
{
	[Property] RangedFloat MinMaxDistance { get; set; } = new( 64, 128 );
	[Property] float GrabLinearDamping { get; set; } = 25;
	[Property] float GrabAngularDamping { get; set; } = 50;
	
	public SceneTraceResult Tr { get; set; }
	Sandbox.Physics.FixedJoint GrabJoint { get; set; }
	PhysicsBody HeldBody { get; set; }
	PhysicsBody GrabBody { get; set; }
	Rotation InitialRotation { get; set; }
	float InitialLinearDamping { get; set; }
	float InitialAngularDamping { get; set; }
	float GrabDistance { get; set; }
	
	protected override void OnStart()
	{
		GrabBody = new PhysicsBody( Scene.PhysicsWorld );
	}
	
	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

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
		if ( IsProxy )
			return;

		if ( !HeldBody.IsValid() ) return;
		
		GrabBody.Position = Tr.StartPosition + Tr.Direction * (HeldBody.IsValid() ? GrabDistance : MinMaxDistance.Max);
		HeldBody.Sleeping = false;
	}
	
	public void Pickup()
	{
		if ( !Tr.Hit || Tr.Body is null || Tr.Body.BodyType == PhysicsBodyType.Static ) return;
		
		HeldBody = Tr.Body;
		GrabDistance = (Tr.HitPosition - Scene.Camera.WorldPosition).Length;

		var localOffset = HeldBody.Transform.PointToLocal( Tr.HitPosition );
		
		GrabJoint?.Remove();
		GrabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( GrabBody ), new PhysicsPoint( HeldBody ) );
		GrabJoint.Point1 = new PhysicsPoint( GrabBody );
		GrabJoint.Point2 = new PhysicsPoint( HeldBody, localOffset, HeldBody.Rotation.Inverse );
		
		var maxForce = 20 * Tr.Body.Mass * Scene.PhysicsWorld.Gravity.Length;
		GrabJoint.SpringLinear = new PhysicsSpring( 5, 1 / HeldBody.Mass, maxForce );

		//InitialRotation = GrabBody.Rotation;
		InitialAngularDamping = HeldBody.AngularDamping; //Keep track of angular damping value before pickup
		InitialLinearDamping = HeldBody.LinearDamping;
		HeldBody.AngularDamping = GrabAngularDamping;
		HeldBody.LinearDamping = GrabLinearDamping;
	}

	public void Drop()
	{
		HeldBody.AngularDamping = InitialAngularDamping; //Reset angular damping
		HeldBody.LinearDamping = InitialLinearDamping;
		
		GrabJoint?.Remove();
		GrabJoint = null;
		HeldBody = null;
	}
	
	protected override void OnPreRender()
	{
		base.OnPreRender();
		
		if ( Tr.Hit && HeldBody is null && Tr.Body.BodyType != PhysicsBodyType.Static )
		{
			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.SolidSphere( Tr.HitPosition, 1 );
		}
	}
}
