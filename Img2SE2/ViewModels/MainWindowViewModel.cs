using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Img2SE2.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Img2SE2.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //{X}|{Y}|{Z}|{HUE}|{Saturation}|{Value}

    public bool UseDetailBlock { get; set; } = true;

    public string Greeting => "Image to Space Engineers 2";

    public SimpleCommand PickImageCommand { get; }
    public SimpleCommand ConvertImageCommand { get; }
    public SimpleCommand ConvertImageHeightMapCommand { get; }

    public Bitmap? SourceImage { get; set; }

    public bool HasMultiFrames { get; set; }

    IStorageFile? File
    {
        get => _file;
        set
        {
            _file = value;
            OnImageSelected();
        }
    }

    public bool Working => _tasks > 0;

    private int? _tasks = 0;

    private int? Tasks
    {
        get => _tasks;
        set
        {
            _tasks = value;
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Working)));
        }
    }

    private StringBuilder _message = new();

    public string Message => _message.ToString();
    public BlockSize[] BlockSizes { get; } = [BlockSize.Detailing, BlockSize.Small, BlockSize.Large];
    public BlockSize SelectedSize { get; set; } = BlockSize.Detailing;

    private IStorageFile? _file;
    private List<FilePickerFileType> _fileTypeFilter;

    public MainWindowViewModel()
    {
        PickImageCommand = new SimpleCommand(PickImage);
        ConvertImageCommand = new SimpleCommand(ConvertImage) { IsEnabled = false };
        ConvertImageHeightMapCommand = new SimpleCommand(ConvertImageHeightMap) { IsEnabled = false };

        _message.AppendLine("Welcome to Image to Space Engineers 2");

        _fileTypeFilter = new List<FilePickerFileType>
        {
            new("Image Files")
            {
                Patterns = new[]
                {
                    "*.png",
                    "*.jpg",
                    "*.jpeg",
                    "*.bmp",
                    "*.gif",
                    "*.tif",
                    "*.tiff"
                },
                MimeTypes = new[]
                {
                    "image/png",
                    "image/jpeg",
                    "image/bmp",
                    "image/tiff"
                },
                AppleUniformTypeIdentifiers = new[]
                {
                    "public.image"
                }
            }
        };
    }

    private async void OnImageSelected()
    {
        Tasks++;

        try
        {
            ConvertImageCommand.IsEnabled = File != null;

            if (File?.Path.AbsolutePath is { } path)
            {
                var info = await Image.IdentifyAsync(path);
                HasMultiFrames = info.FrameMetadataCollection.Count > 1;
                ConvertImageHeightMapCommand.IsEnabled = !HasMultiFrames;
                if (HasMultiFrames)
                {
                    WriteMessage("Multi-Frame Image detected, using 3D image converter");
                    WriteMessage("Height Map is not supported using this configuration");
                }
            }
            else
            {
                HasMultiFrames = false;
            }

            UpdatePreview();
        }
        finally
        {
            Tasks--;
        }
    }

    public async void UpdatePreview()
    {
        try
        {
            using var task = _file?.OpenReadAsync();
            if (task != null && await task is { } stream)
            {
                if (HasMultiFrames)
                {
                    using (var image = await Image.LoadAsync(await task))
                    {
                        var firstFrame = image.Frames.CloneFrame(1);
                        var frameStream = new MemoryStream();
                        await firstFrame.SaveAsBmpAsync(frameStream);
                        frameStream.Position = 0;
                        SourceImage = new Bitmap(frameStream);
                    }
                }
                else
                {
                    SourceImage = new Bitmap(stream);
                }

                OnPropertyChanged(nameof(SourceImage));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async void PickImage() => File = await PickImageFileAsync("Pick a Image");

    public async Task<IStorageFile?> PickImageFileAsync(string title)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } window
            })
        {
            var options = new FilePickerOpenOptions()
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = _fileTypeFilter
            };

            return (await window.StorageProvider.OpenFilePickerAsync(options)).FirstOrDefault();
        }

        return null;
    }

    string GetNewBlueprintFilePath(string? name = null)
    {
        var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(app, $"SpaceEngineers2\\AppData\\SE1GridsToImport\\{name ?? "image"}.txt");
    }

    public async void ConvertImageHeightMap()
    {
        if (_file == null)
            throw new FileNotFoundException();

        try
        {
            Tasks++;

            var stream = await _file.OpenReadAsync();
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            var colorData = ConvertImageTo2DArray(image);

            Rgba32[,]? heightMap = null;
            if (await PickImageFileAsync("Pick a Height Map") is { } mask)
            {
                var maskStream = await mask.OpenReadAsync();
                using Image<Rgba32> maskImage = Image.Load<Rgba32>(maskStream);
                heightMap = ConvertImageTo2DArray(maskImage, colorData.GetLength(1), colorData.GetLength(0));
            }

            var path = GetNewBlueprintFilePath(File?.Name);
            await using var textFile = System.IO.File.CreateText(path);


            foreach (var line in Convert2DArrayToBlocks(colorData, SelectedSize, heightMap))
                await textFile.WriteLineAsync(line);

            WriteMessage("Blueprint generated successfully");
            WriteMessage($"Path: {path}");
        }
        catch (Exception e)
        {
            WriteMessage(e.Message);
        }
        finally
        {
            Tasks--;
        }
    }

    public async void ConvertImage()
    {
        if (_file == null)
            throw new FileNotFoundException();

        try
        {
            Tasks++;
            var stream = await _file.OpenReadAsync();
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            var path = GetNewBlueprintFilePath(File?.Name);

            if (image.Frames.Count > 1)
            {
                var colorData = ConvertImageTo3DArray(image);

                await using var textFile = System.IO.File.CreateText(path);

                foreach (var line in Convert3DArrayToBlocks(colorData, SelectedSize))
                    await textFile.WriteLineAsync(line);
            }
            else
            {
                var colorData = ConvertImageTo2DArray(image);

                await using var textFile = System.IO.File.CreateText(path);

                foreach (var line in Convert2DArrayToBlocks(colorData, SelectedSize))
                    await textFile.WriteLineAsync(line);
            }

            WriteMessage("Blueprint generated successfully");
            WriteMessage($"Path: {path}");
        }
        catch (Exception e)
        {
            WriteMessage(e.Message);
        }
        finally
        {
            Tasks--;
        }
    }

    static Rgba32[,] ConvertImageTo2DArray(Image<Rgba32> image, int? width = null, int? height = null)
    {
        width ??= image.Width;
        height ??= image.Height;

        if (image.Width != width || image.Height != height)
            image.Mutate(x => x.Resize((int)width, (int)height));


        Rgba32[,] colors = new Rgba32[image.Height, image.Width];

        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            colors[y, x] = image[x, y];

        return colors;
    }

    static Rgba32[,,] ConvertImageTo3DArray(Image<Rgba32> image, int? width = null, int? height = null)
    {
        width ??= image.Width;
        height ??= image.Height;

        if (image.Width != width || image.Height != height)
            image.Mutate(x => x.Resize((int)width, (int)height));


        Rgba32[,,] colors = new Rgba32[image.Height, image.Width, image.Frames.Count];

        for (int z = 0; z < image.Frames.Count; z++)
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            colors[y, x, z] = image.Frames[z][x, y];

        return colors;
    }

    static string[] Convert2DArrayToBlocks(Rgba32[,] colors, BlockSize selectedBlock, Rgba32[,]? heightMap = null)
    {
        var array = new string[colors.Length];

        for (int y = 0; y < colors.GetLength(0); y++)
        for (int x = 0; x < colors.GetLength(1); x++)
        {
            var color = new ColorHSV(colors[y, x]);
            var z = heightMap != null && new ColorHSV(heightMap[y, x]).Value > 0.5 ? 1 : 0;
            array[y * colors.GetLength(1) + x] =
                String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|0|4|1|",
                    selectedBlock.GUID,
                    x * selectedBlock.Size,
                    y * selectedBlock.Size,
                    z * selectedBlock.Size,
                    color.Hue,
                    color.Saturation,
                    color.Value);
        }

        return array;
    }

    static string[] Convert3DArrayToBlocks(Rgba32[,,] colors, BlockSize selectedBlock)
    {
        var array = new string[colors.Length];

        for (int y = 0; y < colors.GetLength(0); y++)
        for (int x = 0; x < colors.GetLength(1); x++)
        for (int z = 0; z < colors.GetLength(2); z++)
        {
            var color = new ColorHSV(colors[y, x, z]);
            array[y * colors.GetLength(1) + x] =
                String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|0|4|1|",
                    selectedBlock.GUID,
                    x * selectedBlock.Size,
                    y * selectedBlock.Size,
                    z * selectedBlock.Size,
                    color.Hue,
                    color.Saturation,
                    color.Value);
        }

        return array;
    }

    public void WriteMessage(string message)
    {
        Console.WriteLine(message);
        _message.AppendLine(message);
        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Message)));
    }
}