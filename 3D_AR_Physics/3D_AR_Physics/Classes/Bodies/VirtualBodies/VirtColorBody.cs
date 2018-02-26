using Microsoft.Xna.Framework.Graphics;

namespace AR_Physics
{

    public abstract class VirtColorBody : VirtBody
    {
        BasicEffect colorEff;

        public Microsoft.Xna.Framework.Color MeshColor
        {
            set
            {
                colorEff.DiffuseColor = value.ToVector3();
            }
        }

        public VirtColorBody(MainWindow main)
            : base(main)
        {
            colorEff = new BasicEffect(main.GCtrl.GraphicsDevice);
            colorEff.EnableDefaultLighting();
        }

        public override void Draw()
        {
            for (int i = 0; i < Model.Meshes.Count; i++)
            {
                colorEff.World = Help.Math.Convert(StillDesign.PhysX.MathPrimitives.Matrix.Scaling(MeshScale) * Actor.Shapes[i].GlobalPose);
                colorEff.View = Main.View;
                colorEff.Projection = Main.Projection;
                foreach (var meshPart in Model.Meshes[i].MeshParts)
                {
                    meshPart.Effect = (Effect)colorEff;
                }
                Model.Meshes[i].Draw();
            }
        }
    }
}
