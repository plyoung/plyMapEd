using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PLY.MapEd
{
	public static class MapEdIconsGen
	{
		// TODO: Make icongen async

		public static int IconSize { get; set; } = 128;
		public static int RenderLayer { get; set; } = 2;
		
		public static float CamDistance { get; set; } = 2.5f;
		public static int AntiAliasing { get; set; } = 4; // 2,4,8
		public static bool MarkTextureNonReadable { get; set; } = true;
		public static Vector3 PreviewDirection { get; set; } = new Vector3(-0.57735f, -0.57735f, -0.57735f); // Normalized (-1,-1,-1)
		public static float Padding { get; set; } = 0.15f;
		public static Color BackColor { get; set; } = new Color(0.15f, 0.15f, 0.15f, 1f);
		public static bool Orthographic { get; set; } = false;

		public static bool UseLight { get; set; } = true;
		public static Color LightColor { get; set; } = new Color(1f, 0.9568627f, 0.8392157f, 1f);
		public static float LightIntensity { get; set; } = 1f;
		public static LightShadows LightShadows { get; set; } = LightShadows.None;

		private static System.Action onCompleted; // <guid, texture>
		private static System.Action<string, Texture2D> onIconGenerated; // <guid, texture>

		private static Camera renderCam;
		private static readonly Vector3[] boundingBoxPoints = new Vector3[8];
		private static readonly List<Renderer> renderersList = new List<Renderer>(64);
		private static readonly List<Light> lightsList = new List<Light>(64);

		// ------------------------------------------------------------------------------------------------------------

		public static void Initialize(string iconsRootPath)
		{
			renderCam = new GameObject("[MapEdIconsGenCamera]").AddComponent<Camera>();
			renderCam.gameObject.tag = "EditorOnly";
			renderCam.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy; // HideFlags.HideAndDontSave;
			renderCam.aspect = (float)IconSize / IconSize;
			renderCam.clearFlags = CameraClearFlags.Color;
			renderCam.backgroundColor = BackColor;
			renderCam.cullingMask = 1 << RenderLayer;
			renderCam.depth = -999;
			renderCam.nearClipPlane = 0.01f;
			renderCam.orthographic = Orthographic;

			if (UseLight)
			{
				var light = renderCam.gameObject.AddComponent<Light>();
				light.type = LightType.Directional;
				light.cullingMask = 1 << RenderLayer;
				light.color = LightColor;
				light.intensity = LightIntensity;
				light.shadows = LightShadows;
			}

			// make sure the save/load dit exists
			try { Directory.CreateDirectory(iconsRootPath); } catch { };
		}

		public static void Dispose()
		{
			if (renderCam != null)
			{
				Object.DestroyImmediate(renderCam.gameObject);
				renderCam = null;
			}

			onCompleted = null;
			onIconGenerated = null;
		}

		public static void GenerateIcons(string iconsRootPath, List<string> guids, bool regenerateAllIcons, System.Action onCompleted, System.Action<string, Texture2D> onIconGenerated)
		{
			if (renderCam == null)
			{
				Initialize(iconsRootPath);
			}

			if (regenerateAllIcons)
			{   // delete the old icons
				DeleteAllFiles(iconsRootPath);
			}

			MapEdIconsGen.onCompleted = onCompleted;
			MapEdIconsGen.onIconGenerated = onIconGenerated;

			foreach (var guid in guids)
			{
				if (renderCam == null)
				{   // in case it was destroyed/Dispose was called before this method completed
					return;
				}

				var path = Path.Combine(iconsRootPath, $"{guid}.png");

				// first check if there is not already a texture stored on disk
				if (regenerateAllIcons == false)
				{
					var texture = LoadIcon(path);
					if (texture != null)
					{
						MapEdIconsGen.onIconGenerated(guid, texture);
						continue;
					}
				}

				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
				if (prefab != null)
				{
					var texture = GenerateIcon(path, prefab);
					MapEdIconsGen.onIconGenerated(guid, texture);
				}
			}

			MapEdIconsGen.onCompleted?.Invoke();
			Dispose();
		}

		private static Texture2D LoadIcon(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					var bytes = File.ReadAllBytes(path);
					var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGB24, false);
					texture.LoadImage(bytes, MarkTextureNonReadable);
					if (texture != null) texture.hideFlags = HideFlags.HideAndDontSave;
					return texture;
				}
			}
			catch { }
			return null;
		}

		private static void SaveIcon(string path, Texture2D texture)
		{
			if (texture == null) return;

			try
			{
				var bytes = texture.EncodeToPNG();
				File.WriteAllBytes(path, bytes);
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		private static Texture2D GenerateIcon(string savePath, GameObject prefab)
		{
			Texture2D result = null;

			var targetObj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
			targetObj.hideFlags = HideFlags.HideAndDontSave;

			SetLayerAndFlagsRecursively(targetObj.GetComponent<Transform>());

			// disable lights in object
			targetObj.GetComponentsInChildren(lightsList);
			foreach (var l in lightsList) l.enabled = false;
			lightsList.Clear();

			// position camera
			if (!CalculateBounds(targetObj, out Bounds targetBounds))
			{
				Object.DestroyImmediate(targetObj);
				return null;
			}

			CalculateCameraPosition(targetBounds, targetObj.transform);

			// render
			RenderTexture activeRT = RenderTexture.active;
			RenderTexture renderTexture = null;

			try
			{
				renderTexture = RenderTexture.GetTemporary(IconSize, IconSize);
				renderTexture.antiAliasing = AntiAliasing;

				RenderTexture.active = renderTexture;
				renderCam.targetTexture = renderTexture;
				renderCam.Render();

				result = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
				result.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0, false);

				SaveIcon(savePath, result);

				result.Apply(false, MarkTextureNonReadable);
			}
			finally
			{
				RenderTexture.active = activeRT;
				renderCam.targetTexture = null;
				if (renderTexture != null) 
					RenderTexture.ReleaseTemporary(renderTexture);
			}

			// done with the temporary object
			Object.DestroyImmediate(targetObj);

			return result;
		}

		private static void SetLayerAndFlagsRecursively(Transform transform)
		{
			transform.gameObject.layer = RenderLayer;
			GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);

			int count = transform.childCount;
			for (int i = 0; i < count; i++)
			{
				SetLayerAndFlagsRecursively(transform.GetChild(i));
			}
		}

		// Calculates AABB bounds of the target object (AABB will include its child objects)
		private static bool CalculateBounds(GameObject target, out Bounds bounds)
		{
			renderersList.Clear();
			target.GetComponentsInChildren(renderersList);

			bounds = new Bounds();
			bool hasBounds = false;
			for (int i = 0; i < renderersList.Count; i++)
			{
				if (!renderersList[i].enabled)
					continue;

				if (!hasBounds)
				{
					bounds = renderersList[i].bounds;
					hasBounds = true;
				}
				else
					bounds.Encapsulate(renderersList[i].bounds);
			}

			return hasBounds;
		}

		private static void CalculateCameraPosition(Bounds bounds, Transform targetObjTr)
		{
			MapEdIconsGen.renderCam.transform.rotation = Quaternion.LookRotation(targetObjTr.rotation * PreviewDirection, targetObjTr.up);

			Transform cameraTR = renderCam.GetComponent<Transform>();

			Vector3 cameraDirection = cameraTR.forward;
			float aspect = renderCam.aspect;

			if (Padding != 0f)
				bounds.size *= 1f + Padding * 2f; // Padding applied to both edges, hence multiplied by 2

			Vector3 boundsCenter = bounds.center;
			Vector3 boundsExtents = bounds.extents;
			Vector3 boundsSize = 2f * boundsExtents;

			// Calculate corner points of the Bounds
			Vector3 point = boundsCenter + boundsExtents;
			boundingBoxPoints[0] = point;
			point.x -= boundsSize.x;
			boundingBoxPoints[1] = point;
			point.y -= boundsSize.y;
			boundingBoxPoints[2] = point;
			point.x += boundsSize.x;
			boundingBoxPoints[3] = point;
			point.z -= boundsSize.z;
			boundingBoxPoints[4] = point;
			point.x -= boundsSize.x;
			boundingBoxPoints[5] = point;
			point.y += boundsSize.y;
			boundingBoxPoints[6] = point;
			point.x += boundsSize.x;
			boundingBoxPoints[7] = point;

			if (renderCam.orthographic)
			{
				cameraTR.position = boundsCenter;

				float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
				float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

				for (int i = 0; i < boundingBoxPoints.Length; i++)
				{
					Vector3 localPoint = cameraTR.InverseTransformPoint(boundingBoxPoints[i]);
					if (localPoint.x < minX) minX = localPoint.x;
					if (localPoint.x > maxX) maxX = localPoint.x;
					if (localPoint.y < minY) minY = localPoint.y;
					if (localPoint.y > maxY) maxY = localPoint.y;
				}

				float distance = boundsExtents.magnitude + 1f;
				renderCam.orthographicSize = Mathf.Max(maxY - minY, (maxX - minX) / aspect) * 0.5f;
				cameraTR.position = boundsCenter - cameraDirection * distance;
			}
			else
			{
				Vector3 cameraUp = cameraTR.up, cameraRight = cameraTR.right;

				float verticalFOV = renderCam.fieldOfView * 0.5f;
				float horizontalFOV = Mathf.Atan(Mathf.Tan(verticalFOV * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;

				// Normals of the camera's frustum planes
				Vector3 topFrustumPlaneNormal = Quaternion.AngleAxis(90f + verticalFOV, -cameraRight) * cameraDirection;
				Vector3 bottomFrustumPlaneNormal = Quaternion.AngleAxis(90f + verticalFOV, cameraRight) * cameraDirection;
				Vector3 rightFrustumPlaneNormal = Quaternion.AngleAxis(90f + horizontalFOV, cameraUp) * cameraDirection;
				Vector3 leftFrustumPlaneNormal = Quaternion.AngleAxis(90f + horizontalFOV, -cameraUp) * cameraDirection;

				// Credit for algorithm: https://stackoverflow.com/a/66113254/2373034
				// 1. Find edge points of the bounds using the camera's frustum planes
				// 2. Create a plane for each edge point that goes through the point and has the corresponding frustum plane's normal
				// 3. Find the intersection line of horizontal edge points' planes (horizontalIntersection) and vertical edge points' planes (verticalIntersection)
				//    If we move the camera along horizontalIntersection, the bounds will always with the camera's width perfectly (similar effect goes for verticalIntersection)
				// 4. Find the closest line segment between these two lines (horizontalIntersection and verticalIntersection) and place the camera at the farthest point on that line
				int leftmostPoint = -1, rightmostPoint = -1, topmostPoint = -1, bottommostPoint = -1;
				for (int i = 0; i < boundingBoxPoints.Length; i++)
				{
					if (leftmostPoint < 0 && IsOutermostPointInDirection(i, leftFrustumPlaneNormal)) leftmostPoint = i;
					if (rightmostPoint < 0 && IsOutermostPointInDirection(i, rightFrustumPlaneNormal)) rightmostPoint = i;
					if (topmostPoint < 0 && IsOutermostPointInDirection(i, topFrustumPlaneNormal)) topmostPoint = i;
					if (bottommostPoint < 0 && IsOutermostPointInDirection(i, bottomFrustumPlaneNormal)) bottommostPoint = i;
				}

				Ray horizontalIntersection = GetPlanesIntersection(new Plane(leftFrustumPlaneNormal, boundingBoxPoints[leftmostPoint]), new Plane(rightFrustumPlaneNormal, boundingBoxPoints[rightmostPoint]));
				Ray verticalIntersection = GetPlanesIntersection(new Plane(topFrustumPlaneNormal, boundingBoxPoints[topmostPoint]), new Plane(bottomFrustumPlaneNormal, boundingBoxPoints[bottommostPoint]));

				FindClosestPointsOnTwoLines(horizontalIntersection, verticalIntersection, out Vector3 closestPoint1, out Vector3 closestPoint2);

				cameraTR.position = Vector3.Dot(closestPoint1 - closestPoint2, cameraDirection) < 0 ? closestPoint1 : closestPoint2;
			}
		}

		// Returns whether or not the given point is the outermost point in the given direction among all points of the bounds
		private static bool IsOutermostPointInDirection(int pointIndex, Vector3 direction)
		{
			Vector3 point = boundingBoxPoints[pointIndex];
			for (int i = 0; i < boundingBoxPoints.Length; i++)
			{
				if (i != pointIndex && Vector3.Dot(direction, boundingBoxPoints[i] - point) > 0)
					return false;
			}

			return true;
		}

		// Returns the intersection line of the 2 planes
		private static Ray GetPlanesIntersection(Plane p1, Plane p2)
		{
			// Credit: https://stackoverflow.com/a/32410473/2373034
			Vector3 p3Normal = Vector3.Cross(p1.normal, p2.normal);
			float det = p3Normal.sqrMagnitude;

			return new Ray(((Vector3.Cross(p3Normal, p2.normal) * p1.distance) + (Vector3.Cross(p1.normal, p3Normal) * p2.distance)) / det, p3Normal);
		}

		// Returns the edge points of the closest line segment between 2 lines
		private static void FindClosestPointsOnTwoLines(Ray line1, Ray line2, out Vector3 closestPointLine1, out Vector3 closestPointLine2)
		{
			// Credit: http://wiki.unity3d.com/index.php/3d_Math_functions
			Vector3 line1Direction = line1.direction;
			Vector3 line2Direction = line2.direction;

			float a = Vector3.Dot(line1Direction, line1Direction);
			float b = Vector3.Dot(line1Direction, line2Direction);
			float e = Vector3.Dot(line2Direction, line2Direction);

			float d = a * e - b * b;

			Vector3 r = line1.origin - line2.origin;
			float c = Vector3.Dot(line1Direction, r);
			float f = Vector3.Dot(line2Direction, r);

			float s = (b * f - c * e) / d;
			float t = (a * f - c * b) / d;

			closestPointLine1 = line1.origin + line1Direction * s;
			closestPointLine2 = line2.origin + line2Direction * t;
		}

		private static void DeleteAllFiles(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);
			foreach (FileInfo file in di.GetFiles())
			{
				try { file.Delete(); } catch { }
			}
		}

		// ============================================================================================================
	}
}