$buildConfig = if ($ENV:CONFIGURATION -eq $null) { "Release" } else { $ENV:CONFIGURATION }

$buildFolder = if ($ENV:APPVEYOR_BUILD_FOLDER -eq $null) { $PSScriptRoot } else { $ENV:APPVEYOR_BUILD_FOLDER }

$openCover = 'C:\ProgramData\chocolatey\lib\opencover.portable\tools\OpenCover.Console.exe'

dotnet test

$target = '-target:C:\Program Files\dotnet\dotnet.exe'
$targetArgs = '-targetargs:"test -c:' + $buildConfig + ' --logger:trx;LogFileName=results.trx /p:DebugType=full"' 
$filter = '-filter:+[Wopi*]*-[*Tests]*'
$output = '-output:' + $buildFolder + '\coverage.xml'



& $openCover $target $targetArgs $filter '-register:user' '-oldStyle' '-mergeoutput' $output
	
  