using System;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Kinect;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using StillDesign.PhysX;
using Vector4 = Microsoft.Kinect.Vector4;
using Microsoft.Kinect.Toolkit.Interaction;
using JointType = Microsoft.Kinect.JointType;
using Timer = System.Windows.Forms.Timer;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;
using PxVector3 = StillDesign.PhysX.MathPrimitives.Vector3;

namespace AR_Physics
{
    public class GraphicsControl : Form
    {
        //intrinsiche del sensore
        public const int W = 640;
        public const int H = 480;
        public const int NPIX = W * H;
        public float F;   //lunghezza focale

        public int maxY;

        Stopwatch watch;                                           //cronometro, utilizzato per ottenere la differenza di tempo tra i cicli della pipeline
        //Stopwatch hoverTime = Stopwatch.StartNew();
        Statistics statistics;
        ColorImagePoint[] mappedPoints = new ColorImagePoint[NPIX];// trasforma il frame a colori per adattarsi alla depth map
        SpriteBatch sprite;                                        // oggetto utilizzato per disegnare a schermo

        uint[] virtD = new uint[NPIX];                             // depth map della scena virtuale
        public DepthImagePixel[] realD = new DepthImagePixel[NPIX];       // depth map della scena virtuale
        byte[] virtC = new byte[NPIX * 4];                         // frame a colori della scena virtuale
        byte[] realC = new byte[NPIX * 4];                         // frame a colori della scena reale
        RenderTarget2D target;                                     // superficie di disegno della GPU
        Texture2D frameScene;                                      // superficie da disegnare a schermo
        byte[] finalC = new byte[NPIX * 4];                        // frame analizzato da disegnare a schermo
        Vector3[,] dData = new Vector3[W, H];
        bool[,] isknown = new bool[W, H];

        public float m, q;                                         //parametri (relativi) dell'equazione della retta descrivente il piano
        //public ColorImageFrame ColorF;                             //frame a colori del Kinect
        public ModelDispenser Dispenser;

        public Skeleton[] Skeletons = new Skeleton[6];
        public Vector4 Accel;
        DepthImagePixel dCache;
        UserInfo[] userInfs = new UserInfo[6];
        DepthImagePoint rHand = new DepthImagePoint() { X = 50, Y = 50 }, lHand = new DepthImagePoint() { X = 50, Y = 50 };
        SkeletonPoint lSkelPose, rSkelPose;
        Vector3 lRealPose, rRealPose;
        public bool uTracked;
        public int UserN = 0;
        bool rGrab, lGrab;
        int menuGrabbed = -1;
        InteractionHandEventType gripState;
        int margHitted = -1;// 0: rosso, 1: arancione, 2: giallo, 3: verde, 4: blu, 5: viola, 6: parallelepipedo, 7: sfera, ...

        BasicEffect colorEff;
        Matrix creatingPose;
        Effect depthEff;

        void Initialize()//     inizializzazione oggetti
        {
            colorEff = new BasicEffect(GraphicsDevice);
            colorEff.EnableDefaultLighting();
            depthEff = Content.Load<Effect>("depthmap");

            target = new RenderTarget2D(GraphicsDevice, W, H, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            frameScene = new Texture2D(GraphicsDevice, W, H);
            sprite = new SpriteBatch(GraphicsDevice);
            statistics = new Statistics(this);
            Dispenser = new ModelDispenser(this);
            watch = Stopwatch.StartNew();
        }


        public string Calibrate()
        {


            //ottengo l'angolo d'inclinazione del sensore
            Accel = Main.sensor.Kinect.AccelerometerGetCurrentReading();
            m = -Accel.Z / Accel.Y;    //pendenza del piano relativa
            Main.View = Matrix.CreateRotationX((float)Math.Tan(m));



            //range: zmin 800 zmax 4000;
            Vector2[,] pMat = new Vector2[W, H];
            List<Vector2> pSet = new List<Vector2>();
            List<int> inlIndex = new List<int>();
            int[] bestInlIndex = new int[1];

            // voxelizzazione
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    DepthImagePixel p = realD[y * W + x];
                    if (p.IsKnownDepth)
                    {
                        pMat[x, y] = new Vector2(-(float)p.Depth / 1000, -(float)p.Depth * (H / 2 - y) / -F / 1000);//  trovo i punti reali relativi
                        if ((y - 4) % 8 == 0 && (x - 4) % 8 == 0)
                        {
                            pSet.Add(pMat[x, y]);
                        }
                    }
                }
            }
            Random rand = new Random();
            int nBestInl = 0;
            float localQ = 0;        //intercetta
            int nInl = 0;
            Vector2 point = new Vector2();

            //                                           RANSAC: RANdom SAmple Consensus
            //Trovo il piano orizzontale:

            int iter = 0;
            while (iter < 1000)
            {
                int c = rand.Next(pSet.Count);
                //point = new Vector2() { Y = pSet[c].Y, X = pSet[c].X };
                point = pSet[c];
                // y=mx+q
                // Y=m*depth+q
                //q=Y-m*depth
                //
                localQ = point.Y - m * point.X;
                if (localQ < 0)//  sceglie piano solo al di sotto del punto di vista
                {
                    nInl = 0;
                    inlIndex.Clear();
                    for (int i = 0; i < pSet.Count; i++)
                    {
                        //y=mx+q  ->  mx-y+q=0 <-> ax+by+c=0
                        // formula distanza punto retta:  abs(ax+by+c)/sqrt(a^2+b^2) ->abs(mx-y+q)/sqrt(m^2+1)
                        if (Math.Abs(m * pSet[i].X - pSet[i].Y + localQ) < 0.03)
                        {
                            nInl++;
                            inlIndex.Add(i);
                        }
                    }
                    if (nInl > nBestInl)
                    {
                        nBestInl = nInl;
                        if (nBestInl > 400)
                        {
                            bestInlIndex = new int[nBestInl];
                            inlIndex.CopyTo(bestInlIndex);
                            q = localQ;
                        }
                    }
                }
                iter++;
            }
            //if (nBestInl < 400)
            //{
            //    return "Individuazione del piano fallita, liberare il piano, assicurarsi che sia orizzontale e/o porlo tra 1 e 2 m di distanza dal sensore";
            //}

            //rieseguo l'algoritmo con gli inliers per trovare un valore più preciso di "q"
            List<Vector2> inliers = new List<Vector2>();
            foreach (var i in bestInlIndex)
            {
                inliers.Add(pSet[i]);
            }
            iter = 0;
            while (iter < 100)
            {
                int c = rand.Next(inliers.Count);
                point = inliers[c];//new Vector2() { Y = inliers[c].Y, X = inliers[c].X };

                localQ = point.Y - m * point.X;
                nInl = 0;
                for (int i = 0; i < pSet.Count; i++)
                {
                    if (Math.Abs(m * pSet[i].X - pSet[i].Y + localQ) < 0.01)//  +- 1cm !
                    {
                        nInl++;
                    }
                }
                if (nInl > nBestInl)
                {
                    nBestInl = nInl;
                    q = localQ;
                }
                iter++;
            }

            Main.World.h = Vector3.Transform(new Vector3(0, q, 0), Matrix.Invert(Main.View)).Y;
            //float h = (float)(q * q / m / Math.Sqrt(q * q + q * q / m / m)); //h -> poco < di q

            maxY = 0;
            for (int y = H - 1; y >= 0; y--)
            {
                for (int x = 0; x < W; x++)
                {
                    DepthImagePixel dP = realD[y * W + x];
                    if (dP.IsKnownDepth)
                    {
                        if (dP.PlayerIndex == UserN + 1 && Math.Abs(Vector3.Transform(new Vector3(0, pMat[x, y].Y, pMat[x, y].X), Matrix.Invert(Main.View)).Y - Main.World.h) > 0.04f)
                        {
                            //double fjelfjs = Vector3.Transform(new Vector3(0, pMat[x, y].Y, pMat[x, y].X), Matrix.Invert(Main.View)).Y;
                            maxY = y;
                            goto found;
                        }
                    }
                }
            }
found:
            if (maxY > 400)
            {
                return "Non appoggiarsi al piano";
            }
            //            for (int y = H - 1; y >= 0; y--)
            //            {
            //                for (int x = 0; x < W; x++)
            //                {
            //                    if (realD[y * W + x].IsKnownDepth && pMat[x, y].Y - Main.World.h < 0.03f)
            //                    {
            //                        if (Math.Abs(pMat[x, y].Y - Main.World.h) < 0.1f)
            //                        {
            //                            point = pMat[x, y];
            //                        }
            //                        else
            //                        {
            //                            maxY = y;
            //                            goto found;
            //                        }

            //                    }
            //                }
            //            }
            //found:
           // maxY -= 4; // aggiusto
            //Main.World.h = ((h == float.NaN) ? h : -q);// gestisce valore nullo di pendenza
            //Main.World.d = Vector3.Transform(new Vector3(0, point.Y, point.X), Matrix.Invert(Main.View)).Z;
            float x1 = q / ((H / 2 - maxY) / -F - m);
            float y1 = m * x1 + q;
            Main.World.d = -(float)(Math.Sqrt(x1 * x1 + Math.Pow(q - y1, 2)) - Math.Sqrt(q * q + Main.World.h * Main.World.h));
            //Main.World.d=Vector3.Transform(
            //trasformo le misure in metri
            //Main.World.h /= 1000;
            //Main.World.d /= 1000;
            Main.World.Start();
            return null;
        }

        public void Step()
        {
            if (Main.IsReady)
            {
                Main.Elapsed = watch.Elapsed;
                watch.Restart();

                //aggiorna la scena virtuale
                Main.World.BeginUpdate();

                #region Ottiene i dati dell'interazione UI e li elabora

                uTracked = false;
                for (int i = 0; i < 6; i++)
                {
                    if (Skeletons[i] != null)
                    {
                        if (Skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                        {
                            UserN = i;
                            uTracked = true;
                            break;
                        }
                    }
                }

                using (InteractionFrame interactionF = Main.Interaction.OpenNextFrame(0))
                {
                    if (interactionF != null)
                    {
                        interactionF.CopyInteractionDataTo(userInfs);
                    }
                    else
                    {
                        uTracked = false;
                    }
                }
                if (uTracked)
                {
                    if (Skeletons[UserN].Joints[JointType.HandLeft].TrackingState != JointTrackingState.NotTracked)
                    {
                        lSkelPose = Skeletons[UserN].Joints[JointType.HandLeft].Position; // misure in metri
                        lHand = Main.sensor.Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(lSkelPose, DepthImageFormat.Resolution640x480Fps30);
                        gripState = userInfs[UserN].HandPointers[0].HandEventType;
                        lGrab = (gripState == InteractionHandEventType.None) ? lGrab : (gripState == InteractionHandEventType.Grip);
                    }

                    if (Skeletons[UserN].Joints[JointType.HandRight].TrackingState != JointTrackingState.NotTracked)
                    {
                        rSkelPose = Skeletons[UserN].Joints[JointType.HandRight].Position;
                        rHand = Main.sensor.Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(rSkelPose, DepthImageFormat.Resolution640x480Fps30);
                        gripState = userInfs[UserN].HandPointers[1].HandEventType;
                        rGrab = (gripState == InteractionHandEventType.None) ? rGrab : (gripState == InteractionHandEventType.Grip);
                    }

                    if (!Main.IsPeeked)
                    {
                        if (lHand.Y <= 50 || rHand.Y <= 50)
                        {
                            Main.PeekIn();
                            Main.IsPeeked = true;
                        }
                    }
                    else
                    {
                        if (lHand.Y > 50 && rHand.Y > 50)
                        {
                            if (menuGrabbed == -1)
                            {
                                Main.PeekOut();
                            }
                            Main.IsPeeked = false;
                        }
                        if (lHand.Y <= 50 && lGrab)
                        {
                            menuGrabbed = 0;
                        }
                        if (rHand.Y <= 50 && rGrab)
                        {
                            menuGrabbed = 1;
                        }
                    }
                    if (menuGrabbed != -1)
                    {
                        if (lHand.Y > 50 && rHand.Y > 50)
                        {
                            Main.SetMenuLevel(menuGrabbed == 0 ? lHand.Y : rHand.Y);
                        }
                    }
                    if ((!lGrab && menuGrabbed == 0) || (!rGrab && menuGrabbed == 1))
                    {
                        if (Main.menu.Margin.Bottom > Main.Height * 4 / 5)
                        {
                            if (lHand.Y > 50 && rHand.Y > 50)
                            {
                                Main.MenuUp();
                                Main.IsPeeked = false;
                            }
                        }
                        else
                        {
                            Main.MenuDown();
                        }
                        menuGrabbed = -1;
                    }
                    //if (lHand.X < 5 || rHand.X < 5)
                    //{
                    //    if (lHand.Y < 160 || rHand.Y < 160)
                    //    {
                    //        Main.shapeColor = Color.Red;
                    //    }
                    //    else if (lHand.Y > 320 || rHand.Y > 320)
                    //    {
                    //        Main.shapeColor = Color.Yellow;
                    //    }
                    //    else
                    //    {
                    //        Main.shapeColor = Color.Orange;
                    //    }
                    //}
                    //if (lHand.X > 635 || rHand.X > 635)
                    //{
                    //    if (lHand.Y < 160 || rHand.Y < 160)
                    //    {
                    //        Main.shapeColor = Color.Green;
                    //    }
                    //    else if (lHand.Y > 320 || rHand.Y > 320)
                    //    {
                    //        Main.shapeColor = Color.Violet;
                    //    }
                    //    else
                    //    {
                    //        Main.shapeColor = Color.Blue;
                    //    }
                    //}
                }
                #endregion

                #region  Elaborazione depth-map
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        dCache = realD[y * W + x];
                        if (dCache.IsKnownDepth && dCache.Depth < 2500)// && dCache.PlayerIndex == 0  taglia tutti i punti più distanti di 2,5m
                        {
                            dData[x, y] = Vector3.Transform(new Vector3(-(float)dCache.Depth * (W / 2 - x) / -F / 1000,
                                -(float)dCache.Depth * (H / 2 - y) / -F / 1000, -(float)dCache.Depth / 1000), Matrix.Invert(Main.View));

                            if (y < maxY || dData[x, y].Y - Main.World.h > 0.04f)// taglia tutti i punti appartenenti al piano
                            {
                                isknown[x, y] = true;

                                if (dCache.PlayerIndex == UserN + 1)
                                {
                                    if (y > 465)
                                    {
                                        if (x < 320)
                                        {
                                            Main.ShapeType = 0;
                                        }
                                        else
                                        {
                                            Main.ShapeType = 1;
                                        }
                                        Main.CanCreate = true;
                                    }
                                    else if (x < 15)
                                    {
                                        if (y < 160)
                                        {
                                            Main.ShapeColor = Color.Red;
                                        }
                                        else if (y > 320)
                                        {
                                            Main.ShapeColor = Color.Yellow;
                                        }
                                        else
                                        {
                                            Main.ShapeColor = Color.Orange;
                                        }
                                    }
                                    else if (x > 635)
                                    {
                                        if (y < 160)
                                        {
                                            Main.ShapeColor = Color.Green;
                                        }
                                        else if (y > 320)
                                        {
                                            Main.ShapeColor = Color.Purple;
                                        }
                                        else
                                        {
                                            Main.ShapeColor = Color.Blue;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                isknown[x, y] = false;
                            }
                        }
                        else
                        {
                            isknown[x, y] = false;
                        }
                    }
                }
                Main.World.UpdateDepthSpheres(dData, isknown);
                #endregion

                //Reimpostazione del dispositivo rendering
                GraphicsDevice.Textures[0] = null;
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;

                //Crea nuovi oggetti
                if (Main.DrawMode == DrawMode.sandbox)
                {
                    if (Main.CanCreate && lGrab && rGrab)
                    {
                        colorEff.DiffuseColor = Main.ShapeColor.ToVector3();
                        Main.Creating = true;
                        Main.CanCreate = false;
                    }
                    if (Main.Creating)
                    {
                        lRealPose = Vector3.Transform(new Vector3(lSkelPose.X, lSkelPose.Y, -lSkelPose.Z), Matrix.Invert(Main.View));
                        rRealPose = Vector3.Transform(new Vector3(rSkelPose.X, rSkelPose.Y, -rSkelPose.Z), Matrix.Invert(Main.View));

                        if (!lGrab && !rGrab)
                        {
                            switch (Main.ShapeType)
                            {
                                case 0:
                                    Main.World.VirtObjs.Add(new Box(Main, PxMatrix.Translation(Help.Math.Convert(creatingPose.Translation)),
                                        new PxVector3(creatingPose.Right.X, creatingPose.Up.Y, creatingPose.Backward.Z), 0.5f, Main.ShapeColor));
                                    break;
                                case 1:
                                    Main.World.VirtObjs.Add(new Sphere(Main, PxMatrix.Translation(Help.Math.Convert(creatingPose.Translation)),
                                        creatingPose.Right.X / 2, 0.5f, Main.ShapeColor));
                                    break;
                            }
                            Main.Creating = false;
                        }

                        switch (Main.ShapeType)
                        {
                            case 0:
                                creatingPose = Matrix.CreateScale(Math.Abs(lRealPose.X - rRealPose.X), Math.Abs(lRealPose.Y - rRealPose.Y),
                                    Math.Abs(lRealPose.Z - rRealPose.Z)) * Matrix.CreateTranslation((lRealPose.X + rRealPose.X) / 2,
                                    (lRealPose.Y + rRealPose.Y) / 2, (lRealPose.Z + rRealPose.Z) / 2);
                                break;
                            case 1:
                                creatingPose = Matrix.CreateScale(Vector3.Distance(lRealPose, rRealPose)) * Matrix.CreateTranslation((lRealPose.X + rRealPose.X) / 2,
                                    (lRealPose.Y + rRealPose.Y) / 2, (lRealPose.Z + rRealPose.Z) / 2);
                                break;
                        }

                    }

                }

                //Ottiene la depth-map virtuale
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(Color.White);
                foreach (var body in Main.World.VirtObjs)
                {
                    body.GetDepth();
                }
                if (Main.Creating)
                {
                    DepthCreatingShape();
                }
                GraphicsDevice.SetRenderTarget(null);
                target.GetData(virtD);


                if (Main.DrawMode == DrawMode.sandbox || Main.DrawMode == DrawMode.minigame)
                {
                    #region Visualizza sandbox e minigame


                    //Ottiene il frame a colori virtuale
                    GraphicsDevice.SetRenderTarget(target);
                    GraphicsDevice.Clear(Color.White);
                    if (Main.Creating)
                    {
                        DrawCreatingShape();
                    }
                    foreach (var body in Main.World.VirtObjs)
                    {
                        body.Draw();
                    }
                    GraphicsDevice.SetRenderTarget(null);
                    target.GetData(virtC);

                    //Ottiene il frame a colori reale e lo elabora
                    using (ColorImageFrame colorF = Main.sensor.Kinect.ColorStream.OpenNextFrame(0))
                    {
                        if (colorF != null)
                        {
                            colorF.CopyPixelDataTo(realC);
                        }
                    }
                    Main.sensor.Kinect.CoordinateMapper.MapDepthFrameToColorFrame(DepthImageFormat.Resolution640x480Fps30,
                        realD, ColorImageFormat.RgbResolution640x480Fps30, mappedPoints);

                    //Confronta le depth map e assegna ad una texture i pixel dei punti più vicini al pdv
                    for (int i = 0; i < realD.Length; i++)
                    {
                        if (virtD[i] < realD[i].Depth || (!realD[i].IsKnownDepth && virtD[i] != uint.MaxValue))
                        {
                            finalC[i * 4] = virtC[i * 4];
                            finalC[i * 4 + 1] = virtC[i * 4 + 1];
                            finalC[i * 4 + 2] = virtC[i * 4 + 2];
                            finalC[i * 4 + 3] = 255;
                        }
                        else
                        {
                            int index = mappedPoints[i].Y * W + mappedPoints[i].X;
                            finalC[i * 4] = realC[index * 4 + 2];
                            finalC[i * 4 + 1] = realC[index * 4 + 1];
                            finalC[i * 4 + 2] = realC[index * 4];
                            finalC[i * 4 + 3] = 255;
                        }
                    }
                    frameScene.SetData(finalC);
                    #endregion
                }
                else
                {

                    if (Main.DrawMode == DrawMode.debugPhysics)
                    {
                        #region Visualizza fisica virtuale

                        //Ottiene il frame a colori virtuale
                        GraphicsDevice.SetRenderTarget(target);
                        GraphicsDevice.Clear(Color.White);
                        foreach (var body in Main.World.VirtObjs)
                        {
                            body.DrawDebug();
                        }
                        //foreach (var body in Main.World.RealObjs)
                        //{
                        //    body.DrawDebug();
                        //}
                        foreach (var sphere in Main.World.DepthSph)
                        {
                            sphere.DrawDebug();
                        }
                        GraphicsDevice.SetRenderTarget(null);
                        target.GetData(virtC);
                        frameScene.SetData(virtC);
                        #endregion
                    }
                    else if (Main.DrawMode == DrawMode.debugDepth)
                    {
                        #region Visualizza depth map

                        for (int i = 0; i < realD.Length; i++)
                        {
                            if (!realD[i].IsKnownDepth || virtD[i] < realD[i].Depth)
                            {
                                byte value = (byte)(255 * (2500 - MathHelper.Clamp(virtD[i], 800, 2500)) / 1700); // depth : max depth = x : 255
                                finalC[i * 4] = value;
                                finalC[i * 4 + 1] = value;
                                finalC[i * 4 + 2] = value;
                                finalC[i * 4 + 3] = 255;
                            }
                            else
                            {
                                byte value = (byte)(255 * (2500 - MathHelper.Clamp(realD[i].Depth, 800, 2500)) / 1700);
                                finalC[i * 4] = value;
                                finalC[i * 4 + 1] = value;
                                finalC[i * 4 + 2] = value;
                                finalC[i * 4 + 3] = 255;
                            }
                        }
                        frameScene.SetData(finalC);
                        #endregion
                    }
                    else
                    {
                        #region Visualizza elementi riconosciuti
                        for (int i = 0; i < realD.Length; i++)
                        {
                            if (realD[i].PlayerIndex != 0)
                            {
                                finalC[i * 4] = 0;
                                finalC[i * 4 + 1] = 0;
                                finalC[i * 4 + 2] = 255;     //utente: blu
                                finalC[i * 4 + 3] = 255;
                            }
                            else if (isknown[i % W, i / W])
                            {
                                finalC[i * 4] = 0;
                                finalC[i * 4 + 1] = 255;     //oggetti: verde
                                finalC[i * 4 + 2] = 0;
                                finalC[i * 4 + 3] = 255;
                            }
                            else if (i / W > maxY && Math.Abs(dData[i % W, i / W].Y - Main.World.h) < 0.04)// Math.Abs(m * realD[i].Depth - realD[i].Depth * ((H / 2 - (float)i / W)) / -F + q) < 20)
                            {
                                finalC[i * 4] = 255;
                                finalC[i * 4 + 1] = 0;
                                finalC[i * 4 + 2] = 0;       //piano: rosso
                                finalC[i * 4 + 3] = 255;
                            }
                            else
                            {
                                finalC[i * 4] = 255;
                                finalC[i * 4 + 1] = 255;     //vuoto: bianco
                                finalC[i * 4 + 2] = 255;
                                finalC[i * 4 + 3] = 255;
                            }
                            //if (lGrab && Math.Sqrt(Math.Pow(i % W - lHand.X, 2) + Math.Pow(i / W - lHand.Y, 2)) < 10)
                            //{
                            //    finalC[i * 4] = 255;
                            //    finalC[i * 4 + 1] = 255;
                            //    finalC[i * 4 + 2] = 0;
                            //    finalC[i * 4 + 3] = 255;
                            //}

                            //if (rGrab && Math.Sqrt(Math.Pow(i % W - rHand.X, 2) + Math.Pow(i / W - rHand.Y, 2)) < 10)
                            //{
                            //    finalC[i * 4] = 255;
                            //    finalC[i * 4 + 1] = 255;
                            //    finalC[i * 4 + 2] = 0;
                            //    finalC[i * 4 + 3] = 255;
                            //}
                        }
                        frameScene.SetData(finalC);
                        #endregion
                    }
                }

                //Disegna la texture
                sprite.Begin();
                sprite.Draw(frameScene, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), Color.White);
                if (Main.showStats)
                {
                    statistics.Update();
                    sprite.DrawString(statistics.Font, statistics.Text, new Vector2(5, 5), Color.Red);
                }
                sprite.End();
                GraphicsDevice.Present(new Microsoft.Xna.Framework.Rectangle(0, 0, ClientSize.Width, ClientSize.Height), null, this.Handle);
                Main.World.EndUpdate();

                //permette l'aggiornamento dei frame
                Main.GotDepth = false;
                Main.GotSkeleton = false;
            }
        }

        void DrawCreatingShape()
        {
            for (int i = 0; i < Dispenser.Models[Main.ShapeType].Meshes.Count; i++)
            {
                colorEff.World = creatingPose;
                colorEff.View = Main.View;
                colorEff.Projection = Main.Projection;
                foreach (var meshPart in Dispenser.Models[Main.ShapeType].Meshes[i].MeshParts)
                {
                    meshPart.Effect = (Effect)colorEff;
                }
                Dispenser.Models[Main.ShapeType].Meshes[i].Draw();
            }
        }

        void DepthCreatingShape()
        {
            for (int i = 0; i < Dispenser.Models[Main.ShapeType].Meshes.Count; i++)
            {
                depthEff.Parameters["WVP"].SetValue(creatingPose * Main.View * Main.Projection);

                foreach (var meshpart in Dispenser.Models[Main.ShapeType].Meshes[i].MeshParts)
                {
                    meshpart.Effect = depthEff;
                }
                Dispenser.Models[Main.ShapeType].Meshes[i].Draw();
            }
        }

        #region Framework

        public MainWindow Main;
        public ContentManager Content;
        ServiceContainer Services = new ServiceContainer();

        public GraphicsControl(MainWindow main)
        {
            Main = main;
            FormBorderStyle = FormBorderStyle.None;
            //TopMost = true;

            graphicsDeviceService = GraphicsDeviceService.AddRef(Handle, ClientSize.Width, ClientSize.Height);
            Services.AddService<IGraphicsDeviceService>(graphicsDeviceService);
            Content = new ContentManager(Services, "Content");

            Initialize();
        }

        public GraphicsDevice GraphicsDevice
        {
            get
            {
                return graphicsDeviceService.GraphicsDevice;
            }
        }
        GraphicsDeviceService graphicsDeviceService;

        protected override void Dispose(bool disposing)
        {
            if (graphicsDeviceService != null)
            {
                graphicsDeviceService.Release(disposing);
                graphicsDeviceService = null;
            }
            if (disposing)
            {
                Content.Unload();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Update();
        }

        new void Update()
        {
            bool canDraw = BeginDraw();
            if (canDraw)
            {
                Step();
            }
        }

        private bool BeginDraw()
        {
            if (graphicsDeviceService == null)
            {
                return false;
            }
            bool deviceResetError = HandleDeviceReset();
            if (deviceResetError)
            {
                return false;
            }
            Viewport viewport = new Viewport();
            viewport.X = 0;
            viewport.Y = 0;
            viewport.Width = ClientSize.Width;
            viewport.Height = ClientSize.Height;
            viewport.MinDepth = 0;
            viewport.MaxDepth = 1;
            GraphicsDevice.Viewport = viewport;

            //0,789582239399523 webcam fov
            return true;
        }

        private bool HandleDeviceReset()
        {
            bool deviceNeedsReset = false;
            switch (GraphicsDevice.GraphicsDeviceStatus)
            {
                case GraphicsDeviceStatus.Lost:
                    return true;
                case GraphicsDeviceStatus.NotReset:
                    deviceNeedsReset = true;
                    break;
                default:
                    PresentationParameters pp = GraphicsDevice.PresentationParameters;
                    deviceNeedsReset = (ClientSize.Width > pp.BackBufferWidth) || (ClientSize.Height > pp.BackBufferHeight);
                    break;
            }
            if (deviceNeedsReset)
            {
                try
                {
                    graphicsDeviceService.ResetDevice(ClientSize.Width, ClientSize.Height);
                }
                catch (Exception)
                {
                    return true;
                }
            }
            return false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }
        #endregion
    }

    #region Framework

    class GraphicsDeviceService : IGraphicsDeviceService
    {

        static GraphicsDeviceService singletonInstance;

        static int referenceCount;

        private GraphicsDeviceService(IntPtr windowHandle, int width, int height)
        {
            parameters = new PresentationParameters();
            parameters.BackBufferWidth = Math.Max(width, 1);
            parameters.BackBufferHeight = Math.Max(height, 1);
            parameters.BackBufferFormat = SurfaceFormat.Color;
            parameters.DepthStencilFormat = DepthFormat.Depth24;
            parameters.DeviceWindowHandle = windowHandle;
            parameters.PresentationInterval = PresentInterval.One;
            parameters.IsFullScreen = false;
            parameters.MultiSampleCount = 1;
            m_graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, parameters);
        }

        public static GraphicsDeviceService AddRef(IntPtr windowHandle, int width, int height)
        {
            if (Interlocked.Increment(ref referenceCount) == 1)
            {
                singletonInstance = new GraphicsDeviceService(windowHandle, width, height);
            }
            return singletonInstance;
        }

        public void Release(bool disposing)
        {
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                if (disposing)
                {
                    if (DeviceDisposing != null)
                    {
                        DeviceDisposing(this, EventArgs.Empty);
                    }
                    m_graphicsDevice.Dispose();
                }
                m_graphicsDevice = null;
            }
        }

        public void ResetDevice(int width, int height)
        {
            if (DeviceResetting != null)
            {
                DeviceResetting(this, EventArgs.Empty);
            }
            parameters.BackBufferWidth = Math.Max(parameters.BackBufferWidth, width);
            parameters.BackBufferHeight = Math.Max(parameters.BackBufferHeight, height);
            m_graphicsDevice.Reset(parameters);
            if (DeviceReset != null)
            {
                DeviceReset(this, EventArgs.Empty);
            }
        }

        public GraphicsDevice GraphicsDevice
        {
            get { return m_graphicsDevice; }
        }

        private GraphicsDevice m_graphicsDevice;

        private PresentationParameters parameters;
        public event EventHandler<EventArgs> DeviceDisposing;
        public event EventHandler<EventArgs> DeviceReset;
        public event EventHandler<EventArgs> DeviceResetting;
        public event EventHandler<EventArgs> DeviceCreated;

    }




    public class ServiceContainer : IServiceProvider
    {

        private Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void AddService<T>(T service)
        {
            services.Add(typeof(T), service);
        }

        public object GetService(Type serviceType)
        {
            object service = new object();
            services.TryGetValue(serviceType, out service);
            return service;
        }
    }

    #endregion

}