using System;
using JetBrains.Annotations;
using Bpl = Microsoft.Boogie;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Dafny {

  public class TestGenerationOptions {

    public bool WarnDeadCode = false;
    public enum Modes { None, Block, Path };
    public enum Oracles { None, Spec };
    public enum Minimizations {Topological, Optimal, Random};
    public Modes Mode = Modes.None;
    public Oracles Oracle = Oracles.None;
    public Minimizations Minimization = Minimizations.Topological;
    [CanBeNull] public string TargetMethod = null;
    public uint? SeqLengthLimit = null;
    public uint TestInlineDepth = 0;
    public uint? Fuel = null;
    public bool Verbose = false;
    public bool noPrune = false;
    [CanBeNull] public string PrintBpl = null;
    [CanBeNull] public string PrintStats = null;

    public HashSet<string> blocksToSkip = new HashSet<string>();
    public HashSet<int> blockIdsToSkip = new HashSet<int>();

    [CanBeNull] public string coveredBlocksFile = null;
    [CanBeNull] public string inputConstructorFile = null;
    public int maxTests = -1;


    public bool ParseOption(string name, Bpl.CommandLineParseState ps) {
      var args = ps.args;

      switch (name) {

        case "warnDeadCode":
          WarnDeadCode = true;
          Mode = Modes.Block;
          return true;
        
        case "noPrune":
          noPrune = true;
          return true;

        case "generateTestMode":
          if (ps.ConfirmArgumentCount(1)) {
            Mode = args[ps.i] switch {
              "None" => Modes.None,
              "Block" => Modes.Block,
              "Path" => Modes.Path,
              _ => throw new Exception("Invalid value for generateTestMode")
            };
          }
          return true;
        
        case "generateTestMinimization":
          if (ps.ConfirmArgumentCount(1)) {
            Minimization = args[ps.i] switch {
              "Optimal" => Minimizations.Optimal,
              "Random" => Minimizations.Random,
              "InOrder" => Minimizations.Topological,
              _ => throw new Exception("Invalid value for generateTestMode")
            };
          }
          return true;

        case "generateTestOracle":
          if (ps.ConfirmArgumentCount(1)) {
            Oracle = args[ps.i] switch {
              "None" => Oracles.None,
              "Spec" => Oracles.Spec,
              _ => throw new Exception("Invalid value for generateTestOracle")
            };
          }
          return true;

        case "generateTestSeqLengthLimit":
          var limit = 0;
          if (ps.GetIntArgument(ref limit)) {
            SeqLengthLimit = (uint)limit;
          }
          return true;
        
        case "generateTestFuel":
          var fuel = 0;
          if (ps.GetIntArgument(ref fuel)) {
            Fuel = (uint)fuel;
          }
          return true;

        case "generateTestTargetMethod":
          if (ps.ConfirmArgumentCount(1)) {
            TargetMethod = args[ps.i];
          }
          return true;

        case "generateTestInlineDepth":
          var depth = 0;
          if (ps.GetIntArgument(ref depth)) {
            TestInlineDepth = (uint)depth;
          }
          return true;

        case "generateTestPrintBpl":
          if (ps.ConfirmArgumentCount(1)) {
            PrintBpl = args[ps.i];
          }
          return true;

        case "generateTestPrintStats":
          if (ps.ConfirmArgumentCount(1)) {
            PrintStats = args[ps.i];
          }
          return true;
        
        case "generateTestVerbose":
          Verbose = true;
          return true;

        // Whether to load/save set of pre-covered blocks from an external file.
        case "generateTestLoadCovered":
          if (ps.ConfirmArgumentCount(1)) {
            coveredBlocksFile = args[ps.i];
            var coveredLines = File.ReadAllLines(coveredBlocksFile);
            foreach (string line in coveredLines) {
              blocksToSkip.Add(line);
            }
          }
          return true;

        // Whether to record constructor for generated test input into file.
        case "generateTestSaveInputConstructor":
          if (ps.ConfirmArgumentCount(1)) {
            inputConstructorFile = args[ps.i];
          }
          return true;

        // Maximum number of tests to generate.
        case "generateTestMaxTests":
          var numMaxTests = -1;  
          if (ps.GetIntArgument(ref numMaxTests)) {
            maxTests = numMaxTests;
          }
          return true;
      }

      return false;
    }

    public string Help => @"
/generateTestMode:<None|Block|Path>
    None is the default and has no effect.
    Block prints block-coverage tests for the given program.
    Path prints path-coverage tests for the given program.
    Using /definiteAssignment:3 and /loopUnroll is highly recommended when
    generating tests.
    Please also consider using /prune, which generates more test at the cost
    of weaker test correctness guarantees
/generateTestOracle:<None|Spec>
    Determines the kind of oracles generated for the tests.
    None is the default and has no effect (the test contains no runtime checks).
    Spec asks the tool to generate runtime checks based on method specification
/warnDeadCode
    Use block-coverage tests to warn about potential dead code.
/generateTestSeqLengthLimit:<n>
    If /testMode is not None, using this argument adds an axiom that sets the
    length of all sequences to be no greater than <n>. This is useful in
    conjunction with loop unrolling.
/generateTestTargetMethod:<methodName>
    If specified, only this method will be tested.
/generateTestInlineDepth:<n>
    0 is the default. When used in conjunction with /testTargetMethod, this
    argument specifies the depth up to which all non-tested methods should be
    inlined.
/generateTestPrintBpl:<fileName>
    Print the Boogie code after all transformations to a specified file
/generateTestPrintStats:<fileName>
    Create a json file with the summary statistics about the generated tests
/generateTestPrintTargets<filename>
    Print JSON object of all target methods and their number of hits
/generateTestVerbose
    Print various info as comments for debugging
/generateTestMinimization:<Random|Topological|Optimal>
    Use a given test minimization strategy. 
    Reported statistics might be off if this is used alongside inlining.";

  }
}