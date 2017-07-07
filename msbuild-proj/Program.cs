using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace msbuild_proj
{
    internal class Program
    {
        public static void Main(string[] args)
        {
          if (args.Length == 0)
          {
            Help();
            return;
          }
          
          switch (args[0])
          {
              case "new":
                string projName = args[1];
                bool isConsole = false;
                if (args[1] == "console")
                {
                  isConsole = true;
                  projName = args[2];
                }
                NewProject(projName, isConsole);
                break;
              case "add":
                AddProjectToProject(args[1]);
                break;
              case "sln":
                switch (args[1])
                {
                  case "new":
                    NewSolution(args[2]);
                    break;
                  case "add":
                    AddProjectToSolution(args[2]);
                    break;
                  default:
                    Help();
                    break;
                }
                break;
              case "nuget":
                switch (args[1])
                {
                  case "prepare":
                    CreateNugetPackage(args[2], args[3], args[4]);
                    break;
                  case "add":
                    AddNugetPackageToProject(args[2], args[3], args[4]);
                    break;
                  default:
                    Help();
                    break;
                }
                break;
              case "gitignore":
                CreateGitIgnore();
                break;
              default:
                Help();
                break;
          }
          
        }

      private static void AddNugetPackageToProject(string packageName, string packageVersion, string projectName)
      {
        var currentDir = Environment.CurrentDirectory;
        var projectDir = Path.Combine(currentDir, projectName);
        var projectFilePath = Path.Combine(projectDir, projectName + ".csproj");
        var packageAssemblyLocation =
          Path.Combine(
            Path.Combine(Path.Combine(Path.Combine(currentDir, "packages"), packageName + "." + packageVersion), "lib"),
            "net45");
        string projectFileContents;
        using (var reader = new StreamReader(projectFilePath))
        {
          projectFileContents = reader.ReadToEnd();
        }
        var dlls = Directory.GetFiles(packageAssemblyLocation, "*.dll");
        var indexOfReferenceInclude = projectFileContents.IndexOf("<Reference Include");
        var sb = new StringBuilder();
        foreach (var dll in dlls)
        {
          var dllReferenceContents = GetDllReferenceContents(dll, packageName, packageVersion);
          sb.AppendLine(dllReferenceContents);
        }
        var dllReferenceContentsToAdd = sb.ToString();
        projectFileContents = projectFileContents.Insert(indexOfReferenceInclude, sb.ToString());
        using (var writer = new StreamWriter(projectFilePath, false))
        {
          writer.Write(projectFileContents);
        }
        
        Console.WriteLine("Added Nuget package to project");
      }

      private static string GetDllReferenceContents(string dllFile, string packageName, string packageVersion)
      {
        var assembly = Assembly.LoadFile(dllFile);
        var assemblyFullName = assembly.FullName;
        var currentDir = Environment.CurrentDirectory;
        var strippedOutFilePath = ".." + dllFile.Replace(currentDir, string.Empty);
        strippedOutFilePath = strippedOutFilePath.Replace("/", "\\");
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
          @"<Reference Include=""{0}"">
          <HintPath>{1}</HintPath>
          <EmbedInteropTypes>False</EmbedInteropTypes>
          </Reference>",
          assemblyFullName,
          strippedOutFilePath);
      }

      private static void CreateNugetPackage(string packageName, string packageVersion, string projectName)
      {
        var currentDir = Environment.CurrentDirectory;
        var projectDir = Path.Combine(currentDir, projectName);
        var projectFilePath = Path.Combine(projectDir, projectName + ".csproj");
        var packagesConfig = Path.Combine(projectDir, "packages.config");
        string onlyPackageContents;
        var nugetPackageConfigContents = GetNugetPackageConfigContents(packageName, packageVersion, out onlyPackageContents);
        bool fileExists = false;
        bool writeFile = true;
        if (File.Exists(packagesConfig))
        {
          string existingContents;
          using (var sr = new StreamReader(packagesConfig))
          {
            existingContents = sr.ReadToEnd();
          }
          if (existingContents.Contains(onlyPackageContents))
          {
            Console.WriteLine("An existing packages.config already exists within the specified project for the specified package, doing nothing");
            writeFile = false;
          }
          else
          {
            var indexOfEnd = existingContents.IndexOf("</packages>");
            nugetPackageConfigContents = existingContents.Insert(indexOfEnd, onlyPackageContents + "\n");

            fileExists = true;  
          }
        }

        if (writeFile)
        {
          using (var writer = new StreamWriter(packagesConfig, !fileExists))
          {
            writer.Write(nugetPackageConfigContents);
          }
        }

        //add reference to new config file to project file
        string projContents;
        using (var sr = new StreamReader(projectFilePath))
        {
          projContents = sr.ReadToEnd();
        }
        if (projContents.Contains("packages.config"))
        {
          Console.WriteLine("Project already contains a reference to the package, doing nothing");
          return;
        }
        var lastIndexOfItemGroup = projContents.LastIndexOf("</ItemGroup>");
        var nugetPackageProjectIncludeContents = GetNugetPackageProjectIncludeContents();
        projContents = projContents.Insert(lastIndexOfItemGroup + 12, "\n" + nugetPackageProjectIncludeContents);
        using (var writer = new StreamWriter(projectFilePath, false))
        {
          writer.Write(projContents);
        }
        
        Console.WriteLine("Prepared Nuget package");
        Console.WriteLine("Now run [nuget restore <solutionFile>] to download the assemblies");
        Console.WriteLine("After that you can invoke [msbuild_proj nuget add <packageName> <packageVersion> <project>] to add a reference to it");
      }

      private static object GetNugetPackageProjectIncludeContents()
      {
        return @"<ItemGroup>
    <None Include=""packages.config"" />
          </ItemGroup>";
      }

      private static string GetNugetPackageConfigContents(string packageName, string packageVersion, out string onlyPackageContents)
      {
        onlyPackageContents = string.Format(System.Globalization.CultureInfo.InvariantCulture,
          @"<package id=""{0}"" version=""{1}"" targetFramework=""net452"" />",
          packageName, packageVersion);
        
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
          @"<?xml version=""1.0"" encoding=""utf-8""?>
          <packages>
          {2}
          </packages>",
          packageName, packageVersion, onlyPackageContents);
      }

      private static void AddProjectToSolution(string s)
      {
        throw new NotImplementedException();
      }

      private static void CreateGitIgnore()
      {
        var currentDir = Environment.CurrentDirectory;
        var gitIgnoreFile = Path.Combine(currentDir, ".gitignore");
        if (File.Exists(gitIgnoreFile))
        {
          Console.WriteLine(".gitignore file already exists, doing nothing");
          return;
        }
        
        var gitIgnoreFileContents = GetGitIgnoreContents();
        using (var write = new StreamWriter(gitIgnoreFile))
        {
          write.Write(gitIgnoreFileContents);
        }
        
        Console.WriteLine("Created .gitignore");
      }

      private static void Help()
      {
        Console.WriteLine("Usage: msbuild_proj <args>\n");
        Console.WriteLine("new console <proj>");
        Console.WriteLine("\t\t> Creates a new console project with the name of <proj>\n");
        Console.WriteLine("new <proj>");
        Console.WriteLine("\t\t> Creates a new class library project with the name of <proj>\n");
        Console.WriteLine("add <proj>");
        Console.WriteLine("\t\t> Adds the specified project to the current project\n");
        Console.WriteLine("nuget prepare <packageName> <packageVersion> <projectName>");
        Console.WriteLine("\t\t> Creates a packages.config and adds it to the project\n");
        Console.WriteLine("nuget add <packageName> <packageVersion> <projectName>");
        Console.WriteLine("\t\t> Adds a reference to the specific package to the specified project\n");
        Console.WriteLine("gitignore");
        Console.WriteLine("\t\t> Creates a new .gitignore file with the default .Net ignores");
      }

      private static void AddProjectToProject(string projName)
      {
        var currentDir = Environment.CurrentDirectory;
        var upOne = Path.Combine(currentDir, "..");
        var projDir = Path.Combine(upOne, projName);
        var projFile = Path.Combine(projDir, projName + ".csproj");
        string projContents;
        using (var sr = new StreamReader(projFile))
        {
          projContents = sr.ReadToEnd();
        }
        var indexOfGuidString = projContents.IndexOf("ProjectGuid");
        var guidString = new string(projContents.Skip(indexOfGuidString + 12).Take(36).ToArray());

        var projectReferenceContents = GetProjectReferenceContents(projName, guidString);
        
        //</ItemGroup>
        var currentProjName = new DirectoryInfo(currentDir).Name + ".csproj";
        projFile = Path.Combine(currentDir, currentProjName);
        
        using (var sr = new StreamReader(projFile))
        {
          projContents = sr.ReadToEnd();
        }
        var indexOfLastItemGroup = projContents.LastIndexOf("</ItemGroup>");
        projContents = projContents.Insert(indexOfLastItemGroup + 12, projectReferenceContents);
        using (var writer = new StreamWriter(projFile, false))
        {
          writer.Write(projContents);
        }
        
        Console.WriteLine("Added project reference to project");
      }

      private static string GetProjectReferenceContents(string projName, string projGuid)
      {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
          @"<ItemGroup>
    <ProjectReference Include=""..\{0}\{0}.csproj"">
          <Project>{{{1}}}</Project>
          <Name>{0}</Name>
          <EmbedInteropTypes>False</EmbedInteropTypes>
          </ProjectReference>
          </ItemGroup>",
          projName, projGuid);
      }

      private static void NewSolution(string s)
      {
        throw new NotImplementedException();
      }

      private static void NewProject(string projName, bool isConsole)
      {
        Console.WriteLine("Enter name for newly generated class:");
        var className = Console.ReadLine();
        //var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var currentDir = Environment.CurrentDirectory;
        var projDir = Directory.CreateDirectory(Path.Combine(currentDir, projName));

        string projectGuid;
        var csProjContents = GetCsProjContents(projName, isConsole, className, out projectGuid);
        var projFile = Path.Combine(projDir.FullName, projName + ".csproj");
        using (var writer = new StreamWriter(projFile))
        {
          writer.Write(csProjContents);
        }

        var csFileContents = GetClassFileContents(projName, className, isConsole);
        using (var writer = new StreamWriter(Path.Combine(projDir.FullName, className + ".cs")))
        {
          writer.Write(csFileContents);
        }

        var propertiesDir = Directory.CreateDirectory(Path.Combine(projDir.FullName, "Properties"));
        var assemblyInfoFileContents = GetAssemblyInfoContents(projName, projectGuid);
        using (var write = new StreamWriter(Path.Combine(propertiesDir.FullName, "AssemblyInfo.cs")))
        {
          write.Write(assemblyInfoFileContents);
        }

        if (isConsole)
        {
          var appConfigContents = GetAppConfigContents();
          using (var write = new StreamWriter(Path.Combine(projDir.FullName, "App.config")))
          {
            write.Write(appConfigContents);
          }
        }
        
        Console.WriteLine("Created project {0}", projName);
      }

      private static string GetAppConfigContents()
      {
        return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
          <configuration>
          <startup> 
          <supportedRuntime version=""v4.0"" sku="".NETFramework,Version=v4.5.2"" />
          </startup>
          </configuration>";
      }

      private static string GetAssemblyInfoContents(string projectName, string projectGuid)
      {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
          @"using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle(""{0}"")]
          [assembly: AssemblyDescription("""")]
          [assembly: AssemblyConfiguration("""")]
          [assembly: AssemblyCompany("""")]
          [assembly: AssemblyProduct(""{0}"")]
          [assembly: AssemblyCopyright(""Copyright ©  {1}"")]
          [assembly: AssemblyTrademark("""")]
          [assembly: AssemblyCulture("""")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
          [assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
          [assembly: Guid(""{2}"")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion(""1.0.*"")]
          [assembly: AssemblyVersion(""1.0.0.0"")]
          [assembly: AssemblyFileVersion(""1.0.0.0"")]",
          projectName,
          DateTime.Now.Year,
          projectGuid);
      }

      private static string GetCsProjContents(string projName, bool isConsole, string className, out string projectGuid)
      {
        var outputType = isConsole ? "Exe" : "Library";
        var autoGenBindingStatement = string.Empty;
        var classFile = className + ".cs";
        var appConfigStatement = string.Empty;
        if (isConsole)
        {
          autoGenBindingStatement = "\n<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>\n";
          appConfigStatement = @"
            <ItemGroup>
            <None Include=""App.config"" />
            </ItemGroup>
            ";
        }
        projectGuid = Guid.NewGuid().ToString("D");
            return String.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{1}</ProjectGuid>
    <OutputType>{2}</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>{0}</RootNamespace>
    <AssemblyName>{0}</AssemblyName>
    <TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>{3}
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System""/>

    <Reference Include=""System.Core""/>
    <Reference Include=""System.Xml.Linq""/>
    <Reference Include=""System.Data.DataSetExtensions""/>


    <Reference Include=""Microsoft.CSharp""/>

    <Reference Include=""System.Data""/>

    <Reference Include=""System.Net.Http""/>

    <Reference Include=""System.Xml""/>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""{5}"" />
    <Compile Include=""Properties\AssemblyInfo.cs"" />
  </ItemGroup>{4}
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>",
                projName, projectGuid, outputType, autoGenBindingStatement, appConfigStatement, classFile);
        }

      private static string GetClassFileContents(string projName, string className, bool isConsole)
      {
        var internalContent = "public ";
        var argsContent = string.Empty;
        var staticContent = string.Empty;
        var methodName = className;
        var methodContent = string.Empty;
        if (isConsole)
        {
          internalContent = "internal ";
          argsContent = "string[] args";
          staticContent = " static void";
          methodName = "Main";
          methodContent = "\n            Console.WriteLine(\"Hello, World!\");";
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, @"using System;

namespace {0}
{{
    {2}class {1}
    {{
        public{3} {5}({4})
        {{{6}
        }}
    }}
}}
", projName, className, internalContent, staticContent, argsContent, methodName, methodContent);
      }

      private static string GetGitIgnoreContents()
      {
        return @"## Ignore Visual Studio temporary files, build results, and
## files generated by popular Visual Studio add-ons.

# User-specific files
*.suo
*.user
*.userosscache
*.sln.docstates
*.gitignore


# User-specific files (MonoDevelop/Xamarin Studio)
*.userprefs

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
bld/
[Bb]in/
[Oo]bj/
[Ll]og/

# Visual Studio 2015 cache/options directory
.vs/
# Uncomment if you have tasks that create the project's static files in wwwroot
#wwwroot/

# MSTest test Results
[Tt]est[Rr]esult*/
[Bb]uild[Ll]og.*

# NUNIT
*.VisualState.xml
TestResult.xml

# Build Results of an ATL Project
[Dd]ebugPS/
[Rr]eleasePS/
dlldata.c

# DNX
project.lock.json
project.fragment.lock.json
artifacts/

*_i.c
*_p.c
*_i.h
*.ilk
*.meta
*.obj
*.pch
*.pdb
*.pgc
*.pgd
*.rsp
*.sbr
*.tlb
*.tli
*.tlh
*.tmp
*.tmp_proj
*.log
*.vspscc
*.vssscc
.builds
*.pidb
*.svclog
*.scc

# Chutzpah Test files
_Chutzpah*

# Visual C++ cache files
ipch/
*.aps
*.ncb
*.opendb
*.opensdf
*.sdf
*.cachefile
*.VC.db
*.VC.VC.opendb

# Visual Studio profiler
*.psess
*.vsp
*.vspx
*.sap

# TFS 2012 Local Workspace
$tf/

# Guidance Automation Toolkit
*.gpState

# ReSharper is a .NET coding add-in
_ReSharper*/
*.[Rr]e[Ss]harper
*.DotSettings.user

# JustCode is a .NET coding add-in
.JustCode

# TeamCity is a build add-in
_TeamCity*

# DotCover is a Code Coverage Tool
*.dotCover

# NCrunch
_NCrunch_*
.*crunch*.local.xml
nCrunchTemp_*

# MightyMoose
*.mm.*
AutoTest.Net/

# Web workbench (sass)
.sass-cache/

# Installshield output folder
[Ee]xpress/

# DocProject is a documentation generator add-in
DocProject/buildhelp/
DocProject/Help/*.HxT
DocProject/Help/*.HxC
DocProject/Help/*.hhc
DocProject/Help/*.hhk
DocProject/Help/*.hhp
DocProject/Help/Html2
DocProject/Help/html

# Click-Once directory
publish/

# Publish Web Output
*.[Pp]ublish.xml
*.azurePubxml
# TODO: Comment the next line if you want to checkin your web deploy settings
# but database connection strings (with potential passwords) will be unencrypted
#*.pubxml
*.publishproj

# Microsoft Azure Web App publish settings. Comment the next line if you want to
# checkin your Azure Web App publish settings, but sensitive information contained
# in these scripts will be unencrypted
PublishScripts/

# NuGet Packages
*.nupkg
# The packages folder can be ignored because of Package Restore
**/packages/*
# except build/, which is used as an MSBuild target.
!**/packages/build/
# Uncomment if necessary however generally it will be regenerated when needed
#!**/packages/repositories.config
# NuGet v3's project.json files produces more ignoreable files
*.nuget.props
*.nuget.targets

# Microsoft Azure Build Output
csx/
*.build.csdef

# Microsoft Azure Emulator
ecf/
rcf/

# Windows Store app package directories and files
AppPackages/
BundleArtifacts/
Package.StoreAssociation.xml
_pkginfo.txt

# Visual Studio cache files
# files ending in .cache can be ignored
*.[Cc]ache
# but keep track of directories ending in .cache
!*.[Cc]ache/

# Others
ClientBin/
~$*
*~
*.dbmdl
*.dbproj.schemaview
*.jfm
*.pfx
*.publishsettings
node_modules/
orleans.codegen.cs
tags
.gitignore

# Since there are multiple workflows, uncomment next line to ignore bower_components
# (https://github.com/github/gitignore/pull/1529#issuecomment-104372622)
#bower_components/

# RIA/Silverlight projects
Generated_Code/

# Backup & report files from converting an old project file
# to a newer Visual Studio version. Backup files are not needed,
# because we have git ;-)
_UpgradeReport_Files/
Backup*/
UpgradeLog*.XML
UpgradeLog*.htm

# SQL Server files
*.mdf
*.ldf

# Business Intelligence projects
*.rdl.data
*.bim.layout
*.bim_*.settings

# Microsoft Fakes
FakesAssemblies/

# GhostDoc plugin setting file
*.GhostDoc.xml

# Node.js Tools for Visual Studio
.ntvs_analysis.dat

# Visual Studio 6 build log
*.plg

# Visual Studio 6 workspace options file
*.opt

# Visual Studio LightSwitch build output
**/*.HTMLClient/GeneratedArtifacts
**/*.DesktopClient/GeneratedArtifacts
**/*.DesktopClient/ModelManifest.xml
**/*.Server/GeneratedArtifacts
**/*.Server/ModelManifest.xml
_Pvt_Extensions

# Paket dependency manager
.paket/paket.exe
paket-files/

# FAKE - F# Make
.fake/

# JetBrains Rider
.idea/
*.sln.iml

# CodeRush
.cr/

# Python Tools for Visual Studio (PTVS)
__pycache__/
*.pyc";
      }
    }
}