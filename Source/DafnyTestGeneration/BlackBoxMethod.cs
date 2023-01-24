using System.Collections.Generic;
using Microsoft.Boogie;

namespace DafnyTestGeneration; 

public class BlackBoxMethod {
  private string methodName;
  private Dictionary<Input, List<RequiresExpr>> inputDict;
  private Dictionary<Procedure, List<Requires>> rawInfo;

  public BlackBoxMethod(string methodName, Dictionary<Procedure, List<Requires>> rawInfo) {
    this.methodName = methodName;
    this.rawInfo = rawInfo;
    this.inputDict = RefineInfo();
  }

  private Dictionary<Input, List<RequiresExpr>> RefineInfo() {
    return null;
  }

}