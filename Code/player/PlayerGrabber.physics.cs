using Sandbox.Physics;

namespace Proximity;
public partial class PhysicsGrab
{
	GameObject lastGrabbed = null;

	PhysicsBody _heldBody;

	PhysicsBody HeldBody
	{
		get
		{
			if ( HeldObject != lastGrabbed )
			{
				GetHeldBody();
			}

			lastGrabbed = HeldObject;
			return _heldBody;
		}
	}

	public void GetHeldBody()
	{
		if ( _heldBody.IsValid() && !_heldBody.GetGameObject().Tags.Contains("held"))
		{
			_heldBody.LinearDamping = InitialLinearDamping;
			_heldBody.AngularDamping = InitialAngularDamping;
		}

		if ( !HeldObject.IsValid() )
		{
			_heldBody = null;
			return;
		}
		_heldBody = GetBody( HeldObject );
				
		InitialAngularDamping = _heldBody.AngularDamping;
		InitialLinearDamping = _heldBody.LinearDamping;
				
		_heldBody.LinearDamping = GrabLinearDamping;
		_heldBody.AngularDamping = GrabAngularDamping;
	}

	PhysicsBody GetBody( GameObject gameObject )
	{
		Rigidbody rigidbody = gameObject.Components.Get<Rigidbody>();
		return rigidbody.PhysicsBody;
	}

	PhysicsBody LastBody = null;
	
	public Sandbox.Physics.FixedJoint GrabJoint { get; set; }
	public PhysicsBody GrabBody { get; set; }
	public PhysicsSpring lastSpringAngular { get; set; }
	public bool LastRotating { get; set; }
	
	public void MoveObject()
	{
		if ( !HeldObject.IsValid() || HeldObject.IsProxy || !GrabBody.IsValid() || !HeldBody.IsValid() ) return;

		if ( HeldBody != LastBody && HeldBody != null )
		{
			GrabJoint?.Remove();
			GrabJoint = GetJoint();
		}

		// started rotating
		if ( IsRotating && !LastRotating )
		{
			lastSpringAngular = GrabJoint.SpringAngular;
			var maxForce = 100 * HeldBody.Mass * Scene.PhysicsWorld.Gravity.Length;
			GrabJoint.SpringAngular = new( 15, HeldBody.Mass / 250, maxForce );
		}

		// stopped rotating
		if ( !IsRotating && LastRotating )
		{
			GrabJoint.SpringAngular = lastSpringAngular;
		}

		LastRotating = IsRotating;
		LastBody = HeldBody;

		GrabBody.Position = GrabPosSync;
		GrabBody.Rotation = GrabRotSync;
	}
	
	public Sandbox.Physics.FixedJoint GetJoint()
	{
		var GrabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( GrabBody ), new PhysicsPoint( HeldBody ) );
		GrabJoint.Point1 = new PhysicsPoint( GrabBody );
		GrabJoint.Point2 = new PhysicsPoint( HeldBody, LocalOffset );
		
		var maxForce = 5 * HeldBody.Mass.Clamp( 0, 800 ) * Scene.PhysicsWorld.Gravity.Length;
		GrabJoint.SpringLinear = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce );
		GrabJoint.SpringAngular = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce * 5 );

		return GrabJoint;
	}
}
