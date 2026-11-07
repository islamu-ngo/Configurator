#:project src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj
#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using Configurator.ReleaseEngineering;

return TrustedBundleRuntime.Assemble(args, Console.Out);
