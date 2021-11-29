using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System;

namespace DafnyTestGeneration {
  public class DecisionVisitor : ReadOnlyVisitor {

    private const String PARTITION = "partition";
    private const String CAPTURE_STATE = "captureState";
    private Dictionary<GotoCmd, Expr> gotoToDecisionMapper = new Dictionary<GotoCmd, Expr>();
    private Dictionary<String, List<GotoCmd>> wantedBlocks = new Dictionary<String, List<GotoCmd>>();

    public Dictionary<GotoCmd, Expr> GotoDecisionMapper { get {return gotoToDecisionMapper;} }


    // node.labelTargets : Block, node.labelNames : String
    public override GotoCmd VisitGotoCmd(GotoCmd node) {
      if (node.labelNames.Count > 1) {
        if (wantedBlocks.ContainsKey(node.labelNames[0])) {
          wantedBlocks[node.labelNames[0]].Add(node);
        }
        else {
          var list = new List<GotoCmd>();
          list.Add(node);
          wantedBlocks.Add(node.labelNames[0], list);
        }
      }
      base.VisitGotoCmd(node);
      return node;
    }

    public override Block VisitBlock(Block node) {
      if (wantedBlocks.ContainsKey(node.Label)) {
        var decision = VisitBlockReturnDecision(node);
        if (decision != null) {
          foreach (var assumeCmd in wantedBlocks[node.Label]) {
            gotoToDecisionMapper.Add(assumeCmd, decision);
            Console.Out.Write("adding " + decision.ToString() + "\n");
          }
        }
      }
      base.VisitBlock(node);
      return node;
    }

    // what is node.TransferCmd?
    public Expr? VisitBlockReturnDecision(Block node) {
      //Contract.Requires(node != null);
      //Contract.Ensures(Contract.Result<Block>() != null);
      Expr? potentialDecision = null;
      IEnumerator<Cmd> enumerator = node.Cmds.GetEnumerator();

      while (enumerator.MoveNext()) {

        var cmd = enumerator.Current;
        if (cmd.GetType() == typeof(AssumeCmd)) {
          var assume = (AssumeCmd) cmd;
          if (assume.Attributes != null && assume.Attributes.Key == PARTITION) {
            potentialDecision = assume.Expr;
            break;
          }
        }
      }

      while (enumerator.MoveNext()) {
        
        var cmd = enumerator.Current;
        if (cmd.GetType() == typeof(AssumeCmd)) {
          var assume = (AssumeCmd) cmd;
          if (assume.Attributes != null && assume.Attributes.Key == CAPTURE_STATE) {
            Console.Out.Write(potentialDecision.GetType() + "\n"); // add visit here if needed
            return potentialDecision;
          }
        }
      }

      return null;
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