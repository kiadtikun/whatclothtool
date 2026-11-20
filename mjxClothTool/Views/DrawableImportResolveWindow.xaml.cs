using mjxClothTool.Helpers;
using mjxClothTool.Models.Drawable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace mjxClothTool.Views
{
    [Flags]
    internal enum DrawableImportGender
    {
        None = 0,
        Female = 1,
        Male = 2,
        Both = Female | Male
    }

    public class DrawableImportResolveItem : INotifyPropertyChanged
    {
        internal static readonly IReadOnlyDictionary<string, DrawableImportGender> GenderOptions =
            new Dictionary<string, DrawableImportGender>
            {
                ["Female"] = DrawableImportGender.Female,
                ["Male"] = DrawableImportGender.Male,
                ["Male/Female"] = DrawableImportGender.Both
            };

        public event PropertyChangedEventHandler PropertyChanged;

        public string FilePath { get; }
        public string FileName => ProjectDrawable?.Name ?? Path.GetFileName(FilePath);
        internal GDrawable ProjectDrawable { get; }
        internal bool HasProjectChange => ProjectDrawable != null &&
            (Gender != FromDetectedGender(ProjectDrawable.Sex) ||
             ProjectDrawable.IsProp != IsProp || ProjectDrawable.TypeNumeric != DrawableType);
        public bool RequiresGender { get; }
        public bool RequiresDrawableType { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPreviewed;
        public bool IsPreviewed
        {
            get => _isPreviewed;
            internal set
            {
                if (_isPreviewed != value)
                {
                    _isPreviewed = value;
                    OnPropertyChanged();
                }
            }
        }

        internal DrawableImportGender Gender { get; private set; }
        public bool? IsProp { get; private set; }
        public int? DrawableType { get; private set; }
        public string DrawableTypeName { get; private set; }

        public bool HasGender => Gender != DrawableImportGender.None;
        public bool HasDrawableType => DrawableType.HasValue;
        public bool IsResolved =>
            (!RequiresGender || HasGender) &&
            (!RequiresDrawableType || HasDrawableType);

        public string GenderText => HasGender
            ? GenderOptions.First(option => option.Value == Gender).Key
            : "Gender not set";
        public string DrawableText => HasDrawableType
            ? $"{(IsProp == true ? "Prop" : "Component")} / {DrawableTypeName}"
            : "Properties not set";

        public DrawableImportResolveItem(
            string filePath,
            Enums.SexType? gender,
            (bool IsProp, int DrawableType)? drawableType,
            bool requiresGender,
            bool requiresDrawableType,
            GDrawable projectDrawable = null)
        {
            FilePath = filePath;
            ProjectDrawable = projectDrawable;
            Gender = FromDetectedGender(gender);
            RequiresGender = requiresGender;
            RequiresDrawableType = requiresDrawableType;

            if (drawableType.HasValue)
            {
                SetDrawableType(drawableType.Value.IsProp, drawableType.Value.DrawableType);
            }
        }

        internal void SetGender(DrawableImportGender gender)
        {
            Gender = gender;
            OnResolvedPropertiesChanged();
        }

        public void SetDrawableType(bool isProp, string drawableTypeName)
        {
            IsProp = isProp;
            DrawableTypeName = drawableTypeName;
            DrawableType = EnumHelper.GetValue(drawableTypeName, isProp);
            OnResolvedPropertiesChanged();
        }

        private void SetDrawableType(bool isProp, int drawableType)
        {
            IsProp = isProp;
            DrawableType = drawableType;
            DrawableTypeName = EnumHelper.GetName(drawableType, isProp);
            OnResolvedPropertiesChanged();
        }

        internal static DrawableImportGender FromDetectedGender(Enums.SexType? gender)
        {
            return gender switch
            {
                Enums.SexType.female => DrawableImportGender.Female,
                Enums.SexType.male => DrawableImportGender.Male,
                _ => DrawableImportGender.None
            };
        }

        private void OnResolvedPropertiesChanged()
        {
            OnPropertyChanged(nameof(HasGender));
            OnPropertyChanged(nameof(HasDrawableType));
            OnPropertyChanged(nameof(IsResolved));
            OnPropertyChanged(nameof(GenderText));
            OnPropertyChanged(nameof(DrawableText));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class DrawableImportResolveWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly bool _showGender;
        private readonly bool _showDrawableProperties;
        private readonly bool _reviewDroppedFiles;
        private readonly bool _editProjectDrawables;
        private bool _isApplyingBulkUpdate;
        private CancellationTokenSource _previewLoad;
        private DrawableImportResolveItem _previewedItem;
        private double _lastPreviewMouseX;

        public IEnumerable<string> GenderTypes => DrawableImportResolveItem.GenderOptions
            .Where(option => !_editProjectDrawables || option.Value != DrawableImportGender.Both)
            .Select(option => option.Key);
        public List<string> AssetTypes { get; set; } = ["Component", "Prop"];
        public ObservableCollection<DrawableImportResolveItem> Items { get; } = [];
        internal Dictionary<string, DrawableImportGender> SelectedGenders { get; private set; } = [];
        public Dictionary<string, (bool IsProp, int DrawableType)> SelectedDrawableTypes { get; private set; } = [];

        public Visibility GenderTextVisibility => _showGender ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DrawableTextVisibility => _showDrawableProperties ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GenderControlsVisibility => _showGender ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DrawableControlsVisibility => _showDrawableProperties ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ImportActionsVisibility => _editProjectDrawables ? Visibility.Collapsed : Visibility.Visible;
        public string SubmitLabel => _editProjectDrawables ? "Save changes" : "Continue";
        public string ListName => _editProjectDrawables ? "Selected project drawables" : "Drawables to import";
        public ICollectionView VisibleItems { get; }
        public string HeaderText
        {
            get
            {
                if (_editProjectDrawables)
                    return "Change drawable type";

                if (_reviewDroppedFiles)
                {
                    return "Review dropped drawable files";
                }

                var unresolvedCount = Items.Count(x => !x.IsResolved);
                return unresolvedCount == 1
                    ? "Resolve 1 drawable before import"
                    : $"Resolve {unresolvedCount} drawables before import";
            }
        }
        public string HelpText
        {
            get
            {
                if (_editProjectDrawables)
                    return "Tick items or hold Shift and click rows to select a range. Click a row to preview it, then apply a gender or type.";

                if (_reviewDroppedFiles)
                {
                    return "Tick items to apply changes. Click a row to preview it. Hold Shift and click another row to select a range.";
                }

                var parts = new List<string>();
                if (_showGender)
                {
                    parts.Add("gender");
                }

                if (_showDrawableProperties)
                {
                    parts.Add("drawable properties");
                }

                return $"Tick items or hold Shift and click rows to select a range, then apply {string.Join(" and ", parts)}. Click a row to preview it.";
            }
        }
        public string SelectionSummary => _editProjectDrawables
            ? $"{Items.Count(item => item.IsChecked)} selected, {Items.Count(item => item.HasProjectChange)} with changes"
            : $"{Items.Count(item => item.IsChecked)} selected, {Items.Count(x => x.IsResolved)} of {Items.Count} resolved";

        private bool _showResolvedItems;
        public bool ShowResolvedItems
        {
            get => _showResolvedItems;
            set
            {
                if (_showResolvedItems != value)
                {
                    _showResolvedItems = value;
                    OnPropertyChanged();
                    VisibleItems.Refresh();
                }
            }
        }

        private string _selectedGender;
        public string SelectedGender
        {
            get => _selectedGender;
            set
            {
                if (_selectedGender != value)
                {
                    _selectedGender = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGenderApplyEnabled));
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        private DrawableImportGender PendingGender =>
            SelectedGender != null && DrawableImportResolveItem.GenderOptions.TryGetValue(SelectedGender, out var gender) &&
            (!_editProjectDrawables || gender != DrawableImportGender.Both)
                ? gender
                : DrawableImportGender.None;

        private string _selectedAssetType;
        public string SelectedAssetType
        {
            get => _selectedAssetType;
            set
            {
                if (_selectedAssetType != value)
                {
                    _selectedAssetType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsProp));
                    OnPropertyChanged(nameof(IsDrawableApplyEnabled));
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool IsProp => SelectedAssetType == "Prop";
        public bool IsAssetTypeSelected => DrawableTypes.Count > 0;
        public bool IsGenderApplyEnabled => PendingGender != DrawableImportGender.None && GetControlTargetItems().Any(x => x.RequiresGender);
        public bool IsDrawableApplyEnabled => SelectedDrawableType != null && GetControlTargetItems().Any(x => x.RequiresDrawableType);
        public bool IsSubmitEnabled => _editProjectDrawables
            ? Items.Any(item => item.HasProjectChange)
            : Items.Count > 0 && (Items.All(x => x.IsResolved) || CanResolveSingleUnresolvedItem());

        internal IEnumerable<(GDrawable Drawable, Enums.SexType Sex, bool IsProp, int Type)> ProjectChanges =>
            Items.Where(item => item.HasProjectChange)
                .Select(item => (item.ProjectDrawable,
                    item.Gender == DrawableImportGender.Female ? Enums.SexType.female : Enums.SexType.male,
                    item.IsProp.Value, item.DrawableType.Value));

        private List<string> _drawableTypes = [];
        public List<string> DrawableTypes
        {
            get => _drawableTypes;
            set
            {
                if (_drawableTypes != value)
                {
                    _drawableTypes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAssetTypeSelected));
                    OnPropertyChanged(nameof(IsDrawableApplyEnabled));
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        private string _selectedDrawableType;
        public string SelectedDrawableType
        {
            get => _selectedDrawableType;
            set
            {
                if (_selectedDrawableType != value)
                {
                    _selectedDrawableType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDrawableApplyEnabled));
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public DrawableImportResolveWindow(
            IEnumerable<string> paths,
            Dictionary<string, Enums.SexType?> detectedGenders = null,
            Dictionary<string, (bool IsProp, int DrawableType)?> detectedDrawableTypes = null,
            bool showGender = false,
            bool showDrawableProperties = true,
            bool reviewDroppedFiles = false)
        {
            InitializeComponent();

            _showGender = showGender;
            _showDrawableProperties = showDrawableProperties;
            _reviewDroppedFiles = reviewDroppedFiles;
            _showResolvedItems = reviewDroppedFiles;

            foreach (var path in paths)
            {
                Enums.SexType? gender = null;
                (bool IsProp, int DrawableType)? drawableType = null;
                detectedGenders?.TryGetValue(path, out gender);
                detectedDrawableTypes?.TryGetValue(path, out drawableType);

                var item = new DrawableImportResolveItem(
                    path,
                    gender,
                    drawableType,
                    _showGender,
                    _showDrawableProperties);
                item.PropertyChanged += ImportItem_PropertyChanged;
                Items.Add(item);
            }

            VisibleItems = new ListCollectionView(Items)
            {
                Filter = item => ShowResolvedItems || !((DrawableImportResolveItem)item).IsResolved
            };
            DataContext = this;

            if (reviewDroppedFiles)
            {
                SelectedAssetType = "Component";
                SetDrawableTypeOptions(SelectedAssetType);
                OnItemsChanged();
            }
        }

        internal DrawableImportResolveWindow(IReadOnlyList<GDrawable> drawables)
        {
            InitializeComponent();
            _editProjectDrawables = true;
            _showGender = true;
            _showDrawableProperties = true;
            _showResolvedItems = true;
            Title = "Change drawable type";

            foreach (var drawable in drawables)
            {
                var item = new DrawableImportResolveItem(drawable.FullFilePath, drawable.Sex,
                    (drawable.IsProp, drawable.TypeNumeric), true, true, drawable) { IsChecked = true };
                item.PropertyChanged += ImportItem_PropertyChanged;
                Items.Add(item);
            }

            VisibleItems = new ListCollectionView(Items);
            DataContext = this;
            SelectedAssetType = drawables.FirstOrDefault()?.IsProp == true ? "Prop" : "Component";
            SetDrawableTypeOptions(SelectedAssetType);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_editProjectDrawables)
                ImportList.SelectAll();
            else if (ImportList.Items.Count > 0)
            {
                ImportList.SelectedIndex = 0;
                ((DrawableImportResolveItem)ImportList.SelectedItem).IsChecked = true;
            }
            await ShowPreviewAsync(ImportList.SelectedItem as DrawableImportResolveItem);
        }

        private async void ImportRow_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender == e.NewFocus &&
                sender is ListBoxItem { Content: DrawableImportResolveItem item } &&
                item != _previewedItem)
            {
                await ShowPreviewAsync(item);
            }
        }

        private async void ImportItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: DrawableImportResolveItem item } checkBox)
                return;

            // Move the native Shift-selection anchor without changing the checked items.
            _isApplyingBulkUpdate = true;
            try
            {
                (ItemsControl.ContainerFromElement(ImportList, checkBox) as ListBoxItem)?.Focus();
            }
            finally
            {
                _isApplyingBulkUpdate = false;
            }

            if (item != _previewedItem)
                await ShowPreviewAsync(item);
        }

        private void ImportItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DrawableImportResolveItem.IsChecked) && !_isApplyingBulkUpdate)
            {
                SyncControlSelectionsFromSingleTarget();
                NotifySelectionChanged();
            }
        }

        private async Task ShowPreviewAsync(DrawableImportResolveItem item)
        {
            _previewLoad?.Cancel();
            _previewLoad = null;
            if (_previewedItem != null)
            {
                _previewedItem.IsPreviewed = false;
            }
            _previewedItem = item;

            PreviewTitle.Text = item == null ? "3D Preview" : $"3D Preview — {item.FileName}";
            PreviewTitle.ToolTip = item?.FilePath;
            PreviewModel.Content = null;
            PreviewRotation.Angle = 0;
            PreviewHint.Visibility = Visibility.Collapsed;
            PreviewStatus.Visibility = Visibility.Visible;
            if (item == null)
            {
                PreviewStatus.Text = Items.Count == 0 ? "No drawables to preview." : "Choose a file to preview.";
                return;
            }

            item.IsPreviewed = true;
            PreviewStatus.Text = "Loading preview…";
            using var load = new CancellationTokenSource();
            _previewLoad = load;
            try
            {
                var isProp = item.ProjectDrawable?.IsProp ?? item.IsProp ?? IsProp;
                var texture = item.ProjectDrawable?.Textures
                    .FirstOrDefault(texture => Path.GetExtension(texture.FilePath)?.Equals(".ytd", StringComparison.OrdinalIgnoreCase) == true);
                var texturePath = texture?.FullFilePath;
                var model = await Task.Run(() => DrawableImportPreview.Load(item.FilePath, isProp, load.Token, texturePath), load.Token);
                if (load.IsCancellationRequested)
                {
                    return;
                }

                PreviewModel.Content = model;
                PreviewHint.Visibility = Visibility.Visible;
                PreviewStatus.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) when (load.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!load.IsCancellationRequested)
                {
                    PreviewStatus.Text = "Unable to preview this drawable.";
                    LogHelper.Log($"Unable to preview '{item.FileName}': {ex.Message}", LogType.Warning);
                }
            }
            finally
            {
                if (_previewLoad == load)
                {
                    _previewLoad = null;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _previewLoad?.Cancel();
            base.OnClosed(e);
        }

        private void PreviewViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height > 0 && e.NewSize.Width > 0)
            {
                PreviewCamera.Width = 2.15 * Math.Max(1, e.NewSize.Width / e.NewSize.Height);
            }
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PreviewModel.Content != null && sender is UIElement surface && surface.CaptureMouse())
            {
                _lastPreviewMouseX = e.GetPosition(surface).X;
            }
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is UIElement { IsMouseCaptured: true } surface && e.LeftButton == MouseButtonState.Pressed)
            {
                var x = e.GetPosition(surface).X;
                PreviewRotation.Angle = (PreviewRotation.Angle + x - _lastPreviewMouseX) % 360;
                _lastPreviewMouseX = x;
            }
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement { IsMouseCaptured: true } surface)
            {
                surface.ReleaseMouseCapture();
            }
        }

        private void AssetType_IsUpdated(object sender, Controls.UpdatedEventArgs e)
        {
            var newValue = e.DependencyPropertyChangedEventArgs.NewValue;
            if (newValue == null)
            {
                return;
            }

            SelectedAssetType = newValue as string;
            SelectedDrawableType = null;
            SetDrawableTypeOptions(SelectedAssetType);
        }

        private void ApplyGender_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            var gender = PendingGender;
            if (gender == DrawableImportGender.None)
            {
                return;
            }

            ApplyToSelection(GetControlTargetItems().Where(item => item.RequiresGender),
                item => item.SetGender(gender));
        }

        private void ApplyDrawable_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            if (SelectedDrawableType == null)
            {
                return;
            }

            var isProp = IsProp;
            var drawableType = SelectedDrawableType;
            ApplyToSelection(GetControlTargetItems().Where(item => item.RequiresDrawableType),
                item => item.SetDrawableType(isProp, drawableType));
        }

        private void ApplyToSelection(IEnumerable<DrawableImportResolveItem> items, Action<DrawableImportResolveItem> apply)
        {
            _isApplyingBulkUpdate = true;
            try
            {
                foreach (var item in items)
                {
                    apply(item);
                    if (!_editProjectDrawables && item.IsResolved)
                    {
                        item.IsChecked = false;
                        ImportList.SelectedItems.Remove(item);
                    }
                }

                if (!ShowResolvedItems)
                {
                    VisibleItems.Refresh();
                }
            }
            finally
            {
                _isApplyingBulkUpdate = false;
            }

            OnItemsChanged();
        }

        private void Select_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            if (_editProjectDrawables)
            {
                if (IsSubmitEnabled)
                    DialogResult = true;
                return;
            }

            ResolveSingleUnresolvedItemIfPossible();

            if (!IsSubmitEnabled)
            {
                return;
            }

            if (_showGender)
            {
                SelectedGenders = Items.ToDictionary(
                    item => item.FilePath,
                    item => item.Gender);
            }

            if (_showDrawableProperties)
            {
                SelectedDrawableTypes = Items.ToDictionary(
                    item => item.FilePath,
                    item => (item.IsProp == true, item.DrawableType.GetValueOrDefault()));
            }

            DialogResult = true;
            Close();
        }

        private bool CanResolveSingleUnresolvedItem()
        {
            var unresolvedItems = Items.Where(x => !x.IsResolved).ToList();
            if (unresolvedItems.Count != 1)
            {
                return false;
            }

            var item = unresolvedItems[0];
            var canResolveGender = !item.RequiresGender || item.HasGender || PendingGender != DrawableImportGender.None;
            var canResolveDrawableType = !item.RequiresDrawableType || item.HasDrawableType || SelectedDrawableType != null;

            return canResolveGender && canResolveDrawableType;
        }

        private void ResolveSingleUnresolvedItemIfPossible()
        {
            if (!CanResolveSingleUnresolvedItem())
            {
                return;
            }

            var item = Items.First(x => !x.IsResolved);

            if (item.RequiresGender && !item.HasGender)
            {
                item.SetGender(PendingGender);
            }

            if (item.RequiresDrawableType && !item.HasDrawableType && SelectedDrawableType != null)
            {
                item.SetDrawableType(IsProp, SelectedDrawableType);
            }
        }

        private void Cancel_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetCheckedItems(true);
            ImportList.SelectAll();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetCheckedItems(false);
            ImportList.UnselectAll();
        }

        private void SetCheckedItems(bool isChecked)
        {
            _isApplyingBulkUpdate = true;
            try
            {
                var targets = isChecked ? ImportList.Items.Cast<DrawableImportResolveItem>() : Items;
                foreach (var item in targets)
                    item.IsChecked = isChecked;
            }
            finally
            {
                _isApplyingBulkUpdate = false;
            }
            SyncControlSelectionsFromSingleTarget();
            NotifySelectionChanged();
        }

        private void ImportList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isApplyingBulkUpdate &&
                (ImportList.SelectedItems.Count > 1 || (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
            {
                _isApplyingBulkUpdate = true;
                try
                {
                    foreach (DrawableImportResolveItem item in ImportList.SelectedItems)
                        item.IsChecked = true;
                }
                finally
                {
                    _isApplyingBulkUpdate = false;
                }
                SyncControlSelectionsFromSingleTarget();
                NotifySelectionChanged();
            }
        }

        private async void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: DrawableImportResolveItem item } || !Items.Remove(item))
            {
                return;
            }

            item.PropertyChanged -= ImportItem_PropertyChanged;
            OnItemsChanged();
            if (_previewedItem == item)
            {
                if (ImportList.SelectedItem == null && ImportList.Items.Count > 0)
                {
                    ImportList.SelectedIndex = 0;
                }
                await ShowPreviewAsync(ImportList.SelectedItem as DrawableImportResolveItem);
            }
        }

        private void OnItemsChanged()
        {
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(IsSubmitEnabled));
            if (!_editProjectDrawables)
                SyncControlSelectionsFromSingleTarget();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(IsGenderApplyEnabled));
            OnPropertyChanged(nameof(IsDrawableApplyEnabled));
        }

        private List<DrawableImportResolveItem> GetControlTargetItems()
        {
            return Items.Where(item => item.IsChecked).ToList();
        }

        private bool ShouldShowGenderControls()
        {
            return _showGender;
        }

        private bool ShouldShowDrawableControls()
        {
            return _showDrawableProperties;
        }

        private void SetDrawableTypeOptions(string assetType)
        {
            DrawableTypes = assetType switch
            {
                "Component" => EnumHelper.GetDrawableTypeList(),
                "Prop" => EnumHelper.GetPropTypeList(),
                _ => [],
            };
        }

        private void SyncControlSelectionsFromSingleTarget()
        {
            var targetItems = GetControlTargetItems();
            if (targetItems.Count != 1)
            {
                return;
            }

            var item = targetItems[0];
            if (_showGender && item.HasGender)
            {
                SelectedGender = item.GenderText;
            }

            if (_showDrawableProperties && item.HasDrawableType)
            {
                var assetType = item.IsProp == true ? "Prop" : "Component";
                SelectedAssetType = assetType;
                SetDrawableTypeOptions(assetType);
                SelectedDrawableType = item.DrawableTypeName;
            }
        }

        private static bool NeedsGender(DrawableImportResolveItem item)
        {
            return item.RequiresGender && !item.HasGender;
        }

        private static bool NeedsDrawableType(DrawableImportResolveItem item)
        {
            return item.RequiresDrawableType && !item.HasDrawableType;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
