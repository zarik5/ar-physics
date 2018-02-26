using System;
using System.Diagnostics;
using System.Collections.Generic;
using StillDesign.PhysX;
using Microsoft.Xna.Framework;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;
using PxVector3 = StillDesign.PhysX.MathPrimitives.Vector3;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace AR_Physics
{

	public class Physics
	{
		const int W = 32, H = 24, STEP = 20;

		MainWindow Main;
		Core physxBase;

		public List<VirtBody> VirtObjs = new List<VirtBody>();
		public List<RealBody> RealObjs = new List<RealBody>();
		public List<RealBody> DepthSphs = new List<RealBody>();
		public float SimulationTime;
		public float h; //distanza del sensore dal piano
		public float d; //margine del piano

		public Scene Scene;
		Stopwatch dropShape;

		public Physics(MainWindow main)
		{
			Main = main;
			dropShape = Stopwatch.StartNew();
			physxBase = new Core();

			SceneDescription sceneD = new SceneDescription() { Gravity = new PxVector3(0, -9.81f, 0) };
			Scene = physxBase.CreateScene(sceneD);

			physxBase.SetParameter(PhysicsParameter.SkinWidth, 0.005f);

		}
		public void Initilize()
		{
			//crea le sfere cinematiche che rappresentano la scena reale
			for (int x = 0; x < W; x++)
			{
				for (int y = 0; y < H; y++)
				{
					DepthSphs.Add(new DepthSphere(Main, x, y));
				}
			}
		}

		public void BeginUpdate()
		{
			Scene.Simulate((float)Main.Elapsed.TotalSeconds);
			Scene.FlushStream();
		}

		public void EndUpdate()
		{
			Scene.FetchResults(SimulationStatus.AllFinished, true);
			for (int i = VirtObjs.Count - 1; i >= 0; i--)
			{
				if (VirtObjs[i].Actor.GlobalPosition.Y < h - 1)
				{
					VirtObjs[i].Dispose();
					VirtObjs.RemoveAt(i);
				}
			}
			if (Main.Mode != DrawMode.Sandbox)
			{
				if (dropShape.Elapsed.Seconds > 5)
				{
					float dist = d * 4 / 5;
					if (Main.floorMode && Main.GCtrl.Skeletons[Main.GCtrl.UserN] != null)
					{
						dist = -Main.GCtrl.Skeletons[Main.GCtrl.UserN].Position.Z + 0.6f;
					}
					dropShape.Restart();
					Random rand = new Random();
					int nShape = rand.Next(2);
					if (nShape == 0)
					{
						VirtObjs.Add(new Box(Main, PxMatrix.Translation((float)rand.NextDouble() - 0.5f, 0.5f, dist),
							new PxVector3(0.1f + (float)rand.NextDouble() * 0.2f, 0.1f + (float)rand.NextDouble() * 0.2f,
								0.1f + (float)rand.NextDouble() * 0.2f), 0.5f, Color.Black));
					}
					else if (nShape == 1)
					{
						VirtObjs.Add(new Sphere(Main, PxMatrix.Translation((float)rand.NextDouble() - 0.5f, 0.5f, dist),
							0.1f + (float)rand.NextDouble() * 0.1f, 0.5f, Color.Black));
					}
				}
			}

		}

		public void Reset()
		{
			//cancella tutti gli oggetti
			foreach (var body in VirtObjs)
			{
				body.Dispose();
			}
			foreach (var body in RealObjs)
			{
				body.Dispose();
			}
			VirtObjs.Clear();
			RealObjs.Clear();
		}

		public void Start()
		{
			// aggiunge il piano (un cubo)
			RealObjs.Add(new BoxPlane(Main, h, d));
		}

		public void UpdateDepthSpheres(XVector3[,] vData, bool[,] isKnown)//  32x24
		{
			// voxelizzazione
			for (int a = STEP / 2; a < 480; a += STEP)
			{
				for (int b = STEP / 2; b < 640; b += STEP)
				{
					int x = (b - STEP / 2) / STEP,
						y = (a - STEP / 2) / STEP;
					if (isKnown[b, a])
					{
						if (PxVector3.Distance(DepthSphs[y * W + x].Actor.GlobalPosition, Help.Math.Convert(vData[b, a])) > 0.05f)
						{
							DepthSphs[y * W + x].Actor.GlobalPosition = Help.Math.Convert(vData[b, a]);// "teletrasporta" le sfere
						}
						else
						{
							DepthSphs[y * W + x].Actor.MoveGlobalPositionTo(Help.Math.Convert(vData[b, a])); // sposta le sfere
						}
					}
					else
					{
						DepthSphs[y * W + x].Actor.GlobalPosition = new PxVector3(x, y, 0);// distribuisco le sfere al di fuori del pdv
					}
				}
			}
		}
	}
}