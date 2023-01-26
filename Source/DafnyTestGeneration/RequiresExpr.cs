namespace DafnyTestGeneration; 

public class RequiresExpr {
  public Input Input { get; }
  public string binOp { get; }
  public int compValue { get;  }

  public RequiresExpr(Input input, string binOp, int compValue) {
    this.Input = input;
    this.binOp = binOp;
    this.compValue = compValue;
  }

}