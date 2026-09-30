using UnityEngine;
namespace Perception
{
    public sealed class TrackingPositionFilter
    {
        public Vector3 Position { get; private set; }
        public float Speed => velocity.magnitude;
        public float LagMm { get; private set; }
        Vector3 previous,velocity;
        public void Reset(Vector3 point) {Position=previous=point;velocity=Vector3.zero;LagMm=0;}
        public Vector3 Update(Vector3 point,float dt)
        {
            dt=Mathf.Clamp(dt,.001f,.05f);
            velocity=Vector3.Lerp(velocity,(point-previous)/dt,1-Mathf.Exp(-14f*dt));
            float rate=14f+Mathf.Min(150f,Speed*200f);
            if(Vector3.Distance(Position,point)>.0005f)
                Position=Vector3.Lerp(Position,point,1-Mathf.Exp(-rate*dt));
            previous=point;LagMm=Vector3.Distance(Position,point)*1000;
            return Position;
        }
    }
}
