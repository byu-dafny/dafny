namespace DafnyTestGeneration; 

public class RequiresExpr {
  private Input input;
  private string binOp;
  private int compValue;

  public RequiresExpr(Input input, string binOp, int compValue) {
    this.input = input;
    this.binOp = binOp;
    this.compValue = compValue;
  }
}