# msbuild_proj
Create .Net projects and add references supporting msbuild 2015 format in the command line

Very basic work-in-progress
Essentially did this for myself as I wanted to work with VIM and Omnisharp-VIM in the command line and not require tools
such as Visual Studio.  This tool, combined with dotnet core tools, gives me all the power I need.

For example:

`mkdir ~/src/MySol`

`cd ~/src/MySol`


`dotnet new sln`

`msbuild_proj new console MyConsole`

`msbuild_proj new MyLibrary`

`cd MyConsole`

`msbuild_proj add MyLibrary`

`cd ..`

`dotnet sln add MyConsole/MyConsole.csproj`

`dotnet sln add MyLibrary/MyLibrary.csproj`

`xbuild`

The above performs the following:

* Creates a new solution called "MySol"
* Creates a new console project called "MyConsole"
* Creates a new class library project called "MyLibrary"
* Adds a reference for MyLibrary to MyConsole
* Adds the two projects to the solution
* Builds the solution

