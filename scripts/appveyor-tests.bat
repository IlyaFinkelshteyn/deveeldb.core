echo %configuration%
dotnet test -f netcoreapp1.1 %APPVEYOR_BUILD_FOLDER%\test\DeveelDb.Core.Tests\DeveelDb.Core.Tests.csproj
