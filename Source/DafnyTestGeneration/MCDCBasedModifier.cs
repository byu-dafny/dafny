using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System;

namespace DafnyTestGeneration {

  public class MCDCBasedModifier : ProgramModifier {


    private Implementation? implInQuestion;
    private Program? p;

    private Dictionary<GotoCmd, List<Dictionary<String, bool>>> allTestSets = new();
    private List<ProgramModification> result = new ();

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      this.p = p;
      
      DecisionVisitor decisionVisitor = new DecisionVisitor();
      p = decisionVisitor.VisitProgram(p);  

      MCDCSuiteGenerator generator = new MCDCSuiteGenerator();
      allTestSets = generator.GetTestSuite(decisionVisitor.GotoDecisionMapper);

      p = VisitProgram(p);

      return result;
    }

    public override Implementation VisitImplementation(Implementation node) {
      if (!ProcedureIsToBeTested(node.Name)) {
        return node;
      }
      implInQuestion = node;
      base.VisitImplementation(node);
      return node;
    }

    private Cmd createAssertion(Dictionary<String, bool> testCase) {
      List<String> keyList = new List<String>(testCase.Keys);

      var varsCond = string.Join("&&", keyList.ConvertAll(x => testCase[x] ? x : $"!({x})"));
      var assertCmd = (AssertCmd) GetCmd($"assert !({varsCond});");

      return assertCmd;
    }

    public override Block VisitBlock(Block node) {

      if (node.TransferCmd != null && node.TransferCmd is GotoCmd) {
        var castedGoto = (GotoCmd) node.TransferCmd;
        if (allTestSets.ContainsKey(castedGoto)) {
          foreach(var test in allTestSets[castedGoto]) {
            var assertion = createAssertion(test);

            node.Cmds.Add(assertion);

            if (implInQuestion != null && p != null)
              result.Add(new ProgramModification(p, ProcedureName ?? implInQuestion.Name));

            node.Cmds.Remove(assertion);
          } 
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