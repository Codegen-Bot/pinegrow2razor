using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using CodegenBot;
using Extism;

namespace Pinegrow2Razor;

/// <summary>
/// This class contains all the static methods that codegen.bot calls. See also the Imports class,
/// which contains static methods that we can call from within a bot that are implemented by codegen.bot.
/// </summary>
public class Exports
{
    public static void Main()
    {
        // Note: a `Main` method is required for the app to compile
    }

    [UnmanagedCallersOnly(EntryPoint = "entry_point")]
    public static int Run()
    {
        try
        {
            // Create all our minibots here
            IMiniBot[] miniBots =
            [
                // TODO - remove the ExampleMiniBot entry from this list because it creates a hello world file
                // that won't be useful in real life.
                new ExampleMiniBot(),
            ];

            // Run each minibot in order
            foreach (var miniBot in miniBots)
            {
                try
                {
                    miniBot.Execute();
                }
                catch (Exception e)
                {
                    Imports.Log(
                        new LogEvent()
                        {
                            // Only a critical error will cause codegen.bot to realize that the generated code should not be used
                            Level = LogEventLevel.Critical,
                            Message =
                                "Failed to run minibot {MiniBot}: {ExceptionType} {Message}, {StackTrace}",
                            Args =
                            [
                                miniBot.GetType().Name,
                                e.GetType().Name,
                                e.Message,
                                e.StackTrace ?? "",
                            ],
                        }
                    );
                }
            }

            return 0;
        }
        catch (Exception e)
        {
            Imports.Log(
                new LogEvent()
                {
                    // Only a critical error will cause codegen.bot to realize that the generated code should not be used
                    Level = LogEventLevel.Critical,
                    Message = "Failed to initialize bot: {ExceptionType} {Message}, {StackTrace}",
                    Args = [e.GetType().Name, e.Message, e.StackTrace ?? ""],
                }
            );
            Pdk.SetError($"{e.GetType()}: {e.Message}");
            return 0;
        }
    }
}
