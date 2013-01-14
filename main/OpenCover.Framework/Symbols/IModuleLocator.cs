namespace OpenCover.Framework.Symbols
{
    /// <summary>
    /// Locate module files
    /// </summary>
    public interface IModuleLocator
    {
        string LocateForAssembly(string assemblyName);
    }
}