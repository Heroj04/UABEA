using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;

namespace UABEAvalonia
{
    public class DependenciesWindow : Window
    {
        //controls
        private ListBox boxDependenciesList;
        private ComboBox cbxFiles;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnCancel;
        private Button btnOk;
		
        private AssetWorkspace workspace;

        private Dictionary<AssetsFileInstance, List<AssetsFileDependency>> dependencyMap;
        private HashSet<AssetsFileInstance> dependenciesModified;
        
        private bool askedAboutMoving;

        public DependenciesWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            boxDependenciesList = this.FindControl<ListBox>("boxDependenciesList");
            cbxFiles = this.FindControl<ComboBox>("cbxFiles");
            btnAdd = this.FindControl<Button>("btnAdd");
            btnRemove = this.FindControl<Button>("btnRemove");
            btnMoveUp = this.FindControl<Button>("btnMoveUp");
            btnMoveDown = this.FindControl<Button>("btnMoveDown");
            btnCancel = this.FindControl<Button>("btnCancel");
            btnOk = this.FindControl<Button>("btnOk");
            //generated events
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            btnMoveUp.Click += BtnMoveUp_Click;
            btnMoveDown.Click += BtnMoveDown_Click;
            btnCancel.Click += BtnCancel_Click;
            btnOk.Click += BtnOk_Click;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;

            dependencyMap = new Dictionary<AssetsFileInstance, List<AssetsFileDependency>>();
            dependenciesModified = new HashSet<AssetsFileInstance>();

            askedAboutMoving = false;
        }

        public DependenciesWindow(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            List<DependencyComboBoxItem> comboBoxFiles = new List<DependencyComboBoxItem>();
            DependencyComboBoxItem allFilesItem = new DependencyComboBoxItem("All files", null);
            comboBoxFiles.Add(allFilesItem);

            for (int i = 0; i < workspace.LoadedFiles.Count; i++)
            {
                AssetsFileInstance file = workspace.LoadedFiles[i];
                comboBoxFiles.Add(new DependencyComboBoxItem(file.name, file));
                
                List<AssetsFileDependency> dependencies = new List<AssetsFileDependency>();
                for (int j = 0; j < file.file.dependencies.dependencies.Count; j++)
                {
                    dependencies.Add(file.file.dependencies.dependencies[j]);
                }

                dependencyMap[file] = dependencies;
            }

            cbxFiles.Items = comboBoxFiles;
            cbxFiles.SelectedItem = allFilesItem;

            UpdateListBox();
            SetButtonsEnabled(false);
        }

        private void UpdateListBox()
        {
            if (cbxFiles.SelectedItem == null)
                return;

            AssetsFileInstance? selectedFile = GetSelectedAssetsFile();

            if (selectedFile == null)
            {
                List<DependencyListBoxItem> lbDeps = new List<DependencyListBoxItem>();
                for (int i = 0; i < workspace.LoadedFiles.Count; i++)
                {
                    lbDeps.Add(new DependencyListBoxItem(i, workspace.LoadedFiles[i]));
                }

                boxDependenciesList.Items = lbDeps;

                SetButtonsEnabled(false);
            }
            else
            {
                List<AssetsFileDependency> deps = dependencyMap[selectedFile];

                List<DependencyListBoxItem> lbDeps = new List<DependencyListBoxItem>();
                lbDeps.Add(new DependencyListBoxItem(0, selectedFile));
                for (int i = 0; i < deps.Count; i++)
                {
                    lbDeps.Add(new DependencyListBoxItem(i + 1, deps[i]));
                }

                boxDependenciesList.Items = lbDeps;

                SetButtonsEnabled(true);
            }
        }

        private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
        {
            AssetsFileInstance? selectedFile = GetSelectedAssetsFile();
            if (selectedFile == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You must add a dependency to a file, not the entire workspace.");

                return;
            }

            AddDependencyWindow window = new AddDependencyWindow();
            AssetsFileDependency? dependency = await window.ShowDialog<AssetsFileDependency?>(this);
            if (dependency == null)
            {
                return;
            }

            dependencyMap[selectedFile].Add(dependency);
            dependenciesModified.Add(selectedFile);

            UpdateListBox();
        }

        private async void BtnRemove_Click(object? sender, RoutedEventArgs e)
        {
            AssetsFileInstance? selectedFile = GetSelectedAssetsFile();
            if (selectedFile == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You must remove a dependency from a file, not the entire workspace.");

                return;
            }

            DependencyListBoxItem? dependency = GetSelectedDependency();
            if (dependency == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You must select a dependency to remove.");

                return;
            }

            if (!dependency.isDependency)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You can't remove the base file.");

                return;
            }

            int originalDependencyCount = selectedFile.file.dependencies.dependencyCount;
            if (!askedAboutMoving && boxDependenciesList.SelectedIndex <= originalDependencyCount)
            {
                bool shouldContinue = await ShowMoveConfirmationDialog();
                if (!shouldContinue)
                    return;

                askedAboutMoving = true;
            }

            dependencyMap[selectedFile].Remove(dependency.dependency!);
            dependenciesModified.Add(selectedFile);

            UpdateListBox();
        }

        private void BtnMoveUp_Click(object? sender, RoutedEventArgs e)
        {
            MoveDependency(true);
        }

        private void BtnMoveDown_Click(object? sender, RoutedEventArgs e)
        {
            MoveDependency(false);
        }

        private async void MoveDependency(bool moveUp)
        {
            AssetsFileInstance? selectedFile = GetSelectedAssetsFile();
            if (selectedFile == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You must move a dependency from a file, not the entire workspace.");

                return;
            }

            DependencyListBoxItem? dependency = GetSelectedDependency();
            if (dependency == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You must select a dependency to move.");

                return;
            }

            if (!dependency.isDependency || (moveUp && boxDependenciesList.SelectedIndex == 1))
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You can't move the base file.");

                return;
            }

            List<AssetsFileDependency> deps = dependencyMap[selectedFile];

            if (!moveUp && boxDependenciesList.SelectedIndex == deps.Count)
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "You can't move down any further.");

                return;
            }

            int originalDependencyCount = selectedFile.file.dependencies.dependencyCount;
            int moveUpCheckOffset = moveUp ? 1 : 0; // if moving up, don't allow moving the next item either
            if (!askedAboutMoving && boxDependenciesList.SelectedIndex <= originalDependencyCount + moveUpCheckOffset)
            {
                bool shouldContinue = await ShowMoveConfirmationDialog();
                if (!shouldContinue)
                    return;

                askedAboutMoving = true;
            }

            AssetsFileDependency dep = dependency.dependency!;
            int depIndex = deps.IndexOf(dep);
            int moveUpOffset = moveUp ? -1 : 1;

            dependencyMap[selectedFile].RemoveAt(depIndex);
            dependencyMap[selectedFile].Insert(depIndex + moveUpOffset, dep);
            dependenciesModified.Add(selectedFile);

            UpdateListBox();
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(new HashSet<AssetsFileInstance>());
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            foreach (AssetsFileInstance file in dependenciesModified)
            {
                List<AssetsFileDependency> deps = dependencyMap[file];
                file.file.dependencies.dependencies = deps;
                file.file.dependencies.dependencyCount = deps.Count;
            }

            Close(dependenciesModified);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnAdd.IsEnabled = enabled;
            btnRemove.IsEnabled = enabled;
            btnMoveUp.IsEnabled = enabled;
            btnMoveDown.IsEnabled = enabled;
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateListBox();
        }

        private AssetsFileInstance? GetSelectedAssetsFile()
        {
            DependencyComboBoxItem? selectedItem = (DependencyComboBoxItem?)cbxFiles.SelectedItem;
            AssetsFileInstance? selectedFile = selectedItem?.file;

            return selectedFile;
        }

        private DependencyListBoxItem? GetSelectedDependency()
        {
            return (DependencyListBoxItem?)boxDependenciesList.SelectedItem;
        }

        private async Task<bool> ShowMoveConfirmationDialog()
        {
            var result = await MessageBoxUtil.ShowDialog(this, "Warning",
                "Are you sure you want to (re)move this dependency? This function will not automatically remap\n" + 
                "the file ids in the assets in this file. Use only if you know what you're doing.", ButtonEnum.YesNo);

            return result == ButtonResult.Yes;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private class DependencyComboBoxItem
        {
            public string text;
            public AssetsFileInstance? file;

            public DependencyComboBoxItem(string text, AssetsFileInstance? file)
            {
                this.text = text;
                this.file = file;
            }

            public override string ToString()
            {
                return text;
            }
        }

        private class DependencyListBoxItem
        {
            public int index;
            public AssetsFileInstance? file;
            public AssetsFileDependency? dependency;
            public bool isDependency;

            public DependencyListBoxItem(int index, AssetsFileInstance? file)
            {
                this.index = index;
                this.file = file;
                isDependency = false;
            }

            public DependencyListBoxItem(int index, AssetsFileDependency? dependency)
            {
                this.index = index;
                this.dependency = dependency;
                isDependency = true;
            }

            public override string ToString()
            {
                if (isDependency && dependency != null)
                {
                    if (dependency.assetPath != string.Empty)
                        return $"{index} - {dependency.assetPath}";
                    else
                        return $"{index} - {dependency.guid.mostSignificant:x16}{dependency.guid.leastSignificant:x16}";
                }
                else if (!isDependency && file != null)
                    return $"{index} - {file.name}";
                else
                    return $"{index} - ???";
            }
        }
    }
}
