using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PLY.MapEd
{
	public class MapEdWindow : EditorWindow
	{
		// ------------------------------------------------------------------------------------------------------------
		#region vars

		[System.NonSerialized] private ToolbarMenu foldersDropdown;
		[System.NonSerialized] private ToolbarButton collapseButton;
		[System.NonSerialized] private GroupedItemsView thumbsContainer;

		[System.NonSerialized] private Transform newObjectsContainer;
		[System.NonSerialized] private string thumbsRootPath;
		[System.NonSerialized] private string prefabsRootPath;
		[System.NonSerialized] private List<string> prefabFolders;
		[System.NonSerialized] private List<ThumbItem> thumbItems;

		private const string objectSearchParam = "t:gameobject";

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region const - stylesheet

		private const string thumbsFolderName = "/Library/plyMapEdThumbs/";
		private const string stylesResource = "plyMapEd_StyleSheet";

		private const string uss_IconButton = "ply-icon-toolbar-button";
		private const string uss_FoldersDropdown = "ply-folders-dropdown";
		private const string uss_ToolbarBar = "ply-toolbar";
		private const string uss_BottomBar = "ply-bottombar";
		private const string uss_BottomBarEle = "ply-bottombar-ele";

		private const string icon_Folder = "\U0000EA00";
		private const string icon_Settings = "\U0000EA01";
		private const string icon_Refresh = "\U0000EA02";
		private const string icon_Warning = "\U0000EA03";
		private const string icon_Collapse = "\U0000EA04";
		private const string icon_Expand = "\U0000EA05";
		private const string icon_Loading = "\U0000EA06";

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region system

		[MenuItem("Window/MapEd")]
		public static void Open()
		{
			GetWindow<MapEdWindow>("MapEd");
		}

		protected void OnEnable()
		{
			MapEdSettings.Init();

			thumbsRootPath = GetProjectRootPath() + thumbsFolderName;
			rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>(stylesResource));

			var thumbsSize = MapEdSettings.ThumbsSize;

			// *** Toolbar
			var toolbar = new Toolbar();
			toolbar.AddToClassList(uss_ToolbarBar);
			rootVisualElement.Add(toolbar);

			foldersDropdown = new ToolbarMenu { text = "Choose folder ->" };
			foldersDropdown.AddToClassList(uss_FoldersDropdown);
			toolbar.Add(foldersDropdown);

			var setRootFolderButton = new ToolbarButton { text = icon_Folder, tooltip = "Select Prefabs Root Folder" };
			setRootFolderButton.AddToClassList(uss_IconButton);
			setRootFolderButton.clicked += OnSetFolderButtonClicked;
			toolbar.Add(setRootFolderButton);
			toolbar.Add(new ToolbarSpacer());

			collapseButton = new ToolbarButton { text = icon_Expand, tooltip = "Show/Hide All" };
			collapseButton.AddToClassList(uss_IconButton);
			collapseButton.clicked += OnCollpaseButtonClicked;
			toolbar.Add(collapseButton);

			var settingsButton = new ToolbarButton { text = icon_Settings, tooltip = "Settings" };
			settingsButton.AddToClassList(uss_IconButton);
			settingsButton.clicked += () => MapEdSettingsWindow.Open();
			toolbar.Add(settingsButton);

			// *** Prefabs list (Icons)
			thumbsContainer = new GroupedItemsView(thumbsSize) 
			{ 
				AutoDestroyUnusedThumbImage = true,
				verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible, 
				horizontalScrollerVisibility = ScrollerVisibility.Hidden 
			};
			rootVisualElement.Add(thumbsContainer);

			// *** Bottom bar
			var bottomBar = new Toolbar();
			bottomBar.AddToClassList(uss_BottomBar);
			rootVisualElement.Add(bottomBar);

			var objField = new ObjectField() { objectType = typeof(Transform), allowSceneObjects = true };
			objField.AddToClassList(uss_BottomBarEle);
			objField.RegisterValueChangedCallback(ev => newObjectsContainer = ev.newValue as Transform);

			var thumbSizeSlider = new SliderInt(50, 200);
			thumbSizeSlider.AddToClassList(uss_BottomBarEle);
			thumbSizeSlider.SetValueWithoutNotify(thumbsSize);
			thumbSizeSlider.RegisterValueChangedCallback(OnThumbsSizeSliderValueChanged);

			var refreshThumbs = new ToolbarButton { text = icon_Refresh, tooltip = "Refresh all thumbs" };
			refreshThumbs.AddToClassList(uss_IconButton);
			refreshThumbs.clicked += OnRefreshThumbsButtonClicked;
			
			bottomBar.Add(new ToolbarSpacer());
			bottomBar.Add(objField);
			bottomBar.Add(thumbSizeSlider);
			bottomBar.Add(new ToolbarSpacer());
			bottomBar.Add(refreshThumbs);

			// *** done constructing UI
			SetPrefabsRoot(MapEdSettings.PrefabsRoot);
		}

		protected void OnDisable()
		{
			thumbsContainer.Dispose();
			thumbsContainer = null;
			thumbItems = null;
			newObjectsContainer = null;

			MapEdIconsGen.Dispose();
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region ui callbacks

		protected void OnGUI()
		{
			var ev = Event.current;

			if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
			{
				MapEdTransformTool.SetActiveObject(null);
			}
		}

		private void OnSetFolderButtonClicked()
		{
			var initPath = string.IsNullOrWhiteSpace(MapEdSettings.PrefabsRoot) ? Application.dataPath : MapEdSettings.PrefabsRoot;
			var path = EditorUtility.OpenFolderPanel("Select Prefabs Folder", initPath, "");
			if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
			{
				path = FullPathToRelativePath(path);
				SetPrefabsRoot(path);
			}
		}

		private void OnCollpaseButtonClicked()
		{
			if (thumbsContainer.ExpandNewlyAdded)
			{
				collapseButton.text = icon_Expand;
				thumbsContainer.ExpandNewlyAdded = false;
				thumbsContainer.CollapseAllGroups();
			}
			else
			{
				collapseButton.text = icon_Collapse;
				thumbsContainer.ExpandNewlyAdded = true;
				thumbsContainer.ExpandAllGroups();
			}
		}

		private void OnRefreshThumbsButtonClicked()
		{
			var rootFolder = MapEdSettings.PrefabsRoot;
			if (!string.IsNullOrEmpty(rootFolder))
			{
				// this will cause "all prefabs" to be shown and thus reload of thumbs
				BuildFoldersDropdown(rootFolder, forceGenerateThumbs: true);
			}
		}

		private void OnThumbsSizeSliderValueChanged(ChangeEvent<int> ev)
		{
			int sz = ev.newValue;
			MapEdSettings.ThumbsSize = sz;
			thumbsContainer.SetItemsSize(sz);
		}

		private void OnItemClicked(string guid)
		{
			MapEdTransformTool.SetActiveObject(guid, newObjectsContainer);
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region prefab folders/groups selection

		private void SetPrefabsRoot(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			prefabsRootPath = path;
			MapEdSettings.PrefabsRoot = path;

			BuildFoldersDropdown(path);
		}

		private void BuildFoldersDropdown(string path, bool forceGenerateThumbs = false)
		{
			// remove old entries
			var count = foldersDropdown.menu.MenuItems().Count;
			for (int i = count - 1; i >= 0; i--)
			{
				foldersDropdown.menu.RemoveItemAt(i);
			}

			// add new entries
			prefabFolders = GetPrefabFolders(path);

			foldersDropdown.text = "All Prefabs";
			foldersDropdown.menu.AppendAction("All Prefabs", _ => SetActivePrefabsFolder(-1), DropdownMenuAction.Status.Normal);
			foldersDropdown.menu.AppendSeparator();

			var subLen = path.Length + 1;
			for (int i = 0; i < prefabFolders.Count; i++)
			{
				var actionName = prefabFolders[i][subLen..]; //prefabFolders[i].Substring(subLen);
				foldersDropdown.menu.AppendAction(actionName, act => SetActivePrefabsFolder((int)act.userData), _ => DropdownMenuAction.Status.Normal, i);
			}

			// set "All Prefabs" active
			SetActivePrefabsFolder(-1, forceGenerateThumbs);
		}

		private void SetActivePrefabsFolder(int idx, bool forceGenerateThumbs = false)
		{
			if (idx < 0 || idx >= prefabFolders.Count)
			{
				if (string.IsNullOrEmpty(prefabsRootPath))
				{
					foldersDropdown.text = "Choose folder ->";
					PopulateThumbsList(null, forceGenerateThumbs);
				}
				else
				{
					foldersDropdown.text = "All Prefabs";
					PopulateThumbsList(prefabsRootPath, forceGenerateThumbs);
				}
			}
			else
			{
				foldersDropdown.text = GetLastDirInPath(prefabFolders[idx]);
				PopulateThumbsList(prefabFolders[idx], forceGenerateThumbs);
			}
		}

		private void PopulateThumbsList(string path, bool forceGenerateThumbs)
		{
			thumbItems = null;
			thumbsContainer.RemoveAllGroups();
			if (string.IsNullOrEmpty(path)) return;

			var allGuids = new List<string>(100);
			var folders = GetPrefabFolders(path, includeRoot: prefabsRootPath != path);

			if (folders.Count == 0)
			{
				// if no subfolders returned then root was meant to be used
				folders.Add(path);
			}

			foreach (var f in folders)
			{
				var s = GetLastDirInPath(f);
				var group = thumbsContainer.AddGroup(s, null, null);

				var guids = AssetDatabase.FindAssets(objectSearchParam, new[] { f });
				foreach (var guid in guids)
				{
					var label = GetFileNameInPath(AssetDatabase.GUIDToAssetPath(guid));
					var item = new ThumbItem(label, icon_Loading, icon_Warning) { userData = guid };
					thumbsContainer.AddItemToGroup(group, item);
					item.RegisterCallback<ClickEvent>(_ => OnItemClicked(guid));
					if (!allGuids.Contains(guid)) allGuids.Add(guid);
				}
			}

			// if only one group then auto expand it now
			if (thumbsContainer.GroupsCount == 1)
			{
				thumbsContainer.ExpandAllGroups();
			}

			thumbItems = thumbsContainer.GetAllItems();
			LoadThumbs(allGuids, forceGenerateThumbs);
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region thumbs generator

		private void LoadThumbs(List<string> guids, bool forceGenerateThumbs)
		{
			MapEdIconsGen.UseLight = MapEdSettings.IcoGenUseLight;
			MapEdIconsGen.LightIntensity = MapEdSettings.LightIntensity;
			MapEdIconsGen.GenerateIcons(thumbsRootPath, guids, forceGenerateThumbs, OnThumbsGenCompleted, OnThumbGenerated);
		}

		private void OnThumbGenerated(string guid, Texture2D texture)
		{
			if (thumbItems != null)
			{
				foreach (var item in thumbItems)
				{
					if (item.userData.ToString() == guid)
					{
						item.ShowThumb(true, texture);
						// note: don't break since this could be a main folder showing all entries
						// from subfolders and then those in subfolder do not get assigned an icon
						// break;
					}
				}
			}
		}

		private void OnThumbsGenCompleted()
		{
			if (thumbItems != null)
			{
				// thumbs generator is completed so whatever did not generated has error
				foreach (var item in thumbItems)
				{
					if (item.ThumbIsShown == false)
					{
						item.ShowStatus(icon_Warning);
					}
				}
			}
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region misc

		private static List<string> GetPrefabFolders(string path, bool includeRoot = false)
		{
			var res = new List<string>();
			
			if (includeRoot) res.Add(path);
			GetSubFoldersRecursively(path, res);

			// TODO: Remove folders which does not contain prefabs, except when
			// those folders contain other folders which have prefabs in them

			return res;
		}

		private static void GetSubFoldersRecursively(string path, List<string> list)
		{
			var paths = AssetDatabase.GetSubFolders(path);
			foreach (var p in paths)
			{
				list.Add(p);
				GetSubFoldersRecursively(p, list);
			}
		}

		private static string FullPathToRelativePath(string path)
		{
			return "Assets" + path[Application.dataPath.Length..]; // "Assets" + path.Substring(Application.dataPath.Length);
		}

		private static string GetLastDirInPath(string path)
		{
			var s = path.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
			return s[^1]; // s[s.Length - 1];
		}

		private static string GetProjectRootPath()
		{
			var path = Application.dataPath;
			return path[0..^7]; // rmeove the "/Assets" part  //path.Substring(0, path.Length - 7); // rmeove the "/Assets" part
		}

		private static string GetFileNameInPath(string path)
		{
			try { return Path.GetFileNameWithoutExtension(path); } catch { }
			return string.Empty;
		}

		#endregion
		// ============================================================================================================
	}
}