using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace AR_Physics
{
    public class Statistics
    {
        GraphicsControl GCtrl;
        SpriteBatch _spriteBatch;
        Stopwatch watch;

        public SpriteFont Font;
        public string Text;

        public Statistics(GraphicsControl instance)
        {
            GCtrl = instance;

            _spriteBatch = new SpriteBatch(GCtrl.GraphicsDevice);
            Font = GCtrl.Content.Load<SpriteFont>("SpriteFont1");
            watch = Stopwatch.StartNew();
        }

        public void Update()
        {

            if (watch.ElapsedMilliseconds >= 200)
            {
                watch.Restart();
                Text = "FPS: " + (float)(int)(100000 / GCtrl.Main.Elapsed.TotalMilliseconds) / 100 + "\n" +
                "Number of bodies: " + GCtrl.Main.World.Scene.Actors.Count + "\n" +
                "Plane: y = " + GCtrl.m + " * x - " + -GCtrl.q + "\n" +
                "h: " + GCtrl.Main.World.h + " m\n" +
                "d: " + GCtrl.Main.World.d + " m\n" +
                "Elevation: " + GCtrl.Main.sensor.Kinect.ElevationAngle + "\n"+
                "maxY: " + GCtrl.maxY;
            }
        }
    }

}