using mjxClothTool.Helpers;
using mjxClothTool.Models.Texture;
using CodeWalker.Utils;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace mjxClothTool.Views;

public partial class BulkOptimizeWindow : Window
{
    private bool _isApplying;

    public BulkOptimizeWindow()
    {
        InitializeComponent();
        Closing += (_, e) => e.Cancel = _isApplying;
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var downscale = DownscaleCheckBox.IsChecked == true;
        var changeCompression = CompressionCheckBox.IsChecked == true;
        var mipmapChoice = (MipmapComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Keep original";
        if (!downscale && !changeCompression && mipmapChoice == "Keep original")
        {
            StatusText.Text = "Select at least one optimization option.";
            return;
        }

        _isApplying = true;
        ApplyButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        DownscaleCheckBox.IsEnabled = false;
        CompressionCheckBox.IsEnabled = false;
        MipmapComboBox.IsEnabled = false;
        StatusText.Text = "Reading texture details…";
        OptimizeProgress.IsIndeterminate = true;
        OptimizeProgress.Value = 0;

        var applied = 0;
        var skipped = 0;
        var failed = 0;
        var processed = 0;
        var total = 0;
        try
        {
            var drawables = MainWindow.AddonManager.Addons.SelectMany(addon => addon.Drawables)
                .Where(drawable => !drawable.IsReserved)
                .ToList();
            var textures = drawables.SelectMany(drawable => drawable.Textures).Distinct().ToList();
            await Task.WhenAll(drawables.Select(drawable => drawable.DetailsLoaded));
            var textureMetadata = await Task.WhenAll(textures.Select(texture => texture.DetailsLoaded));
            var embedded = drawables
                .SelectMany(drawable => drawable.Details?.EmbeddedTextures?.Values ??
                    Enumerable.Empty<GTextureEmbedded>())
                .Where(texture => texture != null && (texture.HasOriginalTexture || texture.HasReplacement))
                .Distinct()
                .ToList();

            total = textures.Count + embedded.Count;
            if (total == 0)
            {
                StatusText.Text = "No textures found in this project.";
                return;
            }

            GTextureDetails PlanOptimization(GTextureDetails source, string name)
            {
                if (source is not { Width: > 0, Height: > 0 })
                {
                    failed++;
                    LogHelper.Log($"Could not read texture details for {name}.", LogType.Warning);
                    return null;
                }

                if (BulkTextureOptimizationHelper.TryCreateDetails(source, downscale, changeCompression, mipmapChoice, out var details))
                {
                    details.Type = source.Type;
                    return details;
                }

                skipped++;
                return null;
            }

            var externalJobs = textures.Zip(textureMetadata, (texture, source) =>
                    (Texture: texture, Details: PlanOptimization(source, texture.DisplayName)))
                .Where(job => job.Details != null)
                .ToList();
            var embeddedJobs = embedded.Select(texture =>
                    (Texture: texture, Details: PlanOptimization(texture.Details, texture.Details?.Name ?? texture.OriginalName)))
                .Where(job => job.Details != null)
                .ToList();

            processed = skipped + failed;
            OptimizeProgress.Maximum = total;
            OptimizeProgress.Value = processed;
            OptimizeProgress.IsIndeterminate = false;
            if (processed == total)
            {
                ReportResult();
                return;
            }

            var assetsPath = FileHelper.GetProjectAssetsPath();
            foreach (var (texture, details) in externalJobs)
            {
                StatusText.Text = $"Optimizing {processed + 1}/{total} — {texture.DisplayName}";
                try
                {
                    var bytes = await Task.Run(() => ImgHelper.Optimize(texture, false, details));
                    if (bytes == null) throw new InvalidDataException("Texture could not be converted.");

                    var fileName = $"{Guid.NewGuid():N}.ytd";
                    await File.WriteAllBytesAsync(Path.Combine(assetsPath, fileName), bytes);
                    details.Name = texture.DisplayName;
                    details.Validate();
                    texture.UseOptimizedFile(fileName, details);
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    LogHelper.Log($"Could not optimize {texture.DisplayName}: {ex.Message}", LogType.Warning);
                }

                OptimizeProgress.Value = ++processed;
            }

            foreach (var (texture, details) in embeddedJobs)
            {
                StatusText.Text = $"Optimizing {processed + 1}/{total} — {texture.Details?.Name}";
                try
                {
                    if (!await texture.EnsureTextureDataLoadedAsync() || texture.DisplayTextureData == null)
                        throw new InvalidDataException("Embedded texture data is unavailable.");

                    var source = texture.DisplayTextureData;
                    var (optimized, replacement) = await Task.Run(async () =>
                    {
                        var optimized = await ImgHelper.Optimize(DDSIO.GetDDSFile(source), details);
                        if (optimized == null) throw new InvalidDataException("Embedded texture could not be converted.");
                        return (optimized, DDSIO.GetTexture(optimized));
                    });
                    replacement.Name = texture.Details.Name;

                    var fileName = $"{Guid.NewGuid():N}.dds";
                    await File.WriteAllBytesAsync(Path.Combine(assetsPath, fileName), optimized);
                    texture.SetReplacementTexture(replacement, fileName);
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    LogHelper.Log($"Could not optimize embedded texture {texture.Details?.Name}: {ex.Message}", LogType.Warning);
                }

                OptimizeProgress.Value = ++processed;
            }

            if (applied > 0) SaveHelper.SetUnsavedChanges(true);
            ReportResult();
        }
        catch (Exception ex)
        {
            if (applied > 0) SaveHelper.SetUnsavedChanges(true);
            StatusText.Text = $"Stopped after {processed}/{total}: {ex.Message}";
            LogHelper.Log($"Bulk texture optimization failed: {ex}", LogType.Error);
        }
        finally
        {
            _isApplying = false;
            OptimizeProgress.IsIndeterminate = false;
            CloseButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
            DownscaleCheckBox.IsEnabled = true;
            CompressionCheckBox.IsEnabled = true;
            MipmapComboBox.IsEnabled = true;
        }

        void ReportResult()
        {
            StatusText.Text = $"Optimized: {applied}; skipped: {skipped}; failed: {failed}.";
            LogHelper.Log($"Optimized {applied} textures immediately; {skipped} skipped; {failed} failed.");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
