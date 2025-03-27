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
	
	public Sandbox.Physics.FixedJoint MoveJoint { get; set; }
	public Sandbox.Physics.FixedJoint RotateJoint { get; set; }
	public PhysicsBody MoveBody { get; set; }
	public PhysicsBody RotateBody { get; set; }
	public PhysicsSpring lastSpringAngular { get; set; }
	public bool LastRotating { get; set; }
	
	public void MoveObject()
	{
		if ( !HeldObject.IsValid() || HeldObject.IsProxy || !MoveBody.IsValid() || !HeldBody.IsValid() ) return;

		if ( HeldBody != LastBody && HeldBody != null )
		{
			MoveJoint?.Remove();
			RotateJoint?.Remove();
			MoveJoint = GetJoint(LocalOffset, MoveBody, true, false);
			RotateJoint = GetJoint(Vector3.Zero, RotateBody, false, true);

		}

		// started rotating
		if ( IsRotating && !LastRotating )
		{
			lastSpringAngular = RotateJoint.SpringAngular;
			var maxForce = 100 * HeldBody.Mass * Scene.PhysicsWorld.Gravity.Length;
			RotateJoint.SpringAngular = new( 15, HeldBody.Mass / 250, maxForce * 10);
		}

		// stopped rotating
		if ( !IsRotating && LastRotating )
		{
			RotateJoint.SpringAngular = lastSpringAngular;
		}

		LastRotating = IsRotating;
		LastBody = HeldBody;

		MoveBody.Position = GrabPosSync;
		RotateBody.Position = HeldBody.MassCenter;
		RotateBody.Rotation = GrabRotSync;
	}

	public Sandbox.Physics.FixedJoint GetJoint( Vector3 offset, PhysicsBody body, bool move = true, bool rotate = true )
	{
		var grabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( body ), new PhysicsPoint( HeldBody ) );
		grabJoint.Point1 = new PhysicsPoint( body );
		grabJoint.Point2 = new PhysicsPoint( HeldBody, HeldBody.LocalMassCenter );
		
		var maxForce = 5 * HeldBody.Mass.Clamp( 0, 800 ) * Scene.PhysicsWorld.Gravity.Length;

		var physSpring = new PhysicsSpring( 15, HeldBody.Mass / 250, maxForce * 5 );

		grabJoint.SpringLinear = move ? physSpring : new( 0, 0, 0 );
		grabJoint.SpringAngular = rotate ? physSpring : new( 0, 0, 0 );

		return grabJoint;
	}
}
