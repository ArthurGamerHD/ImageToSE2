using System;

namespace Img2SE2.Models;

public class BlockSize(string name, int size, Guid guid)
{ 
    public static BlockSize Large { get; } = new BlockSize("Large Block", 10, Guid.Parse("2eacbbf2-d8fb-4a78-91dc-7b492517ef97"));
    public static BlockSize Small { get; } = new BlockSize("Small Block", 2, Guid.Parse("632d7385-12b9-47a6-802a-a610d0cbd1e0"));
    public static BlockSize Detailing { get; } = new BlockSize("Detailing Block", 1, Guid.Parse("6c5ed351-0868-40c8-9cf3-3dd9bc201f46"));

    public static BlockSize HeavyLarge { get; } = new BlockSize("Heavy Large Block", 10, Guid.Parse("d4915136-8884-4cf8-9480-f5c98a76c8cf"));

    public static BlockSize HeavySmall { get; } = new BlockSize("Heavy Small Block", 2, Guid.Parse("aa7cb050-c0d6-4311-8ee8-fdc96600b1a2"));
       
    public string Name { get; init; } = name;
    public int Size { get; init; } = size;
    public Guid GUID { get; init; } = guid;
}