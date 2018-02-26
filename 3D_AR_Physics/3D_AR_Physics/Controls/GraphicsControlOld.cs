using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Kinect;
using System.Diagnostics;
namespace AR_Physics
{
    public class GraphicControl : Game
    { 
        public GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Statistics statistics;
        Stopwatch watch = Stopwatch.StartNew();
        public Texture2D tex;
        public MainWindow Main;
        //public ColorImageFrame ColorF;
        //public DepthImageFrame DepthF;
        //public SkeletonFrame SkeletonF;
        //DynamicIndexBuffer indReal;
        //DynamicVertexBuffer vertReal;

        public GraphicControl(MainWindow main)
        {
            Main = main;
            IsFixedTimeStep = false;
            IsMouseVisible = true;

            graphics = new GraphicsDeviceManager(this);
            graphics.PreparingDeviceSettings += this.GraphicsDevicePreparingDeviceSettings;
            graphics.SynchronizeWithVerticalRetrace = true;
            Content.RootDirectory = "Content";


        }
        protected override void Initialize()
        {
            Main.Window_SizeChanged(null, null);

            //graphics.ApplyChanges();
            //indReal=new DynamicIndexBuffer(GraphicsDevice,IndexElementSize.

            //statistics = new Statistics(this);
            tex = new Texture2D(GraphicsDevice, 640, 480);
            Main.Scene.Start();
            base.Initialize();
        }

        private void GraphicsDevicePreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void UnloadContent()
        { }

        public void Update(object sender, ColorImageFrameReadyEventArgs e)
        {
            if (Main.IsReady)
            {
                Main.Elapsed = watch.Elapsed;
                watch.Restart();

                //e.OpenDepthImageFrame();
                //e.OpenSkeletonFrame();
                //using (var frame = Main.sensor.Kinect.DepthStream.OpenNextFrame(0))
                //{
                //    if (frame != null)
                //    {
                //        int a = frame.BytesPerPixel;
                //    }
                //}

                Main.Inp.Update();
                Main.Scene.Update();
                Main.Cam.Update();
                if (Main.Inp.IsPressed(Keys.D))
                {
                    if (Main.DrawMode == 0)//    switch
                    {
                        Main.DrawMode = 1;
                    }
                    else
                    {
                        Main.DrawMode = 0;
                    }
                }
                if (Main.Inp.IsDown(Keys.Escape))
                {
                    if (!Main.disposed)
                    {
                        Dispose();
                    }
                }
                statistics.Update();

                e.OpenColorImageFrame();
                //using (var frame = e.OpenColorImageFrame())
                //{
                //    if (frame != null)
                //    {
                //        byte[] pixels = new byte[frame.PixelDataLength];
                //        frame.CopyPixelDataTo(pixels);
                //        tex.SetData<byte>(pixels);
                //    }
                //}


                GraphicsDevice.Clear(Color.CornflowerBlue);
                foreach (var body in Main.Scene.Objects)
                {
                    body.Draw();
                }
                statistics.Draw();
                GraphicsDevice.Present();


                //base.Update(new GameTime());
            }
        }
        //protected override void Update(GameTime gameTime)
        //{
        //    base.Update(gameTime);
        //}

        protected override void Draw(GameTime gameTime)
        {
        //    if (Main.IsReady)
        //    {
        //        //        ColorF = Main.sensor.Kinect.ColorStream.OpenNextFrame(0);
        //        //        DepthF = Main.sensor.Kinect.DepthStream.OpenNextFrame(0);
        //        //        SkeletonF = Main.sensor.Kinect.SkeletonStream.OpenNextFrame(0);
        //        //        //    try
        //        //        //    {
        //        //        //spriteBatch.Draw(
        //        //        //int a = Main.sensor.Kinect.ColorStream.OpenNextFrame(0).BytesPerPixel;
        //        //        //int b = tex.
        //        //        //    tex.SetData<byte>(Main.sensor.Kinect.ColorStream.OpenNextFrame(10000).GetRawPixelData());

        //        //        //}
        //        //        //catch (Exception)
        //        //        //{

        //        //        //    throw;
        //        //        //}
        //        base.Draw(gameTime);
        //    }
        }
    }
}

