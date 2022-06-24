using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Function = Microsoft.Dafny.Function;
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
    public static async IAsyncEnumerable<string> GetDeadCodeStatistics(Program program) {

      var dafnyInfo = new DafnyInfo(program);
      var modifications = GetModifications(program, dafnyInfo).ToList();
      var blocksReached = modifications.Count;
      HashSet<string> allStates = new();
      HashSet<string> allDeadStates = new();

      // Generate tests based on counterexamples produced from modifications
      for (var i = modifications.Count - 1; i >= 0; i--) {
        await modifications[i].GetCounterExampleLog();
        var deadStates = ((BlockBasedModification)modifications[i]).GetKnownDeadStates();
        if (deadStates.Count != 0) {
          foreach (var capturedState in deadStates) {
            yield return $"Code at {capturedState} is potentially unreachable.";
          }
          blocksReached--;
          allDeadStates.UnionWith(deadStates);
        }
        allStates.UnionWith(((BlockBasedModification)modifications[i]).GetAllStates());
      }

      yield return $"Out of {modifications.Count} basic blocks " +
                   $"({allStates.Count} capturedStates), {blocksReached} " +
                   $"({allStates.Count - allDeadStates.Count}) are reachable. " +
                   $"There might be false negatives if you are not unrolling " +
                   $"loops. False positives are always possible.";
    }

    public static async IAsyncEnumerable<string> GetDeadCodeStatistics(string sourceFile) {
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

    private static IEnumerable<ProgramModification> GetModifications(Program program, DafnyInfo dafnyInfo) {
      // Substitute function methods with function-by-methods
      new AddByMethodRewriter(new ConsoleErrorReporter()).PreResolve(program);
      program.Reporter = new ErrorReporterSink();
      new Resolver(program).ResolveProgram(program);
      // Translate the Program to Boogie:
      var oldPrintInstrumented = DafnyOptions.O.PrintInstrumented;
      DafnyOptions.O.PrintInstrumented = true;
      var boogiePrograms = Translator
        .Translate(program, program.Reporter, new Translator.TranslatorFlags() {disableShortCircuit = true})
        .ToList().ConvertAll(tuple => tuple.Item2);
      DafnyOptions.O.PrintInstrumented = oldPrintInstrumented;

      // Create modifications of the program with assertions for each block\path
      ProgramModifier programModifier =
        DafnyOptions.O.TestGenOptions.Mode == TestGenerationOptions.Modes.Path ? new PathBasedModifier() :
        DafnyOptions.O.TestGenOptions.Mode == TestGenerationOptions.Modes.Branch ? new BranchBasedModifier()
        : new BlockBasedModifier();
       return programModifier.GetModifications(boogiePrograms, dafnyInfo);
    }

    /// <summary>
    /// Generate test methods for a certain Dafny program.
    /// </summary>
    /// <returns></returns>
    public static async IAsyncEnumerable<TestMethod> GetTestMethodsForProgram(
      Program program, DafnyInfo? dafnyInfo = null) {

      dafnyInfo ??= new DafnyInfo(program);
      var modifications = GetModifications(program, dafnyInfo).ToList();

      // Generate tests based on counterexamples produced from modifications
      var testMethods = new ConcurrentBag<TestMethod>();
      for (var i = modifications.Count - 1; i >= 0; i--) {
        var log = await modifications[i].GetCounterExampleLog();
        if (log == null) {
          continue;
        }
        var testMethod = new TestMethod(dafnyInfo, log);
        if (testMethods.Contains(testMethod)) {
          continue;
        }
        testMethods.Add(testMethod);
        yield return testMethod;
      }
    }

    /// <summary>
    /// Return a Dafny class (list of lines) with tests for the given Dafny file
    /// </summary>
    public static async IAsyncEnumerable<string> GetTestClassForProgram(string sourceFile) {

      TestMethod.ClearTypesToSynthesize();
      var source = new StreamReader(sourceFile).ReadToEnd();
      var program = Utils.Parse(source, sourceFile);
      if (program == null) {
        yield break;
      }
      var dafnyInfo = new DafnyInfo(program);
      var rawName = Path.GetFileName(sourceFile).Split(".").First();

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

      await foreach (var method in GetTestMethodsForProgram(program, dafnyInfo)) {
        yield return method.ToString();
      }

      yield return TestMethod.EmitSynthesizeMethods();

      yield return "}";
    }

    private class AddByMethodRewriter : IRewriter {

      protected internal AddByMethodRewriter(ErrorReporter reporter) : base(reporter) { }

      /// <summary>
      /// Turns each function-method into a function-by-method.
      /// Copies body of the function into the body of the corresponding method.
      /// </summary>
      internal void PreResolve(Program program) {
        AddByMethod(program.DefaultModule);
      }

      private static void AddByMethod(TopLevelDecl d) {
        if (d is LiteralModuleDecl moduleDecl) {
          moduleDecl.ModuleDef.TopLevelDecls.ForEach(AddByMethod);
        } else if (d is TopLevelDeclWithMembers withMembers) {
          withMembers.Members.OfType<Function>().Iter(AddByMethod);
        }
      }

      private static void AddByMethod(Function func) {
        if (func.IsGhost || func.Body == null || func.ByMethodBody != null) {
          return;
        }
        var returnStatement = new ReturnStmt(new Token(), new Token(),
          new List<AssignmentRhs> { new ExprRhs(func.Body) });
        func.ByMethodBody = new BlockStmt(new Token(), new Token(),
          new List<Statement> { returnStatement });
      }
    }
  }
}