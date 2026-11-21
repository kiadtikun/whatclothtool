using CodeWalker.GameFiles;
using Microsoft.Win32;
using mjxClothTool.Helpers;
using mjxClothTool.Models.Drawable;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace mjxClothTool.Views;

public partial class PropConverterWindow : Window
{
    private string _sourcePath, _rigPath;
    private PropMeshParts.Part[] _pieces = [];
    private bool _loadingPieces;
    private readonly Queue<ProjectFile> _review = new();
    private string _reviewFolder;
    private bool _reviewMixed;
    private int _reviewSaved, _reviewTotal;
    private string[] _textures = [];
    private RigidPropConverter.Result _preview;
    private bool _fitCamera = true;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly SemaphoreSlim _work = new(1);
    private FreemodePreview _character;
    private Skeleton _characterRig;
    private const string AutoRigLabel = "Auto (local GTA V) · choose override…";
    private int _revision, _characterRevision;
    private string _gameFolder;
    private Point3D _focus;
    private ProjectFile _activeFile;

    private sealed class ProjectFile
    {
        public string Label { get; init; }
        public string Path { get; init; }
        public string OriginalPropType { get; init; }
        public int CharacterIndex, SlotIndex;
        public string Rig, Number, Scale = "1", X = "0", Y = "0", Z = "0", Rx = "0", Ry = "0", Rz = "0";
        public PropMeshParts.Part[] Pieces = [];
        public string[] Textures;
    }

    public PropConverterWindow()
    {
        InitializeComponent();
        _refresh.Tick += (_, _) => RefreshAccessory();
        Loaded += (_, _) => { ShowPieceHint(); LoadCharacter(); };
        Closed += (_, _) => { _revision++; _characterRevision++; _refresh.Stop(); };
    }

    internal PropConverterWindow(IReadOnlyList<GDrawable> drawables) : this()
    {
        var slots = Slot.Items.Cast<ComboBoxItem>().Select(item => (string)item.Tag).ToList();
        var files = drawables.Select(d => new ProjectFile
        {
            Label = $"{(d.HasDisplayName ? d.DisplayName + " · " : "")}{d.Name}  ({d.SexName})",
            Path = d.FullFilePath,
            OriginalPropType = d.IsProp ? d.TypeName : null,
            CharacterIndex = d.Sex == Enums.SexType.female ? 1 : 0,
            SlotIndex = d.IsProp ? Math.Max(0, slots.IndexOf(d.TypeName)) : 0,
            Number = d.DisplayNumber,
            Textures = d.Textures.Where(t => string.Equals(Path.GetExtension(t.FilePath), ".ytd", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.TxtNumber).Select(t => t.FullFilePath).ToArray()
        }).ToList();
        ProjectPanel.Visibility = Visibility.Visible;
        Source.Visibility = SourceLabel.Visibility = Visibility.Collapsed;
        ProjectCount.Text = $"Project files ({files.Count})";
        ProjectFiles.ItemsSource = files;
        ProjectFiles.SelectedIndex = 0;
    }

    private void ProjectFileChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveActiveSettings();
        var selected = ProjectFiles.SelectedItems;
        var file = e.AddedItems.OfType<ProjectFile>().LastOrDefault()
            ?? (_activeFile != null && selected.Contains(_activeFile) ? _activeFile : selected.OfType<ProjectFile>().FirstOrDefault());
        ProjectCount.Text = $"Project files ({selected.Count}/{ProjectFiles.Items.Count} selected)";
        Export.Content = selected.Count > 1 ? $"Export selected ({selected.Count})…" : "Export prop…";
        if (file == null)
        {
            _activeFile = null;
            _sourcePath = null;
            ResetPieces([]);
            Mesh.Content = null;
            ClearGizmo();
            InputChanged(this, null);
            return;
        }
        if (file == _activeFile)
        {
            UpdateExportButton();
            return;
        }
        _activeFile = file;
        _sourcePath = file.Path;
        ResetPieces(file.Pieces);
        _fitCamera = true;
        ClearGizmo();
        Mesh.Content = null;
        Character.SelectedIndex = file.CharacterIndex;
        Slot.SelectedIndex = file.SlotIndex;
        (Number.Text, ScaleBox.Text, _rigPath, _textures) = (file.Number, file.Scale, file.Rig, file.Textures);
        (X.Text, Y.Text, Z.Text) = (file.X, file.Y, file.Z);
        (Rx.Text, Ry.Text, Rz.Text) = (file.Rx, file.Ry, file.Rz);
        Rig.IsEnabled = string.Equals(Path.GetExtension(file.Path), ".ydd", StringComparison.OrdinalIgnoreCase);
        Rig.Content = Path.GetFileName(_rigPath) ?? AutoRigLabel;
        Rig.ToolTip = _rigPath;
        UpdateTextureLabel();
        InputChanged(this, null);
    }

    private void SaveActiveSettings()
    {
        if (_activeFile is { } last)
        {
            last.Pieces = _pieces;
            last.CharacterIndex = Character.SelectedIndex;
            last.SlotIndex = Slot.SelectedIndex;
            (last.Number, last.Scale, last.Rig, last.Textures) = (Number.Text, ScaleBox.Text, _rigPath, _textures);
            (last.X, last.Y, last.Z) = (X.Text, Y.Text, Z.Text);
            (last.Rx, last.Ry, last.Rz) = (Rx.Text, Ry.Text, Rz.Text);
        }
    }

    private void UpdateExportButton() => Export.IsEnabled = ProjectFiles.SelectedItems.Count > 1 || _preview != null;

    private void CharacterChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) LoadCharacter();
    }

    private void GameFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select GTA V Legacy installation" };
        if (dlg.ShowDialog(this) != true) return;
        _gameFolder = dlg.FolderName;
        LoadCharacter();
    }

    private async void LoadCharacter()
    {
        int revision = ++_characterRevision;
        var ped = (string)((ComboBoxItem)Character.SelectedItem).Tag;
        var folder = _gameFolder;
        _character = null;
        _characterRig = null;
        Body.Content = null;
        Mesh.Content = null;
        ClearGizmo();
        _fitCamera = true;
        _revision++;
        _refresh.Stop();
        _preview = null;
        UpdateExportButton();
        CharacterStatus.Text = "Loading character…";
        try
        {
            await _work.WaitAsync();
            FreemodePreview character;
            try
            {
                if (revision != _characterRevision) return;
                character = await Task.Run(() => FreemodePreview.Load(folder ?? FreemodePreview.FindGame(), ped));
            }
            finally { _work.Release(); }
            if (revision != _characterRevision) return;
            _character = character;
            _characterRig = character.Rig;
            Body.Content = character.Body;
            Fit(character.Body.Bounds);
            _fitCamera = true;
            CharacterStatus.Text = ped;
        }
        catch (Exception ex)
        {
            if (revision != _characterRevision) return;
            CharacterStatus.Text = ex.Message + " Use GTA folder to choose the installation.";
        }
        InputChanged(this, null);
    }

    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender == AutoPieces || sender == WholeGlasses || sender == Slot) ShowPieceHint();
        _revision++;
        _preview = null;
        UpdateExportButton();
        _refresh.Stop();
        if (_drag != null) return;
        _canMove = false;
        DrawAxes();
        if (_sourcePath != null) _refresh.Start();
        Status.Text = _sourcePath == null ? "Choose a source accessory." : "Updating preview…";
        if (sender == Slot) _fitCamera = true;
    }

    private void Source_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "GTA V accessory (*.ydd;*.ydr)|*.ydd;*.ydr" };
        if (dlg.ShowDialog(this) != true) return;
        SelectSource(dlg.FileName);
    }

    private void SelectSource(string path)
    {
        _sourcePath = path;
        ResetPieces([]);
        Mesh.Content = null;
        ClearGizmo();
        var stemName = Path.GetFileNameWithoutExtension(path).Split('^').Last();
        foreach (ComboBoxItem item in Slot.Items)
            if (stemName.StartsWith(item.Tag + "_", StringComparison.OrdinalIgnoreCase)) Slot.SelectedItem = item;
        if (Path.GetFileName(_sourcePath).StartsWith("mp_f_freemode_01", StringComparison.OrdinalIgnoreCase))
            Character.SelectedIndex = 1;
        else if (Path.GetFileName(_sourcePath).StartsWith("mp_m_freemode_01", StringComparison.OrdinalIgnoreCase))
            Character.SelectedIndex = 0;
        _fitCamera = true;
        _rigPath = null;
        Rig.Content = AutoRigLabel;
        Rig.ToolTip = null;
        Source.Content = Path.GetFileName(_sourcePath);
        Source.ToolTip = _sourcePath;
        Rig.IsEnabled = Path.GetExtension(_sourcePath).Equals(".ydd", StringComparison.OrdinalIgnoreCase);
        _textures = [];
        try
        {
            if (Rig.IsEnabled)
            {
                // Match the complete drawable number, including the variant boundary.
                var stem = Path.GetFileNameWithoutExtension(_sourcePath);
                var prefix = stem.Contains('^') ? stem[..(stem.LastIndexOf('^') + 1)] : "";
                var parts = stem[prefix.Length..].Split('_');
                if (parts.Length >= 2)
                {
                    var start = $"{prefix}{parts[0]}_diff_{parts[1]}_";
                    _textures = Directory.EnumerateFiles(Path.GetDirectoryName(_sourcePath), "*.ytd")
                        .Where(p => Path.GetFileName(p).StartsWith(start, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                }
            }
            else if (File.Exists(Path.ChangeExtension(_sourcePath, ".ytd")))
                _textures = [Path.ChangeExtension(_sourcePath, ".ytd")];
        }
        catch (IOException ex) { Status.Text = ex.Message; }
        catch (UnauthorizedAccessException ex) { Status.Text = ex.Message; }
        UpdateTextureLabel();
        InputChanged(this, null);
    }

    private void ResetPieces(PropMeshParts.Part[] pieces)
    {
        _loadingPieces = true;
        PieceList.ItemsSource = null;
        PieceList.Visibility = Visibility.Collapsed;
        _pieces = pieces;
        ChoosePieces.IsEnabled = string.Equals(Path.GetExtension(_sourcePath), ".ydd", StringComparison.OrdinalIgnoreCase);
        _loadingPieces = false;
        ShowPieceHint();
    }

    private void ShowPieceHint()
    {
        bool glasses = (Slot.SelectedItem as ComboBoxItem)?.Tag as string == "p_eyes";
        bool whole = glasses && WholeGlasses.IsChecked == true;
        WholeGlasses.Visibility = glasses ? Visibility.Visible : Visibility.Collapsed;
        AutoPieces.Visibility = ChoosePieces.Visibility = whole ? Visibility.Collapsed : Visibility.Visible;
        PieceList.Visibility = whole || PieceList.ItemsSource == null ? Visibility.Collapsed : Visibility.Visible;
        if (whole)
        {
            PieceHint.Text = "All meshes and LODs will move together with the head.";
            return;
        }
        PieceHint.Text = _pieces.Length > 0 ? $"{_pieces.Length} selected · rigid prop, High LOD only"
            : PieceList.IsVisible ? "Click to choose pieces manually. Clear the selection to use the mode above." : AutoPieces.IsChecked == true ? "Automatic for multi-bone outfits" : "Whole drawable";
    }

    private async void ChoosePieces_Click(object sender, RoutedEventArgs e)
    {
        var path = _sourcePath;
        try
        {
            var parts = await Task.Run(() => PropMeshParts.List(path));
            if (_sourcePath != path) return;
            _loadingPieces = true;
            PieceList.ItemsSource = parts;
            foreach (var part in parts.Intersect(_pieces)) PieceList.SelectedItems.Add(part);
            PieceList.Visibility = Visibility.Visible;
            ShowPieceHint();
        }
        catch (Exception ex) { if (_sourcePath == path) Status.Text = ex.Message; }
        finally { _loadingPieces = false; }
    }

    private void PieceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPieces) return;
        _pieces = PieceList.SelectedItems.Cast<PropMeshParts.Part>().ToArray();
        ShowPieceHint();
        _fitCamera = true;
        InputChanged(sender, e);
    }

    private void Rig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Ped skeleton (*.yft)|*.yft" };
        if (dlg.ShowDialog(this) != true) return;
        _rigPath = dlg.FileName;
        Rig.Content = Path.GetFileName(_rigPath);
        Rig.ToolTip = _rigPath;
        InputChanged(sender, e);
    }

    private void Textures_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Texture dictionary (*.ytd)|*.ytd", Multiselect = true };
        if (dlg.ShowDialog(this) != true) return;
        _textures = dlg.FileNames.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToArray();
        UpdateTextureLabel();
        InputChanged(sender, e);
    }

    private void UpdateTextureLabel()
    {
        Textures.Content = _textures.Length == 0 ? "Choose YTD files…" : $"{_textures.Length} YTD variation(s) · change…";
        Textures.ToolTip = string.Join(Environment.NewLine, _textures.Select(Path.GetFileName));
    }

    private static float Read(TextBox input) => Read(input.Text);

    private static float Read(string text)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out float value) ||
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return value;
        throw new ArgumentException("Enter a numeric offset, rotation and scale.");
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        _fitCamera = true;
        InputChanged(sender, e);
        RefreshAccessory();
    }

    private async void RefreshAccessory()
    {
        _refresh.Stop();
        int revision = _revision;
        var path = _sourcePath;
        var rig = _rigPath;
        var character = _character;
        var autoRig = _characterRig;
        var texturePaths = _textures;
        var projectPropType = _activeFile?.OriginalPropType;
        var pieces = _pieces;
        bool allGlassesPieces = WholeGlasses.IsChecked == true;
        bool autoSelect = AutoPieces.IsChecked == true;
        try
        {
            if (path == null) return;
            if (!int.TryParse(Number.Text, out int number)) throw new ArgumentException("Enter a drawable number from 0 to 999.");
            if (_textures.Length > 26) throw new ArgumentException("Select up to 26 texture variations (a–z).");
            var slot = Enum.Parse<Enums.PropNumbers>((string)((ComboBoxItem)Slot.SelectedItem).Tag);
            var offset = new Vector3(Read(X), Read(Y), Read(Z));
            var degrees = new Vector3(Read(Rx), Read(Ry), Read(Rz));
            var scale = Read(ScaleBox);
            Status.Text = "Converting accessory…";
            await _work.WaitAsync();
            try
            {
                if (revision != _revision) return;
                var result = await Task.Run(() =>
                {
                    var ydd = new YddFile();
                    RigidPropConverter.Result prop = null;
                    string issue = null;
                    bool local = true;
                    var displaySlot = slot;
                    try
                    {
                        prop = RigidPropConverter.Convert(path, rig, slot, number, offset, degrees, scale, autoRig, projectPropType, pieces, autoSelect, allGlassesPieces);
                        ydd.Load(prop.Data);
                    }
                    catch (Exception ex)
                    {
                        issue = ex.Message;
                        var bytes = File.ReadAllBytes(path);
                        if (Path.GetExtension(path).Equals(".ydr", StringComparison.OrdinalIgnoreCase))
                        {
                            var ydr = new YdrFile();
                            ydr.Load(bytes);
                            ydd.Drawables = [ydr.Drawable];
                        }
                        else
                        {
                            ydd.Load(bytes);
                            var names = new[] { Path.GetFileName(path).Split('^').Last(), ydd.Drawables?.FirstOrDefault()?.Name, projectPropType + "_" };
                            local = names.Any(n => n?.StartsWith("p_", StringComparison.OrdinalIgnoreCase) == true);
                            foreach (var original in new[]
                            {
                                Enums.PropNumbers.p_head,
                                Enums.PropNumbers.p_eyes,
                                Enums.PropNumbers.p_ears,
                                Enums.PropNumbers.p_lwrist,
                                Enums.PropNumbers.p_rwrist
                            })
                                if (names.Any(n => n?.StartsWith(original + "_", StringComparison.OrdinalIgnoreCase) == true))
                                    displaySlot = original;
                        }
                    }
                    TextureDictionary tex = null;
                    string textureIssue = null;
                    if (texturePaths.Length > 0)
                    {
                        try { tex = CWHelper.GetYtdFile(texturePaths[0]).TextureDict; }
                        catch (Exception ex) { textureIssue = $"Texture preview unavailable: {ex.Message}"; }
                    }
                    var model = DrawableImportPreview.Create(ydd, tex, default, false);
                    var solid = SolidAccessory(model);
                    return (prop, issue, local, displaySlot, textureIssue, model, solid);
                });
                if (revision != _revision) return;
                _placement = result.local && character != null ? character.Anchor(result.displaySlot) : Transform3D.Identity;
                if (result.prop == null)
                {
                    var placement = Matrix3D.Identity;
                    placement.Translate(new Vector3D(offset.X, offset.Y, offset.Z));
                    placement.Append(_placement.Value);
                    _placement = new MatrixTransform3D(placement);
                }
                Mesh.Transform = _placement;
                Mesh.Content = result.model;
                Highlight.Content = result.solid;
                Highlight.Transform = Mesh.Transform;
                _canMove = true;
                _bakedOffset = offset;
                if (_fitCamera)
                {
                    Fit(Mesh.Transform.TransformBounds(result.model.Bounds));
                    _fitCamera = false;
                }
                _preview = result.prop;
                DrawAxes();
                UpdateExportButton();
                Status.Text = (_preview == null ? $"Showing source. {result.issue}" : $"{_preview.SourceBinding} → {slot} · {_preview.Name}.ydd")
                    + (result.textureIssue == null ? "" : $" · {result.textureIssue}");
            }
            finally { _work.Release(); }
        }
        catch (Exception ex)
        {
            if (revision == _revision)
            {
                Status.Text = ex.Message;
            }
        }
    }

    private void FitCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (Body.Content != null) Fit(Body.Content.Bounds);
        else if (Mesh.Content != null) Fit(Mesh.Transform.TransformBounds(Mesh.Content.Bounds));
    }

    private void ZoomPreview(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_drag != null) return;
        Camera.Width = Math.Clamp(Camera.Width * Math.Pow(0.85, e.Delta / 120d), 0.001, 100);
        SetViewAngle();
        e.Handled = true;
    }

    private void Fit(Rect3D b)
    {
        _focus = new(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
        Camera.Width = Math.Max(0.001, new Vector3D(b.SizeX,b.SizeY,b.SizeZ).Length * 1.55)
            * Math.Max(1, Viewport.ActualWidth / Math.Max(1, Viewport.ActualHeight));
        SetViewAngle();
    }
    private void AngleChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SetViewAngle();

    private void SetViewAngle()
    {
        if (Camera == null || Angle == null) return;
        double radians = Angle.Value * Math.PI / 180;
        double dist = Math.Max(1, Camera.Width * 2);
        var direction = new Vector3D(Math.Sin(radians), -Math.Cos(radians), 0.25) * dist;
        Camera.Position = _focus + direction;
        Camera.LookDirection = -direction;
        DrawAxes();
    }

    private void NextReview()
    {
        if (!_review.TryPeek(out var file))
        {
            EndReview();
            return;
        }
        ProjectFiles.UnselectAll();
        ProjectFiles.SelectedItem = file;
        ProjectFiles.ScrollIntoView(file);
        ProjectFiles.IsEnabled = false;
        ReviewHint.Visibility = SkipReview.Visibility = StopReview.Visibility = Visibility.Visible;
        ReviewHint.Text = $"{file.Label} · {_review.Count} remaining. Select the accessory pieces, check the preview, then export.";
        Export.Content = "Export and continue";
        ChoosePieces_Click(this, null);
    }

    private void EndReview()
    {
        _review.Clear();
        _reviewFolder = null;
        ProjectFiles.IsEnabled = true;
        ReviewHint.Visibility = SkipReview.Visibility = StopReview.Visibility = Visibility.Collapsed;
        Export.Content = "Export prop…";
        Status.Text = $"Exported {_reviewSaved}/{_reviewTotal} files.";
        UpdateExportButton();
    }

    private void SkipReview_Click(object sender, RoutedEventArgs e)
    {
        _review.Dequeue();
        NextReview();
    }

    private void StopReview_Click(object sender, RoutedEventArgs e) => EndReview();

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        SaveActiveSettings();
        var files = ProjectFiles.SelectedItems.Cast<ProjectFile>().ToArray();
        var preview = _preview;
        var textures = _textures;
        if (files.Length <= 1 && preview == null) return;
        var destination = _reviewFolder;
        bool reviewing = destination != null;
        if (!reviewing)
        {
            var dlg = new OpenFolderDialog { Title = "Choose export folder" };
            if (dlg.ShowDialog(this) != true) return;
            destination = dlg.FolderName;
        }
        else if (_reviewMixed)
            destination = Path.Combine(destination, (string)((ComboBoxItem)Character.SelectedItem).Tag);
        Panel.IsEnabled = false;
        await _work.WaitAsync();
        try
        {
            if (files.Length <= 1)
            {
                var path = await Task.Run(() => RigidPropConverter.Export(preview, textures, destination));
                if (reviewing)
                {
                    _reviewSaved++;
                    _review.Dequeue();
                    NextReview();
                }
                else Status.Text = $"Saved to {path}.";
                return;
            }

            bool autoSelect = AutoPieces.IsChecked == true;
            bool allGlassesPieces = WholeGlasses.IsChecked == true;
            var slots = Slot.Items.Cast<ComboBoxItem>().Select(i => Enum.Parse<Enums.PropNumbers>((string)i.Tag)).ToArray();
            var peds = Character.Items.Cast<ComboBoxItem>().Select(i => (string)i.Tag).ToArray();
            var rigs = new Dictionary<int, Skeleton>();
            if (_characterRig != null) rigs[Character.SelectedIndex] = _characterRig;
            var folder = _gameFolder;
            var errors = new List<string>();
            bool mixed = files.Select(f => f.CharacterIndex).Distinct().Count() > 1;
            foreach (var file in files)
            {
                Status.Text = $"Exporting {file.Label}…";
                try
                {
                    await Task.Run(() =>
                    {
                        if (!int.TryParse(file.Number, out int number)) throw new ArgumentException("Invalid drawable number.");
                        var sex = file.CharacterIndex;
                        if (!rigs.TryGetValue(sex, out var rig) && file.Rig == null)
                        {
                            try { rig = FreemodePreview.LoadRig(folder ?? FreemodePreview.FindGame(), peds[sex]); }
                            catch (Exception) { /* The drawable may have its own skeleton or be a rigid prop. */ }
                            rigs[sex] = rig;
                        }
                        var result = RigidPropConverter.Convert(file.Path, file.Rig, slots[file.SlotIndex], number,
                            new Vector3(Read(file.X), Read(file.Y), Read(file.Z)),
                            new Vector3(Read(file.Rx), Read(file.Ry), Read(file.Rz)), Read(file.Scale), rig, file.OriginalPropType, file.Pieces, autoSelect, allGlassesPieces);
                        var parent = mixed ? Path.Combine(destination, peds[sex]) : destination;
                        RigidPropConverter.Export(result, file.Textures, parent);
                    });
                }
                catch (InvalidDataException ex) when (ex.Message == RigidPropConverter.NeedsPieces) { _review.Enqueue(file); }
                catch (Exception ex) { errors.Add($"{file.Label}: {ex.Message}"); }
            }
            _reviewSaved = files.Length - errors.Count - _review.Count;
            _reviewTotal = files.Length;
            Status.Text = $"Exported {_reviewSaved}/{files.Length} to {destination}.";
            if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Export details");
            if (_review.Count > 0)
            {
                _reviewFolder = destination;
                _reviewMixed = mixed;
                NextReview();
            }
        }
        catch (Exception ex) { Status.Text = ex.Message; }
        finally { _work.Release(); Panel.IsEnabled = true; }
    }
}
