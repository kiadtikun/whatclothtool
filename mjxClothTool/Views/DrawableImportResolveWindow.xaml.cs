using mjxClothTool.Helpers;
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
        public string FileName => Path.GetFileName(FilePath);
        public bool RequiresGender { get; }
        public bool RequiresDrawableType { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
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
            bool requiresDrawableType)
        {
            FilePath = filePath;
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
        private bool _isApplyingBulkUpdate;
        private CancellationTokenSource _previewLoad;
        private DrawableImportResolveItem _previewedItem;
        private double _lastPreviewMouseX;

        public IEnumerable<string> GenderTypes => DrawableImportResolveItem.GenderOptions.Keys;
        public List<string> AssetTypes { get; set; } = ["Component", "Prop"];
        public ObservableCollection<DrawableImportResolveItem> Items { get; set; } = [];
        internal Dictionary<string, DrawableImportGender> SelectedGenders { get; private set; } = [];
        public Dictionary<string, (bool IsProp, int DrawableType)> SelectedDrawableTypes { get; private set; } = [];

        public Visibility GenderTextVisibility => _showGender ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DrawableTextVisibility => _showDrawableProperties ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GenderControlsVisibility => _showGender ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DrawableControlsVisibility => _showDrawableProperties ? Visibility.Visible : Visibility.Collapsed;
        public IEnumerable<DrawableImportResolveItem> VisibleItems => ShowResolvedItems
            ? Items
            : Items.Where(x => !x.IsResolved);
        public string HeaderText
        {
            get
            {
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
                if (_reviewDroppedFiles)
                {
                    return "Click a file name to preview it. Check files to apply Gender or Drawable Component before import.";
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

                return $"Click a file name to preview it. Check files to apply {string.Join(" and ", parts)}. Use Show resolved to see resolved files.";
            }
        }
        public string SelectionSummary => $"{Items.Count(x => x.IsSelected)} checked, {Items.Count(x => x.IsResolved)} of {Items.Count} resolved";

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
                    OnPropertyChanged(nameof(VisibleItems));
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
            SelectedGender != null && DrawableImportResolveItem.GenderOptions.TryGetValue(SelectedGender, out var gender)
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
        public bool IsSubmitEnabled => Items.Count > 0 && (Items.All(x => x.IsResolved) || CanResolveSingleUnresolvedItem());

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
            ShowResolvedItems = reviewDroppedFiles;

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
                item.IsSelected = reviewDroppedFiles;
                item.PropertyChanged += ImportItem_PropertyChanged;
                Items.Add(item);
            }

            DataContext = this;

            if (reviewDroppedFiles)
            {
                SelectedAssetType = "Component";
                SetDrawableTypeOptions(SelectedAssetType);
                OnItemsChanged();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ShowPreviewAsync(VisibleItems.FirstOrDefault());
        }

        private async void PreviewItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: DrawableImportResolveItem item })
            {
                await ShowPreviewAsync(item);
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
                var isProp = item.IsProp ?? IsProp;
                var model = await Task.Run(() => DrawableImportPreview.Load(item.FilePath, isProp, load.Token), load.Token);
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

            var targetItems = GetControlTargetItems()
                .Where(x => x.RequiresGender)
                .ToList();

            _isApplyingBulkUpdate = true;
            foreach (var item in targetItems)
            {
                item.SetGender(gender);
            }

            foreach (var item in targetItems)
            {
                item.IsSelected = !item.IsResolved;
            }
            _isApplyingBulkUpdate = false;

            OnItemsChanged();
        }

        private void ApplyDrawable_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            if (SelectedDrawableType == null)
            {
                return;
            }

            var isProp = IsProp;
            var drawableType = SelectedDrawableType;
            var targetItems = GetControlTargetItems()
                .Where(x => x.RequiresDrawableType)
                .ToList();

            _isApplyingBulkUpdate = true;
            foreach (var item in targetItems)
            {
                item.SetDrawableType(isProp, drawableType);
            }

            foreach (var item in targetItems)
            {
                item.IsSelected = !item.IsResolved;
            }
            _isApplyingBulkUpdate = false;

            OnItemsChanged();
        }

        private void Select_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
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

            item.IsSelected = !item.IsResolved;
            OnItemsChanged();
        }

        private void Cancel_MyBtnClickEvent(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            var visibleItems = VisibleItems.ToHashSet();

            _isApplyingBulkUpdate = true;
            foreach (var item in Items)
            {
                item.IsSelected = visibleItems.Contains(item);
            }
            _isApplyingBulkUpdate = false;

            OnItemsChanged();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in VisibleItems)
            {
                item.IsSelected = false;
            }

            OnItemsChanged();
        }

        private void ImportItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DrawableImportResolveItem.IsSelected) ||
                e.PropertyName == nameof(DrawableImportResolveItem.IsResolved))
            {
                OnItemsChanged();
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
                await ShowPreviewAsync(VisibleItems.FirstOrDefault());
            }
        }

        private void OnItemsChanged()
        {
            if (!_isApplyingBulkUpdate)
            {
                SyncControlSelectionsFromSingleTarget();
            }

            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(VisibleItems));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(IsGenderApplyEnabled));
            OnPropertyChanged(nameof(IsDrawableApplyEnabled));
            OnPropertyChanged(nameof(IsSubmitEnabled));
            OnPropertyChanged(nameof(GenderControlsVisibility));
            OnPropertyChanged(nameof(DrawableControlsVisibility));
        }

        private List<DrawableImportResolveItem> GetControlTargetItems()
        {
            var selectedItems = Items.Where(x => x.IsSelected).ToList();
            if (selectedItems.Count > 0)
            {
                return selectedItems;
            }

            var unresolvedItems = Items.Where(x => !x.IsResolved).ToList();
            return unresolvedItems.Count == 1 ? unresolvedItems : [];
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
