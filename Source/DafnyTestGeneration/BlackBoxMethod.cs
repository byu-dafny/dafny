using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Boogie;
using Microsoft.Z3;
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
    FindDomains();
  }

  private void RefineInfo() {
    GetInputsFromRaw();
    List<RequiresExpr> requiresExprs = GetRequiresFromRaw();
    foreach (var input in InputDict) {
      foreach (var exp in requiresExprs) {
        if (ContainsInput(input.Key, exp)) {
          input.Value.Add(exp);
        }

        Solver s;
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
      string x = cond.Args[1].ToString();
      int val = int.Parse(Regex.Match(x, @"\d+").Value);
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
  
  // return the tuple containing the value to compare against
  private Tuple<int, int> SelectTuple(List<Tuple<int, int>> domain, int val) {
    return domain.FirstOrDefault(t => val >= t.Item1 && val <= t.Item2);
  }
  
  // remove tuples above a given tuple
  private List<Tuple<int, int>> removeTuples(List<Tuple<int, int>> domain, Tuple<int, int> t, bool upper) {
    if (upper) {
      domain.RemoveRange(domain.IndexOf(t), domain.Count - domain.IndexOf(t));
    } else {
      domain.RemoveRange(0, domain.IndexOf(t) + 1);
    }
    return domain;
  }
  
  // Find domain of each input
  private void FindDomains() {
    // for each input to the method
    foreach (var i in InputDict) {
      List<Tuple<int, int>> domain = new List<Tuple<int, int>>();
      domain.Add(new Tuple<int, int>(int.MinValue, int.MaxValue)); // the domain is initially all integers
      // for each requires clause that mentions that input
      foreach (var r in i.Value) {
        Tuple<int, int> t = SelectTuple(domain, r.compValue);
        if (t == null) {
          continue;
        }
        if (r.binOp == "<") {
          Tuple<int, int> newT = new Tuple<int, int>(t.Item1, r.compValue - 1);
          domain = removeTuples(domain, t, true);
          domain.Add(newT);
        } else if (r.binOp == "<=") {
          Tuple<int, int> newT = new Tuple<int, int>(t.Item1, r.compValue);
          // remove all tuples above old t, old t = t
          domain = removeTuples(domain, t, true);
          domain.Add(newT);
        } else if (r.binOp == ">") {
          Tuple<int, int> newT = new Tuple<int, int>(r.compValue + 1, t.Item2);
          // remove all tuples below old t, old t = t
          domain = removeTuples(domain, t, false);
          domain.Add(newT);
        } else if (r.binOp == ">=") {
          Tuple<int, int> newT = new Tuple<int, int>(r.compValue, t.Item2);
          // remove all tuples below old t, old t = t
          domain = removeTuples(domain, t, false);
          domain.Add(newT);
        } else if (r.binOp == "==") {
          Tuple<int, int> newT = new Tuple<int, int>(r.compValue, r.compValue);
          // remove all tuples and add t
          domain = new List<Tuple<int, int>> { newT };
        } else if (r.binOp == "!=") {
          Tuple<int, int> domain2 = new Tuple<int, int>(t.Item1, r.compValue - 1);
          Tuple<int, int> newT = new Tuple<int, int>(r.compValue + 1, t.Item2);
          // remove old t, add domain2 and newt
          domain.Remove(t);
          //domain.Add(); need to add in the right spot
        }
      }
      DomainDict.Add(i.Key, domain);
    }
  }
}