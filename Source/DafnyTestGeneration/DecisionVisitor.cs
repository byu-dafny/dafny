using System;
using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;

namespace DafnyTestGeneration {
  public class DecisionVisitor : ReadOnlyVisitor {

    private const String PARTITION = "partition";
    private const String CAPTURE_STATE = "captureState";
    private Dictionary<GotoCmd, Decision> gotoToDecisionMapper = new ();

    public Dictionary<GotoCmd, Decision> GotoDecisionMapper { get {return gotoToDecisionMapper;} }

    private bool searchingForExpressions = false;
    private List<Expr> tempNaryList = new List<Expr>();
    private List<Expr> tempIdentList = new List<Expr>();

    private GotoCmd? activeGotoCmd;


    public override GotoCmd VisitGotoCmd(GotoCmd node) {

      if (activeGotoCmd == null && node.labelNames.Count > 1) {
        activeGotoCmd = node;
      }
      return node;
    }

    public override Block VisitBlock(Block node) {
      if (activeGotoCmd != null) {
        var decision = VisitBlockReturnDecision(node);
        if (decision != null) {
          gotoToDecisionMapper.Add(activeGotoCmd, decision);
          ResetAfterCapturingDecision();
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
        if (cmd is AssumeCmd) {
          var assume = (AssumeCmd) cmd;
          if (assume.Attributes != null && assume.Attributes.Key == PARTITION) {
            potentialDecision = assume.Expr;
            break;
          }
        }
      }

      while (enumerator.MoveNext()) {
        
        var cmd = enumerator.Current;
        if (cmd is AssumeCmd) {
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
        if (node is NAryExpr) {

          var naryExpr = (NAryExpr) node;

          if (isBoolInfix(naryExpr.Fun)) {
            tempNaryList.Add(naryExpr);
          }
          else if (isBoolSeparator(naryExpr.Fun)) {
            foreach (var expr in naryExpr.Args) {
              if (expr is IdentifierExpr) {
                  tempIdentList.Add(expr);
              }
              this.Visit(node);
            }
          }
          else {
            tempIdentList.Add(naryExpr);
          }
        }
        else {
          tempIdentList.Add(node);
        }
      }
      else base.VisitExpr(node);
      return (Expr) node;
    }

    private bool isBoolSeparator(IAppliable op) {
      if (op is BinaryOperator) {
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
      if (op is BinaryOperator) {
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
      else if (op is UnaryOperator) {
        var unop = (UnaryOperator) op;
        if (unop.Op == UnaryOperator.Opcode.Not)
          return true;
        else return false;
      }
      else return false;
    }

    private void ResetAfterCapturingDecision() {
      tempNaryList.Clear();
      tempIdentList.Clear();
      activeGotoCmd = null;
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