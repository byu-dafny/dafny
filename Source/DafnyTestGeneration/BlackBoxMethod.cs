using System.Collections.Generic;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Type = System.Type;

namespace DafnyTestGeneration; 

public class BlackBoxMethod {
  private Procedure rawMethod;
  private Dictionary<Input, List<RequiresExpr>> inputDict;
  private List<Requires> rawRequires;

  public BlackBoxMethod(Procedure method, List<Requires> rawInfo) {
    this.rawMethod = method;
    this.rawRequires = rawInfo;
    this.inputDict = new Dictionary<Input, List<RequiresExpr>>();
    RefineInfo();
  }

  private void RefineInfo() {
    GetInputsFromRaw();
    List<RequiresExpr> requiresExprs = GetRequiresFromRaw();
    foreach (var input in inputDict) {
      foreach (var exp in requiresExprs) {
        if (ContainsInput(input.Key, exp)) {
          input.Value.Add(exp);
        }
      }
    }
  }

  private List<RequiresExpr> GetRequiresFromRaw() {
    List<RequiresExpr> requiresExprs = new List<RequiresExpr>();

    foreach (Requires r in rawRequires) {
      NAryExpr cond = (NAryExpr)r.Condition;
      string name = cond.Args[0].ToString();
      string type = cond.Args[0].Type.ToString();
      Input i = new Input(name, type);
      string op = cond.Fun.FunctionName;
      int val = int.Parse(cond.Args[1].ToString());
      requiresExprs.Add(new RequiresExpr(i, op, val));
    }

    return requiresExprs;
  }

  private void GetInputsFromRaw() {
    List<Input> inputs = new List<Input>();
    List<Variable> rawInputs = rawMethod.InParams;
    foreach (Variable v in rawInputs) {
      string name = v.Name;
      string type = v.TypedIdent.Type.ToString();
      inputDict.Add(new Input(name, type), new List<RequiresExpr>());
    }
  }
  
  // look at requires and see if input is in the expression
  private bool ContainsInput(Input input, RequiresExpr expr) {
    return input.name == expr.input.name;
  }

}