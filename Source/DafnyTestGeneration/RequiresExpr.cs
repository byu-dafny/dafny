namespace DafnyTestGeneration; 

public class RequiresExpr {
  public Input input { get; }
  public string binOp { get; }
  public int compValue { get; }

  public RequiresExpr(Input input, string binOp, int compValue) {
    this.input = input;
    this.binOp = binOp;
    this.compValue = compValue;
  }

}