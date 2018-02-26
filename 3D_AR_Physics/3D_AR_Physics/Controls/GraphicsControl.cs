using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;
using Vector4 = Microsoft.Kinect.Vector4;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;
using Button = System.Windows.Controls.Button;

namespace AR_Physics
{
	public class GraphicsControl : Form
	{
		public const int W = 640;
		public const int H = 480;
		public const int NPIX = W * H;
		public float F;   //lunghezza focale
		public int maxY;
		public float m, q;                                         //parametri (relativi) dell'equazione della retta descrivente il piano

		//variabili di comunicazione con MainWindow
		public DepthImagePixel[] realD = new DepthImagePixel[NPIX];// depth map della scena virtuale
		public Skeleton[] Skeletons = new Skeleton[6];
		public Vector4 Accel;
		public bool uTracked;
		public int UserN = 0;

		public ModelDispenser Dispenser;

		Stopwatch watch;                                           //cronometro, utilizzato per ottenere la differenza di tempo tra i cicli della pipeline
		Statistics statistics;
		SpriteBatch sprite;                                        // oggetto utilizzato per disegnare a schermo

		// matrici
		ColorImagePoint[] mappedPoints = new ColorImagePoint[NPIX];// trasforma il frame a colori per adattarsi alla depth map
		uint[] virtD = new uint[NPIX];                             // depth map della scena virtuale
		byte[] virtC = new byte[NPIX * 4];                         // frame a colori della scena virtuale
		byte[] realC = new byte[NPIX * 4];                         // frame a colori della scena reale
		RenderTarget2D target;                                     // superficie di disegno della GPU
		Texture2D frameScene;                                      // superficie da disegnare a schermo
		byte[] finalC = new byte[NPIX * 4];                        // frame analizzato da disegnare a schermo
		Vector3[,] dData = new Vector3[W, H];
		bool[,] isknown = new bool[W, H];


		// variabili per l'analisi della depth-map
		DepthImagePixel dCache;
		UserInfo[] userInfs = new UserInfo[6];
		DepthImagePoint rHand = new DepthImagePoint() { X = 50, Y = 50 }, lHand = new DepthImagePoint() { X = 50, Y = 50 };
		SkeletonPoint lSkelPose, rSkelPose;
		Vector3 lRealPose, rRealPose;
		bool rGrab, lGrab;
		int menuGrabbed = -1;
		InteractionHandEventType gripState;
		int margHitted = -1;// 0: rosso, 1: arancione, 2: giallo, 3: verde, 4: blu, 5: viola, 6: parallelepipedo, 7: sfera, ...
		int margHittedOld;
		BasicEffect colorEff;
		Matrix creatingPose;
		Effect depthEff;
		Effect[] footballEffect;
		int maxD = 2500;


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
			footballEffect = new Effect[2];
			for (int i = 0; i < Dispenser.Models[2].Meshes[0].Effects.Count; i++)
			{
				footballEffect[i] = Dispenser.Models[2].Meshes[0].Effects[i];
				((BasicEffect)footballEffect[i]).EnableDefaultLighting();
			}
			watch = Stopwatch.StartNew();
		}


		//        public string Calibrate()
		//        {


		//            //ottengo l'angolo d'inclinazione del sensore
		//            Accel = Main.sensor.Kinect.AccelerometerGetCurrentReading();
		//            m = -Accel.Z / Accel.Y;    //pendenza del piano relativa
		//            Main.View = Matrix.CreateRotationX((float)Math.Tan(m));



		//            //range: zmin 800 zmax 4000;
		//            Vector2[,] pMat = new Vector2[W, H];
		//            List<Vector2> pSet = new List<Vector2>();
		//            List<int> inlIndex = new List<int>();
		//            int[] bestInlIndex = new int[1];

		//            // voxelizzazione
		//            for (int y = 0; y < H; y++)
		//            {
		//                for (int x = 0; x < W; x++)
		//                {
		//                    DepthImagePixel p = realD[y * W + x];
		//                    if (p.IsKnownDepth)
		//                    {
		//                        pMat[x, y] = new Vector2(-(float)p.Depth / 1000, -(float)p.Depth * (H / 2 - y) / -F / 1000);//  trovo i punti reali relativi
		//                        if ((y - 4) % 8 == 0 && (x - 4) % 8 == 0)
		//                        {
		//                            pSet.Add(pMat[x, y]);
		//                        }
		//                    }
		//                }
		//            }
		//            Random rand = new Random();
		//            int nBestInl = 0;
		//            float localQ = 0;        //intercetta
		//            int nInl = 0;
		//            Vector2 point = new Vector2();

		//            //                                           RANSAC: RANdom SAmple Consensus
		//            //Trovo il piano orizzontale:

		//            int iter = 0;
		//            while (iter < 1000)
		//            {
		//                int c = rand.Next(pSet.Count);
		//                point = new Vector2() { Y = -pSet[c].Y * 1000, X = -pSet[c].X * 1000 };
		//                //point = pSet[c];
		//                // y=mx+q
		//                // Y=m*depth+q
		//                //q=Y-m*depth
		//                //
		//                localQ = point.Y - m * point.X;
		//                if (localQ > 0)//  sceglie piano solo al di sotto del punto di vista
		//                {
		//                    nInl = 0;
		//                    inlIndex.Clear();
		//                    for (int i = 0; i < pSet.Count; i++)
		//                    {
		//                        //y=mx+q  ->  mx-y+q=0 <-> ax+by+c=0
		//                        // formula distanza punto retta:  abs(ax+by+c)/sqrt(a^2+b^2) ->abs(mx-y+q)/sqrt(m^2+1)
		//                        if (Math.Abs(m * -pSet[i].X * 1000 - -pSet[i].Y * 1000 + localQ) < 30)
		//                        {
		//                            nInl++;
		//                            inlIndex.Add(i);
		//                        }
		//                    }
		//                    if (nInl > nBestInl)
		//                    {
		//                        nBestInl = nInl;
		//                        if (nBestInl > 400)
		//                        {
		//                            bestInlIndex = new int[nBestInl];
		//                            inlIndex.CopyTo(bestInlIndex);
		//                            q = localQ;
		//                        }
		//                    }
		//                }
		//                iter++;
		//            }
		//            if (nBestInl < 400)
		//            {
		//                return "Individuazione del piano fallita, liberare il piano, assicurarsi che sia orizzontale e/o porlo tra 1 e 2 m di distanza dal sensore";
		//            }

		//            //rieseguo l'algoritmo con gli inliers per trovare un valore più preciso di "q"
		//            List<Vector2> inliers = new List<Vector2>();
		//            foreach (var i in bestInlIndex)
		//            {
		//                inliers.Add(pSet[i]);
		//            }
		//            iter = 0;
		//            while (iter < 100)
		//            {
		//                int c = rand.Next(inliers.Count);
		//                point = new Vector2() { Y = -inliers[c].Y * 1000, X = -inliers[c].X * 1000 };

		//                localQ = point.Y - m * point.X;
		//                nInl = 0;
		//                for (int i = 0; i < pSet.Count; i++)
		//                {
		//                    if (Math.Abs(m * -pSet[i].X*1000 - -pSet[i].Y*1000 + localQ) < 10)//  +- 1cm !
		//                    {
		//                        nInl++;
		//                    }
		//                }
		//                if (nInl > nBestInl)
		//                {
		//                    nBestInl = nInl;
		//                    q = localQ;
		//                }
		//                iter++;
		//            }

		//            Main.World.h = Vector3.Transform(new Vector3(0, q, 0), Matrix.Invert(Main.View)).Y;
		//            //float h = (float)(q * q / m / Math.Sqrt(q * q + q * q / m / m)); //h -> poco < di q

		//            maxY = 0;
		//            for (int y = H - 1; y >= 0; y--)
		//            {
		//                for (int x = 0; x < W; x++)
		//                {
		//                    DepthImagePixel dP = realD[y * W + x];
		//                    if (dP.IsKnownDepth)
		//                    {
		//                        if (dP.PlayerIndex == UserN + 1 && Math.Abs(Vector3.Transform(new Vector3(0, pMat[x, y].Y, pMat[x, y].X), Matrix.Invert(Main.View)).Y - Main.World.h) > 0.04f)
		//                        {
		//                            //double fjelfjs = Vector3.Transform(new Vector3(0, pMat[x, y].Y, pMat[x, y].X), Matrix.Invert(Main.View)).Y;
		//                            maxY = y;
		//                            goto found;
		//                        }
		//                    }
		//                }
		//            }
		//found:
		//            if (maxY > 400)
		//            {
		//                return "Non appoggiarsi al piano";
		//            }
		//            //            for (int y = H - 1; y >= 0; y--)
		//            //            {
		//            //                for (int x = 0; x < W; x++)
		//            //                {
		//            //                    if (realD[y * W + x].IsKnownDepth && pMat[x, y].Y - Main.World.h < 0.03f)
		//            //                    {
		//            //                        if (Math.Abs(pMat[x, y].Y - Main.World.h) < 0.1f)
		//            //                        {
		//            //                            point = pMat[x, y];
		//            //                        }
		//            //                        else
		//            //                        {
		//            //                            maxY = y;
		//            //                            goto found;
		//            //                        }

		//            //                    }
		//            //                }
		//            //            }
		//            //found:
		//            // maxY -= 4; // aggiusto
		//            //Main.World.h = ((h == float.NaN) ? h : -q);// gestisce valore nullo di pendenza
		//            //Main.World.d = Vector3.Transform(new Vector3(0, point.Y, point.X), Matrix.Invert(Main.View)).Z;
		//            float x1 = q / ((H / 2 - maxY) / -F - m);
		//            float y1 = m * x1 + q;
		//            Main.World.d = -(float)(Math.Sqrt(x1 * x1 + Math.Pow(q - y1, 2)) - Math.Sqrt(q * q + Main.World.h * Main.World.h));
		//            //Main.World.d=Vector3.Transform(
		//            //trasformo le misure in metri
		//            //Main.World.h /= 1000;
		//            //Main.World.d /= 1000;
		//            Main.World.Start();
		//            return null;
		//        }
		public string Calibrate()
		{
			if (Main.floorMode)
			{
				maxD = 4000;
			}
			else
			{
				maxD = 2500;
			}

			//ottengo l'angolo d'inclinazione del sensore
			Accel = Main.sensor.Kinect.AccelerometerGetCurrentReading();
			m = Accel.Z / Accel.Y;    //pendenza del piano relativa
			Main.View = Matrix.CreateRotationX(-(float)Math.Tan(m));



			//range: zmin 800 zmax 4000;
			List<Vector2> pSet = new List<Vector2>();
			List<int> inlIndex = new List<int>();
			int[] bestInlIndex = new int[1];
			//maxY = 0;

			// voxelizzazione
			for (int y = 4; y < H; y += 8)
			{
				for (int x = 4; x < W; x += 8)
				{
					DepthImagePixel p = realD[y * W + x];
					if (p.IsKnownDepth && (Main.floorMode || (p.Depth < 2000 && !Main.floorMode)))
					{
						//if (p.PlayerIndex == UserN + 1 && maxY < y)
						//{
						//    maxY = y;
						//}
						pSet.Add(new Vector2(p.Depth, (float)p.Depth * (y - H / 2) / F));//  trovo i punti reali // X = p.Depth * (x - W / 2) / xf,
					}
				}
			}
			Random rand = new Random();
			int nBestInl = 0;
			float localQ = 0;     //intercetta
			int nInl = 0;
			Vector2 point;


			//                                           RANSAC: RANdom SAmple Consensus
			//Trovo il piano orizzontale:
			int iter = 0;
			while (iter < 1000)
			{
				int c = rand.Next(pSet.Count);
				point = new Vector2() { Y = pSet[c].Y, X = pSet[c].X };
				// y=mx+q
				// Y=m*depth+q
				//q=Y-m*depth
				//
				localQ = point.Y - m * point.X;
				if (localQ > 0)//  sceglie piano solo al di sotto del punto di vista
				{
					nInl = 0;
					inlIndex.Clear();
					for (int i = 0; i < pSet.Count; i++)
					{
						//y=mx+q  ->  mx-y+q=0 <-> ax+by+c=0
						// formula distanza punto retta:  abs(ax+by+c)/sqrt(a^2+b^2) ->abs(mx-y+q)/sqrt(m^2+1)
						if (Math.Abs(m * pSet[i].X - pSet[i].Y + localQ) < 30)
						{
							nInl++;
							inlIndex.Add(i);
						}
					}
					if (nInl > nBestInl)
					{
						nBestInl = nInl;
						//if (nBestInl > 400)
						//{
						bestInlIndex = new int[nBestInl];
						inlIndex.CopyTo(bestInlIndex);
						q = localQ;
						//}
					}
				}
				iter++;
			}
			//if (nBestInl <= 400)
			//{
			//    return "Individuazione del piano fallita, liberare il piano, assicurarsi che sia orizzontale e/o porlo tra 1 e 2 m di distanza";
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
				point = new Vector2() { Y = inliers[c].Y, X = inliers[c].X };

				localQ = point.Y - m * point.X;
				nInl = 0;
				for (int i = 0; i < pSet.Count; i++)
				{
					if (Math.Abs(m * pSet[i].X - pSet[i].Y + localQ) < 10)//  +- 1cm !
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
			maxY = 375;
			if (Main.floorMode)
			{
				maxY = 480;
			}
			//float h = q * q / m / (float)Math.Sqrt(q * q + q * q / m / m) / 1000; //h -> poco < di q      in metri
			//Main.World.h = ((h == float.NaN) ? h : -q / 1000);// gestisce valore nullo di pendenza
			Main.World.h = Vector3.Transform(new Vector3(0, -q, 0), Matrix.Invert(Main.View)).Y;
			float x1 = q / ((maxY - H / 2) / F - m);
			float y1 = m * x1 + q;
			Main.World.d = (float)(Math.Sqrt(x1 * x1 + Math.Pow(q - y1, 2)) - Math.Sqrt(q * q - Main.World.h * Main.World.h));

			Main.World.h /= 1000;
			Main.World.d /= -1000;

			//            maxY = H;
			//            for (int y = H - 1; y >= 0; y--)
			//            {
			//                for (int x = 0; x < W; x++)
			//                {
			//                    Vector2 p = new Vector2(realD[y * W + x].Depth, -(float)realD[y * W + x].Depth * (y - H / 2) / F);
			//                    if (realD[y * W + x].IsKnownDepth && realD[y * W + x].PlayerIndex == UserN + 1 && m * p.X - p.Y + q > 300)
			//                    {
			//                        maxY = y;
			//                        goto found;
			//                    }
			//                }
			//            }
			//found:
			//if (maxY > 400 || maxY < 240)
			//{
			//    return "Posizionarsi centralmente e ad almeno mezzo metro di distanza dal bordo";
			//}
			if (Main.floorMode)
			{
				Main.World.d = -1000;
			}
			Main.World.Start();
			return null;
		}

		public void Step()
		{
			if (Main.IsRunning)
			{
				Main.Elapsed = watch.Elapsed;
				watch.Restart();

				//aggiorna la scena virtuale
				Main.World.BeginUpdate();

				#region Ottiene i dati dell'interazione UI e li elabora

				uTracked = false;
				float nearestP = 3000;
				for (int i = 0; i < 6; i++)
				{
					if (Skeletons[i] != null)
					{
						if (Skeletons[i].TrackingState == SkeletonTrackingState.Tracked && Skeletons[i].Position.Z < nearestP)
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
				}
				#endregion

				#region  Elaborazione depth-map
				margHitted = -1;
				for (int y = 0; y < H; y++)
				{
					for (int x = 0; x < W; x++)
					{
						dCache = realD[y * W + x];
						if (dCache.IsKnownDepth && dCache.Depth < maxD)// && dCache.PlayerIndex == 0  taglia tutti i punti più distanti
						{
							dData[x, y] = Vector3.Transform(new Vector3(-(float)dCache.Depth * (x - W / 2) / -F / 1000,
								-(float)dCache.Depth * (H / 2 - y) / -F / 1000, -(float)dCache.Depth / 1000), Matrix.Invert(Main.View));

							if ((y < maxY && !Main.floorMode) || dData[x, y].Y - Main.World.h > 0.04f)// taglia tutti i punti appartenenti al piano
							{
								isknown[x, y] = true;

								if (dCache.PlayerIndex == UserN + 1)
								{
									if (!Main.Creating && Main.Mode == DrawMode.Sandbox)
									{

										if (x < 15)
										{
											if (y > 320)
											{
												margHitted = 2;
											}
											else if (y < 160)
											{
												margHitted = 0;
											}
											else
											{
												margHitted = 1;
											}
										}
										else if (x > 625)
										{
											if (y > 320)
											{
												margHitted = 5;
											}
											else if (y < 160)
											{
												margHitted = 3;
											}
											else
											{
												margHitted = 4;
											}
										}
										else if (y > 465)
										{
											if (x < 320)
											{
												margHitted = 6;
											}
											else
											{
												margHitted = 7;
											}
										}
									}
									else
									{
										if (x < 15)
										{
											margHitted = 0;
										}
										else if (x > 625)
										{
											margHitted = 1;
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

				if (Main.Mode == DrawMode.Sandbox)
				{
					if (margHitted != margHittedOld)
					{
						Main.gameView.LeaveButtons();
						switch (margHitted)
						{
							case -1:
								break;
							case 0:
								Main.gameView.red.RaiseEvent(Main.MouseEvents);
								break;
							case 1:
								Main.gameView.orange.RaiseEvent(Main.MouseEvents);
								break;
							case 2:
								Main.gameView.yellow.RaiseEvent(Main.MouseEvents);
								break;
							case 3:
								Main.gameView.green.RaiseEvent(Main.MouseEvents);
								break;
							case 4:
								Main.gameView.blue.RaiseEvent(Main.MouseEvents);
								break;
							case 5:
								Main.gameView.purple.RaiseEvent(Main.MouseEvents);
								break;
							case 6:
								Main.gameView.box.RaiseEvent(Main.MouseEvents);
								break;
							case 7:
								Main.gameView.sphere.RaiseEvent(Main.MouseEvents);
								break;
						}
						margHittedOld = margHitted;
					}
				}
				else
				{
					if (margHitted != margHittedOld)
					{
						Main.howMadeView.LeaveButtons();
						if (margHitted == 0)
						{
							Main.howMadeView.leftPage.RaiseEvent(Main.MouseEvents);
						}
						else if (margHitted == 1)
						{
							Main.howMadeView.rightPage.RaiseEvent(Main.MouseEvents);
						}
						Main.DebugChanged();
						margHittedOld = margHitted;
					}

					if (Main.howMadeView.PageTurn == 0)
					{
						Main.Mode = (Main.Mode != DrawMode.DebugDepth) ?
							(DrawMode)((int)Main.Mode - 1) : DrawMode.DebugPhysics;
						Main.howMadeView.PageTurn = -1;
					}
					else if (Main.howMadeView.PageTurn == 1)
					{
						Main.Mode = (Main.Mode != DrawMode.DebugPhysics) ?
							(DrawMode)((int)Main.Mode + 1) : DrawMode.DebugDepth;
						Main.howMadeView.PageTurn = -1;
					}
				}
				#endregion

				//Reimpostazione del dispositivo rendering
				GraphicsDevice.Textures[0] = null;
				GraphicsDevice.BlendState = BlendState.Opaque;
				GraphicsDevice.DepthStencilState = DepthStencilState.Default;

				#region Crea nuovi oggetti
				if (Main.Mode == DrawMode.Sandbox)
				{
					if (Main.gameView.CanCreate && lGrab && rGrab)
					{
						if (Main.gameView.ShapeType == 1)
						{
							////if (new Random().Next(1, 11) == 10)// in 1/20 dei casi esce un pallone da calcio!
							//{
							//    Main.gameView.ShapeType = 2;
							//}
						}
						colorEff.DiffuseColor = Main.gameView.ShapeColor.ToVector3();
						Main.Creating = true;
						Main.gameView.CanCreate = false;
					}
					if (Main.Creating)
					{
						lRealPose = Vector3.Transform(new Vector3(lSkelPose.X, lSkelPose.Y, -lSkelPose.Z), Matrix.Invert(Main.View));
						rRealPose = Vector3.Transform(new Vector3(rSkelPose.X, rSkelPose.Y, -rSkelPose.Z), Matrix.Invert(Main.View));

						switch (Main.gameView.ShapeType)
						{
							case 0:
								creatingPose = Matrix.CreateScale(Math.Max(Math.Abs(lRealPose.X - rRealPose.X) - 0.1f, 0.05f), Math.Max(Math.Abs(lRealPose.Y - rRealPose.Y) - 0.1f, 0.05f),
								   Math.Max(Math.Abs(lRealPose.Z - rRealPose.Z) - 0.1f, 0.05f)) * Matrix.CreateTranslation((lRealPose.X + rRealPose.X) / 2,
									(lRealPose.Y + rRealPose.Y) / 2, (lRealPose.Z + rRealPose.Z) / 2);
								break;
							case 1:
							case 2:
								creatingPose = Matrix.CreateScale(Math.Max(Vector3.Distance(lRealPose, rRealPose) - 0.1f, 0.05f)) * Matrix.CreateTranslation((lRealPose.X + rRealPose.X) / 2,
									(lRealPose.Y + rRealPose.Y) / 2, (lRealPose.Z + rRealPose.Z) / 2);//              A-- questo per evitare la collisione con le mani
								break;
						}

						if (!lGrab && !rGrab)
						{
							switch (Main.gameView.ShapeType)
							{
								case 0:
									Main.World.VirtObjs.Add(new Box(Main, PxMatrix.Translation(Help.Math.Convert(creatingPose.Translation)),
										new StillDesign.PhysX.MathPrimitives.Vector3(creatingPose.Right.X, creatingPose.Up.Y, creatingPose.Backward.Z), 0.5f, Main.gameView.ShapeColor));
									break;
								case 1:
									Main.World.VirtObjs.Add(new Sphere(Main, PxMatrix.Translation(Help.Math.Convert(creatingPose.Translation)),
										creatingPose.Right.X / 2, 0.5f, Main.gameView.ShapeColor));
									break;
								//case 2:
								//    Main.World.VirtObjs.Add(new Football(Main, PxMatrix.Translation(Help.Math.Convert(creatingPose.Translation)),
								//        creatingPose.Right.X / 2, 0.5f));
								//    break;
							}
							Main.Creating = false;
							Main.gameView.UnSelectShapes();
						}
					}
				}
				#endregion


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


				if (Main.Mode == DrawMode.Sandbox)
				{
					#region Visualizza sandbox
					//Ottiene il frame a colori virtuale
					GraphicsDevice.SetRenderTarget(target);
					GraphicsDevice.Clear(Color.White);
					foreach (var body in Main.World.VirtObjs)
					{
						body.Draw();
					}
					if (Main.Creating)
					{
						DrawShape();
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
							int index = (mappedPoints[i].Y * W + mappedPoints[i].X) * 4;
							finalC[i * 4] = realC[index + 2];
							finalC[i * 4 + 1] = realC[index + 1];
							finalC[i * 4 + 2] = realC[index];
							finalC[i * 4 + 3] = 255;
						}
					}
					frameScene.SetData(finalC);
					#endregion
				}
				else
				{

					if (Main.Mode == DrawMode.DebugPhysics)
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
						foreach (var sphere in Main.World.DepthSphs)
						{
							sphere.DrawDebug();
						}
						GraphicsDevice.SetRenderTarget(null);
						target.GetData(virtC);
						frameScene.SetData(virtC);
						#endregion
					}
					else if (Main.Mode == DrawMode.DebugDepth)
					{
						#region Visualizza depth map
						byte value;
						for (int i = 0; i < realD.Length; i++)
						{
							if (!realD[i].IsKnownDepth || virtD[i] < realD[i].Depth)
							{
								value = (byte)(255 * (maxD - MathHelper.Clamp(virtD[i], 800, maxD)) / (maxD - 800)); // depth : max depth = x : 255
							}
							else
							{
								value = (byte)(255 * (maxD - MathHelper.Clamp(realD[i].Depth, 800, maxD)) / (maxD - 800));
							}
							finalC[i * 4] = value;
							finalC[i * 4 + 1] = value;
							finalC[i * 4 + 2] = value;
							finalC[i * 4 + 3] = 255;
						}
						frameScene.SetData(finalC);
						#endregion
					}
					else
					{
						#region Visualizza elementi riconosciuti
						for (int i = 0; i < realD.Length; i++)
						{
							float value = m * realD[i].Depth - realD[i].Depth * (((float)i / W) - H / 2) / F + q;
							if (isknown[i % W, i / W])
							{
								if (value < -20)
								{
									finalC[i * 4] = 255;
									finalC[i * 4 + 1] = 255;     //vuoto: bianco
									finalC[i * 4 + 2] = 255;
									finalC[i * 4 + 3] = 255;
								}
								if (Math.Abs(value) < 20)
								{
									finalC[i * 4] = 255;
									finalC[i * 4 + 1] = 0;
									finalC[i * 4 + 2] = 0;       //piano: rosso
									finalC[i * 4 + 3] = 255;
								}
								else if (realD[i].PlayerIndex != 0)
								{
									finalC[i * 4] = 0;
									finalC[i * 4 + 1] = 0;
									finalC[i * 4 + 2] = 255;     //utente: blu
									finalC[i * 4 + 3] = 255;
								}
								else
								{
									finalC[i * 4] = 0;
									finalC[i * 4 + 1] = 255;     //oggetti: verde
									finalC[i * 4 + 2] = 0;
									finalC[i * 4 + 3] = 255;
								}
								// else if (i / W > maxY && Math.Abs(dData[i % W, i / W].Y - Main.World.h) < 0.04)// Math.Abs(m * realD[i].Depth - realD[i].Depth * ((H / 2 - (float)i / W)) / -F + q) < 20)
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

		void DrawShape()
		{
			if (Main.gameView.ShapeType < 2)
			{
				for (int i = 0; i < Dispenser.Models[Main.gameView.ShapeType].Meshes.Count; i++)
				{
					colorEff.World = creatingPose;
					colorEff.View = Main.View;
					colorEff.Projection = Main.Projection;
					foreach (var meshPart in Dispenser.Models[Main.gameView.ShapeType].Meshes[i].MeshParts)
					{
						meshPart.Effect = (Effect)colorEff;
					}
					Dispenser.Models[Main.gameView.ShapeType].Meshes[i].Draw();
				}
			}
			else
			{
				for (int i = 0; i < Dispenser.Models[Main.gameView.ShapeType].Meshes.Count; i++)
				{
					for (int j = 0; j < Dispenser.Models[Main.gameView.ShapeType].Meshes[i].Effects.Count; j++)
					{
						((BasicEffect)footballEffect[j]).World = creatingPose * Matrix.CreateScale(1 / 100);
						((BasicEffect)footballEffect[j]).View = Main.View;
						((BasicEffect)footballEffect[j]).Projection = Main.Projection;
						Dispenser.Models[Main.gameView.ShapeType].Meshes[i].MeshParts[j].Effect = footballEffect[j];
					}
					Dispenser.Models[Main.gameView.ShapeType].Meshes[i].Draw();
				}
			}

		}

		void DepthCreatingShape()
		{
			for (int i = 0; i < Dispenser.Models[Main.gameView.ShapeType].Meshes.Count; i++)
			{
				depthEff.Parameters["WVP"].SetValue(creatingPose * Main.View * Main.Projection);

				foreach (var meshpart in Dispenser.Models[Main.gameView.ShapeType].Meshes[i].MeshParts)
				{
					meshpart.Effect = depthEff;
				}
				Dispenser.Models[Main.gameView.ShapeType].Meshes[i].Draw();
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
			ShowInTaskbar = false;
			GotFocus += new EventHandler(GraphicsControl_GotFocus);

			graphicsDeviceService = GraphicsDeviceService.AddRef(Handle, ClientSize.Width, ClientSize.Height);
			Services.AddService<IGraphicsDeviceService>(graphicsDeviceService);
			Content = new ContentManager(Services, "Content");

			Initialize();
		}

		void GraphicsControl_GotFocus(object sender, EventArgs e)
		{
			Main.Focus();
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
			if (BeginDraw())
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
			if (System.Threading.Interlocked.Increment(ref referenceCount) == 1)
			{
				singletonInstance = new GraphicsDeviceService(windowHandle, width, height);
			}
			return singletonInstance;
		}

		public void Release(bool disposing)
		{
			if (System.Threading.Interlocked.Decrement(ref referenceCount) == 0)
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