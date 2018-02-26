using Microsoft.Xna.Framework.Graphics;
using StillDesign.PhysX;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;

namespace AR_Physics
{
    public abstract class VirtBody : Body
    {
        Effect depthEff;

        public VirtBody(MainWindow main)
            : base(main)
        {
            depthEff = Main.GCtrl.Content.Load<Effect>("depthmap");
        }

        protected override void Initialize(ShapeDescription shapeD, BodyDescription bodyD, PxMatrix pose)
        {
            ActorDescription actorD = new ActorDescription();
            actorD.BodyDescription = bodyD;
            actorD.Shapes.Add(shapeD);
            actorD.GlobalPose = pose;
            Actor = Main.World.Scene.CreateActor(actorD);
        }

        public void GetDepth()
        {
            for (int i = 0; i < Model.Meshes.Count; i++)
            {
                depthEff.Parameters["WVP"].SetValue(Help.Math.Convert(PxMatrix.Scaling(MeshScale) * Actor.Shapes[i].GlobalPose) * Main.View * Main.Projection);
                foreach (var meshpart in Model.Meshes[i].MeshParts)
                {
                    meshpart.Effect = depthEff;
                }
                Model.Meshes[i].Draw();
            }
        }

        public abstract void Draw();
    }
}
