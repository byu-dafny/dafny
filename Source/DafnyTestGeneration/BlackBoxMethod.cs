using System.Collections.Generic;
using Microsoft.Boogie;
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
    this.inputDict = RefineInfo();
  }

  private Dictionary<Input, List<RequiresExpr>> RefineInfo() {
    GetInputsFromRaw();
    List<RequiresExpr> requiresExprs = GetRequiresFromRaw();
    return null;
  }

  private List<RequiresExpr> GetRequiresFromRaw() {

    foreach (Requires r in rawRequires) {
      // string cond = r.Condition.;
      Input i = new Input("dj", "hfejdk");
    }
    
    return null;
  }

  private void GetInputsFromRaw() {
    List<Input> inputs = new List<Input>();
    List<Variable> rawInputs = rawMethod.InParams;
    foreach (Variable v in rawInputs) {
      string name = v.Name;
      string type = v.TypedIdent.Type.ToString();
      Input i = new Input(name, type);
      List<RequiresExpr> l = new List<RequiresExpr>();
      inputDict.Add(i, l);
    }
  }
  
  //look at requires and see if input is in the expression
  private bool containsInput(Input input, RequiresExpr expr) {
    return false;
  }

}