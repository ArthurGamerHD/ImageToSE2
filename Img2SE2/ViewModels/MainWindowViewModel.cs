using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Collections;
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
            ScheduleTask(OnImageSelected);
        }
    }

    public bool Working => Tasks > 0;

    private int? Tasks
    {
        get => _tasks.Count;
    }

    private StringBuilder _message = new();

    public string Message => _message.ToString();
    public BlockSize[] BlockSizes { get; } = [BlockSize.Detailing, BlockSize.Small, BlockSize.Large];
    public BlockSize SelectedSize { get; set; } = BlockSize.Detailing;

    private IStorageFile? _file;
    private List<FilePickerFileType> _fileTypeFilter;

    private AvaloniaList<Task> _tasks = new();

    public MainWindowViewModel()
    {
        PickImageCommand = new SimpleCommand(PickImage);
        ConvertImageCommand = new SimpleCommand(() => ScheduleTask(ConvertImage)) { IsEnabled = false };
        ConvertImageHeightMapCommand = new SimpleCommand(() => ScheduleTask(ConvertImageHeightMap))
            { IsEnabled = false };

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

    private void ScheduleTask(Func<Task> func)
    {
        var task = Task.Run(func);
        _tasks.Add(task);
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                WriteMessage($"Exception occurred: {t.Exception.Message}");
            }

            _tasks.Remove(task);

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Working)));
        });

        OnPropertyChanged(nameof(Working));
    }


    private async Task OnImageSelected()
    {
        await Dispatcher.UIThread.InvokeAsync(() => ConvertImageCommand.IsEnabled = File != null);

        if (File?.Path.AbsolutePath is { } path)
        {
            var info = await Image.IdentifyAsync(path);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HasMultiFrames = info.FrameMetadataCollection.Count > 1;
                ConvertImageHeightMapCommand.IsEnabled = !HasMultiFrames;
                if (HasMultiFrames)
                {
                    WriteMessage("Multi-Frame Image detected, using 3D image converter");
                    WriteMessage("Height Map is not supported using this configuration");
                }
            });
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => HasMultiFrames = false);
        }

        await UpdatePreview();
    }

    public async Task UpdatePreview()
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

                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(SourceImage)));
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

    public async Task ConvertImageHeightMap()
    {
        if (_file == null)
            throw new FileNotFoundException();

        WriteMessage("Converting image with height map...");
        
        try
        {
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
    }

    public async Task ConvertImage()
    {
        if (_file == null)
            throw new FileNotFoundException();

        WriteMessage("Converting image...");
        
        try
        {
            var stream = await _file.OpenReadAsync();
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);

            var path = GetNewBlueprintFilePath(File?.Name);

            string[] pixels;
            
            if (image.Frames.Count > 1)
            {
                var colorData = ConvertImageTo3DArray(image);
                pixels = Convert3DArrayToBlocks(colorData, SelectedSize);
            }
            else
            {
                var colorData = ConvertImageTo2DArray(image);
                pixels = Convert2DArrayToBlocks(colorData, SelectedSize);
            }
            
            await using var textFile = System.IO.File.CreateText(path);

            var blocks = pixels.Where(a => !string.IsNullOrEmpty(a)).ToArray();

            WriteMessage($"{blocks.Length} Blocks generated");

            if (blocks.Length >= 400000)
                PCUWarning();
                

            foreach (var line in pixels)
                if(!string.IsNullOrEmpty(line))
                    await textFile.WriteLineAsync(line);

            WriteMessage("Blueprint generated successfully");
            WriteMessage($"Path: {path}");
        }
        catch (Exception e)
        {
            WriteMessage(e.Message);
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
            var index = y * colors.GetLength(1) + x;
            if (colors[y, x].A == 0)
            {
                // Skip empty pixels
                array[index] = string.Empty;
                continue;
            }
            
            var color = new ColorHSV(colors[y, x]);
            var z = heightMap != null && new ColorHSV(heightMap[y, x]).Value > 0.5 ? 1 : 0;
            array[index] =
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
            int index = z * colors.GetLength(0) * colors.GetLength(1) + y * colors.GetLength(1) + x;
            if (colors[y, x, z].A == 0)
            {
                // Skip empty pixels
                array[index] = string.Empty;
                continue;
            }
            
            var color = new ColorHSV(colors[y, x, z]);
            array[index] =
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

    private void PCUWarning()
    {
        WriteMessage();
        WriteMessage("-=-=-=-=-=-=-=-=-=-=-=- WARNING -=-=-=-=-=-=-=-=-=-=-=-");
        WriteMessage("This blueprint is too big to be used in SE2 with default PCU limit!");
        WriteMessage("-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-");
        WriteMessage();
    }
    
    public void WriteMessage() => WriteMessage(string.Empty);

    public void WriteMessage(string message)
    {
        Console.WriteLine(message);
        _message.AppendLine(message);
        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Message)));
    }
}