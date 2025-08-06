#All instructions from scratch

Below are the complete instructions for creating, configuring, and deploying a Visual Studio extension (`CallGraphExtension`) that adds a "Create Call Graph" context menu option in Visual Studio Professional 2022. This extension, when triggered on a C# method, executes the `SolutionAnalyzer.exe` to generate a call graph (`callchain.json`) and runs the Angular `call-graph-app` to visualize it. The extension targets .NET 8, addressing the issue where the default VSIX Project template targets .NET Framework 4.7.2. The instructions cover setting up the Visual Studio environment, creating the extension, configuring the `SolutionAnalyzer` and `call-graph-app` projects, and troubleshooting common issues. The solution integrates with the previously provided `SolutionAnalyzer` (generating `callchain.json` with `methodName`, `Class`, `Namespace` properties) and `call-graph-app` (rendering the graph with JPG/PDF export).

### Prerequisites
- **Visual Studio Professional 2022**: Version 17.0 or later, with the "Visual Studio extension development" workload installed.
- **.NET 8 SDK**: For building the extension and `SolutionAnalyzer`.
- **Node.js 18.20.8**: For the Angular `call-graph-app`.
- **npm 10.8.2**: For managing Angular dependencies.
- **Angular CLI 13.3.0**: For building and serving the Angular app.
- **SolutionAnalyzer**: Built as `SolutionAnalyzer.exe` and available at a known path (e.g., `C:\Tools\SolutionAnalyzer.exe`).
- **call-graph-app**: Set up at a known path (e.g., `C:\Projects\call-graph-app`).
- **A C# Solution**: A .NET solution (`.sln`) with C# projects to analyze.

### Step-by-Step Instructions

#### 1. Install the Visual Studio Extension Development Workload
1. Open **Visual Studio Installer**.
2. Select **Modify** for Visual Studio Professional 2022.
3. Under the **Workloads** tab, check **Visual Studio extension development** (includes Visual Studio SDK and VSIX Project template).
4. Install or update to apply changes.
5. Verify the VSIX Project template:
   - In Visual Studio, go to `File > New > Project`.
   - Search for “VSIX” in the project template search box.
   - Confirm the “VSIX Project” template (C#) appears.

#### 2. Create the VSIX Project
1. In Visual Studio 2022, select `File > New > Project`.
2. Search for **VSIX Project** (C#), name it `CallGraphExtension`, and set the location (e.g., `C:\Projects\CallGraphExtension`).
3. The default template targets .NET Framework 4.7.2. We’ll convert it to .NET 8.

#### 3. Configure the VSIX Project for .NET 8
1. **Update `CallGraphExtension.csproj`**:
   - Replace the project file with an SDK-style project targeting .NET 8:
     ```xml
     <Project Sdk="Microsoft.NET.Sdk">
       <PropertyGroup>
         <TargetFramework>net8.0-windows</TargetFramework>
         <UseWPF>true</UseWPF>
         <UseWindowsForms>true</UseWindowsForms>
         <RootNamespace>CallGraphExtension</RootNamespace>
         <AssemblyName>CallGraphExtension</AssemblyName>
         <OutputType>Library</OutputType>
         <GeneratePkgDefFile>false</GeneratePkgDefFile>
         <StartAction>Program</StartAction>
         <StartProgram>$(DevEnvDir)devenv.exe</StartProgram>
         <StartArguments>/rootSuffix Exp</StartArguments>
       </PropertyGroup>
       <ItemGroup>
         <PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.9.34902.98" />
         <PackageReference Include="Microsoft.VSSDK.BuildTools" Version="17.9.4108" PrivateAssets="all" />
         <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.14.0" />
         <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" />
       </ItemGroup>
       <ItemGroup>
         <Reference Include="PresentationCore" />
         <Reference Include="PresentationFramework" />
         <Reference Include="System.Design" />
         <Reference Include="System.Windows.Forms" />
         <Reference Include="WindowsBase" />
       </ItemGroup>
     </Project>
     ```
   - **Notes**:
     - `<TargetFramework>net8.0-windows</TargetFramework>`: Targets .NET 8 with Windows-specific features for Visual Studio compatibility.
     - `<GeneratePkgDefFile>false</GeneratePkgDefFile>`: Disables `.pkgdef` generation for simplicity.
     - `<PackageReference>`: Includes Visual Studio SDK and Roslyn for method analysis.
     - `<StartArguments>/rootSuffix Exp</StartArguments>`: Launches the Experimental Instance for debugging.

2. **Update `source.extension.vsixmanifest`**:
   - Replace with:
     ```xml
     <?xml version="1.0" encoding="utf-8"?>
     <PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011" xmlns:d="http://schemas.microsoft.com/developer/vsx-schema-design/2011">
       <Metadata>
         <Identity Id="CallGraphExtension..e7b9c2f1-9b1a-4b2e-9c5f-7d8e6f2c3a1b" Version="1.0" Language="en-US" Publisher="Your Name" />
         <DisplayName>Call Graph Extension</DisplayName>
         <Description>Generates a call graph for a selected C# method and visualizes it using an Angular app.</Description>
       </Metadata>
       <Installation>
         <InstallationTarget Id="Microsoft.VisualStudio.Pro" Version="[17.0,18.0)">
           <ProductArchitecture>amd64</ProductArchitecture>
         </InstallationTarget>
       </Installation>
       <Dependencies>
         <Dependency Id="Microsoft.Framework.NDP" DisplayName="Microsoft .NET Framework" d:Source="Manual" Version="[4.5,)" />
         <Dependency Id="Microsoft.VisualStudio.MPF.17.0" DisplayName="Visual Studio MPF 17.0" d:Source="Installed" Version="[17.0,18.0)" />
       </Dependencies>
       <Prerequisites>
         <Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor" Version="[17.0,)" DisplayName="Visual Studio core editor" />
       </Prerequisites>
       <Assets>
         <Asset Type="Microsoft.VisualStudio.VsPackage" d:Source="Project" d:ProjectName="%CurrentProject%" Path="|%CurrentProject%;PkgdefProjectOutputGroup|" />
       </Assets>
     </PackageManifest>
     ```
   - **Notes**:
     - Targets Visual Studio Professional 2022 (64-bit) with `amd64` architecture.
     - Specifies version range `[17.0,18.0)` for compatibility.

3. **Add Command Code (`CallGraphCommand.cs`)**:
   - Create or replace `CallGraphCommand.cs`:
     ```csharp
     using System;
     using System.ComponentModel.Design;
     using System.Threading.Tasks;
     using Microsoft.VisualStudio.Shell;
     using Microsoft.VisualStudio.Shell.Interop;
     using Microsoft.CodeAnalysis;
     using Microsoft.CodeAnalysis.CSharp;
     using Microsoft.CodeAnalysis.CSharp.Syntax;
     using System.Diagnostics;

     namespace CallGraphExtension
     {
         internal sealed class CallGraphCommand
         {
             public const int CommandId = 0x0100;
             public static readonly Guid CommandSet = new Guid("e7b9c2f1-9b1a-4b2e-9c5f-7d8e6f2c3a1b");
             private readonly AsyncPackage package;

             private CallGraphCommand(AsyncPackage package, OleMenuCommandService commandService)
             {
                 this.package = package ?? throw new ArgumentNullException(nameof(package));
                 commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

                 var menuCommandID = new CommandID(CommandSet, CommandId);
                 var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
                 menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
                 commandService.AddCommand(menuItem);
             }

             public static CallGraphCommand Instance { get; private set; }

             private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
             {
                 get { return this.package; }
             }

             public static async Task InitializeAsync(AsyncPackage package)
             {
                 await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.CancellationToken);
                 OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
                 Instance = new CallGraphCommand(package, commandService);
             }

             private void OnBeforeQueryStatus(object sender, EventArgs e)
             {
                 ThreadHelper.ThrowIfNotOnUIThread();
                 if (sender is OleMenuCommand menuCommand)
                 {
                     menuCommand.Visible = IsSelectedTextMethod();
                 }
             }

             private bool IsSelectedTextMethod()
             {
                 ThreadHelper.ThrowIfNotOnUIThread();
                 var dte = ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)).Result as EnvDTE.DTE;
                 if (dte?.ActiveDocument?.Selection is EnvDTE.TextSelection selection)
                 {
                     string selectedText = selection.Text.Trim();
                     return !string.IsNullOrEmpty(selectedText) && char.IsLetter(selectedText[0]);
                 }
                 return false;
             }

             private async void Execute(object sender, EventArgs e)
             {
                 await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.CancellationToken);

                 var dte = await ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                 string solutionPath = dte?.Solution?.FullName;
                 string methodName = GetSelectedMethodName(dte);
                 string className = await GetContainingClassNameAsync();
                 string namespaceName = await GetContainingNamespaceNameAsync();

                 if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(className) || string.IsNullOrEmpty(namespaceName) || string.IsNullOrEmpty(solutionPath))
                 {
                     await VsShellUtilities.ShowMessageBoxAsync(
                         this.package,
                         "Unable to determine method details. Please select a method name in a C# file.",
                         "Call Graph Extension",
                         OLEMSGICON.OLEMSGICON_INFO,
                         OLEMSGBUTTON.OLEMSGBUTTON_OK,
                         OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                     return;
                 }

                 // Configure these paths based on your environment
                 string solutionAnalyzerPath = @"C:\Tools\SolutionAnalyzer.exe";
                 string angularAppPath = @"C:\Projects\call-graph-app";
                 string jsonOutputPath = $@"{angularAppPath}\src\assets\callchain.json";

                 // Execute SolutionAnalyzer and ng serve
                 ProcessStartInfo psi = new ProcessStartInfo
                 {
                     FileName = "cmd.exe",
                     Arguments = $"/C \"{solutionAnalyzerPath}\" \"{solutionPath}\" {namespaceName} {className} {methodName} & copy callchain.json \"{jsonOutputPath}\" & cd /d \"{angularAppPath}\" & ng serve",
                     UseShellExecute = true
                 };
                 Process.Start(psi);
             }

             private string GetSelectedMethodName(EnvDTE.DTE dte)
             {
                 ThreadHelper.ThrowIfNotOnUIThread();
                 if (dte?.ActiveDocument?.Selection is EnvDTE.TextSelection selection)
                 {
                     return selection.Text.Trim();
                 }
                 return string.Empty;
             }

             private async Task<string> GetContainingClassNameAsync()
             {
                 var (document, position) = await GetCurrentDocumentAndPositionAsync();
                 if (document == null) return string.Empty;

                 var root = await document.GetSyntaxRootAsync();
                 var token = root.FindToken(position);
                 var method = token.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                 if (method != null)
                 {
                     var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                     return classDecl?.Identifier.Text ?? string.Empty;
                 }
                 return string.Empty;
             }

             private async Task<string> GetContainingNamespaceNameAsync()
             {
                 var (document, position) = await GetCurrentDocumentAndPositionAsync();
                 if (document == null) return string.Empty;

                 var root = await document.GetSyntaxRootAsync();
                 var token = root.FindToken(position);
                 var method = token.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                 if (method != null)
                 {
                     var namespaceDecl = method.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                     return namespaceDecl?.Name.ToString() ?? string.Empty;
                 }
                 return string.Empty;
             }

             private async Task<(Document, int)> GetCurrentDocumentAndPositionAsync()
             {
                 await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.CancellationToken);
                 var textManager = await ServiceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
                 textManager?.GetActiveView(1, null, out IVsTextView textViewCurrent);
                 if (textViewCurrent == null) return (null, 0);

                 textViewCurrent.GetCaretPos(out int line, out int column);

                 var componentModel = await ServiceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
                 var editorAdaptersFactoryService = componentModel?.GetService<IVsEditorAdaptersFactoryService>();
                 var wpfTextView = editorAdaptersFactoryService?.GetWpfTextView(textViewCurrent);
                 if (wpfTextView == null) return (null, 0);

                 var caretPosition = wpfTextView.Caret.Position.BufferPosition.Position;
                 var document = wpfTextView.TextSnapshot.TextBuffer.Properties.GetProperty(typeof(ITextDocument)) as ITextDocument;

                 var roslynDocument = wpfTextView.TextSnapshot.GetRelatedDocuments().FirstOrDefault();
                 return (roslynDocument, caretPosition);
             }
         }
     }
     ```

4. **Add Command Table (`CallGraphCommand.vsct`)**:
   - Create or replace `CallGraphCommand.vsct`:
     ```xml
     <?xml version="1.0" encoding="utf-8"?>
     <CommandTable xmlns="http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable" xmlns:xs="http://www.w3.org/2001/XMLSchema">
       <Extern href="stdidcmd.h"/>
       <Extern href="vsshlids.h"/>
       <Commands package="guidCallGraphCommandPackage">
         <Groups>
           <Group guid="guidCallGraphCommandPackageCmdSet" id="MyMenuGroup" priority="0x0600">
             <Parent guid="guidSHLMainMenu" id="IDM_VS_CTXT_CODEWIN"/>
           </Group>
         </Groups>
         <Buttons>
           <Button guid="guidCallGraphCommandPackageCmdSet" id="cmdidCallGraphCommand" priority="0x0100" type="Button">
             <Parent guid="guidCallGraphCommandPackageCmdSet" id="MyMenuGroup" />
             <Strings>
               <ButtonText>Create Call Graph</ButtonText>
             </Strings>
           </Button>
         </Buttons>
       </Commands>
       <Symbols>
         <GuidSymbol name="guidCallGraphCommandPackage" value="{e7b9c2f1-9b1a-4b2e-9c5f-7d8e6f2c3a1b}" />
         <GuidSymbol name="guidCallGraphCommandPackageCmdSet" value="{a2b3c4d5-6e7f-8g9h-0i1j-2k3l4m5n6o7p}">
           <IDSymbol name="MyMenuGroup" value="0x1020" />
           <IDSymbol name="cmdidCallGraphCommand" value="0x0100" />
         </GuidSymbol>
       </Symbols>
     </CommandTable>
     ```

5. **Update Package Class (`CallGraphCommandPackage.cs`)**:
   - Create or replace `CallGraphCommandPackage.cs`:
     ```csharp
     using Microsoft.VisualStudio.Shell;
     using System;
     using System.Runtime.InteropServices;
     using System.Threading;
     using Task = System.Threading.Tasks.Task;

     namespace CallGraphExtension
     {
         [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
         [Guid(CallGraphCommandPackage.PackageGuidString)]
         [ProvideMenuResource("Menus.ctmenu", 1)]
         public sealed class CallGraphCommandPackage : AsyncPackage
         {
             public const string PackageGuidString = "e7b9c2f1-9b1a-4b2e-9c5f-7d8e6f2c3a1b";

             protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
             {
                 await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                 await CallGraphCommand.InitializeAsync(this);
             }
         }
     }
     ```

6. **Set Up SolutionAnalyzer**:
   - Ensure the `SolutionAnalyzer` project is built:
     ```bash
     cd path/to/SolutionAnalyzer
     dotnet build -c Release
     ```
   - Copy `SolutionAnalyzer.exe` to `C:\Tools\SolutionAnalyzer.exe` (or update the path in `CallGraphCommand.cs`).
   - Verify the `.csproj`:
     ```xml
     <Project Sdk="Microsoft.NET.Sdk">
       <PropertyGroup>
         <OutputType>Exe</OutputType>
         <TargetFramework>net8.0</TargetFramework>
         <ImplicitUsings>enable</ImplicitUsings>
         <Nullable>enable</Nullable>
       </PropertyGroup>
       <ItemGroup>
         <PackageReference Include="Microsoft.Build.Locator" Version="1.7.8" />
         <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.14.0" />
         <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.14.0" />
       </ItemGroup>
     </Project>
     ```

7. **Set Up call-graph-app**:
   - Create or update the Angular project:
     ```bash
     nvm use 18.20.8
     npm install -g @angular/cli@13.3.0
     ng new call-graph-app --style=css --routing=false --skip-tests
     cd call-graph-app
     npm install rxjs@7.5.0 tslib@2.3.0 cytoscape@3.23.0 cytoscape-dagre@2.4.0 jspdf@2.5.1
     npm install --save-dev typescript@4.6.2 @types/cytoscape@3.19.9 @types/cytoscape-dagre@2.3.0 @types/jspdf@2.0.0
     ```
   - Update `package.json`:
     ```json
     {
       "name": "call-graph-app",
       "version": "0.0.0",
       "scripts": {
         "ng": "ng",
         "start": "ng serve",
         "build": "ng build"
       },
       "private": true,
       "dependencies": {
         "@angular/animations": "~13.3.0",
         "@angular/common": "~13.3.0",
         "@angular/compiler": "~13.3.0",
         "@angular/core": "~13.3.0",
         "@angular/forms": "~13.3.0",
         "@angular/platform-browser": "~13.3.0",
         "@angular/platform-browser-dynamic": "~13.3.0",
         "@angular/router": "~13.3.0",
         "rxjs": "~7.5.0",
         "tslib": "^2.3.0",
         "cytoscape": "^3.23.0",
         "cytoscape-dagre": "^2.4.0",
         "jspdf": "^2.5.1"
       },
       "devDependencies": {
         "@angular-devkit/build-angular": "~13.3.0",
         "@angular/cli": "~13.3.0",
         "@angular/compiler-cli": "~13.3.0",
         "typescript": "~4.6.2",
         "@types/cytoscape": "^3.19.9",
         "@types/cytoscape-dagre": "^2.3.0",
         "@types/jspdf": "^2.0.0"
       }
     }
     ```
   - Replace Angular project files (e.g., `call-graph-direct.component.ts`, `.html`, `.css`, `app.module.ts`, etc.) with those from the previous artifact (version ID `2c18691d-08d9-4ff6-b2b5-0a9f68047d64`).

8. **Build and Install the Extension**:
   - In Visual Studio, build the `CallGraphExtension` project (Ctrl+Shift+B).
   - Locate the VSIX file in `bin/Debug/CallGraphExtension.vsix`.
   - Double-click the VSIX file to install, or use `Extensions > Manage Extensions` in Visual Studio.
   - Restart Visual Studio to load the extension.

9. **Test the Extension**:
   - Open a C# solution in Visual Studio 2022.
   - Navigate to a C# file, right-click a method name (e.g., `MyMethod` in `MyNamespace.MyClass`).
   - Select “Create Call Graph” from the context menu.
   - Expected behavior:
     - A command prompt opens.
     - Executes `SolutionAnalyzer.exe` with parameters (e.g., `"C:\Tools\SolutionAnalyzer.exe" "path/to/solution.sln" MyNamespace MyClass MyMethod`).
     - Copies `callchain.json` to `C:\Projects\call-graph-app\src\assets\`.
     - Navigates to `C:\Projects\call-graph-app` and runs `ng serve`.
     - The browser opens to `http://localhost:4200` (or navigate manually) to display the graph.

10. **Configure Paths**:
    - In `CallGraphCommand.cs`, update paths to match your environment:
      ```csharp
      string solutionAnalyzerPath = @"C:\Tools\SolutionAnalyzer.exe"; // Path to SolutionAnalyzer.exe
      string angularAppPath = @"C:\Projects\call-graph-app"; // Path to Angular project
      string jsonOutputPath = $@"{angularAppPath}\src\assets\callchain.json"; // Destination for callchain.json
      ```

### Troubleshooting
- **VSIX Template Not Found**:
  - Ensure the “Visual Studio extension development” workload is installed.
  - Verify the template in `File > New > Project` by searching “VSIX”.
- **Build Errors**:
  - Check NuGet package versions in `CallGraphExtension.csproj`.
  - Clear NuGet cache and restore:
    ```bash
    dotnet nuget locals all --clear
    dotnet restore
    ```
- **Menu Not Showing**:
  - Ensure GUIDs in `CallGraphCommand.cs` and `CallGraphCommand.vsct` match:
    - `guidCallGraphCommandPackage`: `e7b9c2f1-9b1a-4b2e-9c5f-7d8e6f2c3a1b`
    - `guidCallGraphCommandPackageCmdSet`: `a2b3c4d5-6e7f-8g9h-0i1j-2k3l4m5n6o7p`
  - Verify the command is registered for the code editor context (`IDM_VS_CTXT_CODEWIN`).
- **Method Details Not Detected**:
  - Ensure the caret is on the method name when right-clicking.
  - Log Roslyn diagnostics:
    ```csharp
    var diagnostics = (await document.GetSemanticModelAsync()).GetDiagnostics();
    foreach (var diag in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
    {
        System.Diagnostics.Debug.WriteLine($"Roslyn Error: {diag.GetMessage()}");
    }
    ```
- **Command Prompt Issues**:
  - Verify paths (`solutionAnalyzerPath`, `angularAppPath`, `jsonOutputPath`) are correct.
  - Ensure `SolutionAnalyzer.exe` is built and accessible.
  - Check that `ng serve` runs in the Angular project directory (Node.js and Angular CLI installed).
  - Run Visual Studio as an administrator if permission issues occur.
- **SolutionAnalyzer Errors**:
  - Check `SolutionAnalyzer` diagnostics in the command prompt output.
  - Ensure all project references are included in the `.sln` file.
- **Angular App Issues**:
  - Verify `callchain.json` is copied to `src/assets/`.
  - Check that `ng serve` starts successfully (Node.js 18.20.8, npm 10.8.2, Angular CLI 13.3.0).

### Notes
- **Path Configuration**: Update paths in `CallGraphCommand.cs` to match your setup. For production, consider a configuration UI (e.g., Visual Studio options page).
- **Icon**: The `.vsct` omits an icon for simplicity. Add a 16x16 PNG in `Resources/CallGraphCommand.png` and update `.vsct` if desired.
- **Performance**: Large solutions may slow down `SolutionAnalyzer`. Limit recursion depth in `SolutionAnalyzer` if needed:
  ```csharp
  static async Task<Graph> BuildCallGraph(Solution solution, string targetNamespace, string targetClass, string targetMethodName, int maxDepth = 5)
  ```
- **Security**: Ensure proper permissions for `cmd.exe` execution. Run Visual Studio as an administrator if needed.

This guide provides a complete setup for a .NET 8-targeted VSIX extension, integrating `SolutionAnalyzer` and `call-graph-app`. If you need additional features (e.g., configurable paths, enhanced UI), or encounter issues (e.g., extension not loading, command failures), please provide details (e.g., error messages, Visual Studio logs), and I can assist further.
