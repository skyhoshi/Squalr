<#
[Environment]::CurrentDirectory = Get-Location -PSProvider FileSystem

# Parse assembly info from the project
$assemblyInfoPath = "./Squalr/Properties/AssemblyInfo.cs"
$assemblyInfo = [IO.File]::ReadAllText($assemblyInfoPath)
$title = $assemblyInfo | Select-String -Pattern 'AssemblyTitle\(".+"\)' -AllMatches | % { $_.Matches } | % { $_.Value } | %{$_.split('"')[1]}
$version = $assemblyInfo | Select-String -Pattern 'AssemblyVersion\(".+"\)' -AllMatches | % { $_.Matches } | % { $_.Value } | %{$_.split('"')[1]}
$description = $assemblyInfo | Select-String -Pattern 'AssemblyDescription\(".+"\)' -AllMatches | % { $_.Matches } | % { $_.Value } | %{$_.split('"')[1]}

# Variables and paths
$releasesRoot = "Releases"
$sourceRoot = "Squalr/bin/Release/*"
$destinationRoot = "lib/net45"
$exclude = @('*.pdb')
$nugetFile = "Squalr.nuspec"
$nugetFilePath = "./$nugetFile"
$compiledNugetFile = "SqualrCompiled.nuspec"
$compiledNugetFilePath = "./$compiledNugetFile"
$package = "Squalr.$version.nupkg"
$squirrel= "packages/squirrel.windows.1.8.0/tools/Squirrel.exe"
$cert = [Environment]::GetEnvironmentVariables("User")["SQUALR_CODE_SIGN_CERT"]
$pass = [Environment]::GetEnvironmentVariables("User")["SQUALR_CODE_SIGN_PASS"]

# Delete old releases
Remove-Item $releasesRoot -Force -Recurse -ErrorAction Ignore

# Compile new nuspec file using variables from assembly info
$nuspec = [IO.File]::ReadAllText($nugetFilePath)
$nuspec = $nuspec.Replace('$id$', $title)
$nuspec = $nuspec.Replace('$title$', $title)
$nuspec = $nuspec.Replace('$version$', $version)
$nuspec = $nuspec.Replace('$description$', $description)
[IO.File]::WriteAllText($compiledNugetFilePath, $nuspec)

# Remove old files, copy new files to a location in preparation for Squirrel
Remove-Item $destinationRoot -Force -Recurse -ErrorAction Ignore
New-Item -ItemType Directory -Force -Path $destinationRoot
Copy-Item -Path $sourceRoot -Recurse -Destination $destinationRoot -Container -Force -Exclude $exclude

# Build nuget package
Invoke-Expression "nuget pack $($compiledNugetFile) -Properties Configuration=Release"
#>

# Build Release
dotnet publish -c Release -o ".\publish" 

# Find Squirrel.exe path and add an alias
Set-Alias Squirrel ($env:USERPROFILE + "\.nuget\packages\clowd.squirrel\2.9.42\tools\Squirrel.exe");

# Download currently live version
Squirrel http-down --url "https://github.com/Squalr/Squalr"

# build new version and delta updates.
Squirrel pack --framework net7 --packId "Squalr" --packVersion "1.0.0" --packAuthors "Squalr, Inc." --packDir ".\publish" --icon "Squalr/AppIcon.ico" # --splashImage "install.gif"`

# Remove temporary files
# Remove-Item $compiledNugetFile -Force -ErrorAction Ignore
# Remove-Item $package -Force -ErrorAction Ignore