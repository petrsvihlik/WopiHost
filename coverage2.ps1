$ENV:CONFIGURATION = if ($ENV:CONFIGURATION -eq $null) { "Release" } else { $ENV:CONFIGURATION }

$ENV:APPVEYOR_BUILD_FOLDER = if ($ENV:APPVEYOR_BUILD_FOLDER -eq $null) { $PSScriptRoot } else { $ENV:APPVEYOR_BUILD_FOLDER }

$openCover = 'C:\ProgramData\chocolatey\lib\opencover.portable\tools\OpenCover.Console.exe'
dotnet test

$logger = ("--logger:trx;LogFileName=results.trx")    
$targetArgs = '-targetargs: test -c ' + $ENV:CONFIGURATION + ' ' + $logger + ' /p:DebugType=full' 
$filter = '-filter:+[Wopi*]*-[*Tests]*'
& $openCover '-target:C:\Program Files\dotnet\dotnet.exe' $targetArgs '-register:user' $filter '-oldStyle' '-mergeoutput' ('-output:' + $ENV:APPVEYOR_BUILD_FOLDER + '\coverage.xml')
	
  