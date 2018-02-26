using Microsoft.Xna.Framework.Graphics;
using StillDesign.PhysX;
using StillDesign.PhysX.MathPrimitives;

namespace AR_Physics
{
    public abstract class Body
    {
        public Actor Actor;
        protected Model Model;
        protected MainWindow Main;
        protected Vector3 MeshScale;
        protected DebugEffect debugEff;

        public Body(MainWindow main)
        {
            Main = main;
            debugEff = main.DebugEff;
        }

        public void Dispose()
        {
            Actor.Dispose();
        }

        protected abstract void Initialize(ShapeDescription shapeD, BodyDescription bodyD, Matrix pose);

        public void DrawDebug()
        {
            for (int i = 0; i < Model.Meshes.Count; i++)
            {
                foreach (var meshPart in Model.Meshes[i].MeshParts)
                {
                    debugEff.World = Help.Math.Convert(Matrix.Scaling(MeshScale) * Actor.Shapes[i].GlobalPose);
                    debugEff.View = Main.View;
                    debugEff.Projection = Main.Projection;
                    meshPart.Effect = (Effect)debugEff;
                }

                Model.Meshes[i].Draw();
            }
        }
    }
}
