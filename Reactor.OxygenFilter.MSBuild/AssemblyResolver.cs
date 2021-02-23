using System.Collections.Generic;
using Mono.Cecil;

namespace Reactor.OxygenFilter.MSBuild
{
    public class AssemblyResolver : DefaultAssemblyResolver
    {
        protected override AssemblyDefinition SearchDirectory(AssemblyNameReference name, IEnumerable<string> directories, ReaderParameters parameters)
        {
            if (name.Name == "netstandard")
                return null;

            return base.SearchDirectory(name, directories, parameters);
        }
    }
}
