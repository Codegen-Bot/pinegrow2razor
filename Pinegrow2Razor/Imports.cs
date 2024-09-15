using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using CodegenBot;
using Extism;

namespace Pinegrow2Razor;

/// <summary>
/// This class contains all the static methods that we can call that codegen.bot implements. See also the Exports class,
/// which contains static methods that we can implement that are called by codegen.bot.
/// </summary>
public class Imports
{
    [DllImport("extism", EntryPoint = "cgb_graphql")]
    public static extern ulong ExternGraphQL(ulong offset);

    public static string GraphQL<T>(GraphQLRequest<T> request)
    {
        var json = request.ToJsonString();
        using var block = Pdk.Allocate(json);
        var ptr = ExternGraphQL(block.Offset);
        var response = MemoryBlock.Find(ptr).ReadString();
        return response;
    }

    [DllImport("extism", EntryPoint = "cgb_log")]
    public static extern void ExternLog(ulong offset);

    public static void Log(LogEvent logEvent)
    {
        var json = JsonSerializer.Serialize(logEvent, LogEventJsonContext.Default.LogEvent);
        using var block = Pdk.Allocate(json);
        ExternLog(block.Offset);
    }

    [DllImport("extism", EntryPoint = "cgb_random")]
    public static extern ulong ExternRandom(ulong offset);

    public static byte[] GetRandomBytes(int numBytes)
    {
        var text = numBytes.ToString();
        using var block = Pdk.Allocate(text);
        var ptr = ExternRandom(block.Offset);
        var response = MemoryBlock.Find(ptr).ReadBytes();
        return response;
    }
}
