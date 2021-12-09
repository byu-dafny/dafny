using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;

namespace DafnyTestGeneration {

  public static class Main {

    /// <summary>
    /// This method returns each capturedState that is unreachable, one by one,
    /// and then a line with the summary of how many such states there are, etc.
    /// Note that loop unrolling may cause false positives and the absence of
    /// loop unrolling may cause false negatives.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> GetDeadCodeStatistics(Program program) {

      var modifications = GetModifications(program).ToList();
      var blocksReached = modifications.Count;
      HashSet<string> allStates = new();
      HashSet<string> allDeadStates = new();

      // Generate tests based on counterexamples produced from modifications
      for (var i = modifications.Count - 1; i >= 0; i--) {
        modifications[i].GetCounterExampleLog(i);
        var deadStates = ((BlockBasedModification)modifications[i])
          .GetKnownDeadStates();
        if (deadStates.Count != 0) {
          foreach (var capturedState in deadStates) {
            yield return $"Code at {capturedState} is potentially unreachable.";
          }
          blocksReached--;
          allDeadStates.UnionWith(deadStates);
        }
        allStates.UnionWith(((BlockBasedModification)modifications[i])
          .GetAllStates());
      }

      yield return $"Out of {modifications.Count} basic blocks " +
                   $"({allStates.Count} capturedStates), {blocksReached} " +
                   $"({allStates.Count - allDeadStates.Count}) are reachable. " +
                   $"There might be false negatives if you are not unrolling " +
                   $"loops. False positives are always possible.";
    }

    public static IEnumerable<string> GetDeadCodeStatistics(string sourceFile) {
      var source = new StreamReader(sourceFile).ReadToEnd();
      var program = Utils.Parse(source, sourceFile);
      if (program == null) {
        yield return "Cannot parse program";
        yield break;
      }
      foreach (var line in GetDeadCodeStatistics(program)) {
        yield return line;
      }
    }

    private static IEnumerable<ProgramModification> GetModifications(Program program) {
      // Translate the Program to Boogie:
      var oldPrintInstrumented = DafnyOptions.O.PrintInstrumented;
      DafnyOptions.O.PrintInstrumented = true;
      var boogiePrograms = Translator
        .Translate(program, program.reporter)
        .ToList().ConvertAll(tuple => tuple.Item2);
      DafnyOptions.O.PrintInstrumented = oldPrintInstrumented;

      // Create modifications of the program with assertions for each block\path
      ProgramModifier programModifier;
      switch (DafnyOptions.O.TestGenOptions.Mode) {
        case TestGenerationOptions.Modes.Path:
          programModifier = new PathBasedModifier();
          break;
        case TestGenerationOptions.Modes.Require:
          programModifier = new RequireBasedModifier();
          break;
        case TestGenerationOptions.Modes.Ensure:
          throw new NotImplementedException("Ensure is not done yet!");
        case TestGenerationOptions.Modes.Ensure_Strength:
          throw new NotImplementedException("Strength is not done yet!");
        case TestGenerationOptions.Modes.Block:
        default:
          // default to the block because why not
          programModifier = new BlockBasedModifier();
          break;
      }
      return programModifier.GetModifications(boogiePrograms);
    }

    /// <summary>
    /// Generate test methods for a certain Dafny program.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<TestMethod> GetTestMethodsForProgram(
      Program program, DafnyInfo? dafnyInfo = null) {

      dafnyInfo ??= new DafnyInfo(program);
      var modifications = GetModifications(program).ToList();

      if (DafnyOptions.O.TestGenOptions.PrintBoogieFile != null) {
        for (var i = modifications.Count - 1; i >= 0; i--) {
          //Console.Write(modifications[i].ToString());

          string filename = DafnyOptions.O.TestGenOptions.PrintBoogieFile;

          var tw = filename == "-" ? Console.Out : new StreamWriter(filename.Replace(".", "_modification_" + i + "."));
          tw.Write(modifications[i].ToString());
          tw.Flush();
        }
      }

      // Generate tests based on counterexamples produced from modifications
      var testMethods = new ConcurrentBag<TestMethod>();
      for (var i = modifications.Count - 1; i >= 0; i--) {
        var log = modifications[i].GetCounterExampleLog(i);
        if (log == null) {
          Console.Error.WriteLine("No counter example log found");
          continue;
        }

        // split the model
        string[] counterExampleSet = log
                  .Split("*** END_MODEL") // split counter examples on END_MODEL
                  .SkipLast(1) // don't keep the last empty one
                  .Select(x => x += "*** END_MODEL") // re-add the delimiter in becaused used in TestMethod
                  .ToArray(); // convert to array.

        Console.Error.WriteLine("CounterSet:" + counterExampleSet.Length);
        foreach (string ce in counterExampleSet) {
          Console.Error.WriteLine("Found a counter example!");
          var testMethod = new TestMethod(dafnyInfo, ce);
          if (testMethods.Contains(testMethod)) {
            continue;
          }
          testMethods.Add(testMethod);
          yield return testMethod;
        }

      }
    }

    /// <summary>
    /// Return a Dafny class (list of lines) with tests for the given Dafny file
    /// </summary>
    public static IEnumerable<string> GetTestClassForProgram(string sourceFile) {

      var source = new StreamReader(sourceFile).ReadToEnd();
      var program = Utils.Parse(source, sourceFile);
      if (program == null) {
        yield break;
      }
      var dafnyInfo = new DafnyInfo(program);
      var rawName = sourceFile.Split("/").Last().Split(".").First();

      yield return $"include \"{sourceFile}\"";
      yield return $"module {rawName}UnitTests {{";
      foreach (var module in dafnyInfo.ToImport) {
        yield return $"import {module}";
      }

      foreach (var method in GetTestMethodsForProgram(program, dafnyInfo)) {
        yield return method.ToString();
      }

      yield return "}";
    }
  }
}