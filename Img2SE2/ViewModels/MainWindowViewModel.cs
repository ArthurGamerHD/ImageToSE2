using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Img2SE2.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //{X}|{Y}|{Z}|{HUE}|{Saturation}|{Value}
    public const string SMALL_GRID = "632d7385-12b9-47a6-802a-a610d0cbd1e0|{0}|{1}|{2}|{3}|{4}|{5}|0|4|1|";
    public const string DETAIL_GRID = "6c5ed351-0868-40c8-9cf3-3dd9bc201f46|{0}|{1}|{2}|{3}|{4}|{5}|0|4|1|";
    public const int SMALL_GRID_MULTIPLIER = 2;
    public const int DETAIL_GRID_MULTIPLIER = 1;

    public bool UseDetailBlock { get; set; } = true;

    public string Greeting => "Image to Ship Converter SE2";

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

    private IStorageFile? _file;
    private List<FilePickerFileType> _fileTypeFilter;

    public MainWindowViewModel()
    {
        PickImageCommand = new SimpleCommand(PickImage);
        ConvertImageCommand = new SimpleCommand(ConvertImage) { IsEnabled = false };
        ConvertImageHeightMapCommand = new SimpleCommand(ConvertImageHeightMap) { IsEnabled = false };


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

            await using var textFile = System.IO.File.CreateText(
                $"C:\\Users\\Arthur\\AppData\\Roaming\\SpaceEngineers2\\AppData\\SE1GridsToImport\\{File?.Name ?? "image"}.txt");

            var pattern = UseDetailBlock ? DETAIL_GRID : SMALL_GRID;
            var multiplier = UseDetailBlock ? DETAIL_GRID_MULTIPLIER : SMALL_GRID_MULTIPLIER;

            foreach (var line in ConvertArrayToBlocks(colorData, pattern, multiplier, heightMap))
                await textFile.WriteLineAsync(line);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
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

            var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            await using var textFile = System.IO.File.CreateText(
                Path.Combine(app, $"SpaceEngineers2\\AppData\\SE1GridsToImport\\{File?.Name ?? "image"}.txt"));

            var pattern = UseDetailBlock ? DETAIL_GRID : SMALL_GRID;
            var multiplier = UseDetailBlock ? DETAIL_GRID_MULTIPLIER : SMALL_GRID_MULTIPLIER;

            foreach (var line in ConvertArrayToBlocks(colorData, pattern, multiplier))
                await textFile.WriteLineAsync(line);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
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

    static string[] ConvertArrayToBlocks(Rgba32[,] colors, string pattern, int multiplier, Rgba32[,]? heightMap = null)
    {
        var array = new string[colors.Length];

        for (int y = 0; y < colors.GetLength(0); y++)
        for (int x = 0; x < colors.GetLength(1); x++)
        {
            var color = new ColorHSV(colors[y, x]);
            var z = heightMap != null && new ColorHSV(heightMap[y, x]).Value > 0.5 ? 1 : 0;
            array[y * colors.GetLength(1) + x] = String.Format(pattern, x * multiplier,
                y * multiplier, z * multiplier, color.Hue, color.Saturation, color.Value);
        }

        return array;
    }
}