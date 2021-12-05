using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System;

namespace DafnyTestGeneration {

  public class MCDCBasedModifier : ProgramModifier {


    private Implementation implInQuestion;

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      VisitProgram(p);
      Dictionary<Block, List<Dictionary<String, bool>>> blocks = new ();

      var result = new List<ProgramModification>();
      
      DecisionVisitor visitor = new DecisionVisitor();
      p = visitor.VisitProgram(p);  

      MCDCSuiteGenerator generator = new MCDCSuiteGenerator();
      var allTestSets = generator.GetTestSuite(visitor.GotoDecisionMapper);

      AssertionInjectionVisitor injectionVisitor = new AssertionInjectionVisitor(allTestSets, blocks);
      p = injectionVisitor.VisitProgram(p);

      foreach (var block in blocks) {
        foreach(var test in block.Value) {
          var assertion = createAssertion(test);
          var assumeTrue = GetCmd("assume true;");

          block.Key.Cmds.Add(assertion);
          block.Key.Cmds.Add(assumeTrue);

          // foreach (var cmd in block.Key.Cmds) {
          //   Console.Out.Write(cmd.ToString() + "\n");
          // }

          result.Add(new ProgramModification(p, ProcedureName ?? implInQuestion.Name));

          block.Key.Cmds.Remove(assertion);
          block.Key.Cmds.Remove(assumeTrue);
        }
      }

      return result;
    }

    public override Implementation VisitImplementation(Implementation node) {
      if (!ProcedureIsToBeTested(node.Name)) {
        return node;
      }
      //Console.Out.Write("Visiting implementation\n");
      implInQuestion = node;
      return node;
    }

    private Cmd createAssertion(Dictionary<String, bool> testCase) {
      List<String> keyList = new List<String>(testCase.Keys);

      var varsCond = string.Join("&&", keyList.ConvertAll(x => testCase[x] ? x : $"!({x})"));
      //Console.Out.Write("varsCond is " + varsCond + "\n");

      var assertCmd = (AssertCmd) GetCmd($"assert !({varsCond});");

      Console.Out.Write("Assert is " + assertCmd.ToString() + "\n");

      return assertCmd;
    }

    public class AssertionInjectionVisitor : StandardVisitor {

      private Dictionary<GotoCmd, List<Dictionary<String, bool>>?> allTestSets = new();
      private Dictionary<Block, List<Dictionary<String, bool>>?> allBlocks = new ();

      public AssertionInjectionVisitor(Dictionary<GotoCmd, List<Dictionary<String, bool>>?> allTestSets,
        Dictionary<Block, List<Dictionary<String, bool>>?> allBlocks) {
        this.allTestSets = allTestSets;
        this.allBlocks = allBlocks;
      }

      public override Block VisitBlock(Block node) {
        //Console.Out.Write("Visiting block\n");

        if (node.TransferCmd != null && node.TransferCmd.GetType() == typeof(GotoCmd)) {
          var castedGoto = (GotoCmd) node.TransferCmd;
          if (allTestSets != null && allTestSets.ContainsKey(castedGoto)) {
            allBlocks.Add(node, allTestSets[castedGoto]);
          }
        }
        return node;
      }




      public override Procedure? VisitProcedure(Procedure? node)
      {
        if (node == null) {
          return node;
        }

        base.VisitProcedure(node);
    
        return node;
      }

    }

    
  }
}