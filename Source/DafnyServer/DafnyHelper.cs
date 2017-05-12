using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Boogie;
using DafnyServer;
using Microsoft.Boogie.ModelViewer;
using Microsoft.Boogie.ModelViewer.Dafny;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny {
  // FIXME: This should not be duplicated here
  class DafnyConsolePrinter : ConsolePrinter {
    public override void ReportBplError(IToken tok, string message, bool error, TextWriter tw, string category = null) {
      // Dafny has 0-indexed columns, but Boogie counts from 1
      var realigned_tok = new Token(tok.line, tok.col - 1);
      realigned_tok.kind = tok.kind;
      realigned_tok.pos = tok.pos;
      realigned_tok.val = tok.val;
      realigned_tok.filename = tok.filename;
      base.ReportBplError(realigned_tok, message, error, tw, category);

      if (tok is Dafny.NestedToken) {
        var nt = (Dafny.NestedToken)tok;
        ReportBplError(nt.Inner, "Related location", false, tw);
      }
    }
  }

  class DafnyHelper {
    private string fname;
    private string source;
    private string[] args;

    private readonly Dafny.ErrorReporter reporter;
    private Dafny.Program dafnyProgram;
    private IEnumerable<Tuple<string, Bpl.Program>> boogiePrograms;

    public DafnyHelper(string[] args, string fname, string source) {
      this.args = args;
      this.fname = fname;
      this.source = source;
      this.reporter = new Dafny.ConsoleErrorReporter();
    }

    public bool Verify() {
      ServerUtils.ApplyArgs(args, reporter);
      return Parse() && Resolve() && Translate() && Boogie();
    }

    private bool Parse() {
      Dafny.ModuleDecl module = new Dafny.LiteralModuleDecl(new Dafny.DefaultModuleDecl(), null);
      Dafny.BuiltIns builtIns = new Dafny.BuiltIns();
      var success =
          (Dafny.Parser.Parse(source, fname, fname, null, module, builtIns, new Dafny.Errors(reporter)) == 0 &&
           Dafny.Main.ParseIncludes(module, builtIns, new List<string>(), new Dafny.Errors(reporter)) == null);
      if (success) {
        dafnyProgram = new Dafny.Program(fname, module, builtIns, reporter);
      }
      return success;
    }

    private bool Resolve() {
      var resolver = new Dafny.Resolver(dafnyProgram);
      resolver.ResolveProgram(dafnyProgram);
      return reporter.Count(ErrorLevel.Error) == 0;
    }

    private bool Translate() {
      boogiePrograms = Translator.Translate(dafnyProgram, reporter,
          new Translator.TranslatorFlags() { InsertChecksums = true, UniqueIdPrefix = fname });
      // FIXME how are translation errors reported?
      return true;
    }

    private bool BoogieOnce(Bpl.Program boogieProgram) {
      if (boogieProgram.Resolve() == 0 && boogieProgram.Typecheck() == 0) {

        //FIXME ResolveAndTypecheck?
        ExecutionEngine.EliminateDeadVariables(boogieProgram);
        ExecutionEngine.CollectModSets(boogieProgram);
        ExecutionEngine.CoalesceBlocks(boogieProgram);
        ExecutionEngine.Inline(boogieProgram);

        //NOTE: We could capture errors instead of printing them (pass a delegate instead of null)
        switch (
            ExecutionEngine.InferAndVerify(boogieProgram, new PipelineStatistics(), "ServerProgram", null,
                DateTime.UtcNow.Ticks.ToString())) {
          case PipelineOutcome.Done:
          case PipelineOutcome.VerificationCompleted:
            return true;
        }
      }

      return false;
    }

    private bool Boogie() {
      var isVerified = true;
      foreach (var boogieProgram in boogiePrograms) {
        isVerified = isVerified && BoogieOnce(boogieProgram.Item2);
      }
      return isVerified;
    }

    public void Symbols() {
      ServerUtils.ApplyArgs(args, reporter);
      if (Parse() && Resolve()) {
        var symbolTable = new SymbolTable(dafnyProgram);
        var json = symbolTable.ToJson();
        Console.WriteLine("SYMBOLS_START " + json + " SYMBOLS_END");
      } else {
        Console.WriteLine("SYMBOLS_START [] SYMBOLS_END");
      }
    }

    public void CounterExample() {
      var listArgs = args.ToList();
      listArgs.Add("/mv:" + CounterExampleProvider.ModelBvd);
      ServerUtils.ApplyArgs(listArgs.ToArray(), reporter);
      try {
        if (Parse() && Resolve() && Translate()) {
          var counterExampleProvider = new CounterExampleProvider();
          foreach (var boogieProgram in boogiePrograms) {
            RemoveExistingModel();
            BoogieOnce(boogieProgram.Item2);
            counterExampleProvider.LoadModel();
            var json = counterExampleProvider.ToJson();
            Console.WriteLine("COUNTEREXAMPLE_START " + json + " COUNTEREXAMPLE_END");
          }
        }
      } catch (Exception e) {
        Console.WriteLine("Error collection models: " + e.Message);
      }
    }

    private void RemoveExistingModel() {
      if (File.Exists(CounterExampleProvider.ModelBvd)) {
        File.Delete(CounterExampleProvider.ModelBvd);
      }
    }
  }
}