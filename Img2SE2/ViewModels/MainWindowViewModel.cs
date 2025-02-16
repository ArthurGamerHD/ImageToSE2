using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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

    IStorageFile? File
    {
        get => _file;
        set
        {
            _file = value;
            ConvertImageCommand.IsEnabled = File != null;
            ConvertImageHeightMapCommand.IsEnabled = File != null;
            UpdatePreview();
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
                },
                MimeTypes = new[]
                {
                    "image/png",
                    "image/jpeg",
                    "image/bmp",
                },
                AppleUniformTypeIdentifiers = new[]
                {
                    "public.image"
                }
            }
        };
    }

    public async void UpdatePreview()
    {
        try
        {
            using var task = _file?.OpenReadAsync();
            if (task != null && await task is {} stream)
            {
                SourceImage = new Bitmap(stream);
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

            var colorData = await ConvertImageTo2DArray(_file);

            Rgba32[,]? heightMap = null;
            if (await PickImageFileAsync("Pick a Height Map") is { } mask)
            {
                heightMap = await ConvertImageTo2DArray(mask, colorData.GetLength(1), colorData.GetLength(0));
            }

            var path = GetNewBlueprintFilePath(File?.Name);
            await using var textFile = System.IO.File.CreateText(path);
            

            foreach (var line in ConvertArrayToBlocks(colorData, SelectedSize, heightMap))
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

            var colorData = await ConvertImageTo2DArray(_file);
            
            var path = GetNewBlueprintFilePath(File?.Name);
            await using var textFile = System.IO.File.CreateText(path);
            

            foreach (var line in ConvertArrayToBlocks(colorData, SelectedSize))
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

    static async Task<Rgba32[,]> ConvertImageTo2DArray(IStorageFile imageFile, int? width = null, int? height = null)
    {
        var stream = await imageFile.OpenReadAsync();
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);

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

    static string[] ConvertArrayToBlocks(Rgba32[,] colors, BlockSize selectedBlock, Rgba32[,]? heightMap = null)
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

    public void WriteMessage(string message)
    {
        Console.WriteLine(message);
        _message.AppendLine(message);
        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Message)));
    }
}