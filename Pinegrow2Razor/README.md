# Pinegrow2Razor

This is the source code for the bot `bot://hub/dotnet/pinegrow2razor`.

## How to build

This bot can be built by installing the .NET SDK, then running these commands in the bot directory (the directory that contains `bot.json`):

```shell
dotnet workload install wasi-experimental
dotnet build -c Release -r wasi-wasm
codegen.bot push
```

Alternatively, you can use [a docker image specifically designed for building .NET bots](https://hub.docker.com/r/codegenbot/dotnet-bot-builder) like this:

```shell
docker run -v .:/src codegenbot/dotnet-bot-builder:net8.0
```

If the above docker container doesn't work, take a look at [the Dockerfile that builds that container](https://github.com/Codegen-Bot/dotnet-sdk/blob/master/CodegenBot.Builder/Dockerfile) for ideas.

The above command specifically won't work if there are ProjectReferences in the bot's csproj file.
