//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny {
  public class Main {
    /// <summary>
    /// Returns null on success, or an error string otherwise.
    /// </summary>
    public static string ParseCheck(List<string/*!*/>/*!*/ fileNames, string/*!*/ programName, out Program program)
      //modifies Bpl.CommandLineOptions.Clo.XmlSink.*;
    {
      Contract.Requires(programName != null);
      Contract.Requires(fileNames != null);
      program = null;
      ModuleDecl module = new LiteralModuleDecl(new DefaultModuleDecl(), null);
      BuiltIns builtIns = new BuiltIns();
      foreach (string dafnyFileName in fileNames){
        Contract.Assert(dafnyFileName != null);
        if (Bpl.CommandLineOptions.Clo.XmlSink != null && Bpl.CommandLineOptions.Clo.XmlSink.IsOpen) {
          Bpl.CommandLineOptions.Clo.XmlSink.WriteFileFragment(dafnyFileName);
        }
        if (Bpl.CommandLineOptions.Clo.Trace)
        {
          Console.WriteLine("Parsing " + dafnyFileName);
        }

        string err = ParseFile(dafnyFileName, Bpl.Token.NoToken, module, builtIns, new Errors());
        if (err != null) {
          return err;
        }        
      }

      if (!DafnyOptions.O.DisallowIncludes) {
        string errString = ParseIncludes(module, builtIns, new Errors());
        if (errString != null) {
          return errString;
        }
      }

      program = new Program(programName, module, builtIns);

      if (DafnyOptions.O.DafnyPrintFile != null) {
        string filename = DafnyOptions.O.DafnyPrintFile;
        if (filename == "-") {
          Printer pr = new Printer(System.Console.Out);
          pr.PrintProgram(program);
        } else {
          using (TextWriter writer = new System.IO.StreamWriter(filename)) {
            Printer pr = new Printer(writer);
            pr.PrintProgram(program);
          }
        }
      }

      if (Bpl.CommandLineOptions.Clo.NoResolve || Bpl.CommandLineOptions.Clo.NoTypecheck) { return null; }

      Dafny.Resolver r = new Dafny.Resolver(program);
      r.ResolveProgram(program);
      if (DafnyOptions.O.DafnyPrintResolvedFile != null) {
        string filename = DafnyOptions.O.DafnyPrintResolvedFile;
        if (filename == "-") {
          Printer pr = new Printer(System.Console.Out);
          pr.PrintProgram(program);
        } else {
          using (TextWriter writer = new System.IO.StreamWriter(filename)) {
            Printer pr = new Printer(writer);
            pr.PrintProgram(program);
          }
        }
      }
      if (r.ErrorCount != 0) {
        return string.Format("{0} resolution/type errors detected in {1}", r.ErrorCount, programName);
      }

      return null;  // success
    }

    // Lower-case file names before comparing them, since Windows uses case-insensitive file names
    private class IncludeComparer : IComparer<Include> {
      public int Compare(Include x, Include y) {
        return x.fullPath.ToLower().CompareTo(y.fullPath.ToLower());
      }
    }

    public static string ParseIncludes(ModuleDecl module, BuiltIns builtIns, Errors errs) {
      SortedSet<Include> includes = new SortedSet<Include>(new IncludeComparer());
      bool newlyIncluded;
      do {
        newlyIncluded = false;

        List<Include> newFilesToInclude = new List<Include>();
        foreach (Include include in ((LiteralModuleDecl)module).ModuleDef.Includes) {
          bool isNew = includes.Add(include);
          if (isNew) {
            newlyIncluded = true;
            newFilesToInclude.Add(include);
          }
        }

        foreach (Include include in newFilesToInclude) {
          string ret = ParseFile(include.filename, include.tok, module, builtIns, errs, false);
          if (ret != null) {
            return ret;
          }
        }
      } while (newlyIncluded);

      return null; // Success
    }

    private static string ParseFile(string dafnyFileName, Bpl.IToken tok, ModuleDecl module, BuiltIns builtIns, Errors errs, bool verifyThisFile = true) {
      try {
        int errorCount = Dafny.Parser.Parse(dafnyFileName, module, builtIns, errs, verifyThisFile);
        if (errorCount != 0) {
          return string.Format("{0} parse errors detected in {1}", errorCount, dafnyFileName);
        }
      } catch (IOException e) {
        errs.SemErr(tok, "Unable to open included file");
        return string.Format("Error opening file \"{0}\": {1}", dafnyFileName, e.Message);
      }
      return null; // Success
    }

  }
}
