using System;
using System.IO;
namespace BlackHoleBin {
internal static class PathSafety {
    internal static void RejectReparseChain(string path) {
        string current=Path.GetFullPath(path);
        while(!string.IsNullOrEmpty(current)) {
            if((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Symbolic links, junctions and reparse points are not allowed: "+current);
            string parent=Path.GetDirectoryName(current);
            if(string.Equals(parent,current,StringComparison.OrdinalIgnoreCase))break;
            current=parent;
        }
    }
    internal static string ValidateVault(string vault) {
        string root=Path.GetFullPath(RecoveryVault.RootDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string target=Path.GetFullPath(vault).TrimEnd(Path.DirectorySeparatorChar);
        if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(target),root,StringComparison.OrdinalIgnoreCase))
            throw new IOException("Recovery operation must target a direct child of the vault.");
        RejectReparseChain(target);
        return target;
    }
}
}
