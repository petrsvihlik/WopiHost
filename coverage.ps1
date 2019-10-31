$ENV:CONFIGURATION = if ($ENV:CONFIGURATION -eq $null) { "Release" } else { $ENV:CONFIGURATION }

$ENV:APPVEYOR_BUILD_FOLDER = if ($ENV:APPVEYOR_BUILD_FOLDER -eq $null) { $PSScriptRoot } else { $ENV:APPVEYOR_BUILD_FOLDER }

$openCover = 'C:\ProgramData\chocolatey\lib\opencover.portable\tools\OpenCover.Console.exe'

ForEach ($folder in (Get-ChildItem -Path .\ -Directory -Filter *.Tests)) { 
    $project = Get-ChildItem -Path ($folder.FullName+"\*.csproj") | Select-Object -First 1
    
    $logger = ("--logger:trx;LogFileName=" + $folder.FullName + "\results.trx")
    dotnet test $project.FullName
    
    $targetArgs = '-targetargs: test ' + $project.FullName + ' ' +  ' -c ' + $ENV:CONFIGURATION + ' ' + $logger + ' /p:DebugType=full' 
    $filter = '-filter:+[Wopi*]*-[*Tests]*'
    & $openCover '-target:C:\Program Files\dotnet\dotnet.exe' $targetArgs '-register:user' $filter '-oldStyle' '-mergeoutput' ('-output:' + $ENV:APPVEYOR_BUILD_FOLDER + '\coverage.xml')
	
    }
