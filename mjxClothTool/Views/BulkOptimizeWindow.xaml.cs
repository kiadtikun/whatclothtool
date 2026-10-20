using mjxClothTool.Helpers;
using mjxClothTool.Models.Drawable;
using mjxClothTool.Models.Texture;
using CodeWalker.Utils;
using System;
using System.Collections.Generic;
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

        var drawables = MainWindow.AddonManager.Addons.SelectMany(addon => addon.Drawables).ToList();
        var textures = drawables.SelectMany(drawable => drawable.Textures).Distinct().ToList();
        var embedded = drawables
            .SelectMany(drawable => drawable.Details?.EmbeddedTextures?.Values ??
                Enumerable.Empty<GTextureEmbedded>())
            .Where(texture => texture != null)
            .Distinct()
            .ToList();
        var total = textures.Count + embedded.Count;
        if (total == 0)
        {
            StatusText.Text = "No textures found in this project.";
            return;
        }

        _isApplying = true;
        ApplyButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        DownscaleCheckBox.IsEnabled = false;
        CompressionCheckBox.IsEnabled = false;
        MipmapComboBox.IsEnabled = false;
        OptimizeProgress.Maximum = total;
        OptimizeProgress.Value = 0;

        var applied = 0;
        var skipped = 0;
        var failed = 0;
        var processed = 0;
        try
        {
            var assetsPath = FileHelper.GetProjectAssetsPath();
            foreach (var texture in textures)
            {
                StatusText.Text = $"Optimizing {processed + 1}/{total} — {texture.DisplayName}";
                if (BulkTextureOptimizationHelper.TryCreateDetails(texture.TxtDetails, downscale, changeCompression, mipmapChoice, out var details))
                {
                    try
                    {
                        // Conversion is CPU-intensive; keep the progress window responsive.
                        var bytes = await Task.Run(() => ImgHelper.Optimize(texture, false, details).GetAwaiter().GetResult());
                        if (bytes == null) throw new InvalidDataException("Texture could not be converted.");

                        var fileName = $"{Guid.NewGuid():N}.ytd";
                        await File.WriteAllBytesAsync(Path.Combine(assetsPath, fileName), bytes);
                        details.Name = texture.DisplayName;
                        details.Type = texture.TxtDetails?.Type;
                        details.Validate();
                        texture.UseOptimizedFile(fileName, details);
                        applied++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        LogHelper.Log($"Could not optimize {texture.DisplayName}: {ex.Message}", LogType.Warning);
                    }
                }
                else skipped++;

                OptimizeProgress.Value = ++processed;
                await Task.Yield();
            }

            foreach (var texture in embedded)
            {
                StatusText.Text = $"Optimizing {processed + 1}/{total} — {texture.Details?.Name}";
                if (BulkTextureOptimizationHelper.TryCreateDetails(texture.Details, downscale, changeCompression, mipmapChoice, out var details))
                {
                    try
                    {
                        if (!await texture.EnsureTextureDataLoadedAsync() || texture.DisplayTextureData == null)
                            throw new InvalidDataException("Embedded texture data is unavailable.");

                        var source = texture.DisplayTextureData;
                        var dds = await Task.Run(() => DDSIO.GetDDSFile(source));
                        var optimized = await Task.Run(() => ImgHelper.Optimize(dds, details).GetAwaiter().GetResult());
                        if (optimized == null) throw new InvalidDataException("Embedded texture could not be converted.");
                        var replacement = await Task.Run(() => DDSIO.GetTexture(optimized));
                        replacement.Name = texture.Details.Name;

                        var fileName = $"{Guid.NewGuid():N}.dds";
                        await File.WriteAllBytesAsync(Path.Combine(assetsPath, fileName), optimized);
                        var originalType = texture.Details.Type;
                        texture.SetReplacementTexture(replacement, fileName);
                        texture.Details.Type = originalType;
                        texture.OptimizeDetails = null;
                        texture.IsOptimizedDuringBuild = false;
                        applied++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        LogHelper.Log($"Could not optimize embedded texture {texture.Details?.Name}: {ex.Message}", LogType.Warning);
                    }
                }
                else skipped++;

                OptimizeProgress.Value = ++processed;
                await Task.Yield();
            }

            if (applied > 0) SaveHelper.SetUnsavedChanges(true);
            StatusText.Text = $"Optimized: {applied}; skipped: {skipped}; failed: {failed}.";
            LogHelper.Log($"Optimized {applied} textures immediately; {skipped} skipped; {failed} failed.");
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
            CloseButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
            DownscaleCheckBox.IsEnabled = true;
            CompressionCheckBox.IsEnabled = true;
            MipmapComboBox.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
