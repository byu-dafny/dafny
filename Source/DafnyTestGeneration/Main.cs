using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;
using System.Diagnostics;

namespace DafnyTestGeneration {

  public static class Main {

    /// <summary>
    /// This method returns each capturedState that is unreachable, one by one,
    /// and then a line with the summary of how many such states there are, etc.
    /// Note that loop unrolling may cause false positives and the absence of
    /// loop unrolling may cause false negatives.
    /// </summary>
    /// <returns></returns>
    public static async IAsyncEnumerable<string> GetDeadCodeStatistics(Program program) {
      
      DafnyOptions.O.PrintMode = DafnyOptions.PrintModes.Everything;
      ProgramModification.ResetStatistics();
      var modifications = GetModifications(program).ToEnumerable().ToList();
      var blocksReached = modifications.Count;
      HashSet<string> allStates = new();
      HashSet<string> allDeadStates = new();

      // Generate tests based on counterexamples produced from modifications
      for (var i = modifications.Count - 1; i >= 0; i--) {
        await modifications[i].GetCounterExampleLog();
        var deadStates = new HashSet<string>();
        if (!modifications[i].IsCovered) {
          deadStates = modifications[i].CapturedStates;
        }

        if (deadStates.Count != 0) {
          foreach (var capturedState in deadStates) {
            yield return $"Code at {capturedState} is potentially unreachable.";
          }
          blocksReached--;
          allDeadStates.UnionWith(deadStates);
        }
        allStates.UnionWith(modifications[i].CapturedStates);
      }

      yield return $"Out of {modifications.Count} basic blocks " +
                   $"({allStates.Count} capturedStates), {blocksReached} " +
                   $"({allStates.Count - allDeadStates.Count}) are reachable. " +
                   "There might be false negatives if you are not unrolling " +
                   "loops. False positives are always possible.";
    }

    public static async IAsyncEnumerable<string> GetDeadCodeStatistics(string sourceFile) {
      DafnyOptions.O.PrintMode = DafnyOptions.PrintModes.Everything;
      var source = await new StreamReader(sourceFile).ReadToEndAsync();
      var program = Utils.Parse(source, sourceFile);
      if (program == null) {
        yield return "Cannot parse program";
        yield break;
      }
      await foreach (var line in GetDeadCodeStatistics(program)) {
        yield return line;
      }
    }

    private static IAsyncEnumerable<ProgramModification> GetModifications(Program program) {
      var dafnyInfo = new DafnyInfo(program);
      // Translate the Program to Boogie:
      var oldPrintInstrumented = DafnyOptions.O.PrintInstrumented;
      DafnyOptions.O.PrintInstrumented = true;
      var boogiePrograms = Translator
        .Translate(program, program.Reporter)
        .ToList().ConvertAll(tuple => tuple.Item2);
      DafnyOptions.O.PrintInstrumented = oldPrintInstrumented;

      // Create modifications of the program with assertions for each block\path
      ProgramModifier programModifier =
        DafnyOptions.O.TestGenOptions.Mode == TestGenerationOptions.Modes.Path
          ? new PathBasedModifier()
          : new BlockBasedModifier();
      return programModifier.GetModifications(boogiePrograms, dafnyInfo);
    }

    /// <summary>
    /// Generate test methods for a certain Dafny program.
    /// </summary>
    /// <returns></returns>
    public static async IAsyncEnumerable<TestMethod> GetTestMethodsForProgram(Program program) {
      
      DafnyOptions.O.PrintMode = DafnyOptions.PrintModes.Everything;
      ProgramModification.ResetStatistics();
      var dafnyInfo = new DafnyInfo(program);
      HashSet<Implementation> implementations = new();
      Dictionary<Implementation, int> testCount = new();
      Dictionary<Implementation, int> failedTestCount = new();
      // Generate tests based on counterexamples produced from modifications
      var numTestsGenerated = 0;
      HashSet<string> blocksToSkip = DafnyOptions.O.TestGenOptions.blocksToSkip;

      await foreach (var modification in GetModifications(program)) {
        var blockCapturedState = "";
        if (DafnyOptions.O.TestGenOptions.maxTests >= 0) {
          if (numTestsGenerated >= DafnyOptions.O.TestGenOptions.maxTests) {
            yield break;
          }

          if (modification.CapturedStates.Count == 0) {
            continue;
          }

          blockCapturedState = modification.CapturedStates.ToList().First();

          if (blocksToSkip.Contains(blockCapturedState)) {
            Console.WriteLine("// Skipping " +
                              modification.CapturedStates.ToList().First());
            continue;
          }

          Console.WriteLine("// current block:" + blockCapturedState);
        }

        var log = await modification.GetCounterExampleLog();
        implementations.Add(modification.Implementation);
        if (log == null) {
          continue;
        }
        var testMethod = await modification.GetTestMethod(dafnyInfo);
        if (testMethod == null) {
          continue;
        }
        if (!testMethod.IsValid) {
          failedTestCount[modification.Implementation] =
            failedTestCount.GetValueOrDefault(modification.Implementation, 0) +
            1;
        }
        testCount[modification.Implementation] =
          testCount.GetValueOrDefault(modification.Implementation, 0) + 1;
        if (testMethod.IsValid) {
          Console.WriteLine("// newly covered:" + blockCapturedState);
          numTestsGenerated += 1;

          // Write out the set of covered lines.
          if (DafnyOptions.O.TestGenOptions.coveredBlocksFile != null) {
            var alreadyCovered = DafnyOptions.O.TestGenOptions.blocksToSkip;
            alreadyCovered.Add(blockCapturedState);
            var coveredLines = alreadyCovered.ToList();
            File.WriteAllLines(DafnyOptions.O.TestGenOptions.coveredBlocksFile, coveredLines);
          }

          // Save Dafny file for re-constructing the generated test input.
          if (DafnyOptions.O.TestGenOptions.inputConstructorFile != null) {
            var inputLines = testMethod.TestInputConstructionLines();
            File.WriteAllLines(DafnyOptions.O.TestGenOptions.inputConstructorFile, inputLines);
          }
        }
        yield return testMethod;
      }

      if (DafnyOptions.O.TestGenOptions.PrintStats != null) {
        StatsPrinter printer = new StatsPrinter();
        printer.PopulateInformation(dafnyInfo, implementations, testCount, failedTestCount);
        printer.WriteToFile(DafnyOptions.O.TestGenOptions.PrintStats);
      }
    }

    /// <summary>
    /// Return a Dafny class (list of lines) with tests for the given Dafny file
    /// </summary>
    public static async IAsyncEnumerable<string> GetTestClassForProgram(string sourceFile) {
      
      DafnyOptions.O.PrintMode = DafnyOptions.PrintModes.Everything;
      TestMethod.ClearTypesToSynthesize();
      var source = new StreamReader(sourceFile).ReadToEnd();
      var program = Utils.Parse(source, sourceFile);
      if (program == null) {
        yield break;
      }
      var dafnyInfo = new DafnyInfo(program);
      var rawName = Regex.Replace(sourceFile, "[^a-zA-Z0-9_]", "");

      string EscapeDafnyStringLiteral(string str) {
        return $"\"{str.Replace(@"\", @"\\")}\"";
      }

      yield return $"include {EscapeDafnyStringLiteral(sourceFile)}";
      yield return $"module {rawName}UnitTests {{";
      foreach (var module in dafnyInfo.ToImportAs.Keys) {
        // TODO: disambiguate between modules amongst generated tests
        if (module.Split(".").Last() == dafnyInfo.ToImportAs[module]) {
          yield return $"import {module}";
        } else {
          yield return $"import {dafnyInfo.ToImportAs[module]} = {module}";
        }
      }

      await foreach (var method in GetTestMethodsForProgram(program)) {
        yield return method.ToString();
      }
      
      yield return TestMethod.EmitSynthesizeMethods(dafnyInfo);
      yield return "}";
    }
  }
}