namespace OpenCover.Framework.Symbols
{
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Attempt to locate module files on file system using configured search folders.
    /// </summary>
    public class ModuleLocator : IModuleLocator
    {
        private string[] _searchPaths;

        private string[] _extensions = new[] { ".dll", ".exe" };

        /// <summary>
        /// Create instance using specified command line 
        /// </summary>
        /// <param name="commandLine"></param>
        public ModuleLocator(ICommandLine commandLine)
        {
            this._searchPaths = new string[0];
            if (!string.IsNullOrEmpty(commandLine.SymbolDir))
            {
                this._searchPaths = commandLine.SymbolDir.Split(';').Where(x => !string.IsNullOrEmpty(x)).ToArray();
            }
        }

        public string LocateForAssembly(string assemblyName)
        {
            return
                this._searchPaths.SelectMany(
                    searchPath => this._extensions,
                    (searchPath, extension) => Path.Combine(searchPath, assemblyName + extension))
                    .FirstOrDefault(File.Exists);
        }
    }
}