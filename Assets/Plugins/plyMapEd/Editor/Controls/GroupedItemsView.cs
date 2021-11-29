using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PLY.MapEd
{
	public class GroupedItemsView : ScrollView
	{
		public new class UxmlFactory : UxmlFactory<GroupedItemsView, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription { get { yield break; } }
		}

		public bool AutoDestroyUnusedThumbImage { get; set; }
		public bool ExpandNewlyAdded { get; set; }
		public int GroupsCount => groups.Count;		

		private const string ussView = "ply-grouped-items-view";
		private const string ussGroup = ussView + "-group";

		private List<Foldout> groups;
		private int itemSize;

		// ------------------------------------------------------------------------------------------------------------

		public GroupedItemsView()
			: this(120)
		{ }

		public GroupedItemsView(int itemSize)
			: base()
		{
			this.itemSize = itemSize;

			groups = new List<Foldout>();

			AddToClassList(ussView);
		}

		public virtual void Dispose()
		{
			RemoveAllGroups();
		}

		public Foldout AddGroup(string name, object userData, System.Action<bool, Foldout> onOpenClose)
		{
			var gr = new Foldout() { text = name, value = false, userData = userData };
			gr.AddToClassList(ussGroup);
			gr.SetValueWithoutNotify(ExpandNewlyAdded);

			if (onOpenClose != null)
			{
				gr.RegisterValueChangedCallback(ev => onOpenClose(ev.newValue, gr));
			}
			
			groups.Add(gr);
			Add(gr);

			return gr;
		}

		public void RemoveAllGroups()
		{
			foreach (var group in groups)
			{
				foreach (var c in group.Children())
				{
					if (c is ThumbItem thumb) thumb.Dispose();
				}
			}

			groups.Clear();
			Clear();
		}

		public void AddItemToGroup(Foldout group, ThumbItem item)
		{
			group.Add(item);

			item.AutoDestroyUnusedThumbImage = AutoDestroyUnusedThumbImage;
			item.SetSize(itemSize);
		}

		public void SetItemsSize(int size)
		{
			itemSize = size;
			foreach (var group in groups)
			{
				foreach (var ele in group.contentContainer.Children())
				{
					if (ele is ThumbItem thumb)
						thumb.SetSize(size);
				}
			}
		}

		public void CollapseAllGroups()
		{
			foreach (var group in groups)
			{
				group.value = false;
			}
		}

		public void ExpandAllGroups()
		{
			foreach (var group in groups)
			{
				group.value = true;
			}
		}

		public List<ThumbItem> GetAllItems()
		{
			var res = new List<ThumbItem>();
			foreach (var group in groups)
			{
				foreach (var ele in group.contentContainer.Children())
				{
					if (ele is ThumbItem thumb)
						res.Add(thumb);
				}
			}
			return res;
		}

		// ============================================================================================================
	}
}