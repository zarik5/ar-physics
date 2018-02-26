using Microsoft.Xna.Framework.Graphics;

namespace AR_Physics
{
    public abstract class VirtMeshBody : VirtBody
    {
        public VirtMeshBody(MainWindow main) : base(main) { }

        public override void Draw()
        {
            for (int i = 0; i < Model.Meshes.Count; i++)
            {
                foreach (var meshPart in Model.Meshes[i].MeshParts)
                {
                    ((BasicEffect)meshPart.Effect).EnableDefaultLighting();
                    ((BasicEffect)meshPart.Effect).World = Help.Math.Convert(StillDesign.PhysX.MathPrimitives.Matrix.Scaling(MeshScale) * Actor.Shapes[i].GlobalPose);
                    ((BasicEffect)meshPart.Effect).View = Main.View;
                    ((BasicEffect)meshPart.Effect).Projection = Main.Projection;
                }
                Model.Meshes[i].Draw();
            }
        }
    }
}
