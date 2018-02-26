using StillDesign.PhysX;

namespace AR_Physics
{
    public abstract class RealBody : Body
    {
        public RealBody(MainWindow main) : base(main) { }

        protected override void Initialize(ShapeDescription shapeD, BodyDescription bodyD, StillDesign.PhysX.MathPrimitives.Matrix pose)
        {
            if (bodyD != null)
            {
                bodyD.BodyFlags |= BodyFlag.Kinematic;
                bodyD.BodyFlags |= BodyFlag.DisableGravity;
            }
            ActorDescription actorD = new ActorDescription();
            actorD.BodyDescription = bodyD;
            actorD.Shapes.Add(shapeD);
            actorD.GlobalPose = pose;
            Actor = Main.World.Scene.CreateActor(actorD);
        }
    }
}
