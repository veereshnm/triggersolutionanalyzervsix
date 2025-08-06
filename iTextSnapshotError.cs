using System;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using EnvDTE;

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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            var dte = await ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            string solutionPath = dte?.Solution?.FullName;
            string methodName = GetSelectedMethodName(dte);
            string className = await GetContainingClassNameAsync();
            string namespaceName = await GetContainingNamespaceNameAsync();

            if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(className) || string.IsNullOrEmpty(namespaceName) || string.IsNullOrEmpty(solutionPath))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await Task.Yield();
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "Unable to determine method details. Please select a method name in a C# file.",
                        "Call Graph Extension",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                });
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

        private string GetSelectedMethodName(DTE dte)
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
            var textManager = await ServiceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) return (null, 0);

            IVsTextView textViewCurrent = null;
            textManager.GetActiveView(1, null, out textViewCurrent);
            if (textViewCurrent == null) return (null, 0);

            textViewCurrent.GetCaretPos(out int line, out int column);

            var componentModel = await ServiceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            var editorAdaptersFactoryService = componentModel?.GetService<IVsEditorAdaptersFactoryService>();
            var wpfTextView = editorAdaptersFactoryService?.GetWpfTextView(textViewCurrent);
            if (wpfTextView == null) return (null, 0);

            var caretPosition = wpfTextView.Caret.Position.BufferPosition.Position;

            // Get the DTE service to find the active document's file path
            var dte = await ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            var activeDocument = dte?.ActiveDocument;
            if (activeDocument == null) return (null, caretPosition);

            // Get the Roslyn workspace and solution
            var workspace = await ServiceProvider.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            if (workspace == null) return (null, caretPosition);

            // Get the current solution
            var solution = dte?.Solution;
            if (solution == null) return (null, caretPosition);

            // Find the Roslyn document corresponding to the active document's file path
            var documentPath = activeDocument.FullName;
            var roslynWorkspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            var roslynSolution = await roslynWorkspace.OpenSolutionAsync(solution.FullName);
            var roslynDocument = roslynSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath.Equals(documentPath, StringComparison.OrdinalIgnoreCase));

            return (roslynDocument, caretPosition);
        }
    }
}


