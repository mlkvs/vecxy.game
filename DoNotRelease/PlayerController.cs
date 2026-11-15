using System.Numerics;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace DoNotRelease;

/// <summary>Horror's existing dynamic capsule / acceleration controller, adapted to walking only.</summary>
public sealed class PlayerController(IWindow window,IInputManager input,InputMap controls,IPhysicsSystem physics,Camera camera,RigidBody body,GameSettings settings,Soundscape sound,SceneObject spawn) : AComponent
{
    private float _yaw,_pitch,_steps,_bob;
    private Vector3 _last;
    private int _stepIndex;
    public bool CanControl {get;set;}
    public bool Seated {get;set;}
    public Vector3? SeatView {get;set;}
    public (Vector3 Position,Quaternion Rotation)? OverrideView {get;set;}
    public Camera Camera=>camera;
    public RigidBody Body=>body;
    public Quaternion ViewRotation=>Quaternion.CreateFromYawPitchRoll(_yaw,_pitch,0);
    private void Look(Quaternion rotation){var f=Vector3.Transform(-Vector3.UnitZ,rotation);_yaw=MathF.Atan2(-f.X,-f.Z);_pitch=Math.Clamp(MathF.Asin(Math.Clamp(f.Y,-1,1)),-1.43f,1.43f);}
    public void Sit((Vector3 Position,Quaternion Rotation) pose){Seated=true;SeatView=pose.Position;Look(pose.Rotation);}
    public bool IsGrounded {get;private set;}
    public override void Start(){_last=Transform.WorldPosition;}
    public void Reset()
    {
        physics.Teleport(body,spawn.Transform.WorldPosition,spawn.Transform.WorldRotation);body.Velocity=body.AngularVelocity=Vector3.Zero;
        Look(spawn.Transform.WorldRotation);_steps=_bob=0;Seated=false;SeatView=null;_last=Transform.WorldPosition;
    }
    public override void Update(float dt)
    {
        if(CanControl && window.IsCursorCaptured){var m=input.MouseDelta;_yaw-=m.X*settings.Sensitivity;_pitch=Math.Clamp(_pitch-m.Y*settings.Sensitivity,-1.43f,1.43f);}
        var pos=Transform.WorldPosition;var d=pos-_last;d.Y=0;_last=pos;
        if(CanControl && !Seated && IsGrounded && d.Length()<.5f){_steps+=d.Length();_bob+=d.Length()*8;if(_steps>.72f){_steps=0;sound.Play("Step"+(_stepIndex++%4),.36f);}}
        if(Seated && CanControl && controls.GetAction<Vector2>("Move").Value.LengthSquared()>.01f){Seated=false;SeatView=null;}
    }
    public override void FixedUpdate(float dt)
    {
        var pos=Transform.WorldPosition;
        if(pos.Y<spawn.Transform.WorldPosition.Y-2){Reset();return;}
        var origin=Transform.WorldPosition+Vector3.UnitY*.30f;IsGrounded=physics.Raycast(origin,-Vector3.UnitY,.45f,SceneObject,out var hit)&&hit.Normal.Y>.35f;
        var move=CanControl && !Seated ? controls.GetAction<Vector2>("Move").Value:Vector2.Zero;if(move.LengthSquared()>1)move=Vector2.Normalize(move);
        var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw);var target=Vector3.Transform(new Vector3(move.X,0,-move.Y),rotation)*1.65f;
        var velocity=body.Velocity;var horizontal=new Vector3(velocity.X,0,velocity.Z);var delta=target-horizontal;var speed=delta.Length();var maximum=(move.LengthSquared()>0?24:30)*dt;
        horizontal=speed<=maximum||speed<.0001f?target:horizontal+delta/speed*maximum;
        if(!CanControl || Seated)horizontal=Vector3.Zero;
        body.Velocity=new Vector3(horizontal.X,IsGrounded&&velocity.Y< -1?-1:velocity.Y,horizontal.Z);body.AngularVelocity=Vector3.Zero;Transform.WorldRotation=rotation;
    }
    public override void LateUpdate(float dt)
    {
        camera.FieldOfView=settings.FieldOfView;
        if(OverrideView is {} view){camera.Transform.WorldPosition=view.Position;camera.Transform.WorldRotation=view.Rotation;return;}
        var bob=settings.HeadBobbing && CanControl && !Seated && body.Velocity.LengthSquared()>.1f ? MathF.Sin(_bob)*.007f:0;
        camera.Transform.WorldPosition=SeatView ?? Transform.WorldPosition+new Vector3(0,1.70f+bob,0);
        camera.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(_yaw,_pitch,0);
    }
}
