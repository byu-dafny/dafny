using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using System.Reflection;
using System;

namespace DafnyTestGeneration {
  public class DecisionVisitor : ReadOnlyVisitor {

    private const String PARTITION = "partition";
    private const String CAPTURE_STATE = "captureState";
    private Dictionary<GotoCmd, Decision> gotoToDecisionMapper = new ();
    //private Dictionary<String, List<GotoCmd>> wantedBlocks = new Dictionary<String, List<GotoCmd>>();

    public Dictionary<GotoCmd, Decision> GotoDecisionMapper { get {return gotoToDecisionMapper;} }

    private bool searchingForExpressions = false;
    private List<Expr> tempNaryList = new List<Expr>();
    private List<Expr> tempIdentList = new List<Expr>();

    private GotoCmd? activeGotoCmd;

    private const String READ = "read";


    // node.labelTargets : Block, node.labelNames : String
    public override GotoCmd VisitGotoCmd(GotoCmd node) {
      if (activeGotoCmd == null && node.labelNames.Count > 1) {
        // if (wantedBlocks.ContainsKey(node.labelNames[0])) {
        //   wantedBlocks[node.labelNames[0]].Add(node);
        // }
        // else {
        //   var list = new List<GotoCmd>();
        //   list.Add(node);
        //   wantedBlocks.Add(node.labelNames[0], list);
        // }
        activeGotoCmd = node;
      }
      //VisitGotoCmd(node);
      return node;
    }

    public override Block VisitBlock(Block node) {
      //if (wantedBlocks.ContainsKey(node.Label)) {
      if (activeGotoCmd != null) {
        var decision = VisitBlockReturnDecision(node);
        if (decision != null) {
          //foreach (var assumeCmd in wantedBlocks[node.Label]) {
            gotoToDecisionMapper.Add(activeGotoCmd, decision);
            tempNaryList.Clear();
            tempIdentList.Clear();
            activeGotoCmd = null;
            //Console.Out.Write("adding " + decision.decisionExpr.ToString() + "\n");
          //}
        }
      }
      base.VisitBlock(node);
      return node;
    }

    public Decision? VisitBlockReturnDecision(Block node) {

      Expr? potentialDecision = null;
      IEnumerator<Cmd> enumerator = node.Cmds.GetEnumerator();

      while (enumerator.MoveNext()) {

        var cmd = enumerator.Current;
        if (cmd.GetType() == typeof(AssumeCmd)) {
          var assume = (AssumeCmd) cmd;
          if (assume.Attributes != null && assume.Attributes.Key == PARTITION) {
            potentialDecision = assume.Expr;
            //Console.Out.Write("potentialDecision is " + potentialDecision.GetType() + potentialDecision.ToString() + "\n");
            break;
          }
        }
      }

      while (enumerator.MoveNext()) {
        
        var cmd = enumerator.Current;
        if (cmd.GetType() == typeof(AssumeCmd)) {
          var assume = (AssumeCmd) cmd;
          if (assume.Attributes != null && assume.Attributes.Key == CAPTURE_STATE && potentialDecision != null) {
            searchingForExpressions = true;

            VisitExpr(potentialDecision);

            searchingForExpressions = false;
            return new Decision(potentialDecision, tempNaryList, tempIdentList);
          }
        }
      }

      return null;
    }

    public override Expr VisitExpr(Expr node)
    {
      if (searchingForExpressions) {
        if (node.GetType() == typeof(NAryExpr)) {
          //Console.Out.Write("In Nary Expr\n");
          // cast to NAryExpr
          var naryExpr = (NAryExpr) node;
          //Console.Out.Write("naryExpr fun is " + naryExpr.Fun.ToString());

          if (isBoolInfix(naryExpr.Fun)) {
            //Console.Out.Write("Adding " + naryExpr.ToString() + " to tempSet\n");

            tempNaryList.Add(naryExpr);
          }
          else if (isBoolSeparator(naryExpr.Fun)) {
            foreach (var expr in naryExpr.Args) {
              if (expr.GetType() == typeof(IdentifierExpr)) {
                  //Console.Out.Write("Adding " + expr.ToString() + " to tempSet\n");

                  tempIdentList.Add(expr);
              }
              this.Visit(node);
            }
          }
          else if (naryExpr.Fun.ToString() == "read") {
            tempIdentList.Add(naryExpr);
            //Console.Out.Write("Adding " + naryExpr.ToString() + " to tempSet\n");
          }

        }
        else {
          //TODO change this, not every visit to identexpr should be saved
          //Console.Out.Write("Maybe add an else for " + node.GetType() + "\n");
          //Console.Out.Write("Adding " + node.ToString() + " to tempSet\n");

          tempIdentList.Add(node);
        }
      }
      else base.VisitExpr(node);
      return (Expr) node;
    }

    private bool isBoolSeparator(IAppliable op) {
      if (op.GetType() == typeof(BinaryOperator)) {
        var binop = (BinaryOperator) op;
        if (binop.Op == BinaryOperator.Opcode.And ||
          binop.Op == BinaryOperator.Opcode.Or) {
            return true;
          }
        else return false;
      }
      else return false;
    }

    private bool isBoolInfix(IAppliable op) {
      if (op.GetType() == typeof(BinaryOperator)) {
        var binop = (BinaryOperator) op;
        if (binop.Op == BinaryOperator.Opcode.Eq || 
          binop.Op == BinaryOperator.Opcode.Neq ||
          binop.Op == BinaryOperator.Opcode.Gt ||
          binop.Op == BinaryOperator.Opcode.Ge ||
          binop.Op == BinaryOperator.Opcode.Lt ||
          binop.Op == BinaryOperator.Opcode.Le) {
            return true;
          }
        else return false;
      }
      else if (op.GetType() == typeof(UnaryOperator)) {
        var unop = (UnaryOperator) op;
        if (unop.Op == UnaryOperator.Opcode.Not)
          return true;
        else return false;
      }
      else return false;
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

  public class Decision {
    public Expr decisionExpr;
    public List<Expr> exprNarySet;
    public List<Expr> exprIdentSet;

    public Decision(Expr decisionExpr, List<Expr> exprNarySet, List<Expr> exprIdentSet) {
      this.decisionExpr = decisionExpr;
      this.exprNarySet = new List<Expr>(exprNarySet);
      this.exprIdentSet = new List<Expr>(exprIdentSet);
    }
  }
}