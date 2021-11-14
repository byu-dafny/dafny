using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Program = Microsoft.Boogie.Program;
using System.Linq;

namespace DafnyTestGeneration {

  /// <summary>
  /// Records a modification of the boogie program under test. The modified
  /// program has an assertion that should fail provided a certain block is
  /// visited / path is taken.
  /// </summary>
  public class ProgramModification {

    private readonly string procedure; // procedure to be tested
    private readonly Program program;

    public ProgramModification(Program program, string procedure) {
      this.program = DeepCloneProgram(program);
      this.procedure = procedure;
    }

    /// <summary>
    /// Deep clone the program.
    /// </summary>
    private static Program DeepCloneProgram(Program program) {
      var oldPrintInstrumented = DafnyOptions.O.PrintInstrumented;
      var oldPrintFile = DafnyOptions.O.PrintFile;
      DafnyOptions.O.PrintInstrumented = true;
      DafnyOptions.O.PrintFile = "-";
      var textRepresentation = Utils.CaptureConsoleOutput(
        () => program.Emit(new TokenTextWriter(Console.Out)));
      Microsoft.Boogie.Parser.Parse(textRepresentation, "", out var copy);
      DafnyOptions.O.PrintInstrumented = oldPrintInstrumented;
      DafnyOptions.O.PrintFile = oldPrintFile;
      return copy;
    }

    /// <summary>
    /// Setup CommandLineArguments to prepare verification. This is necessary
    /// because the procsToCheck field in CommandLineOptions (part of Boogie)
    /// is private meaning that the only way of setting this field is by calling
    /// options.Parse() on a new DafnyObject.
    /// </summary>
    private static DafnyOptions SetupOptions(string procedure) {
      var options = new DafnyOptions();
      options.Parse(new[] { "/proc:" + procedure });
      options.EnhancedErrorMessages = 1;
      options.ModelViewFile = "-";
      options.ProverOptions = new List<string>() {
        "O:model_compress=false",
        "O:model_evaluator.completion=true",
        "O:model.completion=true"
      };
      options.ProverOptions.AddRange(DafnyOptions.O.ProverOptions);
      options.LoopUnrollCount = DafnyOptions.O.LoopUnrollCount;
      options.DefiniteAssignmentLevel = DafnyOptions.O.DefiniteAssignmentLevel;
      options.WarnShadowing = DafnyOptions.O.WarnShadowing;
      options.VerifyAllModules = DafnyOptions.O.VerifyAllModules;
      options.TimeLimit = DafnyOptions.O.TimeLimit;
      return options;
    }

    /// <summary>
    /// Return the counterexample log produced by trying to verify this modified
    /// version of the original boogie program. Return null if this
    /// counterexample does not cover any new SourceModifications.
    /// </summary>
    public virtual string? GetCounterExampleLog(int index) {
      var oldOptions = DafnyOptions.O;
      var options = SetupOptions(procedure);
      DafnyOptions.Install(options);
      var uniqueId = Guid.NewGuid().ToString();
      program.Resolve();
      program.Typecheck();
      ExecutionEngine.EliminateDeadVariables(program);
      ExecutionEngine.CollectModSets(program);
      ExecutionEngine.CoalesceBlocks(program);
      ExecutionEngine.Inline(program);

      // write the "pre-infer boogie" to a file
      if (oldOptions.TestGenOptions.PrintBoogieFile != null) {
        Console.Error.WriteLine("WRITING TO FILE");
        string filename = oldOptions.TestGenOptions.PrintBoogieFile;
        var tw = filename == "-" ? Console.Out : new StreamWriter(filename.Replace(".", "_modification_" + index + "_preexe."));

        var textRepresentation = (program != null) ?
        Utils.CaptureConsoleOutput(() => program.Emit(new TokenTextWriter(Console.Out))) : "";
        tw.Write(textRepresentation);
        tw.Flush();
      }

      var log = Utils.CaptureConsoleOutput(
        () => ExecutionEngine.InferAndVerify(program,
          new PipelineStatistics(), uniqueId,
          _ => { }, uniqueId));
      DafnyOptions.Install(oldOptions);

      if (oldOptions.TestGenOptions.PrintBoogieFile != null) {
        Console.Error.WriteLine("WRITING VERIFICATION RESULTS TO FILE");
        string filename = oldOptions.TestGenOptions.PrintBoogieFile;
        var tw = filename == "-" ? Console.Out : new StreamWriter(filename.Replace(".", "_modification_" + index + "_preexe_RES."));

        tw.Write(log);
        tw.Flush();
      }


      // make sure that there is a counterexample (i.e. no parse errors, etc):
      string? line;
      var stringReader = new StringReader(log);
      while ((line = stringReader.ReadLine()) != null) {
        if (line.StartsWith("Block |") || line.StartsWith("Impl |")) {
          return log;
        }
      }




      //Console.Error.WriteLine("Counter Example Log:" + log);
      return null;
    }

    // just dump a single implementation
    /*public override string ToString() {
      var original = base.ToString() ?? "";
      var procedureName = procedure ?? "";
      var implementation = program.Implementations.FirstOrDefault(i => i.Name == procedureName);
      var textRepresentation = (implementation != null) ?
        Utils.CaptureConsoleOutput(
          () => implementation.Emit(new TokenTextWriter(Console.Out), 0))
        : "";
      return original + "#" + procedureName + " :\n" + textRepresentation;
    }*/

    // dump the entire program
    public override string ToString() {
      var original = base.ToString() ?? "";
      var procedureName = procedure ?? "";
      var textRepresentation = (program != null) ?
        Utils.CaptureConsoleOutput(
          () => program.Emit(new TokenTextWriter(Console.Out)))
        : "";
      return textRepresentation;
    }

  }
}