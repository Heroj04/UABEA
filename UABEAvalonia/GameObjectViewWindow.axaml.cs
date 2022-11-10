using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.IO;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class GameObjectViewWindow : Window
    {
        //controls
        private TreeView gameObjectTreeView;
        private AssetDataTreeView componentTreeView;
        private MenuItem menuVisitAsset;
        private ComboBox cbxFiles;
        private Button btnExpand;
        private Button btnCollapse;
        private Button btnExport;

        private InfoWindow win;
        private AssetWorkspace workspace;

        private bool ignoreDropdownEvent;

        public GameObjectViewWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            gameObjectTreeView = this.FindControl<TreeView>("gameObjectTreeView");
            componentTreeView = this.FindControl<AssetDataTreeView>("componentTreeView");
            menuVisitAsset = this.FindControl<MenuItem>("menuVisitAsset");
            cbxFiles = this.FindControl<ComboBox>("cbxFiles");
            btnExpand = this.FindControl<Button>("btnExpand");
            btnCollapse = this.FindControl<Button>("btnCollapse");
            btnExport = this.FindControl<Button>("btnExport");
            //generated events
            gameObjectTreeView.SelectionChanged += GameObjectTreeView_SelectionChanged;
            menuVisitAsset.Click += MenuVisitAsset_Click;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
            btnExpand.Click += BtnExpand_Click;
            btnCollapse.Click += BtnCollapse_Click;
            btnExport.Click += BtnExport_Click;
        }

        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace) : this()
        {
            this.win = win;
            this.workspace = workspace;

            ignoreDropdownEvent = true;

            componentTreeView.Init(workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();
        }

        private void GameObjectTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            object? selectedItemObj = e.AddedItems[0];
            if (selectedItemObj == null)
                return;

            TreeViewItem selectedItem = (TreeViewItem)selectedItemObj;
            if (selectedItem.Tag == null)
                return;

            AssetContainer gameObjectCont = (AssetContainer)selectedItem.Tag;
            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            AssetTypeValueField components = gameObjectBf["m_Component"]["Array"];

            componentTreeView.Reset();

            foreach (AssetTypeValueField data in components)
            {
                AssetTypeValueField component = data["component"];
                AssetContainer componentCont = workspace.GetAssetContainer(gameObjectCont.FileInstance, component, false);
                componentTreeView.LoadComponent(componentCont);
            }
        }

        private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)componentTreeView.SelectedItem;
            if (item != null && item.Tag != null)
            {
                AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                win.SelectAsset(info.fromFile, info.fromPathId);
            }
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // this event happens after the constructor
            // is called, so this is the only way to do it
            if (ignoreDropdownEvent)
            {
                ignoreDropdownEvent = false;
                return;
            }

            PopulateHierarchyTreeView();
        }

        private void BtnExpand_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (componentTreeView.SelectedItem != null && componentTreeView.SelectedItem is TreeViewItem treeItem)
            {
                componentTreeView.ExpandAllChildren(treeItem);
            }
        }

        private void BtnCollapse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (componentTreeView.SelectedItem != null && componentTreeView.SelectedItem is TreeViewItem treeItem)
            {
                componentTreeView.CollapseAllChildren(treeItem);
            }
        }

        private async void BtnExport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            object? selectedItemObj = gameObjectTreeView.SelectedItem;
            if (selectedItemObj == null)
                return;

            TreeViewItem selectedItem = (TreeViewItem)selectedItemObj;
            if (selectedItem.Tag == null)
                return;

            AssetContainer gameObjectCont = (AssetContainer)selectedItem.Tag;
            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                SelectDumpWindow selectDumpWindow = new SelectDumpWindow(true);
                string? extension = await selectDumpWindow.ShowDialog<string?>(this);

                if (extension == null)
                    return;
                
                await exportTree(selectedItem, dir, extension);
                MessageBoxUtil.ShowDialog(win, "Export", "Exported " + gameObjectBf["m_Name"].AsString);
            }
        }

        private async Task exportTree(TreeViewItem tree, string basePath, string extension) {
            if (tree.Tag == null)
                return;
            
            AssetContainer gameObjectCont = (AssetContainer)tree.Tag;
            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            AssetTypeValueField components = gameObjectBf["m_Component"]["Array"];
            
            Directory.CreateDirectory(basePath);

            // Export current game objects components
            foreach (AssetTypeValueField data in components)
            {
                AssetTypeValueField component = data["component"];
                AssetContainer componentCont = workspace.GetAssetContainer(gameObjectCont.FileInstance, component, false);
                Extensions.GetUABENameFast(workspace, componentCont, false, out string assetName, out string _);
                assetName = Extensions.ReplaceInvalidPathChars(assetName);
                string file = Path.Combine(basePath, $"{assetName}-{Path.GetFileName(componentCont.FileInstance.path)}-{componentCont.PathId}.{extension}");

                using (FileStream fs = File.OpenWrite(file))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    AssetTypeValueField baseField = workspace.GetBaseField(componentCont);

                    AssetImportExport dumper = new AssetImportExport();
                    if (extension == "json")
                        dumper.DumpJsonAsset(sw, baseField);
                    else //if (extension == "txt")
                        dumper.DumpTextAsset(sw, baseField);
                }
            }

            // Export child game objects
            foreach (TreeViewItem childTree in tree.Items)
            {
                string childBasePath = basePath + "\\" + childTree.Header;
                exportTree(childTree, childBasePath, extension);
            }
        }

        private void PopulateFilesComboBox()
        {
            AvaloniaList<object> comboBoxItems = (AvaloniaList<object>)cbxFiles.Items;
            foreach (AssetsFileInstance fileInstance in workspace.LoadedFiles)
            {
                ComboBoxItem comboItem = new ComboBoxItem()
                {
                    Content = fileInstance.name,
                    Tag = fileInstance
                };
                comboBoxItems.Add(comboItem);
            }
            cbxFiles.SelectedIndex = 0;
        }

        private void PopulateHierarchyTreeView()
        {
            ComboBoxItem? selectedComboItem = (ComboBoxItem?)cbxFiles.SelectedItem;
            if (selectedComboItem == null)
                return;

            AssetsFileInstance? fileInstance = (AssetsFileInstance?)selectedComboItem.Tag;
            if (fileInstance == null)
                return;

            //clear treeview
            gameObjectTreeView.Items = new AvaloniaList<object>();

            foreach (var asset in workspace.LoadedAssets)
            {
                AssetContainer assetCont = asset.Value;

                AssetClassID assetType = (AssetClassID)assetCont.ClassId;
                bool isTransformType = assetType == AssetClassID.Transform || assetType == AssetClassID.RectTransform;

                if (assetCont.FileInstance == fileInstance && isTransformType)
                {
                    AssetTypeValueField transformBf = workspace.GetBaseField(assetCont);
                    AssetTypeValueField transformFatherBf = transformBf["m_Father"];
                    long pathId = transformFatherBf["m_PathID"].AsLong;
                    //is root GameObject
                    if (pathId == 0)
                    {
                        LoadGameObjectTreeItem(assetCont, transformBf, null);
                    }
                }
            }
        }

        private void LoadGameObjectTreeItem(AssetContainer transformCont, AssetTypeValueField transformBf, TreeViewItem? parentTreeItem)
        {
            TreeViewItem treeItem = new TreeViewItem();

            AssetTypeValueField gameObjectRef = transformBf["m_GameObject"];
            AssetContainer gameObjectCont = workspace.GetAssetContainer(transformCont.FileInstance, gameObjectRef, false);

            if (gameObjectCont == null)
                return;

            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            string name = gameObjectBf["m_Name"].AsString;

            treeItem.Header = name;
            treeItem.Tag = gameObjectCont;

            AssetTypeValueField children = transformBf["m_Children"]["Array"];
            foreach (AssetTypeValueField child in children)
            {
                AssetContainer childTransformCont = workspace.GetAssetContainer(transformCont.FileInstance, child, false);
                AssetTypeValueField childTransformBf = workspace.GetBaseField(childTransformCont);
                LoadGameObjectTreeItem(childTransformCont, childTransformBf, treeItem);
            }

            AvaloniaList<object> parentItems;
            if (parentTreeItem == null)
            {
                parentItems = (AvaloniaList<object>)gameObjectTreeView.Items;
            }
            else
            {
                parentItems = (AvaloniaList<object>)parentTreeItem.Items;
            }
            parentItems.Add(treeItem);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
