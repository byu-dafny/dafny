using System;
using System.Collections.Generic;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Type = System.Type;

namespace DafnyTestGeneration; 

public class BlackBoxMethod {
  private readonly Procedure rawMethod;
  private readonly List<Requires> rawRequires;
  public Dictionary<Input, List<RequiresExpr>> InputDict { get; }
  public Dictionary<Input, List<Tuple<int, int>>> DomainDict { get; }

  public BlackBoxMethod(Procedure method, List<Requires> rawInfo) {
    this.rawMethod = method;
    this.rawRequires = rawInfo;
    this.InputDict = new Dictionary<Input, List<RequiresExpr>>();
    this.DomainDict = new Dictionary<Input, List<Tuple<int, int>>>();
    RefineInfo();
  }

  private void RefineInfo() {
    GetInputsFromRaw();
    List<RequiresExpr> requiresExprs = GetRequiresFromRaw();
    foreach (var input in InputDict) {
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
      InputDict.Add(new Input(name, type), new List<RequiresExpr>());
    }
  }
  
  // look at requires and see if input is in the expression
  private bool ContainsInput(Input input, RequiresExpr expr) {
    return input.name == expr.Input.name;
  }
  
  private Tuple<int, int> SelectTuple(List<Tuple<int, int>> domain) {
    return null;
  }
  
  // Find domain of each input
  // private void FindDomains() {
  //   foreach (var i in InputDict) {
  //     List<Tuple<int, int>> domain = new List<Tuple<int, int>>();
  //     domain.Add(new Tuple<int, int>(int.MinValue, int.MaxValue));
  //     foreach (var r in i.Value) {
  //       if (r.compValue > domain. || r.compValue < domain.Item1) {
  //         continue;
  //       }
  //       if (r.binOp == "<") {
  //         domain = new Tuple<int, int>(domain.Item1, r.compValue - 1);
  //       } else if (r.binOp == "<=") {
  //         domain = new Tuple<int, int>(domain.Item1, r.compValue);
  //       } else if (r.binOp == ">") {
  //         domain = new Tuple<int, int>(r.compValue + 1, domain.Item2);
  //       } else if (r.binOp == ">=") {
  //         domain = new Tuple<int, int>(r.compValue, domain.Item2);
  //       } else if (r.binOp == "==") {
  //         domain = new Tuple<int, int>(r.compValue, r.compValue);
  //       } else if (r.binOp == "!=") {
  //         Tuple<int, int> domain2 = new Tuple<int, int>(domain.Item1, r.compValue - 1);
  //         domain = new Tuple<int, int>(r.compValue + 1, domain.Item2);
  //       }
  //     }
  //   }
  // }

}