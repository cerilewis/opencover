﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace OpenCover.Gendarme.Signer
{
    class Program
    {
        private const string TargetFolder = @"..\tools\GendarmeSigned";
        private const string SourceFolder = @"packages\Mono.Gendarme.2.11.0.20121120\tools";
        private const string StrongNameKey = @"..\build\Version\opencover.gendarme.snk";

        static void Main(string[] args)
        {


            var baseFolder = Path.Combine(Assembly.GetAssembly(typeof(Program)).Location, @"..\..\..\..");

            if (!Directory.Exists(Path.Combine(baseFolder, TargetFolder))) 
                Directory.CreateDirectory(Path.Combine(baseFolder, TargetFolder));

            if (AlreadySigned(baseFolder))
            {
                Console.WriteLine("Gendarme Framework is already Signed");
                return;
            }

            Console.WriteLine("Signing Gendarme Framework");
            SignGendarmeFramework(baseFolder);

            Console.WriteLine("Signing Gendarme Rules Maintainability");
            SignGendarmeRulesMaintainability(baseFolder);
        }

        private static bool AlreadySigned(string baseFolder)
        {
            var frameworkAssembly = Path.Combine(baseFolder, TargetFolder + @"\Gendarme.Framework.dll");
            if (File.Exists(frameworkAssembly))
            {
                try
                {
                    var frameworkDefinition = AssemblyDefinition.ReadAssembly(frameworkAssembly);
                    return frameworkDefinition.Name.HasPublicKey;
                }
                catch
                {
                }
            }
            return false;
        }

        private static void SignGendarmeRulesMaintainability(string baseFolder)
        {
            var frameworkAssembly = Path.Combine(baseFolder, TargetFolder + @"\Gendarme.Framework.dll");
            var frameworkDefinition = AssemblyDefinition.ReadAssembly(frameworkAssembly);
            var frameworkAssemblyRef = AssemblyNameReference.Parse(frameworkDefinition.Name.ToString());

            var key = Path.Combine(baseFolder, StrongNameKey);
            var assembly = Path.Combine(baseFolder, SourceFolder + @"\Gendarme.Rules.Maintainability.dll");
            var newAssembly = Path.Combine(baseFolder, TargetFolder + @"\Gendarme.Rules.Maintainability.dll");

            assembly = Path.GetFullPath(assembly);
            newAssembly = Path.GetFullPath(newAssembly);

            File.Copy(assembly, newAssembly, true);
            var definition = AssemblyDefinition.ReadAssembly(newAssembly);

            // update all type references to the now signed base assembly
            foreach (var typeReference in definition.MainModule.GetTypeReferences())
            {
                if (typeReference.Scope.Name == frameworkDefinition.Name.Name)
                {
                    typeReference.Scope = frameworkAssemblyRef;
                }
            }

            // update assembly references to use the now signed base assembly
            var oldReference = definition.MainModule.AssemblyReferences.FirstOrDefault(x => x.Name == frameworkDefinition.Name.Name);
            if (oldReference != null)
            {
                definition.MainModule.AssemblyReferences.Remove(oldReference);
                definition.MainModule.AssemblyReferences.Add(frameworkAssemblyRef);
            }

            var keyPair = new StrongNameKeyPair(new FileStream(key, FileMode.Open, FileAccess.Read));
            definition.Write(newAssembly, new WriterParameters() { StrongNameKeyPair = keyPair });

        }

        private static void SignGendarmeFramework(string baseFolder)
        {
            var key = Path.Combine(baseFolder, StrongNameKey);
            var assembly = Path.Combine(baseFolder, SourceFolder + @"\Gendarme.Framework.dll");
            var newAssembly = Path.Combine(baseFolder, TargetFolder + @"\Gendarme.Framework.dll");

            assembly = Path.GetFullPath(assembly);
            newAssembly = Path.GetFullPath(newAssembly);

            File.Copy(assembly, newAssembly, true);
            var definition = AssemblyDefinition.ReadAssembly(newAssembly);
            var keyPair = new StrongNameKeyPair(new FileStream(key, FileMode.Open, FileAccess.Read));
            definition.Write(newAssembly, new WriterParameters() { StrongNameKeyPair = keyPair });
        }
    }
}
